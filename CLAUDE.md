# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ShipButtlr is a Unity 6 (6000.3.7f1) 3D naval action game — a 1v1 torpedo battle between a player-controlled ship and a bot. It is a native Unity project — there are no npm/yarn scripts, Makefiles, or CLI build commands. All building, running, and testing is done through the Unity Editor.

**Target platform:** Android. The game is locked to **landscape orientation only** (`allowedAutorotateToPortrait: 0`, `allowedAutorotateToPortraitUpsideDown: 0` in `ProjectSettings/ProjectSettings.asset`).

**Coin economy:** Win awards +20 coins, losing awards +1 consolation coin. Stored in `PlayerPrefs` under key `"Coins"` via `CoinManager`. `CoinManager.AddCoins(amount)` accepts negative values (used for purchases).

**PlayerPrefs keys** (all persistence is via PlayerPrefs — no save files):
| Key | Type | Default | Meaning |
|---|---|---|---|
| `"Coins"` | int | 0 | Coin balance (managed by `CoinManager`) |
| `"YellowShipOwned"` | int | 0 | 1 = yellow ship purchased |
| `"YellowRedShipOwned"` | int | 0 | 1 = yellow-red ship unlocked via `pizza1` |
| `"PiratShipOwned"` | int | 0 | 1 = pirate ship purchased |
| `"SelectedShip"` | string | `"blue"` | Active ship: `"blue"`, `"yellow"`, `"yellowred"`, or `"pirat"` |
| `"Promo_pizza1"` … `"Promo_pizza8"` | int | 0 | 1 = promo code already redeemed |
| `"CurrentLevel"` | int | 1 | Highest unlocked level: 1 (open water) or 2 (islands) |

## Development Workflow

- Open the project in Unity 6 Editor to build, run, and test
- Edit C# scripts in VS Code with IntelliSense via the `visualstudiotoolsforunity.vstuc` extension
- Attach the VS Code debugger to a running Unity Editor instance using the "Attach to Unity" launch config (`.vscode/launch.json`)
- Unity Test Framework (com.unity.test-framework 1.6.0) is available for writing Play Mode and Edit Mode tests via the Unity Test Runner window
- **Rebuild scenes from scratch**: run `ShipButtlr > Build All` from the Unity menu bar — this executes `Assets/Scripts/Editor/GameSetup.cs`, which programmatically creates all materials, prefabs, and both scenes. Script-only changes (no scene/material changes) are picked up by Unity automatically without a rebuild.

## Player Controls

- **Move**: WASD or arrow keys (forward/back/turn) **or virtual joystick** (left side of screen on Android)
- **Fire**: Space or left mouse button **or tap right side of screen** — 2 s cooldown between shots

## Architecture

### Tech Stack
- **Engine:** Unity 6000.3.7f1 with Universal Render Pipeline (URP) 17.3.0
- **Input:** Unity New Input System 1.18.0 — gameplay scripts poll `Keyboard.current` / `Mouse.current` / `Touchscreen.current` directly rather than using the generated `InputSystem_Actions` class
- **UI:** Legacy `UnityEngine.UI` (`Text`, `Image`, `Button`, `InputField`) — not TextMeshPro. `GameSetup` uses `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. Canvas mode: `ScreenSpaceOverlay`, `ScaleWithScreenSize` at 1920×1080.
- **Scripting:** C# targeting .NET Standard 2.1

### Key Directories
- `Assets/Scripts/` — all game logic scripts
- `Assets/Scripts/VirtualJoystick.cs` — mobile joystick; exposes `Direction` (Vector2 −1…1); uses UI pointer events (`IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler`)
- `Assets/Scripts/FireZone.cs` — mobile fire zone; exposes `IsPressed`; polls `Touchscreen.current` in `Update()` (NOT UI events — avoids blocking the End Panel buttons)
- `Assets/Scripts/Editor/GameSetup.cs` — editor-only tool; run via `ShipButtlr > Build All`
- `Assets/Scenes/MainMenu.unity` / `Assets/Scenes/GameScene.unity` — the two scenes in build order
- `Assets/Prefabs/` — `Torpedo.prefab`, `ExplosionEffect.prefab` (created by Build All)
- `Assets/Materials/` — URP Lit materials (created by Build All); includes Water, Sand, Grass, TreeTrunk, TreeLeaves, Skybox, Player, Bot, Torpedo
- `Assets/Settings/` — URP render pipeline assets; PC and Mobile variants (`PC_RPAsset`, `Mobile_RPAsset`)

### Scene Flow
`MainMenu` → `GameScene` (play) → `MainMenu` (main menu button) or reload `GameScene` (play again). Scene names passed to `SceneManager.LoadScene` must match exactly: `"MainMenu"` and `"GameScene"`.

### Shop System

The Shop is a modal overlay panel on the MainMenu canvas, built entirely in `BuildShopPanel()` inside `GameSetup.cs`. All shop state is driven by `MainMenu.RefreshShopUI()` — this is the single method that reads PlayerPrefs and sets `SetActive` / `interactable` on every shop widget. Call it whenever state might have changed (on open, after buy, after sell, after select, after promo redemption).

**Two tabs** (content panels toggled via `ShowToBuyTab()` / `ShowBoughtTab()`):
- **To Buy** — shows ship cards for purchasable unowned ships (top area), plus a **Promo Code section** at the bottom (anchors 0.02–0.98 × 0.02–0.50 within ToBuyContent). The promo section has a label, a legacy `InputField`, a REDEEM button, and a feedback `Text`.
- **Bought** — shows all owned ships. Blue ship is always present. Each purchasable ship's card has: SELECT button + SELL button (both hidden when that ship is selected; replaced by "✓ SELECTED" label). Selling is blocked on the currently selected ship.

**Promo codes** (`MainMenu.OnRedeemPromoCode()`): valid codes are defined in the static array `MainMenu.s_validPromoCodes` (`pizza1`–`pizza8`). Each grants +5 coins once; used codes are tracked in PlayerPrefs (`"Promo_<code>"`). Feedback: green on success, orange for already-used, red for invalid.

- **`pizza1` special case**: also sets `"YellowRedShipOwned" = 1` and shows "+5 COINS + SHIP!" feedback.
- **`resett` special code**: not in `s_validPromoCodes`; handled first in `OnRedeemPromoCode()`. Iterates `s_validPromoCodes`, subtracts `PromoCodeReward` coins and clears the PlayerPrefs flag for each redeemed code; if `pizza1` was reset, also clears `"YellowRedShipOwned"`; also resets `"PiratShipOwned"` and deducts 200 coins if owned. Reverts `"SelectedShip"` to `"blue"` if any reset ship was selected. Shows cyan feedback. Can be used any number of times (not stored in PlayerPrefs).
- **`resetl` special code**: not in `s_validPromoCodes`; handled in `OnRedeemPromoCode()` before the valid-code check. Sets `"CurrentLevel"` to 1 and immediately updates `MainMenu.levelText` to "LEVEL: 1" so the top-right HUD reflects the reset without a scene reload. Shows cyan feedback. Can be used any number of times (not stored in PlayerPrefs).
- **Future-proofing**: add new codes to `s_validPromoCodes` and `resett` covers them automatically. If a new code unlocks a ship, add the ship-revocation logic to the `pizza1WasReset`-style check in the `resett` block.

**Ship card pattern in Bought tab:**
- Standard (purchasable) ships — blue, yellow: ColorSwatch `(0.05,0.45)–(0.95,0.92)`, ShipNameText `(0,0.30)–(1,0.43)`, bottom strip `(0.04–0.26)`: SELECT (left, 0.05–0.52) + SELL (right, 0.55–0.95, red tint), replaced by "✓ SELECTED" label when selected.
- Promo-only ships — yellow-red: same swatch/name layout but **no SELL button**; SELECT spans full width `(0.05–0.95)`. The swatch uses two nested `Image` children (bottom half yellow, top half red) instead of a single solid color.

**Adding a new purchasable ship:** add its card to ToBuyContent and BoughtContent in `BuildShopPanel()`, add corresponding `public GameObject`/`public Button` fields to `MainMenu`, add `PlayerPrefs` key for ownership, add a new `"SelectedShip"` string value, and extend `RefreshShopUI()` and `ApplySelectedShip()` (in `PlayerShip.cs`) to handle the new variant.

**Adding a new promo-only ship:** add its card to BoughtContent only (not ToBuyContent), add `public GameObject`/`public Button` fields to `MainMenu`, add `PlayerPrefs` ownership key, handle in `RefreshShopUI()` and `ApplySelectedShip()`, add the unlock to the relevant promo code's special-case block in `OnRedeemPromoCode()`, and add ship-revocation to the `resett` block (model after the `pizza1WasReset` pattern).

**Runtime ship appearance** is applied in `PlayerShip.ApplySelectedShip()` — called from `Start()` after the default HP/health-bar setup. It reads `"SelectedShip"` from PlayerPrefs, looks up the matching `ShipStats` from `ShipData`, and sets `moveSpeed`, `maxHP`, `currentHP`, `fireInterval`, and `torpedoDamage` from it, then sets hull/cabin colors and refreshes the health bar. The yellow-red ship uses split colors: yellow hull + red cabin. All ship stats are centrally defined in `Assets/Scripts/ShipData.cs`.

### Mobile Controls (Android)

Both keyboard and touch inputs are active simultaneously — works in Editor (keyboard) and on device (touch).

**Virtual Joystick** (`VirtualJoystick.cs`):
- Fixed position, bottom-left of HUD canvas: 260×260 circle at anchoredPosition `(150, 150)` from the `(0,0)` anchor
- Background circle (white, 25 % alpha) + inner stick circle (white, 75 % alpha, 110×110)
- Uses Unity UI pointer events — `raycastTarget = true` on its Image, so it participates in the UI event system
- `PlayerShip` reads `virtualJoystick.Direction` (Vector2) and adds it to keyboard forward/turn each frame

**Fire Zone** (`FireZone.cs`):
- Covers the right 55 % of screen (`anchorMin.x = 0.45`), transparent Image with **`raycastTarget = false`**
- Uses `Touchscreen.current` in `Update()` — checks if any active touch has `position.x > Screen.width * 0.45f`
- **Do NOT switch to UI pointer events** — doing so would re-introduce the bug where the transparent panel absorbs taps meant for the End Panel "PLAY AGAIN" / "MAIN MENU" buttons (FireZone is the last sibling in the HUD canvas, so it would be the topmost raycast target)
- `PlayerShip` reads `fireZone.IsPressed` in `HandleFiring()`; automatically returns false when `GameManager.IsGameOver`

**`PlayerShip.HandleMovement()` input order:**
1. Read keyboard (null-checked — `Keyboard.current` is null on Android with no physical keyboard)
2. Add joystick direction, clamp result to −1…1
3. Apply movement

**HUD canvas sibling order** (matters for raycast priority — last = topmost):
`PlayerHP` → `BotHP` → `EndPanel` → `CoinText` → `LevelText` → `JoystickBackground` → `FireZone`

### Core Script Relationships

```
CoinManager (DontDestroyOnLoad singleton — spawned in MainMenu, survives to GameScene;
             duplicate in GameScene is silently destroyed by Awake singleton check)

GameManager (non-persistent singleton, IsGameOver)
  ├── PlayerShip  → TakeDamage() → GameManager.PlayerDefeated()
  │               → PushOutOfIslands() / PushOutOfShip() — code-based collision
  │               → TryShake() → GameManager.ShakeCamera()
  ├── BotShip     → TakeDamage() → GameManager.BotDefeated()
  │               → PushOutOfIslands() / PushOutOfShip() — code-based collision
  │               → Level 3: ApplyPiratShipVisual() hides red box hull/cabin, instantiates
  │                 Assets/Models/PiratShip/pirat_ship.fbx scaled to 12 units (hull length)
  ├── Torpedo     → routes damage via isPlayerTorpedo flag + root tag lookup
  ├── HealthBar   → driven by SetHealth(current, max) calls from ships
  │               → scales fill RectTransform localScale.x (not fillAmount); pivot (0, 0.5)
  ├── IslandData  → data component on each island root; stores radius for collision
  └── CameraFollow → Shake() coroutine (unscaledDeltaTime); shakeOffset applied
                     directly to transform.position AFTER the lerp (not inside it)
                     → Start() snaps to correct position instantly (no lerp drift on game start)
                     → follows player at distance=21.6, height=8.8, smoothSpeed=5
                     → looks at target.position + Vector3.up*5 (tilted up to show sky)
```

**GameManager** is a non-persistent singleton (not `DontDestroyOnLoad`). It sets `Time.timeScale = 0f` on game-over and restores it to `1f` on scene reload. `ShakeCamera(duration, magnitude)` is the public entry point for all camera shake — do not call `CameraFollow.Shake()` directly.

**BotShip visual**: Bot ship type is determined by level at runtime in `BotShip.Start()`:
- **Level 1** — **Black ship**: box geometry scaled down by 1/1.5 (root `localScale = (0.667, 0.667, 0.667)`), hull/cabin painted near-black. Hull world length ≈ 8 units; collision half-radius = 4f; `minPlayerDist = 14f`.
- **Level 2** — **Blue ship**: hull/cabin painted blue via `material.SetColor`. Hull 12 units; collision half-radius = 6f; `minPlayerDist = 18f`.
- **Level 3** — **Pirate ship** (`pirat_ship.fbx`): box MeshRenderers are disabled, FBX instantiated as a child and auto-scaled so its longest XZ axis = 12 units. Both player and bot share the same `pirat_ship.fbx` asset (`Assets/Models/PiratShip/`). The Hull BoxCollider stays active for torpedo hits.

`ApplyBotShipVisual(isBlack, isPirat)` in `BotShip` dispatches to `ApplyBlackShipVisual()`, `ApplyPiratShipVisual()`, or blue-color assignment. The same `ApplyPiratShipVisual()` logic exists in `PlayerShip` for when the player selects the pirate ship. The FBX is exported from Blender (Z-up, `bakeAxisConversion: 0`) so rotation `(-90°X, 270°Y, 0°Z)` is applied before AABB measurement.

**Black ship** is bot-only (not available in the shop). It appears only on Level 1.

**BotShip AI** has two states — `AIM` and `REPOSITION` — in a forward-only attack-run loop:
- `AIM` (default) → rotates bow toward player (`RotateToward`), advances via `MoveForwardClamped()`, fires when player is within 10° of the bow and cooldown is ready, then transitions to `REPOSITION`.
- `REPOSITION` (post-fire) → rotates to a peel-off heading perpendicular to the player (random side, ±25° jitter), drives 24 units (2 hull lengths) forward via `MoveForwardClamped()`, then returns to `AIM`.

Bot fires every 1.6–2.4 s (randomised), with a 2 s initial delay. Torpedoes are aimed directly at the player's position, but the bot can only fire when the player is within the 10° forward arc (`FiringArc = 10f`) — no side-shots.

**18-unit floor** (`MinPlayerDist = 18f` — 1.5 hull lengths): all movement goes through `MoveForwardClamped()`. Steps that would cross inside 18 units from the player are projected onto the 18-unit ring — the bot slides/circles at the boundary rather than stopping or fleeing. The bot **never retreats** regardless of HP.

**Movement constraints**: bot moves forward only (`transform.forward`) — no strafing. Rotation and movement are strictly separated; only one `RotateToward` call per frame per state.

**Torpedo** collision uses `collision.transform.root` to reach the ship script, since ships are multi-object hierarchies (root → Hull, Cabin, TorpedoSpawn). Tags used: `"Player"`, `"Enemy"`, `"Wall"`, `"Island"`. Collisions against friendly ships are silently ignored. Self-collision on spawn is prevented by a 0.1 s `Invoke` delay. Torpedoes explode on island base colliders (islands are tagged `"Island"`).

### Game Balance Values
| Stat | Value |
|---|---|
| Ship HP — blue / yellow | 7 |
| Ship HP — yellow-red | 8 |
| Ship HP — pirate | 7 |
| Ship HP — black (bot L1) | 2 |
| Torpedo damage — blue / yellow / yellow-red / black | 1 |
| Torpedo damage — pirate | 1.2 |
| Torpedo speed | 50 units/s |
| Torpedo lifetime | 5 s |
| Player fire cooldown — blue / yellow / yellow-red | 2 s |
| Player fire cooldown — pirate | 1.5 s |
| Bot fire interval | ±20% of ship base delay |
| Player move speed — blue ship | 20 units/s |
| Player move speed — yellow ship | 40 units/s (2× blue) |
| Player move speed — yellow-red ship | 24 units/s (1.2× blue) |
| Player move speed — pirate ship | 20 units/s (same as blue) |
| Bot move speed — L1 (black) | 15 units/s |
| Bot move speed — L2 (blue) | 20 units/s |
| Bot move speed — L3 (pirate) | 20 units/s |
| Bot fire delay — L1 (black) | 2.5 s base (±20%) |
| Yellow ship cost | 150 coins |
| Yellow ship sell price | 75 coins (half price) |
| Pirate ship cost | 200 coins |
| Pirate ship sell price | 100 coins (half price) |
| Promo code reward | 5 coins |

### Arena & Environment
- **Sea plane**: 2000×2000 units (scale 200), covers the full visible horizon
- **Gameplay area**: Ships soft-clamped to ±95 on X and Z; Y always forced to 0
- **Walls**: Invisible `BoxCollider`-only at ±105 — catch torpedoes and other physics objects
- **Sky**: Procedural skybox (`Skybox/Procedural` shader) with sun disk wired to the directional light via `RenderSettings.sun`
- **Islands**: 6 decorative islands (flat cylinders, sandy base + green top) placed at ±42–80 units from center, each with 2–3 procedural trees (cylinder trunk + sphere canopy). Island positions and tree layout are deterministic (seeded `System.Random`).

### Collision System (No Rigidbody on Ships)
Both ships move via direct `transform.position +=` — Unity physics does not run on them. Collision is resolved in code each frame after the position update:

- **Ship vs island**: `PushOutOfIslands()` in each ship script. Reads `IslandData[]` (found in `Start()`). Circle-vs-circle in XZ: `shipRadius = 6f` (hull half-length), pushes ship center to `islandRadius + 6` from island center.
- **Ship vs ship**: `PushOutOfShip()` in each ship script. Combined minimum distance = 12 units (6 per ship).
- Island base `CapsuleCollider` is kept (not destroyed) so torpedoes physically collide with them. Tree and grass-top colliders are destroyed.

### Levels System

Two levels share a single GameScene. Level state persists via `"CurrentLevel"` PlayerPrefs key (highest unlocked level — never decreases).

- **Level 1** — open water, black bot ship: all 6 islands are parented under `IslandsRoot`. `GameManager.Awake()` calls `islandsRoot.SetActive(false)` when `playingLevel == 1`. Island collision is a no-op.
- **Level 2** — island map, blue bot ship: `IslandsRoot` active; islands behave as before.
- **Level 3** — Skull Shoals, pirate bot ship: `Islands3Root` active; one skull island + 6 rocks.
- **Level 4** — Volcano, survival mode: `Islands4Root` active; central Vulcano.fbx island. **Win condition: survive 120 s without dying.** One black ship spawns at game start (the scene's BotShip); an additional black ship spawns every 11 s (up to 5 total). Bots that are destroyed are simply removed — no game over. A countdown timer (`TimerText`, 48pt white bold, top-centre HUD) is shown; the bot HP bar is hidden. When the timer hits 0, all remaining bots are destroyed and `TriggerVictory()` is called. **Volcano eruption**: the volcano island has a `VolcanoEruption` component that launches a `VolcanoStone` projectile every 5 s, aimed at the player's current XZ position. Stones arc up from the volcano top `(0.5, 13.5, 1.4)` with `VertSpeed=22`, `Gravity=14`; time-of-flight is solved analytically so the stone lands on target. On impact the stone spawns a `SplashEffect` (blue-white burst particles), shakes the camera, and deals **3 damage** to the player if within 8 units. Prefabs: `Assets/Prefabs/VolcanoStone.prefab`, `Assets/Prefabs/SplashEffect.prefab` (created by Build All). Scripts: `VolcanoStone.cs`, `VolcanoEruption.cs`.
- **Progression**: `TriggerVictory()` (and `BotDefeated()` for levels 1-3) advances `"CurrentLevel"` by 1, capped at 4. Loss never changes the level.
- **Play Again after win**: advances to `playingLevel + 1` (capped at 4). Play Again after loss replays the same level.
- **UI**: A `"LEVEL: X"` text (black, 30pt) sits below the coin counter (top-left, anchoredPosition `(20, -75)`) on both the MainMenu canvas (`MainMenu.levelText`) and the GameScene HUD (`GameManager.levelText`). GameManager sets it from `playingLevel` in `Start()`.
- **Coin text color**: yellow (`Color.yellow`) on both canvases. **Level text color**: black (`Color.black`).

### Map Panel

A full-screen map modal on the MainMenu opened via the **MAP** button (between PLAY and SHOP). Shows a treasure-map image with clickable island hit areas.

**Map images** live in `Assets/Resources/` (copied from `images/` at project root by `GameSetup.CopyMapImages()` during Build All, imported as Sprites):
- `Level_1.png` — Coral Cove bright, rest dim. Shown when `CurrentLevel == 1`.
- `Level_2.png` — Coral Cove + Pirate's Rest bright. Shown when `CurrentLevel >= 2`.

**Runtime flow** (`MainMenu.cs`):
- `OnMapClicked()` → calls `RefreshMapImage()` (loads correct sprite via `Resources.Load<Sprite>()`) → sets `island2Button.interactable` based on `CurrentLevel` → shows panel.
- `OnIsland1Clicked()` / `OnIsland2Clicked()` → sets `MainMenu.levelToPlay = 1 or 2` → loads GameScene.
- `OnCloseMapClicked()` → hides panel.

**`MainMenu.levelToPlay` static override** (session-only, not persisted):
- Default `-1` — GameManager falls back to `PlayerPrefs.GetInt("CurrentLevel", 1)`.
- Set by map island buttons before loading GameScene.
- `GameManager.Awake()` reads it into `private int playingLevel`, then resets `levelToPlay = -1`.
- `MainMenu.Start()` also resets it to `-1` as a safety guard.
- Normal PLAY button never touches `levelToPlay` — always plays the current progress level.

**`GameManager` fields for level tracking:**
- `private int playingLevel` — which level this session is actually running (set in Awake, used for HUD and PlayAgain logic).
- `private bool wonLastGame` — set true in `BotDefeated()`, false by default. Used in `PlayAgain()` to decide whether to advance.

**Map panel structure** (built by `GameSetup.BuildMapPanel()`):
- `MapPanel` (full-screen, dark overlay) → `MapImage` (fills panel, sprite swapped at runtime) + `Island1Button`…`Island4Button` (transparent hit areas) + `CloseButton` (top-right "X").
- Island button anchors are calibrated against image pixel positions (image ≈ 600×400). **Do not estimate — measure from the island body, not the label text:**

| Button | anchorMin | anchorMax | Notes |
|---|---|---|---|
| Island1 (Coral Cove) | (0.07, 0.17) | (0.19, 0.31) | bottom-left |
| Island2 (Pirate's Rest) | (0.20, 0.47) | (0.32, 0.61) | middle-left |
| Island3 (Skull Shoals) | (0.23, 0.15) | (0.39, 0.31) | bottom-center |
| Island4 (Storm Isle) | (0.37, 0.38) | (0.60, 0.72) | center — volcano body, NOT the label to its right |

**4-button MainMenu layout** (anchor Y bands):
- PLAY: 0.56–0.65 | MAP: 0.44–0.53 | SHOP: 0.32–0.41 | QUIT: 0.20–0.29

### Adding New Islands
Islands are created in `GameSetup.CreateIsland(Vector3 pos, float radius, int treeCount, mats, Transform parent = null)`. Each island root gets an `IslandData` component (runtime data) and the `"Island"` tag. All calls in `BuildGameScene()` pass `islandsRootGO.transform` as the parent so the levels system can toggle them. To add an island: add a `CreateIsland(...)` call in `BuildGameScene()` with `islandsRootGO.transform` as the parent — no other changes needed.

### Rendering
Two URP quality tiers:
- `PC_RPAsset` / `PC_Renderer` — full-quality desktop rendering
- `Mobile_RPAsset` / `Mobile_Renderer` — optimized mobile rendering

`MakeMat(assetName, color, smoothness, metallic)` creates and saves URP Lit materials. Water uses `smoothness=0.8, metallic=0.1` for reflectivity; all other gameplay materials use defaults (0, 0).
