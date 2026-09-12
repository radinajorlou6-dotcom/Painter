# Painter

A 2D side-scrolling platformer about restoring colour to a drained world. The world starts
colourless and inert; emptying a paint bucket brings a colour back, and with it the parts of the
level that colour governs — water you can swim in, vines you can climb, doors you can walk through.

Built in **Unity 6000.4.3f1** with URP, Cinemachine 3 and the Input System.

---

## Contents

- [Getting started](#getting-started)
- [Controls](#controls)
- [The colour system](#the-colour-system)
- [Player](#player)
- [Enemies](#enemies)
- [The boss](#the-boss)
- [Camera](#camera)
- [Parallax backgrounds](#parallax-backgrounds)
- [Save and progression](#save-and-progression)
- [UI](#ui)
- [Feedback systems](#feedback-systems)
- [Project conventions](#project-conventions)
- [Editor tools](#editor-tools)
- [Scenes](#scenes)
- [Known issues](#known-issues)

---

## Getting started

1. Open the project in **Unity 6000.4.3f1**. Other versions may work but aren't tested.
2. Open a gameplay scene — `Assets/Scenes/recovered jam.unity` is the current level.
3. Press Play.

Every scene needs a `SaveSystem` object (a `GameManager.prefab` instance). It's `DontDestroyOnLoad`
and owns progression, save/load and input map state — without it, abilities read as locked and
colour unlocks do nothing.

### Packages

| Package | Version | Used for |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.4.0 | URP 2D renderer, 2D lights |
| `com.unity.cinemachine` | 3.1.6 | camera rig, shake and confining |
| `com.unity.inputsystem` | 1.19.0 | all input |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 | save serialisation |
| `com.unity.2d.*` | — | tilemaps, sprite shapes, Aseprite/PSD import |

---

## Controls

Bound in `Assets/PlayerControls.inputactions`. Three maps: **Player**, **Combat**, **UI**.

| Action | Binding | Map | Notes |
|---|---|---|---|
| Move | `WASD` / left stick | Player | |
| Jump | `Space` | Player | coyote time + jump buffering |
| Interact | `G` | Player | paint buckets, chests |
| Curse | `Q` | Player | hold, hover an enemy, release |
| Slingshot | `Shift` + `RMB` | Player | drag to aim, release to launch |
| Primary attack | `LMB` | Combat | **tap** = projectile, **drag** = slash along the drawn path |
| Shield | `RMB` | Combat | draw a shield line |
| Draw platform | `Shift` + `LMB` | Combat | spends ink |
| Pause | `P` | Player / UI | |
| Insta-kill | `K` | UI | debug only |

`GameManager.RefreshInputMapStates()` enables Player + Combat while playing and UI while
paused or dead. **It looks maps up by literal name**, so a new action must go in one of those three
maps or it will never be enabled.

---

## The colour system

The core mechanic, and the thing most of the codebase hangs off.

`PaintColour { Blue, Red, Green, Yellow }` — see `Assets/Scripts/Saver/GameData.cs`.

Emptying a bucket calls `GameManager.SaveBucketState(colour, true)`, which records it, raises
`GameManager.OnColourUnlocked`, and autosaves.

### Two halves, deliberately separate

**Gameplay** — what a colour unlock does to *colliders*:

| Component | Locked | Unlocked |
|---|---|---|
| `ColourControl` | tilemap is solid | collider disabled, passable |
| `WaterZone` | solid, walkable | collider becomes a trigger, swimmable |
| `VineZone` | pass-through | climbable |
| `ColourDoor` | blocks the way | collider off or trigger, walk through |

**Visuals** — what it does to *pixels*:

| Component | Use for |
|---|---|
| `ColourReveal` | whole-object drain → restore. Tilemaps, backdrops, enemies, props |
| `TilemapColourMask` | per-tile gating within **one** tilemap, so Ground can hold blue and green patches without splitting its collider |
| `ColourCurtain` | a white sheet over already-coloured art that fades away on unlock |
| `DrainedPalette` | one `ScriptableObject` holding the project-wide drained tint and fade duration |
| `ColourTint` | shared helper: collects Tilemaps + SpriteRenderers, resolves tints, runs the fade |

Keeping them apart is what lets one visual component serve a tilemap, a parallax layer and an
enemy alike, with a single locked→unlocked transition.

### Tint vs curtain — which to use

`Tilemap.color` and `SpriteRenderer.color` are a **per-channel multiply**. They can only darken.

- **White-mask art** (`Assets/Tilemap/monochrome_tilemap_transparent.png` is a 1-bit pure-white
  silhouette) — tinting is a true recolour. Use `ColourReveal` / `TilemapColourMask`.
- **Already-coloured art** — no tint value turns blue art white. Use `ColourCurtain`, which covers
  it and fades away.

### Per-tile gating

`TilemapColourMask` sits on a tilemap and holds a list of regions. Each region points at a hidden
sibling tilemap — you paint the shapes you want claimed, and its renderer is switched off at
runtime. A region with **no** mask is the catch-all for every remaining cell.

Tile assets ship with `TileFlags.LockColor`, which makes `SetColor` silently do nothing;
the component clears it per cell. That's the detail that makes any of it work.

---

## Player

`PlayerMovement` — run, jump, wall-slide, wall-jump, swim, climb. Coyote time, jump buffering,
slope handling and separate animation buffers for jump and fall.

Public state other systems read: `isGrounded`, `isGroundedOn`, `isWalled`, `isSlingshotting`,
`isSwimming`, `isClimbing`.

`PlayerMovement.IsAbilityBlocked(AbilityType)` gates abilities while swimming or climbing —
the allowed set is a serialized list, so Slingshot can stay usable in water while the rest switch off.

### Abilities

`AbilityType { Slingshot, PlatformDraw, ShieldDraw, Curse }`, each unlocked by a paint bucket
with `grantsAbility` ticked.

- **Slingshot** (`SlingshotAbility`) — drag to aim, release to launch. Charge-based, refills on landing.
- **Platform draw** (`CombatInput`) — draw a platform, spends from an ink budget.
- **Shield draw** (`PlayerCombat`) — draw a shield line that takes a set number of hits.
- **Curse** (`CurseAbility`) — hold, hover an enemy, release to freeze it. A tether shows whether
  the target is valid; missing costs nothing, so only a landed stun starts the cooldown.

`PlayerCombat` also owns the melee slash, the projectile and shield durability. `allowRangedAttack`
is a per-scene toggle for levels that should be melee-only.

---

## Enemies

`EnemyBase` — detection, ground/ledge checks, knockback, death, and the stun system. Health is
**composed** via a required `Health` component rather than inherited.

| Class | Behaviour |
|---|---|
| `ChaserAI` | patrols, chases, swings when close |
| `KnightAI` | armoured (permanently `Invulnerable`), charges and bashes, only sees the side it faces |
| `BaseEnemyAI` | leaper; hops on a timer, sticks to walls, detonates on contact |
| `BossAI` | see below |

### Stun

`EnemyBase.ApplyStun(duration)` freezes an enemy with `RigidbodyConstraints2D.FreezeAll` —
**not** a velocity write, because `TakeKnockback` runs on the *attacker* and rewrites the victim's
velocity every frame, so zeroing it once would be undone the next frame.

It never touches colliders, `Health`, or `Health.Invulnerable`. A stunned enemy stays hittable and
killable, which is the whole point.

`CancelActions()` is the subclass hook for stopping an in-flight attack — it must **not** call
`StopAllCoroutines()`, which would kill `DetectionRoutine` and leave the enemy permanently blind.

---

## The boss

`BossAI` — a three-phase duellist that teleports between fixed anchors rather than walking.

| Phase | Behaviour |
|---|---|
| **Duel** | idle tell → attack → cooldown → blink to a random anchor → repeat |
| **Shielded** (66%) | stops blinking, invulnerable except at its `BossWeakpoint`s. Destroy them all to end it |
| **Frenzy** (33%) | cycles anchors in a fixed learnable order, invulnerable. Three landed stuns end it, each opening a window of real vulnerability. The duel then resumes at max aggression |

Both interruptions are one-way doors — the thresholds latch, so healing can't replay a phase.

Three duel attacks: **melee** inside `meleeRange`, **ranged** outside it, and **spikes** on an
interval, which erupt across the whole arena floor and ignore distance entirely.

- **Arena** — a `Collider2D`. She stays idle until the player is inside it.
- **Arena barriers** — sealed behind the player on entry, opened on her death.
- **Reset on player death** — health, phases, weakpoints, barriers and position all restored, so a
  sealed arena can't become a dead end.
- **Key drop** — a `KeyPickup` prefab spawned unparented on death; collecting it loads the victory scene.

Her collider is never disabled, including mid-teleport — the curse targets by hovering a collider,
so a boss who vanished from physics while blinking would be untargetable exactly when you're aiming.

---

## Camera

A Cinemachine 3 rig plus four components. **The rule: if the player walks into it and shouldn't see
out, it's a `CameraRoom`. If you just want the view to change, it's a `CameraZoomZone`. Everything
else is covered by the single `CameraBounds`.**

| Component | Count | Trigger | Does |
|---|---|---|---|
| `CameraBounds` | **one per scene** | no | stops the camera leaving the map. Never changes zoom |
| `CameraRoom` | one per enclosed space | yes | fits the camera to the room's walls **and** confines it there |
| `CameraZoomZone` | any | yes | zoom multiplier, no confining |
| `CameraShake` | on the vcam | — | `CinemachineExtension`, unscaled so it plays through hitstop |

`CameraZoomController` drives the lens on the single vcam rather than blending between several —
`CameraShake` is a singleton extension, so a second vcam would leave shake applied to whichever
camera wasn't live. `CameraRoomConfiner` is added automatically and clamps at `Stage.Body`,
before `CameraShake`'s `Finalize`, so shake sits on top of a confined position instead of fighting it.

Zones stack, most recently entered wins — overlap them at doorways and the handover has no
intermediate state.

**You cannot fully stop a 2D camera seeing past a wall.** The view is a rectangle; any rectangle
containing the player in an L-shaped room includes something behind a wall. Confine, tighten the
framing in tight spaces, and paint solid tiles a screen deep behind the play area.

---

## Parallax backgrounds

`ParallaxController` drives every `ParallaxLayer` from one `LateUpdate` at
`[DefaultExecutionOrder(1000)]` — after `CinemachineBrain`, so layers read the camera's *final*
position with shake included.

The origin is latched at the end of the **first** `LateUpdate`, not in `Awake`. The composer's
`CenterOnActivate` snaps the camera onto the player on frame one, and a layer that cached its
origin before that snap would be offset for the whole level.

| Feature | Field |
|---|---|
| Per-axis depth | `parallaxFactor` — 1 = glued to camera, 0 = world speed, **negative = in front of the player** |
| Infinite tiling | `loopX` / `loopY`, snapping by whole spans so the seam is invisible |
| Constant drift | `autoScrollSpeed` |
| Room binding | `room` — anchors a layer to a `CameraRoom` so it can't drift out. Works with looping |
| Follow limits | `limitFollowX/Y` + `fadeOutDistance` — stop following past a world X, and cross-fade to the next backdrop |

Room binding and follow limits are the same clamp with different bounds. Use the room where one
exists; use follow limits in open areas that have no `CameraRoom`.

---

## Save and progression

`GameManager` is the hub: game state, progression, scene transitions, save orchestration.
UI never touches `SceneManager` or the save system directly.

```
SaveService  ──> ISerializer (JsonSaveSerializer)
             └─> IStorage    (FileStorage → saveFile.json)
```

Systems implement `ISaveable` (`SaveId`, `CaptureState`, `RestoreState`) and register with
`GameManager.RegisterSaveable`. The Memento pattern — `SaveService` never knows what's inside a
snapshot, so new systems join saving without it changing.

- **Autosave** fires on every colour unlock, carrying the respawn point with it.
- **Checkpoints** are recorded by `Checkpoint` triggers and by paint buckets. The save stores which
  level a checkpoint belongs to, so it can't leak into another one.
- **Death** plays the death animation, fades to black, respawns at the checkpoint and fades back in.
  `autoRespawn` on `PlayerHealth` skips the death menu entirely.

### The ordering trap

`RestoreState` replays `OnColourUnlocked` for every unlocked colour during `ContinueGame()` —
**before** `SceneManager.LoadScene`. Nothing in the level exists yet, so those events reach nobody.

Every colour-reactive component therefore **polls on startup as well as subscribing**. Don't
"simplify" any of them to event-only.

---

## UI

| Component | Role |
|---|---|
| `ResourceBar` | one dumb 0..1 bar — health, ink, shield, cooldown all use it |
| `PlayerHudUI` | drives health, platform ink, shield and curse |
| `BossHealthBarUI` | boss health + weakpoints, hides itself when there's no boss |
| `ScreenFader` | full-screen fade, unscaled so it plays while frozen |
| `PauseMenuUI` / `MainMenuUI` | menus |
| `TutorialManager` / `TutorialZone` | contextual prompts driven by zones and signals |

The HUD **polls** rather than subscribing — three of its four resources have no change event, so
subscribing would mean adding events across three gameplay scripts purely to serve the UI. Reading
four numbers a frame costs nothing and keeps the dependency pointing one way.

---

## Feedback systems

| System | API | Notes |
|---|---|---|
| `HitStop` | `Instance.Freeze(duration)` | drives `Time.timeScale`, overlapping calls extend rather than stack |
| `CameraShake` | `Instance.Shake(intensity, duration)` | must live on the CinemachineCamera |
| `AudioController` | `Play(AudioType)`, `StartLoop`, `StopLoop` | per-entity, pooled sources. Missing entries warn once rather than spam |
| `AnimationController` | `PlayAnimation(AnimationType)` | code-driven, no transition graph |
| `ObjectPooling` | `SpawnFromPool`, `ReturnToPool` | projectiles only; enemies are not pooled |

### AnimationController

Maps each `AnimationType` to an Animator **state name** in the Inspector and plays it with
`Animator.Play` — no transitions, no parameters. Add the clips to the controller and map them;
don't wire arrows.

**`uninterruptible` is the flag that matters.** `PlayerMovement` and the enemy AI push idle/run
animations every frame, so an interruptible attack clip is overwritten before a frame of it is
drawn. Tick it on attacks, hurt, stun and death.

---

## Project conventions

### Tags
`Enemy`, `Shield`, `Spikes`, `Weakpoint`. `Health.HazardTag` is `"Spikes"` — anything with
`killedByHazards` dies on contact with it.

### Layers
`0` Default · `3` Drawn Platforms · `4` Water · `6` Ground · `7` Walls · `8` Player ·
`9` Shield · `10` Enemies · `11` Ignorables · `12` Interactable · `13` Destroyabe environment

### Sorting layers, back to front
`Background` → `Default` → `Environment` → `Enemies` → `Player` → `Drawn Platforms` → `In front`

> **2D lights target sorting layers explicitly.** A sprite on a layer no `Light2D` covers renders
> **black**. If new art comes out black, add its sorting layer to the Global Light 2D's
> **Target Sorting Layers**.

### Enums are append-only

`AbilityType`, `PaintColour`, `AnimationType` and `AudioType` are all serialized **by index** in
prefab and scene lists. Appending is safe; **inserting silently repoints every existing row** to a
different value.

### Logging
Use `DebugUtils.Log` / `LogWarning` / `LogError`, which strip out of builds.

---

## Editor tools

| Menu | Does |
|---|---|
| `Tools → Painter → Build HUD Canvas` | builds the whole HUD canvas — five bars, wired to `PlayerHudUI` and `BossHealthBarUI`. Undoable. Save the scene afterwards |

`Assets → Create → Painter → Drained Palette` creates the shared drained-colour asset. Save it as
`Assets/Resources/DrainedPalette.asset` so components can find it. It's optional — without it
everything falls back to built-in defaults.

---

## Scenes

| Scene | Purpose |
|---|---|
| `recovered jam.unity` | **the current level** |
| `MainMenu.unity` | title screen |
| `Victory.unity` | end screen, loaded by `KeyPickup` |
| `Tutorial.unity`, `Tutorial 1.unity`, `Tutorial 2.unity` | tutorial levels |
| `Level 1.unity`, `Level Knight.unity` | older/test levels |
| `SampleScene.unity` | Unity default, unused |

Any scene you load at runtime must be in **File → Build Settings**.

---

## Known issues

- **`VineZone.Awake` calls `SetTrigger(true)` unconditionally**, so locked vines are already
  pass-through despite the docstring claiming they read as solid scenery.
- **`KnightEnemy.prefab` has two duplicate `AnimationController` components.** Harmless — both carry
  the same map — but one should be deleted.
- **`Player.prefab`'s `UI/InstaKill` action event has an unassigned target** (`m_Target: {fileID: 0}`),
  so the debug kill key does nothing. This is why `CurseAbility` binds its action by name in code
  rather than relying on the Inspector's event list.
- **`Level 1.unity`'s vcam is disabled**, so camera framing does nothing in that scene.
- **`PlayerCombat.meleeCooldown` and `stabCooldown` are declared and never read.**
- **`Assets/Sprites/Sprite sheets/tilemap-Sheet.png`** is a second, fully-coloured tileset backing 94
  tile assets. The colour system is built around the monochrome tileset; tinting won't drain this one.
