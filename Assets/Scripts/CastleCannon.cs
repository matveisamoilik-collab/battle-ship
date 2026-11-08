using UnityEngine;

// Level 6: a tower cannon on the Dreadhold castle (one on every tower). It traverses the full
// 360°, so there are no blind zones: while the player is within range it turns toward them,
// raises the barrel and lobs a cannonball, with a muzzle flash and a puff of grey smoke.
public class CastleCannon : MonoBehaviour
{
    public Transform  barrel;            // pitches about its local X axis
    public Transform  muzzle;            // child of barrel at the barrel tip
    public GameObject cannonballPrefab;
    public GameObject flashPrefab;       // ExplosionEffect, used as muzzle flash
    public GameObject smokePrefab;       // CannonSmoke, blown out along the barrel

    private const float Interval = 5f;     // 6 cannons can all reach the player now

    private const float MinRange = 10f;
    private const float MaxRange = 110f;
    private const float TurnSpeed = 90f;   // deg/s
    private const float AimTolerance = 3f; // fire only when the turret is this close to the target yaw
    private const float LeadFactor  = 0.5f;
    private const float BallSpeedXZ = 35f;

    private Quaternion restLocalRot;
    private Quaternion barrelRestRot;
    private float      yaw;              // current yaw offset from rest (deg)
    private float      nextFire;
    private Transform  player;
    private Vector3    lastPlayerPos;
    private Vector3    playerVel;
    private CastleIsland island;

    void Start()
    {
        restLocalRot  = transform.localRotation;
        island        = GetComponentInParent<CastleIsland>();
        if (barrel != null) barrelRestRot = barrel.localRotation;
        nextFire = Time.time + Random.Range(1.5f, Interval + 1.5f); // staggered first shots

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            lastPlayerPos = player.position;
        }
    }

    bool Active =>
        player != null &&
        GameManager.Instance != null &&
        !GameManager.Instance.IsGameOver &&
        !GameManager.Instance.Level6Finale;

    void Update()
    {
        if (!Active) return;

        float dt = Time.deltaTime;
        if (dt > 0f)
            playerVel = Vector3.Lerp(playerVel, (player.position - lastPlayerPos) / dt, 5f * dt);
        lastPlayerPos = player.position;

        // Rest direction in world space (follows the island if it tilts)
        Vector3 restFwd = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * restLocalRot * Vector3.forward;
        restFwd.y = 0f;
        restFwd.Normalize();

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist  = toPlayer.magnitude;
        float angle = Vector3.SignedAngle(restFwd, toPlayer, Vector3.up);
        bool inRange = dist >= MinRange && dist <= MaxRange;

        // Full traverse: keep tracking the player even out of range, so it is ready when they close in
        yaw = Mathf.MoveTowardsAngle(yaw, angle, TurnSpeed * dt);
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        transform.localRotation = restLocalRot * Quaternion.Euler(0f, yaw, 0f);

        Vector3 target = PredictTarget();
        Vector3 vel    = SolveVelocity(MuzzlePos(), target, out _);
        if (barrel != null)
        {
            float pitch = inRange
                ? Mathf.Atan2(vel.y, new Vector2(vel.x, vel.z).magnitude) * Mathf.Rad2Deg
                : 10f;
            pitch = Mathf.Clamp(pitch, -10f, 60f);
            barrel.localRotation = barrelRestRot * Quaternion.Euler(-pitch, 0f, 0f);
        }

        if (inRange && Time.time >= nextFire && Mathf.Abs(Mathf.DeltaAngle(yaw, angle)) <= AimTolerance)
            Fire(target);
    }

    Vector3 MuzzlePos() => muzzle != null ? muzzle.position : transform.position + Vector3.up * 1f;

    Vector3 PredictTarget()
    {
        Vector3 p = new Vector3(player.position.x, 0f, player.position.z);
        Vector3 from = MuzzlePos();
        float dXZ = new Vector2(p.x - from.x, p.z - from.z).magnitude;
        float t   = FlightTime(dXZ);
        Vector3 v = playerVel; v.y = 0f;
        Vector3 lead = p + v * t * LeadFactor;
        lead.x = Mathf.Clamp(lead.x, -98f, 98f);
        lead.z = Mathf.Clamp(lead.z, -98f, 98f);
        return lead;
    }

    static float FlightTime(float dXZ) => Mathf.Clamp(dXZ / BallSpeedXZ, 1.2f, 3f);

    static Vector3 SolveVelocity(Vector3 from, Vector3 to, out float t)
    {
        Vector3 d = to - from;
        float dXZ = new Vector2(d.x, d.z).magnitude;
        t = FlightTime(dXZ);
        float vy = (d.y + 0.5f * Cannonball.Gravity * t * t) / t;
        return new Vector3(d.x / t, vy, d.z / t);
    }

    void Fire(Vector3 target)
    {
        nextFire = Time.time + Interval * Random.Range(0.85f, 1.15f);
        if (cannonballPrefab == null) return;

        Vector3 from = MuzzlePos();
        Vector3 vel  = SolveVelocity(from, target, out _);
        var go = Instantiate(cannonballPrefab, from, Quaternion.identity);
        var ball = go.GetComponent<Cannonball>();
        bool bomb = island != null && island.BombMode;   // after 2 pillars are down: bombs with area damage
        if (ball != null) ball.Launch(vel, bomb, target);

        if (flashPrefab != null) Instantiate(flashPrefab, from, Quaternion.identity);
        if (smokePrefab != null)
        {
            Vector3 dir = barrel != null ? from - barrel.position : vel;
            if (dir.sqrMagnitude < 1e-4f) dir = vel;
            Instantiate(smokePrefab, from, Quaternion.LookRotation(dir.normalized));
        }
        SoundManager.PlayAt("big_explosion", from, 0.45f, 0.08f, 1.4f);
    }
}
