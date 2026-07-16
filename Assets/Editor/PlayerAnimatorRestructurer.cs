using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Reorganizes a "spaghetti" Animator Controller into a modular, industry-standard
/// structure: three Sub-State Machines (SM_Locomotion, SM_Combat, SM_Status), each
/// with an empty hub, a consolidated locomotion Blend Tree, and hub-and-spoke combat.
///
/// SAFETY: This never deletes AnimationClips (they are separate .anim assets, only
/// referenced) and never deletes existing parameters. It only ADDS the parameters
/// needed for the new structure. It rebuilds layer 0's state graph in place, so make
/// a backup of the .controller first (the menu item / caller does this).
/// </summary>
public static class PlayerAnimatorRestructurer
{
    // --- Clip lookup by name (resolved from the AssetDatabase so we never depend on old state objects) ---
    private static AnimationClip Clip(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip " + name))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (c != null && c.name == name) return c;
        }
        Debug.LogWarning("[Restructurer] Clip not found: " + name + " (a placeholder/empty motion will be used).");
        return null;
    }

    [MenuItem("Tools/Player Animator/Restructure (Selected Controller)")]
    private static void RestructureSelected()
    {
        var ac = Selection.activeObject as AnimatorController;
        if (ac == null)
        {
            EditorUtility.DisplayDialog("Restructure Animator",
                "Select an AnimatorController asset in the Project window first.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(ac);
        string backup = path.Replace(".controller", "_BACKUP.controller");
        if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(backup))
            AssetDatabase.CopyAsset(path, backup);

        Restructure(ac);
        EditorUtility.DisplayDialog("Restructure Animator",
            "Done. Backup saved at:\n" + backup, "OK");
    }

    /// <summary>Applies the modular restructure to the given controller (layer 0).</summary>
    public static void Restructure(AnimatorController ac)
    {
        // ---------------------------------------------------------------
        // 1. PARAMETERS — preserve all existing, add only what's missing.
        // ---------------------------------------------------------------
        EnsureParam(ac, "HasShield", AnimatorControllerParameterType.Bool);
        EnsureParam(ac, "Slash", AnimatorControllerParameterType.Trigger);
        EnsureParam(ac, "Stab", AnimatorControllerParameterType.Trigger);
        EnsureParam(ac, "Projectile", AnimatorControllerParameterType.Trigger);
        EnsureParam(ac, "PlatformDraw", AnimatorControllerParameterType.Trigger);

        // ---------------------------------------------------------------
        // 2. Resolve clips (by name, from disk).
        // ---------------------------------------------------------------
        var idleClip = Clip("Player_Idle");
        var runClip = Clip("Player_run");
        var fallClip = Clip("Player_fall");
        var jumpClip = Clip("Player_jump");
        var hurtClip = Clip("Player_hurt");
        var deathClip = Clip("Player_death");
        var shieldIdleClip = Clip("Painter_Shield_idle");
        var shieldDrawClip = Clip("Shield");

        // ---------------------------------------------------------------
        // 3. Wipe layer 0's graph (states/sub-SMs/AnyState) — clips & params survive.
        // ---------------------------------------------------------------
        var root = ac.layers[0].stateMachine;

        foreach (var t in new List<AnimatorStateTransition>(root.anyStateTransitions))
            root.RemoveAnyStateTransition(t);
        foreach (var t in new List<AnimatorTransition>(root.entryTransitions))
            root.RemoveEntryTransition(t);
        foreach (var css in new List<ChildAnimatorStateMachine>(root.stateMachines))
            root.RemoveStateMachine(css.stateMachine);
        foreach (var cs in new List<ChildAnimatorState>(root.states))
            root.RemoveState(cs.state);

        // ---------------------------------------------------------------
        // 4. BASE LAYER hub + three sub-state machines.
        // ---------------------------------------------------------------
        var rootHub = root.AddState("Root_Hub", new Vector3(0, 0, 0));
        root.defaultState = rootHub;

        var smLoco = root.AddStateMachine("SM_Locomotion", new Vector3(-260, 140, 0));
        var smCombat = root.AddStateMachine("SM_Combat", new Vector3(260, 140, 0));
        var smStatus = root.AddStateMachine("SM_Status", new Vector3(0, 300, 0));

        // ================= SM_STATUS =================
        var statusHub = smStatus.AddState("Status_Hub", new Vector3(0, 0, 0));
        smStatus.defaultState = statusHub;
        var hurtState = smStatus.AddState("Player_hurt", new Vector3(-220, 140, 0));
        hurtState.motion = hurtClip; hurtState.speed = 1f;
        var deathState = smStatus.AddState("Player_death", new Vector3(220, 140, 0));
        deathState.motion = deathClip; deathState.speed = 0.7f;

        // ================= SM_COMBAT =================
        var combatHub = smCombat.AddState("Combat_Hub", new Vector3(0, 0, 0));
        smCombat.defaultState = combatHub;
        var shieldDraw = smCombat.AddState("ShieldDraw", new Vector3(-260, 120, 0));
        shieldDraw.motion = shieldDrawClip; shieldDraw.speed = 1f;
        var slash = smCombat.AddState("Slash", new Vector3(-90, 200, 0));   // placeholder (empty)
        var stab = smCombat.AddState("Stab", new Vector3(90, 200, 0));      // placeholder (empty)
        var projectile = smCombat.AddState("Projectile", new Vector3(260, 120, 0)); // placeholder
        var platformDraw = smCombat.AddState("PlatformDraw", new Vector3(0, 240, 0)); // placeholder

        var combatActions = new (AnimatorState state, string trigger)[]
        {
            (shieldDraw, "Shield"),
            (slash, "Slash"),
            (stab, "Stab"),
            (projectile, "Projectile"),
            (platformDraw, "PlatformDraw"),
        };

        // ================= SM_LOCOMOTION =================
        var locoHub = smLoco.AddState("Locomotion_Hub", new Vector3(0, 0, 0));
        smLoco.defaultState = locoHub;

        // Grounded (normal) blend tree: Idle <-> Run on Speed
        var grounded = smLoco.AddState("Grounded", new Vector3(-40, 140, 0));
        var btMove = NewTree(ac, "BT_Movement", "Speed");
        btMove.children = new[]
        {
            NewChild(idleClip, 0f, 0.8f),
            NewChild(runClip, 1f, 1.0f),
        };
        grounded.motion = btMove; grounded.speed = 1f;

        // Grounded_Shield blend tree: ShieldIdle <-> [Run with Shield placeholder=null] on Speed
        var groundedShield = smLoco.AddState("Grounded_Shield", new Vector3(-320, 140, 0));
        var btShield = NewTree(ac, "BT_Movement_Shield", "Speed");
        btShield.children = new[]
        {
            NewChild(shieldIdleClip, 0f, 1.0f),
            NewChild(null, 1f, 1.0f), // placeholder slot for "Run with Shield" clip
        };
        groundedShield.motion = btShield; groundedShield.speed = 1f;

        var jumpState = smLoco.AddState("Player_jump", new Vector3(220, 60, 0));
        jumpState.motion = jumpClip; jumpState.speed = 0.5f;
        var fallState = smLoco.AddState("Player_fall", new Vector3(220, 220, 0));
        fallState.motion = fallClip; fallState.speed = 0.5f;

        // ---------------------------------------------------------------
        // 5. TRANSITIONS
        // ---------------------------------------------------------------

        // Base: hub routes into locomotion on startup.
        Route(rootHub, grounded);

        // Base AnyState interrupts (approved): Die + Hurt.
        var anyDie = root.AddAnyStateTransition(deathState);
        anyDie.canTransitionToSelf = false; anyDie.hasExitTime = false; anyDie.duration = 0.1f;
        anyDie.AddCondition(AnimatorConditionMode.If, 0, "Died");

        var anyHurt = root.AddAnyStateTransition(hurtState);
        anyHurt.canTransitionToSelf = false; anyHurt.hasExitTime = false; anyHurt.duration = 0.1f;
        anyHurt.AddCondition(AnimatorConditionMode.If, 0, "gotHurt");

        // --- Locomotion internal ---
        Route(locoHub, grounded); // empty hub -> resting blend tree

        // Shield swap (bool can't be a blend axis, so we swap between two BT states)
        var toShield = Anim(grounded, groundedShield, false, 0f, 0.1f);
        toShield.AddCondition(AnimatorConditionMode.If, 0, "HasShield");
        var fromShield = Anim(groundedShield, grounded, false, 0f, 0.1f);
        fromShield.AddCondition(AnimatorConditionMode.IfNot, 0, "HasShield");

        // Airborne spokes from both grounded variants.
        foreach (var g in new[] { grounded, groundedShield })
        {
            var toJump = Anim(g, jumpState, false, 0f, 0.05f);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var toFall = Anim(g, fallState, false, 0f, 0.1f);
            toFall.AddCondition(AnimatorConditionMode.Less, -0.001f, "yVelocity");
            toFall.AddCondition(AnimatorConditionMode.IfNot, 0, "isGrounded");
        }

        var jumpToFall = Anim(jumpState, fallState, true, 0.4f, 0.1f); // preserved behaviour
        var jumpToGround = Anim(jumpState, grounded, false, 0f, 0.1f);
        jumpToGround.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");
        var fallToGround = Anim(fallState, grounded, false, 0f, 0.1f);
        fallToGround.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");

        // Combat entry from the grounded resting state (keeps AnyState limited to Die/Hurt).
        foreach (var ca in combatActions)
        {
            var enter = Anim(grounded, ca.state, false, 0f, 0.1f);
            enter.AddCondition(AnimatorConditionMode.If, 0, ca.trigger);
        }

        // --- Combat internal: hub-and-spoke ---
        foreach (var ca in combatActions)
        {
            // forward spoke (enables chaining while already in combat)
            var fwd = Anim(combatHub, ca.state, false, 0f, 0.1f);
            fwd.AddCondition(AnimatorConditionMode.If, 0, ca.trigger);
            // return spoke: back to hub when the clip ends (Exit Time = 1)
            Anim(ca.state, combatHub, true, 1.0f, 0.1f);
        }
        // Lowest-priority default exit: leave combat back to locomotion when no chain trigger is pending.
        Route(combatHub, grounded);

        // --- Status internal ---
        var hurtToDeath = Anim(hurtState, deathState, false, 0f, 0.1f);
        hurtToDeath.AddCondition(AnimatorConditionMode.If, 0, "Died");
        Anim(hurtState, statusHub, true, 0.9f, 0.1f); // recover -> hub
        Route(statusHub, grounded);                    // hub -> resume locomotion
        // Player_death is terminal (destruction handled by animation event / C#).

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Restructurer] '" + ac.name + "' restructured into SM_Locomotion / SM_Combat / SM_Status.");
    }

    // ---------------- helpers ----------------

    private static void EnsureParam(AnimatorController ac, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in ac.parameters)
            if (p.name == name) return;
        ac.AddParameter(name, type);
    }

    private static BlendTree NewTree(AnimatorController ac, string name, string blendParam)
    {
        var tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.Simple1D,
            blendParameter = blendParam,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, ac);
        return tree;
    }

    private static ChildMotion NewChild(Motion motion, float threshold, float timeScale)
    {
        return new ChildMotion
        {
            motion = motion,
            threshold = threshold,
            timeScale = timeScale,
            position = Vector2.zero,
            directBlendParameter = "Speed"
        };
    }

    /// <summary>Animated transition with explicit exit-time/duration.</summary>
    private static AnimatorStateTransition Anim(AnimatorState from, AnimatorState to, bool hasExitTime, float exitTime, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = hasExitTime;
        t.exitTime = exitTime;
        t.hasFixedDuration = true;
        t.duration = duration;
        return t;
    }

    /// <summary>Instant, condition-less routing transition (used for hub -> resting state).</summary>
    private static AnimatorStateTransition Route(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0f;
        return t;
    }
}
