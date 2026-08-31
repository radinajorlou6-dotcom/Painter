using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the placeholder HUD canvas — the five bars and the two driver components — from a menu
/// item, so the wiring is done once here rather than by hand in every scene that needs it.
///
/// Written as an editor tool rather than by editing the scene file directly: hand-authored scene
/// YAML has to invent fileIDs and would be silently clobbered the next time Unity saved the scene
/// it was open in. Everything created here goes through Undo, so a bad run is one Ctrl+Z away.
/// </summary>
public static class HudBuilder
{
    private const string MenuPath = "Tools/Painter/Build HUD Canvas";

    // Left column of player bars, stacked downward from the top-left corner.
    private const float Margin = 16f;
    private const float BarWidth = 260f;
    private const float BarHeight = 24f;
    private const float BarSpacing = 8f;

    [MenuItem(MenuPath)]
    public static void BuildHud()
    {
        GameObject canvasObject = CreateCanvas();

        // Player bars, top-left, in the order they matter while playing.
        ResourceBar health = CreateBar(canvasObject.transform, "Health Bar", 0,
            new Color(0.32f, 0.85f, 0.36f), new Color(0.85f, 0.19f, 0.19f));
        ResourceBar ink = CreateBar(canvasObject.transform, "Platform Ink Bar", 1,
            new Color(0.35f, 0.62f, 1f), new Color(0.20f, 0.25f, 0.40f));
        ResourceBar shield = CreateBar(canvasObject.transform, "Shield Bar", 2,
            new Color(0.95f, 0.85f, 0.35f), new Color(0.40f, 0.36f, 0.18f));
        ResourceBar curse = CreateBar(canvasObject.transform, "Curse Bar", 3,
            new Color(0.75f, 0.35f, 1f), new Color(0.30f, 0.16f, 0.42f));

        // Boss bars, centred along the top where a boss bar is expected to be.
        ResourceBar bossHealth = CreateBossBar(canvasObject.transform, "Boss Health Bar", 0f,
            new Color(0.85f, 0.19f, 0.19f), new Color(0.30f, 0.06f, 0.06f));
        ResourceBar bossWeakpoints = CreateBossBar(canvasObject.transform, "Boss Weakpoint Bar", -26f,
            new Color(1f, 0.75f, 0.25f), new Color(0.35f, 0.26f, 0.09f));

        GameObject driverObject = new GameObject("HUD Driver", typeof(PlayerHudUI), typeof(BossHealthBarUI));
        Undo.RegisterCreatedObjectUndo(driverObject, "Build HUD Canvas");
        driverObject.transform.SetParent(canvasObject.transform, false);

        // Assigned through SerializedObject because every field on these components is a private
        // [SerializeField] — the UI can be wired without any of them being made public.
        SerializedObject playerHud = new SerializedObject(driverObject.GetComponent<PlayerHudUI>());
        playerHud.FindProperty("healthBar").objectReferenceValue = health;
        playerHud.FindProperty("platformInkBar").objectReferenceValue = ink;
        playerHud.FindProperty("shieldBar").objectReferenceValue = shield;
        playerHud.FindProperty("curseBar").objectReferenceValue = curse;
        playerHud.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject bossHud = new SerializedObject(driverObject.GetComponent<BossHealthBarUI>());
        bossHud.FindProperty("healthBar").objectReferenceValue = bossHealth;
        bossHud.FindProperty("weakpointBar").objectReferenceValue = bossWeakpoints;
        bossHud.ApplyModifiedPropertiesWithoutUndo();

        WarnIfNoEventSystem();

        Selection.activeGameObject = canvasObject;
        EditorSceneManager.MarkSceneDirty(canvasObject.scene);

        Debug.Log("HUD canvas built. The bars find the Player and the boss themselves at runtime — " +
                  "nothing else to wire up. Save the scene to keep it.");
    }

    private static GameObject CreateCanvas()
    {
        // RectTransform named explicitly rather than left to RequireComponent: the code below
        // reads it back straight away, and a plain Transform here would be a null dereference.
        GameObject canvasObject = new GameObject("HUD Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Build HUD Canvas");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Below the ScreenFader, which has to be able to black out the HUD along with everything
        // else during a death or a level transition.
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); // matches the project's target
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvasObject;
    }

    /// <summary>One player bar, anchored to the top-left and offset down by its row index.</summary>
    private static ResourceBar CreateBar(Transform parent, string name, int row, Color full, Color empty)
    {
        float y = -Margin - row * (BarHeight + BarSpacing);

        GameObject root = CreateBarRoot(parent, name,
            new Vector2(0f, 1f), new Vector2(Margin, y), new Vector2(BarWidth, BarHeight));

        return FinishBar(root, full, empty);
    }

    /// <summary>A boss bar, centred along the top and wider than the player's.</summary>
    private static ResourceBar CreateBossBar(Transform parent, string name, float yOffset, Color full, Color empty)
    {
        GameObject root = CreateBarRoot(parent, name,
            new Vector2(0.5f, 1f), new Vector2(0f, -Margin + yOffset), new Vector2(720f, 22f));

        return FinishBar(root, full, empty);
    }

    private static GameObject CreateBarRoot(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Build HUD Canvas");
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image background = root.GetComponent<Image>();
        background.sprite = BuiltinSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = false; // a HUD must never eat clicks meant for the game

        return root;
    }

    /// <summary>Adds the fill child and the ResourceBar, and wires the two together.</summary>
    private static ResourceBar FinishBar(GameObject root, Color full, Color empty)
    {
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(fillObject, "Build HUD Canvas");
        fillObject.transform.SetParent(root.transform, false);

        // Stretched to the parent with a small inset, so the background reads as a frame.
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        Image fill = fillObject.GetComponent<Image>();
        fill.sprite = BuiltinSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.color = full;
        fill.raycastTarget = false;

        ResourceBar bar = Undo.AddComponent<ResourceBar>(root);

        SerializedObject serialized = new SerializedObject(bar);
        serialized.FindProperty("fill").objectReferenceValue = fill;
        serialized.FindProperty("container").objectReferenceValue = root;
        serialized.FindProperty("fullColour").colorValue = full;
        serialized.FindProperty("emptyColour").colorValue = empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return bar;
    }

    /// <summary>Unity's stock UI sprite, so the placeholder bars have rounded ends out of the box.</summary>
    private static Sprite BuiltinSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    /// <summary>
    /// Warned about rather than created. An EventSystem built here would need the Input System's
    /// UI module to match the rest of the project, and getting that wrong breaks menu input in a
    /// way that is annoying to trace — every gameplay scene already has one.
    /// </summary>
    private static void WarnIfNoEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        Debug.LogWarning("No EventSystem in this scene. The HUD itself doesn't need one — it never " +
                         "takes clicks — but menus will not respond until you add one.");
    }
}
