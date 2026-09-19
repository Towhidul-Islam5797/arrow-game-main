## Escape Game — Complete Architecture and Editing Walkthrough

**Project:** `Arrow game`  
**Unity:** `6000.4.9f1`  
**Rendering:** Universal Render Pipeline (URP), orthographic gameplay camera  
**Primary build scene:** `Assets/MultiTech Studio/Escape Game/Scene/Level Screen.unity`  
**Runtime namespace:** `MultiTechStudio.EscapeGame`

This document explains how the Escape Game is assembled, what happens at runtime, how its data moves through the project, and how to safely edit or recreate every major gameplay system. It is written for a developer who is learning Unity and for an AI assistant that needs a reliable map of the project.

---

## 1. What this game is

The Escape Game is a level-based grid puzzle. Each level contains one or more arrow-shaped lines. A line always moves forward in the direction implied by its final two grid dots.

- The player taps an arrow line to start or advance it.
- A moving line shifts forward one grid cell at a time.
- If the next cell is free, the line moves.
- If the line reaches the grid edge, it continues off-screen and is removed.
- If the next cell belongs to another arrow, the move is blocked and the player loses one life.
- The level is complete when every spawned arrow has exited the screen.

The game is **data-driven**:

1. A `LevelAsset` ScriptableObject stores the grid dimensions, camera size, and arrow paths as grid coordinates.
2. `LevelManager` reads that asset at runtime.
3. `GridManager` creates/reuses the needed dots.
4. `LevelManager` instantiates an arrow prefab for every saved arrow definition.
5. `ArrowLine` owns movement, collision/occupancy checks, rendering, and exiting.

The central design principle is: **level content lives in assets, while behaviour lives in scene managers and prefab components.**

---

## 2. Project map

```text
Assets/MultiTech Studio/Escape Game
├── Animations/                 # Animator Controllers and clips for snake/train/UI variants
├── AudioClips/                 # Music, UI, movement, collision, and victory WAV files
├── Documents/                  # Package documentation; this walkthrough lives here
├── Editor/                     # Unity Editor-only tools; excluded from builds
├── Fonts/                      # Lilita One and TextMeshPro font assets
├── Levels/                     # Level1.asset through Level100.asset
├── Materials/                  # Arrow, neon, snake, train, confetti, and skybox materials
├── Prefabs/                    # Reusable gameplay and UI objects
├── Scene/
│   ├── Level Screen.unity      # Shipped gameplay scene and enabled build scene
│   └── Level Editor.unity      # Designer scene for authoring/saving level layouts
├── Scripts/                    # Runtime systems
├── Settings/                   # URP assets and volume profiles
├── Sprites/                    # Gameplay, UI, shop, snake, and train artwork
├── Themes/                     # Cosmetic ThemeData assets and CurrentTheme.asset
└── ProjectSetupData.asset      # Central IAP/ad identifiers and settings
```

Other project scenes exist, but they are not part of the Escape Game build flow:

- `Assets/Scenes/SampleScene.unity` is a minimal sample scene.
- `Assets/Settings/Scenes/URP2DSceneTemplate.unity` is a scene template.

---

## 3. The two important scenes

### 3.1 `Level Screen.unity` — the player-facing scene

This is the build scene. It has the following high-level hierarchy:

```text
Level Screen
├── Main Camera                 # Orthographic Camera + CameraController
│   └── Background              # Theme-controlled SpriteRenderer background
├── Directional Light
├── Global Volume               # Uses Settings/SampleSceneProfile.asset
├── Grid                         # GridManager and generated Dot children
│   └── Dot_x_y                 # One Dot object per configured grid coordinate
├── Confetti Cannon - Diamonds  # Victory particle prefab instance
├── Canvas                       # Screen-space UI and UiManager
│   ├── Main Screen
│   ├── Shop Screen
│   ├── Play Ui
│   ├── Play_Result
│   ├── Play_Fail
│   ├── Setting Panel
│   ├── Buy Hint Panel
│   └── Buy Grid Panel
├── EventSystem
├── LevelManager
├── Firebase
├── Ads Manager
├── Iap Manager
├── Tutorial Manager
├── Theme Setup                  # ThemeManager
└── Sound Manager
```

Important scene assignments currently include:

- `Grid` uses `Dot.prefab`, starts configured as a 15×15 grid, has 1.5 spacing and origin `(-8.73, -7.82)`.
- `LevelManager` references `Level1.asset` through `Level100.asset`, `Arrow.prefab`, `CurrentTheme.asset`, the grid, camera, tutorial manager, and UI manager.
- `Canvas` contains both `UiManager` and `FeatureManager`.
- The camera is orthographic and initially has size `28`; `CameraController` changes it for each level.
- The Canvas uses `ScreenSpaceOverlay` and a `ConstantPixelSize` scaler.

### 3.2 `Level Editor.unity` — the authoring scene

This scene is for creating level data, not for shipping gameplay. It contains a large grid, `LevelManager`, `RuntimePainter`, `GameMode`, Canvas, and an EventSystem.

`GameMode` starts in **Draw** mode in this scene. While playing in the Editor:

- Drag across adjacent, unoccupied dots to draw an arrow path.
- The runtime painter previews the path with a `LineRenderer`.
- Releasing the pointer spawns an `ArrowLine` from that path.
- Use its Editor-only Game View buttons to undo the newest arrow or save all arrows into a `LevelAsset`.

Do not rely on manually placed arrows in the level editor scene as your shipped level data. The output you want is a `LevelAsset` in `Levels/`, which must then be assigned in `LevelManager.levels` in `Level Screen.unity`.

---

## 4. Runtime lifecycle: from pressing Play to winning a level

This is the full normal gameplay pipeline.

```text
Unity loads Level Screen
        │
        ├─ GridManager.Awake/OnEnable → GridManager.I is set
        ├─ LevelManager.Awake → LevelManager.Instance is set; creates `_Arrows` child if needed
        ├─ SoundManager.Awake → builds audio dictionaries/source pools and persists itself
        └─ ThemeManager.Awake → ThemeManager.Instance is set
        │
        ├─ LevelManager.Start
        │  ├─ Applies current theme's arrow prefab
        │  ├─ Reads saved `LevelIndex` from PlayerPrefs
        │  ├─ Calls LoadLevel(index)
        │  └─ connects onLevelWin → UiManager.LevelComplete
        │
        ├─ LoadLevel(index)
        │  ├─ Clears previous spawned arrows
        │  ├─ GridManager.ApplyFromAsset(level)
        │  ├─ CameraController.SetForNewLevel(level.camSize)
        │  ├─ Builds dot lookup
        │  ├─ Shows level-specific tutorial
        │  ├─ Instantiates every saved ArrowLineDef
        │  └─ Hides unused dots after a short delay
        │
        ├─ UiManager.Start
        │  ├─ registers UI button callbacks
        │  ├─ updates level, coins, and life UI
        │  └─ starts music ID `music1`
        │
        └─ Player presses Play
           ├─ UiManager switches from menu to gameplay UI
           ├─ Grid dots animate when DOTween is enabled
           └─ LevelManager.levelState becomes Playing

Player taps an arrow
        │
        └─ ArrowLine.Update detects the tap using Physics2D overlap
           └─ ArrowLine.TryAdvance → StepOnce
              ├─ free next dot → shift the line forward
              ├─ occupied next dot → onLineHitAction → LevelManager.OnLineHit → UiManager.RemoveLife
              └─ no next dot → exit animation → LevelManager.OnLineExited
                                           │
                                           └─ last arrow exited → onLevelWin → UiManager.LevelComplete
```

### State gate

`LevelManager.levelState` prevents accidental game input at the wrong time.

| `LevelState` | Meaning | Typical owner |
|---|---|---|
| `NotStarted` | Level loaded but player has not started gameplay | Menu/settings flow |
| `Playing` | Arrow input and camera controls are active | `UiManager.PlayButtonClicked` |
| `Completed` | Victory flow is active | `UiManager.LevelComplete` |
| `Failed` | Lives reached zero and failure UI is active | `UiManager.RemoveLife` |
| `Paused` | Feature purchase dialog is open | `FeatureManager` |

`ArrowLine.Update` only accepts manual clicks when the game is in Play mode and `levelState == Playing`.

---

## 5. Core gameplay architecture

### 5.1 `LevelAsset` — the saved level format

**Source:** `Scripts/LevelAsset.cs`

`LevelAsset` is a ScriptableObject. A ScriptableObject is a Unity asset that stores data independently from a scene. This is why 100 different levels can use the same scripts and prefabs.

```csharp
LevelAsset
├── levelName
├── grid: GridDef
│   ├── width
│   ├── height
│   ├── spacing
│   └── origin
├── arrows: List<ArrowLineDef>
├── camSize
├── arrowCount                 # Metadata for editor/design use
├── dotsCount                  # Metadata for editor/design use
├── totalPathPoints            # Metadata for editor/design use
├── coinsEarned
└── levelDifficulty            # Easy, Medium, Hard, VeryHard
```

Each `ArrowLineDef` contains:

```csharp
ArrowLineDef
├── path: List<Vector2Int>     # Ordered cells, tail to head
├── stepTime                   # Stored by painter; runtime manager currently overrides it
├── zOffset                    # LineRenderer depth
├── occupyAllNodes             # Whether its whole body reserves dots
├── headZ                      # Head visual depth
├── lineColor
├── hitColor
└── startIndex
```

A path is a list of grid coordinates, not world positions. For example, a three-cell horizontal arrow could be:

```text
[(2, 4), (3, 4), (4, 4)]
```

The arrow head is at `(4, 4)`, because it is the final entry. Its direction is `(4,4) - (3,4) = (1,0)`, meaning it moves right.

### 5.2 `GridManager` — creates and indexes the grid

**Source:** `Scripts/GridManager.cs`

`GridManager` is the bridge between level coordinate data and visible `Dot` GameObjects.

Responsibilities:

- Holds grid dimensions, spacing, and origin.
- Creates, reuses, positions, names, and removes dots in `RebuildSmart()`.
- Converts between coordinate systems.
- Maintains a dictionary from `Vector2Int` coordinate to `Dot` for quick lookup.
- Clears occupancy when a level reloads.
- Hides free dots after the level has been assembled.
- Provides optional DOTween dot animations.

Coordinate conversion is simple:

```csharp
worldX = origin.x + gridX * spacing;
worldY = origin.y + gridY * spacing;
```

The inverse uses `Mathf.RoundToInt`, so dots should be placed on the expected spacing grid.

`RebuildSmart()` is deliberately safer than destroying and recreating every dot. It:

1. Ensures a `_Dots` parent exists if no parent was assigned.
2. Reads existing children and keeps valid unique dots that remain in bounds.
3. Removes invalid or duplicate children.
4. Reuses valid dots, creates only missing dots, and assigns names like `Dot_4_2`.
5. Rebuilds both the 2D array and coordinate dictionary.
6. Shows all dots before `LevelManager` later hides unused ones.

### 5.3 `Dot` — one grid cell

**Source:** `Scripts/Dot.cs`

A dot has only three important pieces of state:

```csharp
public Vector2Int G;        // Grid coordinate
public ArrowLine occupant;  // Arrow claiming this grid point, otherwise null
public bool IsFree => occupant == null;
```

`HideDot()` and `ShowDot()` enable/disable its `SpriteRenderer`. A hidden dot still exists and can still be used for grid/path logic.

### 5.4 `LevelManager` — the gameplay orchestrator

**Source:** `Scripts/LevelManager.cs`

`LevelManager` is the central coordinator and singleton (`LevelManager.Instance`). It does not perform line movement itself. It tells the grid and arrow prefabs what level to create, then responds to arrow events.

Important fields:

- `levels`: ordered list of all `LevelAsset` assets.
- `arrowLinePrefab`: default cosmetic/gameplay arrow prefab.
- `currentThemeAsset`: optional `CurrentTheme` asset that can override the arrow prefab.
- `arrowsParent`: runtime parent for all spawned arrows. If empty, a `_Arrows` child is created automatically.
- `currentLevelIndex`: persisted as PlayerPrefs key `LevelIndex`.
- `startLives`, `coinsRequiredToRetry`, `tutorialZoomLevel`, and `moveSpeedForStep`.
- `onLevelWin`: UnityEvent to which `UiManager.LevelComplete` is added in `Start()`.

`LoadLevel(index)` follows this exact order:

1. Delete previously spawned arrows.
2. Validate the level list/index.
3. Save the selected index to `PlayerPrefs`.
4. Tell `GridManager` to apply the asset's grid definition and rebuild dots.
5. Reset/set the camera using `level.camSize`.
6. Build the grid-coordinate-to-dot lookup.
7. Show a tutorial if that index requires one.
8. Convert every `ArrowLineDef.path` coordinate into actual `Dot` references.
9. Instantiate the configured arrow prefab under `_Arrows`.
10. Copy movement/rule/visual values onto the arrow instance.
11. Set its path, synchronize its LineRenderer immediately, and add it to `_spawnedLines`.
12. After 0.5 seconds, hide all unoccupied dots.

When the final arrow exits, `OnLineExited` removes it from `_spawnedLines`; if the list is empty, `onLevelWin` fires.

### 5.5 `ArrowLine` — the moving puzzle piece

**Source:** `Scripts/ArrowLine.cs`

`ArrowLine` requires a `LineRenderer` and `EdgeCollider2D`. This is enforced by `[RequireComponent]`; any arrow-themed prefab intended for runtime use must satisfy it.

A valid arrow prefab also normally needs:

```text
Arrow prefab root
├── ArrowLine                  # Movement, occupancy, input, events
├── LineRenderer               # Visible body line
├── EdgeCollider2D             # Tap/click hit target; kept synchronized with line
└── Head                       # Optional Transform referenced by `headVisual`
```

#### Path and direction

The `nodes` list is ordered tail-to-head. On every step, the component reads the final two nodes:

```text
previous = nodes[n - 2]
head     = nodes[n - 1]
direction = head.G - previous.G
nextCell = head.G + direction
```

This is why the final two saved path cells must be cardinal neighbours. If they are not, movement direction becomes incorrect.

#### Occupancy rules

At `Start()`, the line claims its nodes. Each `Dot` remembers one `occupant` arrow.

When the arrow successfully moves:

1. The tail dot is released if `occupyAllNodes` is true.
2. Every node reference shifts one place toward the tail.
3. The target dot becomes the new final/head node.
4. The target is claimed.
5. The `LineRenderer` positions and `EdgeCollider2D` points are rebuilt.

Before moving, it checks the next dot:

- No dot ahead: the arrow starts the exit sequence.
- A different arrow owns the dot: collision/failure. `onLineHitAction` fires, the line flashes `hitColor`, and queued input is cleared.
- Free or self-owned dot: it moves.

#### Player input and queued movement

`Update()` checks for mouse or first-touch input. It projects the pointer through the assigned camera or `Camera.main`, then uses `Physics2D.OverlapCircleAll()` around the pointer. When its own collider is one of the hits, it calls `TryAdvance()`.

While a DOTween movement animation is active, a tap queues up to three extra steps. With `autoRunToEnd` enabled on `LevelManager`, a line continues stepping automatically after each successful step. The value is copied to every spawned arrow.

#### Rendering, head, and exiting

- `SyncVisualImmediate()` converts `nodes` into world positions at `zOffset`, then writes them to the `LineRenderer` and collider.
- The `headVisual` is positioned on the final rendered segment and rotated to face its travel direction.
- At the edge, `StartExit()` frees all claimed dots first, so other arrows can use that space.
- `ExitStepOnce()` shifts all visible line positions forward repeatedly until every point is beyond the camera viewport plus `exitMargin`.
- Then it calls `LevelManager.OnLineExited(this)` and destroys itself.

#### Hint support

`CanBeRemove()` probes forward from the arrow head until it finds either an unavailable cell or the grid edge. It returns `true` only if the path ahead can reach the edge without another arrow blocking it. `LevelManager.FindLineCanBeRemove()` blinks the first qualifying line.

### 5.6 `UiManager` — menus, lives, currency, and result flow

**Source:** `Scripts/UiManager.cs`

`UiManager` is attached to the Canvas and controls the named panels assigned in the Inspector. It registers button callbacks in `Start()` and removes them in `OnDisable()`.

Important data:

- `coins` is persisted with PlayerPrefs key `Coins`.
- `lives` is runtime-only and reset by `UpdateLives()` for a level attempt.
- `OnCoinsChanged` triggers both coin text fields to refresh.

Key flows:

| Action | What happens |
|---|---|
| Main Play button | Hides menu, enables play UI, resets display, animates grid dots, changes state to `Playing` |
| A line hits another line | `RemoveLife()` hides one icon; at zero, waits two seconds then shows failure panel |
| Final arrow exits | `LevelComplete()` enables confetti, animates grid, increments level index, changes state, then shows result panel |
| Next Level | Optionally shows interstitial, adds normal reward coins, loads the already-incremented index, re-enters play |
| Rewarded result button | After a rewarded ad callback, gives double reward and loads next level |
| Retry with coins | Deducts retry cost, resets lives, restores gameplay UI |
| Retry with ad | After a rewarded ad callback, resets lives to one and restores gameplay UI |
| Main menu | Reloads current level, returns to main menu, and sets state to `NotStarted` |

The completion coin animation instantiates `Coin Anim.prefab` copies beneath the result panel. With DOTween they fan out and then move toward the normal coin text. Without DOTween they move immediately and are destroyed after a delay.

### 5.7 `FeatureManager` — hints and show-grid consumables

**Source:** `Scripts/FeatureManager.cs`

This component is on the Canvas. It manages two consumable counters:

- `HintCount` in PlayerPrefs
- `GridCount` in PlayerPrefs

If the player has an item, clicking its button decrements the count and calls:

- Hint: `LevelManager.FindLineCanBeRemove()`
- Grid: `LevelManager.ShowGrid()`

If the player has no item, it pauses gameplay (`LevelState.Paused`) and opens the appropriate purchase panel. The item can be bought with coins or awarded after a rewarded ad callback.

### 5.8 `CameraController` — level framing and optional player camera controls

**Source:** `Scripts/CameraController.cs`

The gameplay camera is orthographic. The controller:

- resets the camera position and sets zoom on every level load;
- permits mouse wheel and two-finger pinch zoom only when level index is at least 5 and gameplay state is `Playing`;
- permits mouse drag or one-finger panning within optional configured limits;
- scales the background proportionally to camera size, keeping the background visually filled.

`LevelAsset.camSize` is the intended initial orthographic size for that level. Set it in the level editor scene before saving a level asset.

---

## 6. Visual, theme, and prefab pipeline

### 6.1 Available prefabs

| Prefab | Purpose / required components |
|---|---|
| `Arrow.prefab` | Default gameplay arrow; root has `ArrowLine`, child `Head` |
| `Arrow Neon.prefab` | Arrow variant with `ArrowLine` and `LineColors` |
| `Snake 1/2/3.prefab` | Themed moving lines with `ArrowLine`, `LineAnimation`, and `LineColors` |
| `Train.prefab`, `Train 1.prefab` | Themed moving lines with `ArrowLine`, `LineAnimation`, and `LineColors` |
| `Dot.prefab` | Grid point; root contains `Dot`; layer is `Ignore Raycast` |
| `Coin Anim.prefab` | UI coin spawned by completion animation; layer is `UI` |
| `Confetti Cannon - Diamonds.prefab` | Particle effect instantiated in the level scene |
| `Theme Ui.prefab` | Shop item UI prefab used by `ThemeShopManager` |

### 6.2 How themes work

**Sources:** `Scripts/ThemeData.cs`, `Scripts/CurrentTheme.cs`, `Scripts/ThemeManager.cs`

A theme is another ScriptableObject (`ThemeData`) containing:

```text
ThemeData
├── themeName / themeIcon
├── arrowPrefab                # Cosmetic ArrowLine-compatible prefab
├── haveDifferentColors
├── colors                     # Palette used by LineColors
├── skyboxMaterial
├── gridIcon                   # Sprite assigned to existing dot SpriteRenderers
├── background                 # Sprite assigned to Background SpriteRenderer
└── price
```

`CurrentTheme.asset` stores the one active `ThemeData` reference. `ThemeManager` applies it at startup and when a player selects a purchased theme:

1. Set `CurrentTheme.ActiveTheme`.
2. Use the theme arrow prefab as `LevelManager.arrowLinePrefab`.
3. Set `RenderSettings.skybox`.
4. Set the `Background` SpriteRenderer's sprite.
5. Replace the sprite on existing grid dots.
6. Update an optional UI icon.
7. Reload the level so existing arrows use the new prefab.

`LineColors` runs on themed arrow prefab instances. It copies the active theme palette, picks one random color, creates a new material instance, applies the color to the line and related sprites, and calls `ArrowLine.SetBaseColor()` so a collision flash resets correctly.

`LineAnimation` hooks itself into the `ArrowLine` events. It starts an idle state and triggers configured Animator states for hit and exit events.

### 6.3 Theme shop pipeline

**Sources:** `ThemeShopManager.cs`, `ThemeShopItemUI.cs`

At startup, `ThemeShopManager`:

1. Finds missing manager references.
2. Marks every theme priced `0` as purchased.
3. Instantiates one `Theme Ui` item for each item in `availableThemes`.
4. Reads the purchased state from PlayerPrefs key `Theme_Purchased_<asset-name>`.
5. Marks the current theme item active.

Each item either displays its price and a Buy action, or `USE` and a Use action. A successful purchase immediately applies the new theme.

### 6.4 Creating a new visual theme

1. Create or duplicate an arrow-compatible prefab in `Prefabs/`.
2. Ensure it has `ArrowLine`, `LineRenderer`, `EdgeCollider2D`, and a correctly assigned head Transform. Add `LineAnimation` and/or `LineColors` only if the new visual needs them.
3. Create a `ThemeData` asset under `Themes/` using **Assets > Create > Tools > MultiTech Studio > Escape Game Theme Data**.
4. Assign name, icon, arrow prefab, optional palette, skybox, dot sprite, background, and price.
5. Add the theme asset to `ThemeShopManager.availableThemes` in `Level Screen.unity` if it should be purchasable.
6. Test a theme change in play mode. Theme application reloads the active level.

Editor-only theme tooling is available at **Tools > MultiTech Studio > Escape Game Setup**. It can create/edit/apply themes, edit colors, and update the `CurrentTheme` asset.

---

## 7. Level creation pipeline

### Recommended designer workflow

1. Open `Scene/Level Editor.unity`.
2. Select the `Grid` object and configure `width`, `height`, `spacing`, and `origin` in `GridManager`.
3. Ensure the intended camera framing is visible; its orthographic size becomes `LevelAsset.camSize` when saving.
4. Enter Play mode.
5. Keep `GameMode` in **Draw** mode. `H` toggles Draw/Play; Space steps all arrows when in Play mode.
6. Drag across cardinally adjacent dots to form one arrow path. Turns are valid; diagonal jumps, loops, overlap, and drawing over existing arrows are rejected.
7. Release to create the runtime arrow.
8. Use the runtime painter's Editor-only Game View UI to undo if needed.
9. Set `levelAssetName` on `RuntimePainter` and use **Save Arrows as Level Asset**.
10. A new/updated asset is written to `Levels/<levelAssetName>.asset`.
11. Open `Level Screen.unity`, add that asset to `LevelManager.levels` in the desired order, and test from a clean level index.

### What the painter saves

`ArrowLineRuntimePainterL.SaveArrowsAsLevel()` gathers all `ArrowLine` objects, serializes each dot's `G` coordinate into `ArrowLineDef.path`, captures visual/movement settings, copies grid data, captures the current orthographic camera size, computes metadata, and writes the asset to `Levels/`.

### Screenshot import (experimental editor-only path)

The runtime painter also contains an optional screenshot import feature, disabled by default. It can load a PNG/JPG, optionally use manual grid dimensions, sample cells for dark/light strokes, flood-fill connected components, trace paths, attempt arrowhead direction detection, remove overlapping paths, and produce temporary arrow lines for confirmation. It is a convenience feature, not the primary authoring workflow; always inspect and test imported paths before saving.

### Level validation checklist

Before shipping a level, verify:

- Every arrow has at least two dots.
- The final two dots of every arrow are cardinally adjacent.
- Paths do not overlap at spawn time when `occupyAllNodes` is enabled.
- The intended solution allows one arrow to leave without being blocked.
- Camera size frames the complete grid on the intended device.
- `coinsEarned` and `levelDifficulty` are set intentionally.
- The asset is present in `LevelManager.levels` in the intended sequence.

---

## 8. Audio, ads, IAP, analytics, and optional SDK pipeline

### 8.1 Sound

**Source:** `Scripts/SoundManager.cs`

`SoundManager` is a persistent singleton and creates two pools of 2D `AudioSource`s:

- Sound effects pool: default size 10.
- Music pool: default size 2.

Inspector-assigned `AudioClipEntry` records map a string ID to clip plus multiplier. Runtime code uses IDs such as:

```text
music1          # startup menu music
button          # normal button presses
close           # closing panels
arrow           # arrow tap
whoosh          # arrow leaves screen
block           # collision/block
level_complete  # victory
```

Player choices are persisted through:

```text
SoundEnabled, MusicEnabled, SoundVolume, MusicVolume
```

`SettingsUIManager` drives the two toggles. It temporarily changes the level state while settings are open, then restores the previous state when closed.

### 8.2 Ads

**Source:** `Scripts/AdsManager.cs`

Ads are conditional. All Google Mobile Ads code only compiles when `MT_ADMOB` is defined and the AdMob package provides `GoogleMobileAds.Api.MobileAds`.

`ProjectSetupData.asset` stores banner, interstitial, rewarded interstitial, rewarded ad IDs, plus threshold/settings for interstitials. The defaults embedded in the script are Google test IDs and should be replaced with production IDs before release.

Supported flow:

- A No Ads purchase writes PlayerPrefs key `NoAdsPurchase` and hides assigned ad buttons.
- Interstitials can be shown after a configured level threshold if enabled, loaded, and No Ads is not owned.
- Rewarded ad callbacks grant the retry, extra completion reward, hint, or grid benefit requested by the caller.

When the SDK is absent, availability checks return false. This makes the base project compile without AdMob.

### 8.3 IAP

**Source:** `Scripts/IapManager.cs`

IAP is conditional on `MT_IAP`. `ProjectSetupData.iapProducts` holds product ID, item type, product type, and reward amount. Scene UI button references remain in `IapManager.allIaps`, because a ScriptableObject cannot safely hold scene-button references.

Supported item types include `NoAds`, bundles, and coin products. Successful purchases add coins and/or call `AdsManager.SetNoAdsPurchase(true)`.

The code currently uses the older `IDetailedStoreListener` pattern behind the optional define. It includes comments that a full IAP v5 migration would require the newer event-driven API.

### 8.4 Firebase

**Source:** `Scripts/FirebaseManager.cs`

Firebase is conditional on `MT_FIREBASE`. If enabled, startup checks dependencies. `SendLevelData()` writes current level and current coin count as Crashlytics custom keys. `UiManager.PlayButtonClicked()` calls it.

### 8.5 Compile-time integration strategy

**Sources:** `Editor/AutoDefines.cs`, `Editor/SDKDefinesHelper.cs`

The project avoids mandatory third-party SDK dependencies by using compile symbols:

| Define | Enables |
|---|---|
| `MT_ADMOB` | Google Mobile Ads calls |
| `MT_FIREBASE` | Firebase/Crashlytics calls |
| `MT_IAP` | Unity IAP calls and shop UI |
| `MT_DOTWEEN` | DOTween movement/UI/grid animation |

`AutoDefines` detects AdMob, Firebase, and IAP types when Unity loads and updates their defines. DOTween is never auto-added; it is removed when missing and can be enabled manually from the setup window. Without DOTween, gameplay remains functional but uses immediate visual changes and `Invoke`/coroutine fallbacks instead of tweened movement and UI effects.

---

## 9. Persistent player data

The project uses `PlayerPrefs`, which is simple local storage. It persists between launches on a device but is not cloud save, encrypted storage, or server-authoritative progression.

| Key | Owner | Meaning |
|---|---|---|
| `LevelIndex` | `LevelManager` | Zero-based current progression index |
| `Coins` | `UiManager` | Soft currency amount |
| `HintCount` | `FeatureManager` | Owned hint consumables |
| `GridCount` | `FeatureManager` | Owned grid-reveal consumables |
| `Theme_Purchased_<asset-name>` | `ThemeShopManager` | Per-theme purchase state |
| `NoAdsPurchase` | `AdsManager` | No-ads ownership flag |
| `SoundEnabled`, `MusicEnabled` | `SoundManager` | Audio toggles |
| `SoundVolume`, `MusicVolume` | `SoundManager` | Audio volume values |

For testing from a fresh profile, clear PlayerPrefs in a temporary debug action or reset the app's local data. Do not change saved key names in a published product unless you add a migration path.

---

## 10. Script-by-script reference

### Core runtime

| Script | Responsibility |
|---|---|
| `LevelManager.cs` | Loads level assets, spawns/removes arrows, controls progression, victory event, themed prefab selection, grid hints |
| `GridManager.cs` | Rebuilds/reuses dots, coordinate conversion, dot lookup, occupancy reset, grid visual effects |
| `Dot.cs` | One grid coordinate and its occupying arrow reference |
| `ArrowLine.cs` | Input hit detection, path movement, occupancy, exit detection, rendering/collider synchronization, arrow events |
| `ArrowFacingLines.cs` | Optional temporary forward-facing guide lines; not part of the primary loop unless assigned/used |
| `ArrowLineRuntimePainterL.cs` | Play-mode level authoring, undo, save-to-LevelAsset, experimental screenshot import |
| `LevelAsset.cs` | Serializable level/grid/arrow definition types |
| `GameMode.cs` | Draw-vs-Play toggle for authoring/testing; `H` toggles, Space advances all lines in Play |
| `CameraController.cs` | Per-level framing and player pan/zoom controls |
| `TutorialManager.cs` | Shows tutorial 1 for index 0 and tutorial 6 at `tutorialZoomLevel`; hides both otherwise |

### UI, monetisation, and services

| Script | Responsibility |
|---|---|
| `UiManager.cs` | Screen panels, level/life/coin presentation, retry/next/menu flow, reward animation |
| `FeatureManager.cs` | Hint/grid item ownership, purchases, ad rewards, temporary pause state |
| `SettingsUIManager.cs` | Settings panel, audio toggles, visual toggle animation |
| `SoundManager.cs` | Pooled two-dimensional audio and saved audio preferences |
| `AdsManager.cs` | Optional banner/interstitial/rewarded ad load/display flow |
| `IapManager.cs` | Optional IAP initialization, product button mapping, and reward grants |
| `FirebaseManager.cs` | Optional Firebase dependency initialization and Crashlytics values |
| `ProjectSetupData.cs` | Central ScriptableObject for ad IDs and IAP product data |

### Themes and animation

| Script | Responsibility |
|---|---|
| `ThemeData.cs` | Cosmetic configuration asset format |
| `CurrentTheme.cs` | Active theme reference plus a runtime change event |
| `ThemeManager.cs` | Applies a chosen theme to prefab, skybox, background, dots, icon, then reloads level |
| `ThemeShopManager.cs` | Creates theme store items, tracks PlayerPrefs ownership, buys/uses themes |
| `ThemeShopItemUI.cs` | Displays and operates one theme store entry |
| `LineColors.cs` | Chooses a random active-theme palette color for a spawned line instance |
| `LineAnimation.cs` | Plays Animator idle/hit/exit states based on arrow events |

### Editor-only tools

| Script | Responsibility |
|---|---|
| `EscapeGameSetupWindow.cs` | Adds **Tools > MultiTech Studio > Escape Game Setup** window |
| `ThemeHelpers.cs` | Finds/creates the themes folder, default assets, and selected editor theme |
| `ThemeOperations.cs` | Creates/deletes/applies themes to scene/prefab/current-theme data |
| `ThemeTabDrawer.cs` | Theme list/detail editor UI |
| `SettingsTabDrawer.cs` | SDK/setup data controls and status UI |
| `SDKDefinesHelper.cs` | Read/write scripting defines for selected build target |
| `AutoDefines.cs` | Detects installed SDK types and adjusts relevant defines on editor load |

---

## 11. Safe edit and recreation recipes

### Change a level's puzzle layout

Preferred: use `Level Editor.unity`, paint the new layout, save an asset, and update `LevelManager.levels`.

Alternative: select the relevant `LevelX.asset` and edit its `grid` and `arrows` lists manually. Each path must remain tail-to-head and cardinally adjacent. Manual editing is faster for small coordinate changes but easier to break.

### Add level 101

1. Create it through the level editor and save it as `Level101.asset` under `Levels/`.
2. Set `levelName`, reward, difficulty, and camera size.
3. Open `Level Screen.unity`.
4. Add `Level101.asset` to the end of `LevelManager.levels`.
5. Test loading it by setting PlayerPrefs `LevelIndex` to `100` in a controlled test environment.

`IncreaseLevelNumber()` wraps back to index 0 after the last assigned asset. If you want a different end-of-content behaviour, change that method deliberately.

### Change move speed globally

Set `LevelManager.moveSpeedForStep` in `Level Screen.unity`. During spawn, this currently overrides every `ArrowLineDef.stepTime` value. Therefore, editing `stepTime` inside an individual level definition does not change gameplay speed unless `LevelManager.TrySpawnLineFromDef()` is changed to use `def.stepTime` instead.

### Add a new arrow skin

1. Duplicate `Arrow.prefab`.
2. Keep the required `ArrowLine`, `LineRenderer`, and `EdgeCollider2D` components.
3. Keep/assign `headVisual` to the new `Head` child.
4. If it animates, add/configure `LineAnimation` and an Animator.
5. If it varies color, add/configure `LineColors` and a material reference.
6. Reference it from a `ThemeData` asset.

### Change game balance

- Starting lives: `LevelManager.startLives`
- Standard level reward: each `LevelAsset.coinsEarned`
- Retry coin price: `LevelManager.coinsRequiredToRetry`
- Hint/grid purchase prices: `FeatureManager.hintPrice` and `gridPrice`
- Theme price: each `ThemeData.price`
- Ad threshold: `ProjectSetupData.interstitialLevelThreshold`

### Add a new tutorial

`TutorialManager` is currently hardcoded to two tutorial objects and two indices. To add more:

1. Add the tutorial panel under Canvas.
2. Add a serialized `GameObject` reference to `TutorialManager`.
3. Add a corresponding case to `ShowTutorial(int levelIndex)`.
4. Ensure `HideTutorial()` deactivates it.
5. Assign the UI object in `Level Screen.unity`.

For a scalable content-heavy tutorial system, replace the switch with a serialized list/dictionary of level index to panel reference.

---

## 12. Important implementation notes and review points

These are observable behaviours in the current code that matter when extending it.

1. **`LevelManager` uses singleton references without universal null checks.** Required scene managers must exist and initialize correctly.
2. **The project uses legacy `UnityEngine.Input` APIs** even though the project has the Input System package installed and active input handling supports both. Preserve current behaviour unless intentionally migrating input end-to-end.
3. **`ArrowLine` depends on 2D physics for clicking.** Make sure new themed arrow prefabs retain a working `EdgeCollider2D`; otherwise the visual can appear but not accept clicks.
4. **The arrow direction is inferred, not separately stored.** The last two path cells are gameplay-critical.
5. **`occupyAllNodes` must match the intended puzzle rules.** With it true, the full line body blocks other lines. `ArrowLine.Start()` claims all nodes.
6. **Theme application calls `levelManager.ReloadLevel()`.** Changing a theme immediately recreates the level's arrow instances.
7. **Optional SDK branches compile out when their define is absent.** Test features both with and without these integrations when modifying shared systems.
8. **DOTween is optional.** Never add direct DOTween calls outside `#if MT_DOTWEEN` without adding a non-DOTween fallback.
9. **The project is mobile-oriented.** Project settings include mobile/PC quality profiles, all orientations enabled, and mobile targets set to the Mobile quality level.
10. **`CurrentTheme.OnThemeChanged` exists but ThemeManager's event subscription is currently commented out.** Theme application is presently driven explicitly through `ThemeManager.ApplyTheme()`.
11. **Arrow `startIndex` is private and written by reflection in `LevelManager`.** This works but is fragile; if refactoring `ArrowLine`, replace that reflection with a public initialization method.
12. **Retry flows do not call `LevelManager.LoadLevel()` directly.** They mostly reset UI/lives and change state. Verify intended board reset behavior carefully before changing retry rules.

---

## 13. Technical setup summary

### Installed/observed Unity packages

- Universal Render Pipeline `17.4.0`
- Input System `1.19.0`
- uGUI `2.0.0`
- Timeline `1.8.12`
- Unity Test Framework `1.6.0`
- Visual Scripting `1.9.11`
- Unity 2D feature package `2.0.2`
- Device Simulator devices `1.0.1`
- IDE integration packages and Unity Version Control proxy

### Build and graphics

- The only enabled build scene is `Scene/Level Screen.unity`.
- URP is configured with separate mobile and PC pipeline/renderer assets.
- Android, iPhone, and WebGL use Mobile quality by default; Standalone uses PC quality.
- Project defaults include 1024×768 resolution and portrait/landscape autorotation enabled.

---

## 14. Copyable AI/developer handoff prompt

Use the following message with another GPT or developer. Give it this Markdown file and the Unity project folder.

```text
You are taking over a Unity 6000.4 URP project named Arrow game. The gameplay package is at:
Assets/MultiTech Studio/Escape Game

Read `Assets/MultiTech Studio/Escape Game/Documents/EscapeWalkthrough.md` before making any change. Treat it as the current architecture map, then verify any specific source file you modify.

The game is a data-driven grid puzzle:
- `LevelAsset` assets in `Levels/` store grid settings, ordered arrow paths, camera size, reward, and difficulty.
- `LevelManager` loads a level, asks `GridManager` to build the dots, spawns one `ArrowLine` prefab per saved definition, and wins when all arrows leave.
- `ArrowLine` derives movement direction from the final two path dots, claims Dot occupancy, checks blocking, moves or exits, and raises hit/exit UnityEvents.
- `UiManager` owns menus, lives, coins, retry, completion, and UI transitions.
- Themes are `ThemeData` assets applied by `ThemeManager`; applying a theme reloads the level.
- Ads, Firebase, IAP, and DOTween are optional and controlled by `MT_ADMOB`, `MT_FIREBASE`, `MT_IAP`, and `MT_DOTWEEN` defines. Keep conditional compilation and fallbacks intact.

When implementing changes:
1. Preserve `MultiTechStudio.EscapeGame` namespace and existing asset-driven design.
2. Inspect the target scene/prefab/script before editing it.
3. Keep arrow paths cardinal and ordered tail-to-head.
4. Keep `ArrowLine`, `LineRenderer`, and `EdgeCollider2D` on every gameplay arrow prefab.
5. Do not hardcode per-level content into scripts; add/change `LevelAsset` data instead.
6. Do not remove non-DOTween fallbacks or optional-SDK compilation guards.
7. Confirm Inspector references after creating or changing any scene object, prefab, or ScriptableObject.
8. Explain every change in beginner-friendly language and name the affected files/assets.

For source-level details, workflows, persistence keys, prefab requirements, known implementation notes, and extension recipes, use EscapeWalkthrough.md as the starting point.
```

---

## 15. First places to look for common tasks

| You want to… | Start here |
|---|---|
| Edit a puzzle | `Scene/Level Editor.unity` → save `Levels/LevelX.asset` |
| Change game rules/movement | `Scripts/ArrowLine.cs` and `Scripts/LevelManager.cs` |
| Change grid dimensions/visual behaviour | `Scripts/GridManager.cs`, `Prefabs/Dot.prefab` |
| Change screen flow, lives, coins | `Scripts/UiManager.cs` |
| Change hints/grid purchases | `Scripts/FeatureManager.cs` |
| Add an arrow/snake/train visual | `Prefabs/`, `Themes/`, `ThemeData.cs` |
| Change or add UI | `Scene/Level Screen.unity` Canvas hierarchy and `UiManager` references |
| Change audio | `Sound Manager` Inspector entries, `AudioClips/`, `SoundManager.cs` |
| Enable/store configure optional services | `ProjectSetupData.asset`, Tools > Escape Game Setup, service scripts |
| Understand all saved local values | Section 9 of this document |

The safest mental model is: **assets describe content, prefabs describe reusable objects, managers coordinate systems, and the scene wires those systems together through Inspector references.**
