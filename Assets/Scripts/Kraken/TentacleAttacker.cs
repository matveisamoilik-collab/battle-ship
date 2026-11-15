using UnityEngine;

// One kraken tentacle (Level 7). Phase 1A: 8 of them stand around the arena and slam the player;
// Phase 2C: 2 smaller ones beside the throne throw rocks.
// Hierarchy (built by GameSetup): this root at the base, y = 0 -> "Pivot" (yaw / scale / rise
// depth) -> the KrakenTentacle.fbx instance with an Animator (states Idle, Rise, Windup, Slam,
// Recover, Throw, Sink - clips split from one Blender timeline). The model faces +Z: the Slam clip
// lays it flat along +Z with the tip `reach` units out, so a slam is aimed by yawing the pivot at
// the target and scaling it uniformly to make the tip land there.
public class TentacleAttacker : MonoBehaviour
{
    public enum State { Hidden, Rising, Idle, Telegraph, Slamming, Holding, Recovering, Throwing, Sinking }

    [Header("Parts")]
    public Transform pivot;
    public Animator  animator;
    public Transform tip;               // T8 bone (tip marker)

    [Header("Model timings (written by Build All from KrakenTentacle.clips.json)")]
    public float reach            = 36f;   // tip distance in the Slam pose at scale 1
    public float slamImpactTime   = 0.3f;  // Slam clip: start -> impact (clip end)
    public float recoverDuration  = 1.13f;
    public float throwReleaseTime = 0.5f;
    public float throwDuration    = 1.0f;
    public float baseScale        = 1f;    // throne throwers are smaller
    public float sinkDepth        = 40f;

    BossEncounterController host;
    KrakenLevelConfig       cfg;

    State      state = State.Hidden;
    float      t;              // time in the current state
    float      duration;
    Quaternion homeYaw;
    Quaternion fromYaw, toYaw;
    float      fromScale, toScale;
    float      blendTime;
    Vector3    target;
    DangerZone zone;
    bool       slamStarted;
    bool       released;

    static readonly int IdleHash    = Animator.StringToHash("Idle");
    static readonly int RiseHash    = Animator.StringToHash("Rise");
    static readonly int WindupHash  = Animator.StringToHash("Windup");
    static readonly int SlamHash    = Animator.StringToHash("Slam");
    static readonly int RecoverHash = Animator.StringToHash("Recover");
    static readonly int ThrowHash   = Animator.StringToHash("Throw");
    static readonly int SinkHash    = Animator.StringToHash("Sink");

    public State CurrentState => state;
    public bool  IsIdle       => state == State.Idle;
    public bool  IsAttacking  => state == State.Telegraph || state == State.Slamming || state == State.Holding || state == State.Recovering;
    public bool  IsHidden     => state == State.Hidden;
    public Vector3 BasePosition => KrakenUtil.Flat(transform.position);

    public void Init(BossEncounterController host, KrakenLevelConfig cfg, Vector3 faceToward)
    {
        this.host = host;
        this.cfg  = cfg;
        homeYaw = LocalYaw(faceToward - transform.position, Quaternion.identity);
        HideImmediate();
    }

    // ---- commands ---------------------------------------------------------------------------

    public void Emerge(float time)
    {
        if (state != State.Hidden && state != State.Sinking) return;
        pivot.gameObject.SetActive(true);
        pivot.localRotation = homeYaw;
        pivot.localScale    = Vector3.one * baseScale;
        Enter(State.Rising, time);
        Play(RiseHash, 0.05f);
        host.Splash(BasePosition, 1.6f * baseScale);
        SoundManager.PlayAt("splash", BasePosition, 0.7f, 0.1f, 0.7f);
    }

    public void Sink(float time)
    {
        if (state == State.Hidden || state == State.Sinking) return;
        CancelZone();
        Enter(State.Sinking, time);
        Play(SinkHash, 0.15f);
    }

    public void HideImmediate()
    {
        CancelZone();
        state = State.Hidden;
        if (pivot != null)
        {
            pivot.localPosition = new Vector3(0f, -sinkDepth, 0f);
            pivot.gameObject.SetActive(false);
        }
    }

    public void ShowIdleImmediate()
    {
        pivot.gameObject.SetActive(true);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = homeYaw;
        pivot.localScale    = Vector3.one * baseScale;
        Enter(State.Idle, 0f);
        Play(IdleHash, 0f);
    }

    // Phase 1A: telegraph a slam on target for cfg.telegraphTime, then slam
    public void StartSlam(Vector3 target)
    {
        if (state != State.Idle) return;
        this.target = KrakenUtil.Flat(target);
        Enter(State.Telegraph, cfg.telegraphTime);
        AimAt(this.target, 0.35f);
        zone = host.GetZone();
        zone.Begin(this.target, cfg.SlamRadius, cfg.telegraphTime, BossEncounterController.DangerRed);
        slamStarted = false;
        Play(WindupHash, 0.25f);
        SoundManager.PlayAt("splash", BasePosition, 0.5f, 0.1f, 0.6f);
    }

    // Phase 2C: wind up and fling a rock at target (the host launches it at the release frame)
    public void Throw(Vector3 target)
    {
        if (state != State.Idle) return;
        this.target = KrakenUtil.Flat(target);
        Enter(State.Throwing, throwDuration);
        AimAt(this.target, 0.3f, keepScale: true);
        released = false;
        Play(ThrowHash, 0.1f);
    }

    // Distance-to-reach ratio score for picking the best tentacle (lower is better)
    public float ReachScore(Vector3 point, out bool inRange)
    {
        float s = Vector3.Distance(BasePosition, KrakenUtil.Flat(point)) / Mathf.Max(0.1f, reach * baseScale);
        inRange = s >= cfg.tentacleScaleMin && s <= cfg.tentacleScaleMax;
        return Mathf.Abs(s - 1f);
    }

    // ---- update -----------------------------------------------------------------------------

    void Enter(State s, float dur)
    {
        state    = s;
        t        = 0f;
        duration = dur;
    }

    void AimAt(Vector3 point, float time, bool keepScale = false)
    {
        Vector3 d = KrakenUtil.Flat(point - transform.position);
        fromYaw   = pivot.localRotation;
        toYaw     = LocalYaw(d, homeYaw);
        fromScale = pivot.localScale.x;
        float s   = keepScale ? baseScale
                  : Mathf.Clamp(d.magnitude / Mathf.Max(0.1f, reach), cfg.tentacleScaleMin, cfg.tentacleScaleMax) * baseScale;
        toScale   = s;
        blendTime = time;
    }

    void Update()
    {
        if (state == State.Hidden) return;
        t += Time.deltaTime;

        // yaw / scale blend
        if (blendTime > 0f)
        {
            float k = KrakenUtil.Smooth01(t / blendTime);
            pivot.localRotation = Quaternion.Slerp(fromYaw, toYaw, k);
            pivot.localScale    = Vector3.one * Mathf.Lerp(fromScale, toScale, k);
            if (k >= 1f) blendTime = 0f;
        }

        switch (state)
        {
            case State.Rising:
            {
                float k = KrakenUtil.Smooth01(t / Mathf.Max(0.01f, duration));
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(-sinkDepth * pivot.localScale.y, 0f, k), 0f);
                if (t >= duration) { Enter(State.Idle, 0f); Play(IdleHash, 0.2f); }
                break;
            }
            case State.Telegraph:
                if (!slamStarted && t >= duration - slamImpactTime)
                {
                    slamStarted = true;
                    Play(SlamHash, 0.05f);
                }
                if (t >= duration)
                {
                    host.ResolveSlam(target, cfg.SlamRadius, cfg.slamDamage);
                    CancelZone();
                    Enter(State.Holding, cfg.slamHoldTime);
                }
                break;
            case State.Holding:
                if (t >= duration)
                {
                    Enter(State.Recovering, recoverDuration);
                    Play(RecoverHash, 0.1f);
                    fromYaw   = pivot.localRotation;
                    toYaw     = homeYaw;
                    fromScale = pivot.localScale.x;
                    toScale   = baseScale;
                    blendTime = recoverDuration;
                }
                break;
            case State.Recovering:
                if (t >= duration) { Enter(State.Idle, 0f); Play(IdleHash, 0.25f); }
                break;
            case State.Throwing:
                if (!released && t >= throwReleaseTime)
                {
                    released = true;
                    Vector3 from = tip != null ? tip.position : transform.position + Vector3.up * 10f * baseScale;
                    host.LaunchRock(from, target);
                }
                if (t >= duration)
                {
                    Enter(State.Idle, 0f);
                    Play(IdleHash, 0.2f);
                }
                break;
            case State.Sinking:
            {
                float k = KrakenUtil.Smooth01(t / Mathf.Max(0.01f, duration));
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(0f, -sinkDepth * pivot.localScale.y, k * k), 0f);
                if (t >= duration) HideImmediate();
                break;
            }
        }
    }

    // World-space heading -> pivot local rotation (the root may be rotated, e.g. under the throne)
    Quaternion LocalYaw(Vector3 worldDir, Quaternion fallback)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.01f) return fallback;
        Vector3 local = transform.InverseTransformDirection(worldDir.normalized);
        local.y = 0f;
        return local.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(local.normalized) : fallback;
    }

    void Play(int hash, float fade)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        if (fade <= 0f) animator.Play(hash, 0, 0f);
        else animator.CrossFadeInFixedTime(hash, fade);
    }

    void CancelZone()
    {
        if (zone != null) { host.ReleaseZone(zone); zone = null; }
    }
}
