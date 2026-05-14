# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ShipButtlr is a Unity 6 (6000.3.7f1) 3D naval action game — a 1v1 torpedo battle between a player-controlled ship and a bot. It is a native Unity project — there are no npm/yarn scripts, Makefiles, or CLI build commands. All building, running, and testing is done through the Unity Editor.

**Coin economy:** Win awards +20 coins, losing awards +1 consolation coin. Stored in `PlayerPrefs` under key `"Coins"` via `CoinManager`. `CoinManager.AddCoins(amount)` accepts negative values (used for purchases).

**PlayerPrefs keys** (all persistence is via PlayerPrefs — no save files):
| Key | Type | Default | Meaning |
|---|---|---|---|
| `"Coins"` | int | 0 | Coin balance (managed by `CoinManager`) |
| `"YellowShipOwned"` | int | 0 | 1 = yellow ship purchased |
| `"SelectedShip"` | string | `"blue"` | Active ship: `"blue"` or `"yellow"` |
| `"Promo_pizza1"` … `"Promo_pizza8"` | int | 0 | 1 = promo code already redeemed |

## Development Workflow

- Open the project in Unity 6 Editor to build, run, and test
- Edit C# scripts in VS Code with IntelliSense via the `visualstudiotoolsforunity.vstuc` extension
- Attach the VS Code debugger to a running Unity Editor instance using the "Attach to Unity" launch config (`.vscode/launch.json`)
- Unity Test Framework (com.unity.test-framework 1.6.0) is available for writing Play Mode and Edit Mode tests via the Unity Test Runner window
- **Rebuild scenes from scratch**: run `ShipButtlr > Build All` from the Unity menu bar — this executes `Assets/Scripts/Editor/GameSetup.cs`, which programmatically creates all materials, prefabs, and both scenes. Script-only changes (no scene/material changes) are picked up by Unity automatically without a rebuild.

## Player Controls

- **Move**: WASD or arrow keys (forward/back/turn)
- **Fire**: Space or left mouse button — 2 s cooldown between shots

## Architecture

### Tech Stack
- **Engine:** Unity 6000.3.7f1 with Universal Render Pipeline (URP) 17.3.0
- **Input:** Unity New Input System 1.18.0 — existing gameplay scripts poll `Keyboard.current` / `Mouse.current` directly rather than using the generated `InputSystem_Actions` class
- **UI:** Legacy `UnityEngine.UI` (`Text`, `Image`, `Button`, `InputField`) — not TextMeshPro. `GameSetup` uses `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. Canvas mode: `ScreenSpaceOverlay`, `ScaleWithScreenSize` at 1920×1080.
- **Scripting:** C# targeting .NET Standard 2.1

### Key Directories
- `Assets/Scripts/` — all game logic scripts
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

**Promo codes** (`MainMenu.OnRedeemPromoCode()`): valid codes are `pizza1`–`pizza8`, each grants +5 coins once. Used codes are tracked per-code in PlayerPrefs (`"Promo_<code>"`). Feedback text turns green on success, orange for already-used, red for invalid.

**Ship card pattern in Bought tab** (both blue and yellow cards share this layout):
- ColorSwatch: `anchorMin=(0.05, 0.45)`, `anchorMax=(0.95, 0.92)`
- ShipNameText: `anchorMin=(0, 0.30)`, `anchorMax=(1, 0.43)`
- Bottom strip (0.04–0.26): SELECT button (left half, 0.05–0.52) + SELL button (right half, 0.55–0.95, red tint), OR "✓ SELECTED" label (full width) when selected

**Adding a new purchasable ship:** add its card to ToBuyContent and BoughtContent in `BuildShopPanel()`, add corresponding `public GameObject`/`public Button` fields to `MainMenu`, add `PlayerPrefs` key for ownership, add a new `"SelectedShip"` string value, and extend `RefreshShopUI()` and `ApplySelectedShip()` (in `PlayerShip.cs`) to handle the new variant.

**Runtime ship appearance** is applied in `PlayerShip.ApplySelectedShip()` — called from `Start()`. It reads `"SelectedShip"` from PlayerPrefs and sets both the color (`hull.material.SetColor("_BaseColor", …)` — uses `.material` instance, not `.sharedMaterial`) and `moveSpeed` on the PlayerShip component.

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
  ├── Torpedo     → routes damage via isPlayerTorpedo flag + root tag lookup
  ├── HealthBar   → driven by SetHealth(current, max) calls from ships
  │               → scales fill RectTransform localScale.x (not fillAmount); pivot (0, 0.5)
  ├── IslandData  → data component on each island root; stores radius for collision
  └── CameraFollow → Shake() coroutine (unscaledDeltaTime); shakeOffset applied
                     directly to transform.position AFTER the lerp (not inside it)
```

**GameManager** is a non-persistent singleton (not `DontDestroyOnLoad`). It sets `Time.timeScale = 0f` on game-over and restores it to `1f` on scene reload. `ShakeCamera(duration, magnitude)` is the public entry point for all camera shake — do not call `CameraFollow.Shake()` directly.

**BotShip AI** has three states driven by distance and HP:
- `APPROACH` (default) → moves toward player; transitions to `FLANK` when distance < 45
- `FLANK` → orbits player at ~40-unit radius; transitions back to `APPROACH` when distance > 55
- `RETREAT` → moves directly away for 3 seconds when HP < 30%; then returns to `APPROACH`

Bot fires every 1.6–2.4 s (randomised), with a 2 s initial delay. It aims at the player's position at the moment of firing — no leading/prediction.

**Torpedo** collision uses `collision.transform.root` to reach the ship script, since ships are multi-object hierarchies (root → Hull, Cabin, TorpedoSpawn). Tags used: `"Player"`, `"Enemy"`, `"Wall"`, `"Island"`. Collisions against friendly ships are silently ignored. Self-collision on spawn is prevented by a 0.1 s `Invoke` delay. Torpedoes explode on island base colliders (islands are tagged `"Island"`).

### Game Balance Values
| Stat | Value |
|---|---|
| Ship HP (both) | 245 |
| Torpedo damage | 35 (7 hits to kill) |
| Torpedo speed | 50 units/s |
| Torpedo lifetime | 5 s |
| Player fire cooldown | 2 s |
| Bot fire interval | 1.6–2.4 s random |
| Player move speed — blue ship | 15 units/s |
| Player move speed — yellow ship | 30 units/s (2× blue) |
| Bot move speed | 10 units/s |
| Yellow ship cost | 150 coins |
| Yellow ship sell price | 75 coins (half price) |
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

### Adding New Islands
Islands are created in `GameSetup.CreateIsland(Vector3 pos, float radius, int treeCount, mats)`. Each island root gets an `IslandData` component (runtime data) and the `"Island"` tag. To add an island: add a `CreateIsland(...)` call in `BuildGameScene()` — no other changes needed.

### Rendering
Two URP quality tiers:
- `PC_RPAsset` / `PC_Renderer` — full-quality desktop rendering
- `Mobile_RPAsset` / `Mobile_Renderer` — optimized mobile rendering

`MakeMat(assetName, color, smoothness, metallic)` creates and saves URP Lit materials. Water uses `smoothness=0.8, metallic=0.1` for reflectivity; all other gameplay materials use defaults (0, 0).
