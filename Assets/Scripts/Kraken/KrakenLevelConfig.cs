using UnityEngine;

// Every tunable number of the Level 7 kraken encounter. Distances are in SHIP LENGTHS (SL);
// multiply by shipLength (or use the *U helpers) for Unity units.
// The asset lives at Assets/Config/KrakenLevelConfig.asset. Build All creates it only when it is
// missing, so balancing changes made in the Inspector survive rebuilds.
[CreateAssetMenu(menuName = "ShipButtlr/Kraken Level Config", fileName = "KrakenLevelConfig")]
public class KrakenLevelConfig : ScriptableObject
{
    [Header("Scale")]
    [Tooltip("Player ship length in Unity units (Hull BoxCollider length). Build All measures it; " +
             "use the context menu 'Measure ship length' with GameScene open to re-measure.")]
    public float shipLength = 12f;

    [Header("Arena")]
    [Tooltip("Arena circle diameter (SL).")]
    public float arenaDiameterSL = 7f;
    [Tooltip("The ship centre is kept this far inside the arena edge (SL).")]
    public float arenaWallMarginSL = 0.25f;

    [Header("Phase 1A — Tentacles")]
    public int   tentacleCount        = 8;
    [Tooltip("Radius of the tentacle ring (SL). Slightly outside the arena edge.")]
    public float tentacleRingSL       = 3.75f;
    public float tentaclePhaseDuration = 20f;
    [Tooltip("Seconds the red danger zone is shown before the slam lands.")]
    public float telegraphTime        = 1.5f;
    public int   maxConcurrentAttacks = 3;
    [Tooltip("Random delay between new attacks (s), used while fewer than maxConcurrentAttacks are active.")]
    public float attackIntervalMin    = 0.55f;
    public float attackIntervalMax    = 1.1f;
    public float slamDamage           = 1f;
    [Tooltip("Danger-zone radius (SL). The slam hits if any part of the hull is inside.")]
    public float slamRadiusSL         = 0.6f;
    [Tooltip("Seconds the tentacle lies on the water after the slam before recovering.")]
    public float slamHoldTime         = 0.45f;
    public float tentacleRiseTime     = 1.2f;
    public float tentacleSinkTime     = 1.2f;
    [Tooltip("Uniform tentacle scale range used to make the tip reach the target.")]
    public float tentacleScaleMin     = 0.6f;
    public float tentacleScaleMax     = 1.6f;

    [Header("Phase 1B — Whirlpool")]
    public float whirlpoolDiameterSL   = 7.5f;
    [Tooltip("How far beyond the arena edge the whirlpool centre sits (SL).")]
    public float whirlpoolCentreOutSL  = 1.875f;
    public float whirlpoolDuration     = 3f;
    [Tooltip("Pull speed at the whirlpool rim and at its centre (units/s). Blue ship = 20.")]
    public float pullSpeedRim          = 9f;
    public float pullSpeedCentre       = 19.5f;
    [Tooltip("The pull fades to zero at this multiple of the whirlpool radius.")]
    public float pullReachMultiplier   = 1.6f;
    [Tooltip("Share of the whirlpool area the emerging kraken covers.")]
    [Range(0.1f, 1f)] public float emergenceAreaFraction = 0.5f;
    public float emergenceTelegraph    = 1.0f;
    public float emergenceDamage       = 2f;
    [Tooltip("Extra knockback beyond the emergence-zone edge (SL).")]
    public float emergenceKnockbackSL  = 0.35f;

    [Header("Phase 1C — Kraken surfaced")]
    public float surfacedDuration = 5f;
    public float krakenHP         = 5f;
    public float inkFirstDelay    = 0.7f;
    public float inkInterval      = 1.5f;
    public float inkFlightTime    = 1.3f;
    public float inkGravity       = 30f;
    public float inkDamage        = 1f;
    [Tooltip("Ink hits if it lands this close to the hull (SL).")]
    public float inkHitRadiusSL   = 0.15f;
    [Tooltip("Fraction of the player's velocity the ink leads by (0 = aim at the ship).")]
    [Range(0f, 1f)] public float inkLead = 0.35f;
    [Range(0.05f, 0.95f)] public float inkScreenCoverage = 0.8f;
    public float inkHoldTime      = 1f;
    public float inkFadeTime      = 0.5f;
    public float krakenDiveTime   = 1.5f;
    public float krakenDeathTime  = 3f;

    [Header("Phase 2A — Sunfish")]
    public float sunfishHP           = 2f;
    public float sunfishSpeed        = 3f;
    public float sunfishMinPlayerDistSL = 2f;
    public float sunfishRiseTime     = 1.5f;

    [Header("Phase 2 — Arena")]
    [Tooltip("Phase 2 arena diameter = arenaDiameterSL x this.")]
    public float phase2ArenaScale  = 2f;
    [Tooltip("Rocks forming the Phase 2 arena edge. Each new contact costs HP (the L5+ island-crash rule).")]
    public int   edgeRockCount     = 48;
    [Tooltip("Ship-collision radius of one edge rock (units).")]
    public float edgeRockRadius    = 4.5f;
    public float edgeRockRiseTime  = 2f;

    [Header("Phase 2B — Throne kraken + jellyfish")]
    public float throneHP            = 8f;
    public float throneTelegraph     = 1.5f;
    public float throneRiseTime      = 2.5f;
    [Tooltip("Ship-collision radius of the throne (SL).")]
    public float throneRadiusSL      = 1.2f;
    public int   jellyfishCount      = 4;
    public float jellyfishHP         = 1f;
    public float jellyfishSpeed      = 12f;
    public float jellyfishTurnRate   = 120f;
    public float jellyfishContactDamage = 1f;
    [Tooltip("Contact hits when the jellyfish centre is this close to the hull (SL).")]
    public float jellyfishContactSL  = 0.2f;
    public float jellyfishContactCooldown = 1.5f;
    [Tooltip("Normal torpedo hitbox radius (units).")]
    public float jellyfishHitbox     = 1.8f;
    public float jellySurfaceIntervalMin = 5f;
    public float jellySurfaceIntervalMax = 8f;
    public float jellySurfaceDuration    = 2.5f;
    [Range(0f, 1f)] public float jellySurfacedSpeedMul = 0.3f;
    public float jellySurfacedHitboxMul  = 2f;

    [Header("Phase 2B — Second jellyfish wave")]
    [Tooltip("After the throne kraken has lost this much HP in the rock phase, a second wave spawns (once).")]
    public float secondWaveAfterDamage = 4f;
    public int   secondWaveJellyfish   = 5;
    [Tooltip("HP the kraken king restores when the second wave starts.")]
    public float secondWaveHeal        = 1f;

    [Header("Phase 2C — Rocks")]
    public int   rockThrowers    = 2;
    [Tooltip("Seconds between throws (the throwers alternate).")]
    public float rockInterval    = 1.569f;   // 2.2 s / 1.21 / 1.1589 (21 % then 15.89 % faster)
    public float rockFlightTime  = 1.7f;
    public float rockGravity     = 30f;
    public float rockDamage      = 1f;
    [Tooltip("Rock hits if it lands this close to the hull (SL).")]
    public float rockHitRadiusSL = 0.48f;
    [Range(0f, 1f)] public float rockLead = 0.4f;

    [Header("Victory")]
    public float throneSinkTime  = 3f;
    public float dolphinDelay    = 0.6f;
    public float dolphinShowTime = 4f;
    public int   dolphinCount    = 7;
    [Tooltip("Radius of the dolphin ring around the player ship (units).")]
    public float dolphinRingRadius = 18f;

    [Header("Pools")]
    public int inkPoolSize    = 6;
    public int rockPoolSize   = 6;
    public int zonePoolSize   = 12;
    public int inkSplatCached = 3;

    // ---- derived (Unity units) ----
    public float SL(float shipLengths)  => shipLengths * shipLength;
    public float ArenaRadius            => arenaDiameterSL * 0.5f * shipLength;
    public float ArenaClampRadius       => ArenaRadius - SL(arenaWallMarginSL);
    public float TentacleRingRadius     => SL(tentacleRingSL);
    public float SlamRadius             => SL(slamRadiusSL);
    public float WhirlpoolRadius        => whirlpoolDiameterSL * 0.5f * shipLength;
    public float WhirlpoolCentreDist    => ArenaRadius + SL(whirlpoolCentreOutSL);
    public float EmergenceRadius        => WhirlpoolRadius * Mathf.Sqrt(emergenceAreaFraction);
    public float ThroneRadius           => SL(throneRadiusSL);
    public float Phase2ArenaRadius      => ArenaRadius * phase2ArenaScale;

    void OnValidate()
    {
        shipLength           = Mathf.Max(0.1f, shipLength);
        tentacleCount        = Mathf.Max(1, tentacleCount);
        maxConcurrentAttacks = Mathf.Max(1, maxConcurrentAttacks);
        attackIntervalMax    = Mathf.Max(attackIntervalMin, attackIntervalMax);
        jellySurfaceIntervalMax = Mathf.Max(jellySurfaceIntervalMin, jellySurfaceIntervalMax);
        rockThrowers         = Mathf.Clamp(rockThrowers, 1, 2);
    }

    // Reads the hull length from the player ship in the open scene
    [ContextMenu("Measure ship length")]
    public void MeasureShipLength()
    {
        var player = GameObject.FindWithTag("Player");
        float len = MeasureShipLength(player);
        if (len > 0f)
        {
            shipLength = len;
            Debug.Log("[Kraken] shipLength measured: " + len);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        else Debug.LogWarning("[Kraken] No PlayerShip with a Hull BoxCollider in the open scene.");
    }

    public static float MeasureShipLength(GameObject player)
    {
        if (player == null) return 0f;
        var hull = player.transform.Find("Hull");
        var box  = hull != null ? hull.GetComponent<BoxCollider>() : null;
        if (box == null) return 0f;
        return box.size.z * hull.lossyScale.z;
    }
}
