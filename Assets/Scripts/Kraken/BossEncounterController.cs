using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Level 7 "Kraken's Throne" - the two-phase boss encounter, as one state machine.
//
//  Intro -> P1_Tentacles (20 s, 8 tentacles, <= 3 slams at once, 1.5 s red telegraph)
//        -> P1_Whirlpool (3 s pull from outside the arena, kraken emerges: 2 dmg + knockback)
//        -> P1_Surfaced  (5 s vulnerable, spits ink: 1 dmg + 80 % screen ink)
//              alive -> P1_Dive -> P1_Tentacles (HP persists)      dead -> P1_KrakenDeath
//        -> P2_Sunfish (2 HP, harmless) -> P2_ThroneRise -> P2_Jellyfish (throne shielded)
//        -> P2_Rocks (2 tentacles throw rocks, throne damageable, 8 HP)
//              after 4 damage (once): +1 HP, shield back, 5 more jellyfish -> P2_Jellyfish -> P2_Rocks
//        -> Victory (7 dolphins around the ship) -> Done
// Phase 2 plays in an arena twice as wide, walled by rocks that cost HP on contact.
//
// Every number comes from KrakenLevelConfig. Children (tentacles, whirlpool, kraken, sunfish,
// throne, jellyfish, dolphin) do their own animation and call back into this host.
// Debug (Editor / development builds): F1..F9 jump to a state, F10 deals 1 damage to the current
// target; a "DBG" button on the HUD opens the same jumps as buttons. Gizmos show the geometry.
public class BossEncounterController : MonoBehaviour, IArcOwner
{
    public enum State
    {
        None, Intro, P1_Tentacles, P1_Whirlpool, P1_Surfaced, P1_Dive, P1_KrakenDeath,
        P2_Sunfish, P2_ThroneRise, P2_Jellyfish, P2_Rocks, Victory, Done
    }

    public static readonly Color DangerRed  = new Color(1f, 0.12f, 0.1f, 0.4f);
    public static readonly Color InkPurple  = new Color(0.35f, 0.1f, 0.55f, 0.4f);
    public static readonly Color RockOrange = new Color(1f, 0.55f, 0.1f, 0.4f);

    [Header("Config")]
    public KrakenLevelConfig config;

    [Header("Actors")]
    public TentacleAttacker[] tentacles;
    public Whirlpool          whirlpool;
    public SurfacedKraken     kraken;
    public Sunfish            sunfish;
    public ThroneKraken       throne;
    public DolphinApplause    dolphin;
    public GameObject         jellyfishPrefab;
    public GameObject         edgeRockPrefab;     // Phase 2 arena wall rock (IslandData + collider + model)

    [Header("Pooled prefabs / effects")]
    public GameObject dangerZonePrefab;
    public GameObject inkPrefab;
    public GameObject rockPrefab;
    public GameObject splashPrefab;
    public GameObject explosionPrefab;

    [Header("UI / visuals")]
    public CameraSplat inkSplat;
    public BossHUD     hud;
    public Material    arenaRingMaterial;

    // ---- runtime ----
    State   state = State.None;
    float   stateTime;
    Transform  player;
    PlayerShip playerShip;
    Vector3    lastPlayerPos;
    Vector3    playerVel;

    SimplePool zonePool, inkPool, rockPool;
    readonly List<DangerZone>    activeZones = new List<DangerZone>();
    readonly List<ArcProjectile> activeArcs  = new List<ArcProjectile>();
    readonly List<Jellyfish>     jellies     = new List<Jellyfish>();

    float      attackTimer;
    bool       tentaclesSinking;
    float      sinkTimer;
    Vector3    whirlCentre;
    DangerZone stateZone;           // emergence / throne-rise telegraph
    bool       flag;                // per-state one-shot
    float      inkTimer;
    float      rockTimer;
    int        nextThrower;
    float      sunfishDeadTime = -1f;
    int        lastShownSecond = -1;
    bool       dolphinShown;
    bool       phase2;              // bigger arena + rock wall
    bool       secondWaveDone;      // second jellyfish wave already triggered
    Transform  edgeRocks;           // parent of the Phase 2 wall rocks
    float      edgeRockRise = -1f;  // >= 0 while the rocks rise
    Transform  arenaRing;
    readonly List<DolphinApplause> dolphins = new List<DolphinApplause>();

    // Current arena radius: Phase 2 is phase2ArenaScale times wider
    public float ArenaRadius => phase2 ? config.Phase2ArenaRadius : config.ArenaRadius;
    public Transform  Player      => player;
    public Vector3    ArenaCentre => KrakenUtil.Flat(transform.position);
    public List<Jellyfish> Jellies => jellies;
    public State      CurrentState => state;

    // ------------------------------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------------------------------

    void Start()
    {
        if (config == null) config = ScriptableObject.CreateInstance<KrakenLevelConfig>();
        var pgo = GameObject.FindWithTag("Player");
        if (pgo != null)
        {
            player = pgo.transform;
            playerShip = pgo.GetComponent<PlayerShip>();
            lastPlayerPos = player.position;
        }

        zonePool = new SimplePool(dangerZonePrefab, transform, config.zonePoolSize);
        if (inkPrefab  != null) inkPool  = new SimplePool(inkPrefab,  transform, config.inkPoolSize);
        if (rockPrefab != null) rockPool = new SimplePool(rockPrefab, transform, config.rockPoolSize);

        PlaceTentacles();
        foreach (var t in tentacles) if (t != null) t.Init(this, config, ArenaCentre);
        if (whirlpool != null) whirlpool.Init(config);
        if (kraken    != null) kraken.Init(this, config);
        if (sunfish   != null) sunfish.Init(this, config);
        if (throne    != null) throne.Init(this, config);
        if (dolphin   != null)
        {
            // one dolphin is built by GameSetup; the rest of the ring are runtime copies
            dolphins.Add(dolphin);
            for (int i = 1; i < config.dolphinCount; i++)
                dolphins.Add(Instantiate(dolphin.gameObject, dolphin.transform.parent).GetComponent<DolphinApplause>());
            for (int i = 0; i < dolphins.Count; i++) dolphins[i].Init(this, playSounds: i == 0);
        }
        BuildEdgeRocks();

        if (inkSplat != null)
        {
            inkSplat.coverageTarget = config.inkScreenCoverage;
            inkSplat.holdDuration   = config.inkHoldTime;
            inkSplat.fadeDuration   = config.inkFadeTime;
            inkSplat.Pregenerate(config.inkSplatCached);
        }

        BuildArenaRing();
        if (hud != null) hud.Show("KRAKEN", "PHASE 1 / 2", config.krakenHP, config.krakenHP, BossHUD.KrakenColor);
        if (hud != null) hud.SetDim(true);
        SetState(State.Intro);
        BuildDebugPanel();
    }

    // Spread the tentacles evenly on the ring (positions come from the config, so changing the
    // count or radius needs no rebuild)
    void PlaceTentacles()
    {
        int n = tentacles != null ? tentacles.Length : 0;
        for (int i = 0; i < n; i++)
        {
            if (tentacles[i] == null) continue;
            bool on = i < config.tentacleCount;
            tentacles[i].gameObject.SetActive(on);
            float a = (i + 0.5f) * Mathf.PI * 2f / Mathf.Max(1, Mathf.Min(n, config.tentacleCount));
            tentacles[i].transform.position = ArenaCentre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * config.TentacleRingRadius;
        }
    }

    // Thin glowing ring on the water marking the arena edge
    void BuildArenaRing()
    {
        if (arenaRingMaterial == null) return;
        const int seg = 96;
        float r = config.ArenaRadius, w = 0.7f;
        var verts = new Vector3[seg * 2];
        var tris  = new int[seg * 6];
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            verts[i * 2]     = d * (r - w * 0.5f);
            verts[i * 2 + 1] = d * (r + w * 0.5f);
            int n = (i + 1) % seg;
            tris[i * 6]     = i * 2;     tris[i * 6 + 1] = n * 2;     tris[i * 6 + 2] = i * 2 + 1;
            tris[i * 6 + 3] = i * 2 + 1; tris[i * 6 + 4] = n * 2;     tris[i * 6 + 5] = n * 2 + 1;
        }
        var mesh = new Mesh { name = "ArenaRing", vertices = verts, triangles = tris };
        mesh.RecalculateNormals();
        var go = new GameObject("ArenaRing");
        go.transform.SetParent(transform, false);
        arenaRing = go.transform;
        go.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = arenaRingMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Phase 2 wall: a ring of rocks on the edge of the doubled arena (hidden during Phase 1)
    void BuildEdgeRocks()
    {
        if (edgeRockPrefab == null || config.edgeRockCount <= 0) return;
        edgeRocks = new GameObject("EdgeRocks").transform;
        edgeRocks.SetParent(transform, false);
        var rng = new System.Random(77);
        float r = config.Phase2ArenaRadius;
        for (int i = 0; i < config.edgeRockCount; i++)
        {
            float a = i * Mathf.PI * 2f / config.edgeRockCount;
            var go = Instantiate(edgeRockPrefab, edgeRocks);
            go.transform.localPosition = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            float s = 0.85f + (float)rng.NextDouble() * 0.35f;
            var model = go.transform.Find("Model");
            if (model != null) model.localScale *= s;
            var island = go.GetComponent<IslandData>();
            if (island != null) island.radius = config.edgeRockRadius;
        }
        edgeRocks.gameObject.SetActive(false);
    }

    // Enter / leave the Phase 2 layout: doubled arena, rock wall rising out of the sea
    void SetPhase2(bool on, bool instant)
    {
        if (phase2 == on && !instant) return;
        phase2 = on;
        if (arenaRing != null) arenaRing.localScale = on ? new Vector3(config.phase2ArenaScale, 1f, config.phase2ArenaScale) : Vector3.one;
        if (edgeRocks != null)
        {
            edgeRocks.gameObject.SetActive(on);
            edgeRockRise = on && !instant ? 0f : -1f;
            edgeRocks.localPosition = new Vector3(0f, on && !instant ? -12f : 0f, 0f);
            if (on && !instant)
            {
                SoundManager.PlayAt("big_explosion", ArenaCentre + Vector3.forward * config.Phase2ArenaRadius, 0.8f, 0f, 0.5f);
                GameManager.Instance?.ShakeCamera(config.edgeRockRiseTime, 0.2f);
            }
        }
        if (playerShip != null) playerShip.RefreshIslands();
    }

    void TickEdgeRocks()
    {
        if (edgeRockRise < 0f || edgeRocks == null) return;
        edgeRockRise += Time.deltaTime;
        float k = KrakenUtil.Smooth01(edgeRockRise / Mathf.Max(0.01f, config.edgeRockRiseTime));
        edgeRocks.localPosition = new Vector3(0f, Mathf.Lerp(-12f, 0f, k), 0f);
        if (k >= 1f) edgeRockRise = -1f;
    }

    // ------------------------------------------------------------------------------------------
    // State machine
    // ------------------------------------------------------------------------------------------

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver)
        {
            if (playerShip != null) playerShip.SetExternalDrift(Vector3.zero);
            return;
        }
        if (player != null)
        {
            Vector3 v = (player.position - lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            playerVel = Vector3.Lerp(playerVel, KrakenUtil.Flat(v), Mathf.Clamp01(Time.deltaTime * 6f));
            lastPlayerPos = player.position;
        }
        stateTime += Time.deltaTime;
        DebugKeys();
        TickEdgeRocks();
        Tick();
    }

    void SetState(State s)
    {
        ExitState(state);
        state = s;
        stateTime = 0f;
        flag = false;
        lastShownSecond = -1;
        EnterState(s);
    }

    void EnterState(State s)
    {
        switch (s)
        {
            case State.Intro:
                SetArena(true);
                Objective("KRAKEN'S THRONE");
                break;

            case State.P1_Tentacles:
                SetArena(true);
                foreach (var t in tentacles) if (t != null && t.gameObject.activeSelf) t.Emerge(config.tentacleRiseTime);
                attackTimer = config.tentacleRiseTime + 0.4f;
                tentaclesSinking = false;
                if (hud != null) { hud.SetDim(true); hud.SetHP(kraken.HP, kraken.MaxHP); }
                break;

            case State.P1_Whirlpool:
            {
                SetArena(false);
                Vector3 dir = player != null ? KrakenUtil.Flat(player.position - ArenaCentre) : Vector3.zero;
                if (dir.sqrMagnitude < 4f) dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                whirlCentre = WhirlpoolCentreFor(dir);
                whirlpool.Begin(whirlCentre, config.WhirlpoolRadius);
                Objective("PHASE 1 · ESCAPE THE WHIRLPOOL!");
                break;
            }

            case State.P1_Surfaced:
                SetArena(true);
                inkTimer = config.inkFirstDelay;
                Objective("PHASE 1 · ATTACK THE KRAKEN!");
                if (hud != null) { hud.SetDim(false); hud.SetHP(kraken.HP, kraken.MaxHP); }
                break;

            case State.P1_Dive:
                kraken.Dive(config.krakenDiveTime);
                Objective("THE KRAKEN DIVES...");
                if (hud != null) hud.SetDim(true);
                break;

            case State.P1_KrakenDeath:
                kraken.Die(config.krakenDeathTime);
                Objective("THE KRAKEN SINKS...");
                if (hud != null) hud.SetHP(0f, kraken.MaxHP);
                break;

            case State.P2_Sunfish:
                SetPhase2(true, instant: false);
                secondWaveDone = false;
                SetArena(true);
                sunfish.Surface(PickSunfishSpot());
                sunfishDeadTime = -1f;
                Objective("PHASE 2 · SINK THE SUNFISH");
                if (hud != null) hud.Show("SUNFISH", "PHASE 2 / 2", config.sunfishHP, config.sunfishHP, BossHUD.FishColor);
                break;

            case State.P2_ThroneRise:
                SetArena(true);
                stateZone = GetZone();
                stateZone.Begin(ArenaCentre, config.ThroneRadius, config.throneTelegraph, DangerRed);
                Objective("PHASE 2 · THE KRAKEN KING RISES!");
                if (hud != null) { hud.Show("KRAKEN KING", "PHASE 2 / 2", config.throneHP, config.throneHP, BossHUD.KrakenColor); hud.SetDim(true); }
                break;

            case State.P2_Jellyfish:
                SetArena(true);
                if (!throne.IsRisen) throne.ShowRisenImmediate();
                throne.SetShield(true);
                SpawnJellyfish(secondWaveDone ? config.secondWaveJellyfish : config.jellyfishCount);
                UpdateJellyObjective();
                if (hud != null) { hud.SetDim(false); hud.SetShield(true); hud.SetHP(throne.HP, config.throneHP); }
                break;

            case State.P2_Rocks:
                SetArena(true);
                if (!throne.IsRisen) throne.ShowRisenImmediate();
                throne.SetShield(false);
                foreach (var th in throne.throwers) if (th != null) th.Emerge(config.tentacleRiseTime);
                rockTimer = config.tentacleRiseTime + 0.6f;
                nextThrower = 0;
                Objective("PHASE 2 · ATTACK THE KRAKEN KING!");
                if (hud != null) { hud.SetShield(false); hud.SetDim(false); hud.SetHP(throne.HP, config.throneHP); }
                break;

            case State.Victory:
                GameManager.Instance?.SetPlayerInvulnerable(true);
                ClearProjectilesAndZones();
                foreach (var j in jellies) if (j != null) Destroy(j.gameObject);
                jellies.Clear();
                if (throne.CurrentState != ThroneKraken.State.Hidden) throne.Sink(config.throneSinkTime);
                Objective("THE KRAKEN KING IS DEFEATED!");
                if (hud != null) hud.SetHP(0f, config.throneHP);
                dolphinShown = false;
                break;
        }
    }

    void ExitState(State s)
    {
        switch (s)
        {
            case State.P1_Whirlpool:
                if (playerShip != null) playerShip.SetExternalDrift(Vector3.zero);
                if (whirlpool != null) whirlpool.End();
                ReleaseStateZone();
                break;
            case State.P2_ThroneRise:
                ReleaseStateZone();
                break;
        }
    }

    void Tick()
    {
        switch (state)
        {
            case State.Intro:
                if (stateTime >= 1.2f) SetState(State.P1_Tentacles);
                break;

            case State.P1_Tentacles:
                TickTentacles();
                break;

            case State.P1_Whirlpool:
                TickWhirlpool();
                break;

            case State.P1_Surfaced:
                if (!kraken.IsAlive) { SetState(State.P1_KrakenDeath); break; }
                inkTimer -= Time.deltaTime;
                if (inkTimer <= 0f)
                {
                    SpitInk();
                    inkTimer = config.inkInterval;
                }
                if (stateTime >= config.surfacedDuration) SetState(State.P1_Dive);
                break;

            case State.P1_Dive:
                if (stateTime >= config.krakenDiveTime + 0.2f) SetState(State.P1_Tentacles);
                break;

            case State.P1_KrakenDeath:
                if (stateTime >= config.krakenDeathTime + 0.6f) SetState(State.P2_Sunfish);
                break;

            case State.P2_Sunfish:
                if (!sunfish.IsAlive && sunfishDeadTime < 0f) sunfishDeadTime = stateTime;
                if (sunfishDeadTime >= 0f && stateTime - sunfishDeadTime >= 1.8f) SetState(State.P2_ThroneRise);
                break;

            case State.P2_ThroneRise:
                if (!flag && stateTime >= config.throneTelegraph)
                {
                    flag = true;
                    ReleaseStateZone();
                    throne.Rise(config.throneRiseTime);
                }
                if (flag && throne.IsRisen) SetState(State.P2_Jellyfish);
                break;

            case State.P2_Jellyfish:
                if (AliveJellyfish() == 0) SetState(State.P2_Rocks);
                break;

            case State.P2_Rocks:
                if (!throne.IsAlive) { SetState(State.Victory); break; }
                if (!secondWaveDone && throne.HP <= config.throneHP - config.secondWaveAfterDamage)
                {
                    // second wave: the king heals, raises the shield again and calls more jellyfish
                    secondWaveDone = true;
                    throne.Heal(config.secondWaveHeal);
                    SetState(State.P2_Jellyfish);
                    break;
                }
                rockTimer -= Time.deltaTime;
                if (rockTimer <= 0f && player != null)
                {
                    int n = Mathf.Min(config.rockThrowers, throne.throwers.Length);
                    var th = n > 0 ? throne.throwers[nextThrower % n] : null;
                    if (th != null && th.IsIdle)
                    {
                        th.Throw(LeadTarget(config.rockFlightTime + th.throwReleaseTime, config.rockLead));
                        nextThrower++;
                        rockTimer = config.rockInterval;
                    }
                    else rockTimer = 0.2f;
                }
                break;

            case State.Victory:
            {
                float showAt = config.throneSinkTime + config.dolphinDelay;
                if (!dolphinShown && stateTime >= showAt)
                {
                    dolphinShown = true;
                    if (hud != null) hud.Hide();
                    if (player != null)
                    {
                        // ring of dolphins around the ship, all applauding the player
                        int n = dolphins.Count;
                        float start = Mathf.Atan2(player.forward.x, player.forward.z);
                        for (int i = 0; i < n; i++)
                        {
                            float a = start + i * Mathf.PI * 2f / n;
                            Vector3 pos = KrakenUtil.Flat(player.position) + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * config.dolphinRingRadius;
                            dolphins[i].Play(pos, player.position, i * 0.12f);
                        }
                    }
                }
                if (stateTime >= showAt + config.dolphinShowTime)
                {
                    SetState(State.Done);
                    GameManager.Instance?.LevelCleared();
                }
                break;
            }
        }
    }

    // ---- Phase 1A ---------------------------------------------------------------------------

    void TickTentacles()
    {
        float remaining = config.tentaclePhaseDuration - stateTime;
        int sec = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        if (sec != lastShownSecond)
        {
            lastShownSecond = sec;
            Objective(string.Format("PHASE 1 · DODGE THE TENTACLES  {0}:{1:00}", sec / 60, sec % 60));
        }

        if (remaining > 0f)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                if (ActiveAttacks() < config.maxConcurrentAttacks) TryStartSlam();
                attackTimer = Random.Range(config.attackIntervalMin, config.attackIntervalMax);
            }
            return;
        }

        // Time up: let running slams finish, then everything sinks
        if (!tentaclesSinking)
        {
            if (ActiveAttacks() > 0) return;
            tentaclesSinking = true;
            sinkTimer = config.tentacleSinkTime + 0.3f;
            foreach (var t in tentacles) if (t != null) t.Sink(config.tentacleSinkTime);
            Objective("THE TENTACLES SINK...");
            return;
        }
        sinkTimer -= Time.deltaTime;
        if (sinkTimer <= 0f) SetState(State.P1_Whirlpool);
    }

    int ActiveAttacks()
    {
        int n = 0;
        foreach (var t in tentacles) if (t != null && t.IsAttacking) n++;
        return n;
    }

    void TryStartSlam()
    {
        if (player == null) return;
        Vector3 target = KrakenUtil.Flat(player.position);
        TentacleAttacker best = null, fallback = null;
        float bestScore = float.MaxValue, fallbackScore = float.MaxValue;
        foreach (var t in tentacles)
        {
            if (t == null || !t.gameObject.activeSelf || !t.IsIdle) continue;
            float s = t.ReachScore(target, out bool inRange);
            if (inRange && s < bestScore) { bestScore = s; best = t; }
            if (s < fallbackScore) { fallbackScore = s; fallback = t; }
        }
        var pick = best != null ? best : fallback;
        if (pick != null) pick.StartSlam(target);
    }

    // Called by a tentacle when its slam lands
    public void ResolveSlam(Vector3 pos, float radius, float damage)
    {
        Splash(pos, 2.2f);
        SoundManager.PlayAt("big_explosion", pos, 0.8f, 0.1f, 0.6f);
        SoundManager.PlayAt("splash", pos, 1f, 0.1f, 0.8f);
        bool hit = player != null && KrakenUtil.HullDistance(player, pos) <= radius;
        if (hit)
        {
            playerShip?.TakeDamage(damage);
            SoundManager.PlayAt("hit_ship", pos, 1f);
            GameManager.Instance?.ShakeCamera(0.4f, 0.5f);
        }
        else GameManager.Instance?.ShakeCamera(0.25f, 0.25f);
    }

    // ---- Phase 1B ---------------------------------------------------------------------------

    Vector3 WhirlpoolCentreFor(Vector3 dir)
    {
        Vector3 c = ArenaCentre + dir.normalized * config.WhirlpoolCentreDist;
        float lim = 95f - config.WhirlpoolRadius * 0.3f;
        c.x = Mathf.Clamp(c.x, -lim, lim);
        c.z = Mathf.Clamp(c.z, -lim, lim);
        return c;
    }

    void TickWhirlpool()
    {
        if (playerShip != null) playerShip.SetExternalDrift(whirlpool.PullAt(player.position));

        if (stateZone == null && stateTime >= config.whirlpoolDuration - config.emergenceTelegraph)
        {
            stateZone = GetZone();
            stateZone.Begin(whirlCentre, config.EmergenceRadius, config.emergenceTelegraph, DangerRed);
            Objective("PHASE 1 · THE KRAKEN IS COMING - GET AWAY!");
        }

        if (stateTime < config.whirlpoolDuration) return;

        playerShip?.SetExternalDrift(Vector3.zero);
        ReleaseStateZone();
        kraken.Emerge(whirlCentre, player != null ? player.position : ArenaCentre);
        if (player != null && KrakenUtil.HullDistance(player, whirlCentre) < config.EmergenceRadius)
        {
            float d = Vector3.Distance(KrakenUtil.Flat(player.position), whirlCentre);
            Vector3 away = KrakenUtil.Flat(player.position - whirlCentre);
            if (away.sqrMagnitude < 0.01f) away = -whirlCentre;
            playerShip.TakeDamage(config.emergenceDamage);
            playerShip.ApplyKnockback(away, Mathf.Max(0f, config.EmergenceRadius - d) + config.SL(config.emergenceKnockbackSL) + KrakenUtil.HullHalfLength, 0.4f);
            SoundManager.PlayAt("hit_ship", player.position, 1f);
            GameManager.Instance?.ShakeCamera(0.6f, 0.7f);
        }
        SetState(State.P1_Surfaced);
    }

    // ---- Phase 1C: ink ----------------------------------------------------------------------

    void SpitInk()
    {
        if (inkPool == null || player == null) return;
        Vector3 target = LeadTarget(config.inkFlightTime, config.inkLead);
        var zone = GetZone();
        zone.Begin(target, KrakenUtil.HullHalfWidth + config.SL(config.inkHitRadiusSL), config.inkFlightTime, InkPurple);
        var go = inkPool.Get(kraken.MouthPosition, Quaternion.identity);
        var ap = go.GetComponent<ArcProjectile>();
        ap.marker = zone;
        ap.Launch(kraken.MouthPosition, new Vector3(target.x, 0.3f, target.z), config.inkFlightTime, config.inkGravity, this);
        activeArcs.Add(ap);
        SoundManager.PlayAt("splash", kraken.MouthPosition, 0.9f, 0.1f, 1.6f);
    }

    // ---- Phase 2C: rocks --------------------------------------------------------------------

    // Called by a throwing tentacle at its release frame
    public void LaunchRock(Vector3 from, Vector3 target)
    {
        if (rockPool == null || state != State.P2_Rocks) return;
        var zone = GetZone();
        zone.Begin(target, KrakenUtil.HullHalfWidth + config.SL(config.rockHitRadiusSL), config.rockFlightTime, RockOrange);
        var go = rockPool.Get(from, Random.rotation);
        var ap = go.GetComponent<ArcProjectile>();
        ap.marker = zone;
        ap.Launch(from, new Vector3(target.x, 0.3f, target.z), config.rockFlightTime, config.rockGravity, this);
        activeArcs.Add(ap);
        SoundManager.PlayAt("anchor_throw", from, 0.8f, 0.1f, 0.8f);
    }

    public void OnArcLanded(ArcProjectile p, Vector3 pos)
    {
        activeArcs.Remove(p);
        if (p.marker != null) { ReleaseZone(p.marker); p.marker = null; }
        bool ink = p.kind == ArcProjectile.Kind.Ink;
        float r = ink ? config.SL(config.inkHitRadiusSL) : config.SL(config.rockHitRadiusSL);
        bool hit = player != null && !GameManager.Instance.IsGameOver && KrakenUtil.HullDistance(player, pos) <= r;
        if (ink)
        {
            if (hit)
            {
                playerShip?.TakeDamage(config.inkDamage);
                if (inkSplat != null) inkSplat.ShowCached();
                SoundManager.Play("poop_splat", 1f, 0.1f, 0.7f);
                GameManager.Instance?.ShakeCamera(0.25f, 0.25f);
            }
            else SoundManager.PlayAt("splash", pos, 0.6f, 0.1f, 1.4f);
            Splash(pos, 0.9f);
            inkPool.Release(p.gameObject);
        }
        else
        {
            if (hit)
            {
                playerShip?.TakeDamage(config.rockDamage);
                if (explosionPrefab != null) Instantiate(explosionPrefab, pos + Vector3.up, Quaternion.identity);
                SoundManager.PlayAt("hit_ship", pos, 1f);
                GameManager.Instance?.ShakeCamera(0.35f, 0.4f);
            }
            else SoundManager.PlayAt("hit_island", pos, 0.7f, 0.1f, 0.7f);
            Splash(pos, 2f);
            rockPool.Release(p.gameObject);
        }
    }

    // ---- Phase 2B ---------------------------------------------------------------------------

    void SpawnJellyfish(int n)
    {
        foreach (var j in jellies) if (j != null) Destroy(j.gameObject);
        jellies.Clear();
        if (jellyfishPrefab == null) return;
        float r = config.ThroneRadius + config.SL(1f);
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < n; i++)
        {
            float a = (start + i * 360f / n) * Mathf.Deg2Rad;
            Vector3 pos = ArenaCentre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
            var go = Instantiate(jellyfishPrefab, pos, Quaternion.LookRotation(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a))), transform);
            var j = go.GetComponent<Jellyfish>();
            j.Init(this, config, i * 0.35f);
            jellies.Add(j);
            Splash(pos, 1f);
        }
    }

    int AliveJellyfish()
    {
        int n = 0;
        foreach (var j in jellies) if (j != null && j.IsAlive) n++;
        return n;
    }

    void UpdateJellyObjective() => Objective((secondWaveDone ? "PHASE 2 · SECOND WAVE! SINK THE JELLYFISH (" : "PHASE 2 · SINK THE JELLYFISH (")
                                             + AliveJellyfish() + " LEFT) - THE KING IS SHIELDED");

    public void OnJellyfishKilled(Jellyfish j)
    {
        if (state == State.P2_Jellyfish) UpdateJellyObjective();
    }

    public void OnShieldBlocked(Vector3 pos)
    {
        Splash(pos, 0.7f);
    }

    public void OnThroneRisen()
    {
        if (playerShip != null) playerShip.RefreshIslands();
    }

    // ---- shared host services ----------------------------------------------------------------

    public void OnBossDamaged()
    {
        if (hud == null) return;
        switch (state)
        {
            case State.P1_Surfaced: case State.P1_Whirlpool: hud.SetHP(kraken.HP, kraken.MaxHP); break;
            case State.P2_Sunfish: hud.SetHP(sunfish.HP, config.sunfishHP); break;
            case State.P2_Rocks: case State.P2_Jellyfish: hud.SetHP(throne.HP, config.throneHP); break;
        }
    }

    public DangerZone GetZone()
    {
        var go = zonePool.Get(Vector3.zero, Quaternion.identity);
        var z = go.GetComponent<DangerZone>();
        activeZones.Add(z);
        return z;
    }

    public void ReleaseZone(DangerZone z)
    {
        if (z == null) return;
        activeZones.Remove(z);
        zonePool.Release(z.gameObject);
    }

    void ReleaseStateZone()
    {
        if (stateZone != null) { ReleaseZone(stateZone); stateZone = null; }
    }

    public void Splash(Vector3 pos, float scale)
    {
        if (splashPrefab == null) return;
        var s = Instantiate(splashPrefab, KrakenUtil.Flat(pos), Quaternion.identity);
        s.transform.localScale = Vector3.one * scale;
    }

    Vector3 LeadTarget(float time, float lead)
    {
        Vector3 t = KrakenUtil.Flat(player.position) + playerVel * time * lead;
        Vector3 off = t - ArenaCentre;
        float max = ArenaRadius + config.SL(0.5f);
        if (off.magnitude > max) t = ArenaCentre + off.normalized * max;
        return t;
    }

    Vector3 PickSunfishSpot()
    {
        Vector3 p = player != null ? KrakenUtil.Flat(player.position) : ArenaCentre;
        float minD = config.SL(config.sunfishMinPlayerDistSL);
        Vector3 best = ArenaCentre;
        for (int i = 0; i < 12; i++)
        {
            Vector2 r = Random.insideUnitCircle * ArenaRadius * 0.7f;
            Vector3 c = ArenaCentre + new Vector3(r.x, 0f, r.y);
            best = c;
            if (Vector3.Distance(c, p) >= minD) break;
        }
        return best;
    }

    void SetArena(bool on)
    {
        if (playerShip == null) return;
        if (on) playerShip.SetArenaConstraint(ArenaCentre, phase2 ? config.Phase2ArenaRadius : config.ArenaClampRadius);
        else    playerShip.ClearArenaConstraint();
    }

    void Objective(string text) => GameManager.Instance?.SetObjectiveText(text);

    void ClearProjectilesAndZones()
    {
        for (int i = activeArcs.Count - 1; i >= 0; i--)
        {
            var a = activeArcs[i];
            if (a == null) continue;
            a.Cancel();
            if (a.marker != null) a.marker = null;
            if (a.kind == ArcProjectile.Kind.Ink) inkPool?.Release(a.gameObject); else rockPool?.Release(a.gameObject);
        }
        activeArcs.Clear();
        for (int i = activeZones.Count - 1; i >= 0; i--)
            if (activeZones[i] != null) zonePool.Release(activeZones[i].gameObject);
        activeZones.Clear();
        stateZone = null;
    }

    // ------------------------------------------------------------------------------------------
    // Debug: jump to any state
    // ------------------------------------------------------------------------------------------

    static readonly State[] DebugStates =
    {
        State.P1_Tentacles, State.P1_Whirlpool, State.P1_Surfaced, State.P1_KrakenDeath,
        State.P2_Sunfish, State.P2_ThroneRise, State.P2_Jellyfish, State.P2_Rocks, State.Victory
    };
    static readonly string[] DebugLabels = { "1A", "1B", "1C", "K DIE", "FISH", "RISE", "JELLY", "ROCKS", "WIN", "HIT" };

    void DebugKeys()
    {
        if (!Debug.isDebugBuild) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f1Key.wasPressedThisFrame) JumpTo(DebugStates[0]);
        else if (kb.f2Key.wasPressedThisFrame) JumpTo(DebugStates[1]);
        else if (kb.f3Key.wasPressedThisFrame) JumpTo(DebugStates[2]);
        else if (kb.f4Key.wasPressedThisFrame) JumpTo(DebugStates[3]);
        else if (kb.f5Key.wasPressedThisFrame) JumpTo(DebugStates[4]);
        else if (kb.f6Key.wasPressedThisFrame) JumpTo(DebugStates[5]);
        else if (kb.f7Key.wasPressedThisFrame) JumpTo(DebugStates[6]);
        else if (kb.f8Key.wasPressedThisFrame) JumpTo(DebugStates[7]);
        else if (kb.f9Key.wasPressedThisFrame) JumpTo(DebugStates[8]);
        else if (kb.f10Key.wasPressedThisFrame) DebugDamage();
    }

    public void JumpTo(State target)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        ExitState(state);
        state = State.None;
        ClearProjectilesAndZones();
        foreach (var t in tentacles) if (t != null) t.HideImmediate();
        whirlpool.HideImmediate();
        kraken.HideImmediate();
        sunfish.HideImmediate();
        throne.HideImmediate();
        foreach (var d in dolphins) if (d != null) d.HideImmediate();
        foreach (var j in jellies) if (j != null) Destroy(j.gameObject);
        jellies.Clear();
        playerShip?.SetExternalDrift(Vector3.zero);
        GameManager.Instance?.SetPlayerInvulnerable(false);

        bool phase1 = target == State.P1_Tentacles || target == State.P1_Whirlpool || target == State.P1_Surfaced;
        SetPhase2(!(phase1 || target == State.P1_KrakenDeath), instant: true);
        secondWaveDone = false;
        if (phase1 && !kraken.IsAlive) kraken.ResetHP();
        if (hud != null)
        {
            if (phase1 || target == State.P1_KrakenDeath)
                hud.Show("KRAKEN", "PHASE 1 / 2", kraken.HP, kraken.MaxHP, BossHUD.KrakenColor);
        }
        switch (target)
        {
            case State.P1_Surfaced:
            case State.P1_KrakenDeath:
            {
                Vector3 dir = player != null ? KrakenUtil.Flat(player.position - ArenaCentre) : Vector3.forward;
                if (dir.sqrMagnitude < 4f) dir = Vector3.forward;
                whirlCentre = WhirlpoolCentreFor(dir);
                if (!kraken.IsAlive) kraken.ResetHP();
                kraken.Emerge(whirlCentre, player != null ? player.position : ArenaCentre);
                break;
            }
            case State.P2_Jellyfish:
            case State.P2_Rocks:
            case State.Victory:
                throne.ShowRisenImmediate();
                if (hud != null) hud.Show("KRAKEN KING", "PHASE 2 / 2", throne.HP, config.throneHP, BossHUD.KrakenColor);
                break;
        }
        SetState(target);
        Debug.Log("[Kraken] Debug jump -> " + target);
    }

    public void DebugDamage()
    {
        switch (state)
        {
            case State.P1_Surfaced: case State.P1_Whirlpool:
                kraken.DebugDamage(1f); break;
            case State.P2_Sunfish:
                sunfish.DebugDamage(1f); break;
            case State.P2_Jellyfish:
                foreach (var j in jellies) if (j != null && j.IsAlive) { j.DebugKill(); break; }
                break;
            case State.P2_Rocks:
                throne.DebugDamage(1f); break;
        }
        OnBossDamaged();
    }

    void BuildDebugPanel()
    {
        if (!Debug.isDebugBuild || inkSplat == null) return;
        var canvas = inkSplat.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var row = new GameObject("KrakenDebugRow", typeof(RectTransform));
        row.transform.SetParent(canvas.transform, false);
        var rowRT = (RectTransform)row.transform;
        rowRT.anchorMin = rowRT.anchorMax = rowRT.pivot = new Vector2(0f, 1f);
        rowRT.anchoredPosition = new Vector2(20f, -195f);
        rowRT.sizeDelta = new Vector2(10 * 78f, 52f);
        row.SetActive(false);

        MakeDebugButton(canvas.transform, "DBG", new Vector2(20f, -135f), () => row.SetActive(!row.activeSelf), font);
        for (int i = 0; i < DebugLabels.Length; i++)
        {
            int idx = i;
            System.Action act = idx < DebugStates.Length ? (System.Action)(() => JumpTo(DebugStates[idx])) : DebugDamage;
            MakeDebugButton(row.transform, DebugLabels[i], new Vector2(i * 78f, 0f), act, font);
        }
        // keep the fire zone the topmost raycast element order-wise irrelevant (it polls touches)
    }

    static void MakeDebugButton(Transform parent, string label, Vector2 pos, System.Action onClick, Font font)
    {
        var go = new GameObject("Dbg_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(72f, 46f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());
        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)tgo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = tgo.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    // ------------------------------------------------------------------------------------------
    // Gizmos: arena, tentacle ring, whirlpool + emergence zone, throne radius
    // ------------------------------------------------------------------------------------------

    void OnDrawGizmos()
    {
        if (config == null) return;
        Vector3 c = KrakenUtil.Flat(transform.position) + Vector3.up * 0.2f;
        KrakenUtil.DrawCircle(c, config.ArenaRadius, Color.cyan);
        KrakenUtil.DrawCircle(c, config.Phase2ArenaRadius, new Color(1f, 0.6f, 0.2f));   // Phase 2 arena / rock wall
        KrakenUtil.DrawCircle(c, config.ArenaClampRadius, new Color(0f, 1f, 1f, 0.35f));
        KrakenUtil.DrawCircle(c, config.TentacleRingRadius, Color.yellow);
        Vector3 w = Application.isPlaying && state == State.P1_Whirlpool
            ? whirlCentre + Vector3.up * 0.2f
            : c + new Vector3(0f, 0f, -config.WhirlpoolCentreDist);   // preview on the player-spawn side
        KrakenUtil.DrawCircle(w, config.WhirlpoolRadius, new Color(0.2f, 0.5f, 1f));
        KrakenUtil.DrawCircle(w, config.WhirlpoolRadius * config.pullReachMultiplier, new Color(0.2f, 0.5f, 1f, 0.3f));
        KrakenUtil.DrawCircle(w, config.EmergenceRadius, Color.red);
        KrakenUtil.DrawCircle(c, config.ThroneRadius, Color.magenta);
    }
}
