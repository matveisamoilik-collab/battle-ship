# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ShipButtlr is a Unity 6 (6000.3.7f1) 3D naval action game — a 1v1 torpedo battle between a player-controlled ship and a bot. It is a native Unity project — there are no npm/yarn scripts, Makefiles, or CLI build commands. All building, running, and testing is done through the Unity Editor.

**Coin economy:** Win awards +20 coins, losing awards +1 consolation coin. Stored in `PlayerPrefs` under key `"Coins"` via `CoinManager`.

## Development Workflow

- Open the project in Unity 6 Editor to build, run, and test
- Edit C# scripts in VS Code with IntelliSense via the `visualstudiotoolsforunity.vstuc` extension
- Attach the VS Code debugger to a running Unity Editor instance using the "Attach to Unity" launch config (`.vscode/launch.json`)
- Unity Test Framework (com.unity.test-framework 1.6.0) is available for writing Play Mode and Edit Mode tests via the Unity Test Runner window
- **Rebuild scenes from scratch**: run `ShipButtlr > Build All` from the Unity menu bar — this executes `Assets/Scripts/Editor/GameSetup.cs`, which programmatically creates all materials, prefabs, and both scenes. Script-only changes (no scene/material changes) are picked up by Unity automatically without a rebuild.

## Architecture

### Tech Stack
- **Engine:** Unity 6000.3.7f1 with Universal Render Pipeline (URP) 17.3.0
- **Input:** Unity New Input System 1.18.0 — existing gameplay scripts poll `Keyboard.current` / `Mouse.current` directly rather than using the generated `InputSystem_Actions` class
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

### Core Script Relationships

```
GameManager (singleton, IsGameOver)
  ├── PlayerShip  → TakeDamage() → GameManager.PlayerDefeated()
  │               → PushOutOfIslands() / PushOutOfShip() — code-based collision
  │               → TryShake() → GameManager.ShakeCamera()
  ├── BotShip     → TakeDamage() → GameManager.BotDefeated()
  │               → PushOutOfIslands() / PushOutOfShip() — code-based collision
  ├── Torpedo     → routes damage via isPlayerTorpedo flag + root tag lookup
  ├── HealthBar   → driven by SetHealth(current, max) calls from ships
  ├── IslandData  → data component on each island root; stores radius for collision
  ├── HealthBar   → scales fill RectTransform localScale.x (not fillAmount); pivot set to (0, 0.5)
  └── CameraFollow → Shake() coroutine (unscaledDeltaTime); shakeOffset applied
                     directly to transform.position AFTER the lerp (not inside it)
```

**GameManager** is a non-persistent singleton (not `DontDestroyOnLoad`). It sets `Time.timeScale = 0f` on game-over and restores it to `1f` on scene reload. `ShakeCamera(duration, magnitude)` is the public entry point for all camera shake — do not call `CameraFollow.Shake()` directly.

**BotShip AI** has three states driven by distance and HP:
- `APPROACH` (default) → moves toward player; transitions to `FLANK` when distance < 45
- `FLANK` → orbits player at ~40-unit radius; transitions back to `APPROACH` when distance > 55
- `RETREAT` → moves directly away for 3 seconds when HP < 30%; then returns to `APPROACH`

**Torpedo** collision uses `collision.transform.root` to reach the ship script, since ships are multi-object hierarchies (root → Hull, Cabin, TorpedoSpawn). Tags used: `"Player"`, `"Enemy"`, `"Wall"`, `"Island"`. Collisions against friendly ships are silently ignored. Self-collision on spawn is prevented by a 0.1 s `Invoke` delay. Torpedoes explode on island base colliders (islands are tagged `"Island"`).

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
