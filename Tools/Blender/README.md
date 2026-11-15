# Level 7 — Kraken's Throne

A two-phase boss fight in the shared `GameScene` (root object `Level7_KrakensThrone`). It is unlocked after Level 6 and reached from the map ("7. Kraken's Throne", top right) or with **PLAY** once `CurrentLevel` is 7.

## How the encounter works

`BossEncounterController` (`Assets/Scripts/Kraken/`) is one state machine. Every number in it comes from the config asset.

```
Intro (1.2 s)
  └─► P1_Tentacles ── 20 s ──► tentacles sink ──► P1_Whirlpool ── 3 s ──► P1_Surfaced ── 5 s ──┐
         ▲                                                     (kraken emerges)                  │
         └──────────────────── P1_Dive ◄── kraken still alive (HP is kept) ◄────────────────────┤
                                                                                                 │ kraken HP 0
P1_KrakenDeath ◄─────────────────────────────────────────────────────────────────────────────────┘
  └─► P2_Sunfish (2 HP) ─► P2_ThroneRise ─► P2_Jellyfish (throne shielded) ─► P2_Rocks (throne 8 HP) ─► Victory ─► Done
                                                     ▲                              │ throne lost 4 HP (once):
                                                     └──── 5 more jellyfish ◄───────┘ +1 HP, shield back
```

Distances are in ship lengths (SL). The player hull is 12 units long, so 1 SL = 12 u.

| Phase | What happens |
|---|---|
| **1A Tentacles** | 8 tentacles stand on a ring just outside the 7 SL arena. At most 3 attack at once. Each attack shows a red disc on the water at the ship's position. An inner disc grows to fill the outer one over **1.5 s**, and the slam lands exactly when it is full. A hit deals 1 damage if any part of the hull is inside the disc. The phase lasts 20 s, then running slams finish and the tentacles sink. |
| **1B Whirlpool** | A whirlpool 7.5 SL across opens just outside the arena, on the player's side. The arena wall is released for these 3 s. The pull is 9 u/s at the rim and 19.5 u/s in the middle (the blue ship does 20), so steer away early. The surfaced kraken scales with the emergence zone. In the last second a red disc marks the emergence zone (half the whirlpool's area). If the ship is in it when the kraken bursts out, it takes **2 damage** and is knocked clear of the body. |
| **1C Surfaced** | The kraken is vulnerable for 5 s (5 HP in total, and the HP carries over between cycles). It spits ink blobs on an arc; a purple disc marks each landing spot. An ink hit deals **1 damage** and covers **80 %** of the screen for 1 s, then fades out over 0.5 s. If the kraken survives the window it dives and the cycle restarts at 1A. With a 1.5–2 s fire cooldown that usually takes two cycles. |
| **2A Sunfish** | The arena doubles to 14 SL and a ring of 48 rocks rises on its edge. Each new contact with a rock costs 1 HP (the L5+ island-crash rule). A harmless mola mola drifts in the arena. 2 hits sink it. |
| **2B Throne + jellyfish** | The kraken king rises in the middle of the arena on his throne (warning disc first). The throne blocks the ship, and ramming it costs 1 HP under the L5+ island rule. A bubble **shield** blocks torpedoes while any of the 4 jellyfish is alive. Jellyfish chase the ship and sting on contact (1 damage, then they bounce off and cool down). Every 5–8 s each one stops on the surface for 2.5 s: it slows to 30 % speed, its hitbox doubles, and it glows with a ring. |
| **2C Rocks** | The shield pops. Two smaller tentacles beside the throne take turns throwing rocks on arcs; an orange disc (1.5× the earlier size) marks each landing spot (1 damage). The king takes damage now (8 HP). Once he has lost 4 HP, he heals 1, the shield returns and **5 more jellyfish** appear (once). After they die the rocks resume. |
| **Victory** | The throne sinks, **7 dolphins** leap out in a ring around the ship and clap their flippers, then the normal "VICTORY!" end panel appears (+20 coins, `CurrentLevel` → 7). |

Pooling: danger zones, ink blobs and rocks come from `SimplePool`. The 3 ink-overlay textures are baked over the first frames of the level, so a hit never stalls a frame.

## Tuning

Select **`Assets/Config/KrakenLevelConfig.asset`** and edit it in the Inspector. Every timing, HP, damage, radius, count, speed and pool size is there, with tooltips; distances are in SL. Build All only creates the asset when it is missing, so **your values survive rebuilds**. Build All does re-measure `shipLength` from the player Hull collider. You can also use the context menu **Measure ship length** with GameScene open.

Changes to the tentacle count or ring radius need no rebuild either: the controller places the tentacles at runtime. Build All creates up to `tentacleCount` of them.

## Debug tools

These work in the Editor and in development builds only.

- **F1–F9** jump to: 1A tentacles, 1B whirlpool, 1C surfaced, kraken death, sunfish, throne rise, jellyfish, rocks, victory. **F10** deals 1 damage to the current target.
- A small **DBG** button (top left, under LEVEL) opens the same jumps as touch buttons, for testing on a phone with a development build.
- Select `Level7_KrakensThrone` in the Scene view to see the gizmos: arena circle (cyan) and wall clamp, tentacle ring (yellow), whirlpool + pull reach (blue), emergence zone (red), throne radius (magenta).
- Headless play-through: `Unity.exe -batchmode -projectPath <proj> -executeMethod KrakenSmokeTest.Run -logFile Logs/batch_smoke.log` (editor closed). It plays the whole encounter and writes `Logs/kraken_smoke.log` (state changes, damage taken per state, errors) and `Screenshots/level7_play_*.png`. Exit code 0 = pass.

## Regenerating the models

```
"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" -b --factory-startup --python Tools/Blender/build_kraken.py
```

This writes `3d/level7/*.fbx`, `KrakenTentacle.clips.json`, `Dolphin.clips.json` and `kraken.blend`. Then run **ShipButtlr › Build All**, which imports them into `Assets/Models/Kraken/`, splits the clips, creates `Assets/Animations/Kraken/Tentacle.controller` / `Dolphin.controller`, and rebuilds the prefabs and the scene. The script fails if any FBX is ≥ 2 MB or any mesh has more than 3 000 triangles. The largest is about 1 600.

| Model | Notes |
|---|---|
| `KrakenTentacle` | Rigged: bones `T0`–`T7`, plus `T8` as the tip marker where rocks are released. One timeline split into **Idle** (loop), **Rise**, **Windup** (loop), **Slam** (ends flat along +Z, tip 36 u out), **Recover**, **Throw**, **Sink**. The script checks the slam pose and writes its reach to the JSON. |
| `Dolphin` | Rigged, modelled upright (tail-walking): `D0`–`D4` spine, `FlipL`/`FlipR`. Clips **Jump**, **Applaud** (loop, 2 claps/s). |
| `KrakenBody`, `KrakenThrone` + `KrakenKing` (same origin), `Sunfish`, `Jellyfish`, `KrakenRock`, `InkBlob` | Static meshes with flat material slots (`L7_*`). Build All maps the slots to saved URP materials of the same name. |
| `KrakenWhirlpool` | A funnel disc with planar UVs. Unity draws it nearly flat (the sea plane is opaque) with `Assets/Shaders/Whirlpool.shader`, an animated URP unlit spiral. |

All geometry is written in Unity coordinates and exported with `-Z forward / Y up`, then imported with `bakeAxisConversion`. Build All verifies orientation from the geometry (eyes, belly, slam tip) and logs `Orientation OK/FAIL — …`. The tentacle needs a 180° yaw fix after import, and Build All applies it automatically.
