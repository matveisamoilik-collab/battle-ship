using UnityEngine;

// Phase 1 kraken body (KrakenBody.fbx). Bursts out of the whirlpool, stays surfaced and
// vulnerable for cfg.surfacedDuration while BossEncounterController makes it spit ink, then dives.
// Its HP persists between cycles. Player torpedoes hit its capsule hurtbox.
public class SurfacedKraken : MonoBehaviour, ITorpedoTarget
{
    public enum State { Hidden, Emerging, Surfaced, Diving, Dying }

    public Transform  pivot;       // rises / sinks / bobs
    public Transform  mouth;       // ink spawn point
    public Collider[] hurtboxes;
    public HitFlash   flash;
    public float      hiddenDepth = 34f;
    // KrakenBody.fbx was sized for this emergence radius; the body scales to the configured zone
    const float ModelEmergenceRadius = 21.2f;

    BossEncounterController host;
    KrakenLevelConfig       cfg;
    State state = State.Hidden;
    float t, duration;
    float hp;
    Vector3 tipAxis;

    public float HP    => hp;
    public float MaxHP => cfg != null ? cfg.krakenHP : 5f;
    public bool  IsAlive => hp > 0f;
    public bool  IsVulnerable => state == State.Emerging || state == State.Surfaced;
    public State CurrentState => state;
    public Vector3 MouthPosition => mouth != null ? mouth.position : transform.position + Vector3.up * 10f;

    public void Init(BossEncounterController host, KrakenLevelConfig cfg)
    {
        this.host = host;
        this.cfg  = cfg;
        hp = cfg.krakenHP;
        float s = Mathf.Max(0.2f, cfg.EmergenceRadius / ModelEmergenceRadius);
        pivot.localScale = Vector3.one * s;
        hiddenDepth *= s;
        HideImmediate();
    }

    public void ResetHP() => hp = cfg.krakenHP;

    public void Emerge(Vector3 centre, Vector3 facePoint)
    {
        transform.position = KrakenUtil.Flat(centre);
        FaceToward(facePoint, 1f);
        gameObject.SetActive(true);
        pivot.localPosition = new Vector3(0f, -hiddenDepth, 0f);
        pivot.localRotation = Quaternion.identity;
        SetHurtboxes(true);
        state = State.Emerging; t = 0f; duration = 0.55f;
        host.Splash(transform.position, 4f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            host.Splash(transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * cfg.EmergenceRadius * 0.8f, 2f);
        }
        SoundManager.PlayAt("big_explosion", transform.position, 1f, 0.05f, 0.55f);
        SoundManager.PlayAt("splash", transform.position, 1f, 0.05f, 0.6f);
        GameManager.Instance?.ShakeCamera(0.5f, 0.5f);
    }

    public void Dive(float time)
    {
        if (state == State.Hidden) return;
        SetHurtboxes(false);
        state = State.Diving; t = 0f; duration = time;
        host.Splash(transform.position, 3f);
        SoundManager.PlayAt("splash", transform.position, 0.9f, 0.05f, 0.6f);
    }

    public void Die(float time)
    {
        SetHurtboxes(false);
        hp = 0f;
        state = State.Dying; t = 0f; duration = time;
        tipAxis = Vector3.Cross(Vector3.up, transform.forward);
        SoundManager.PlayAt("big_explosion", transform.position, 1f, 0f, 0.6f);
        SoundManager.PlayAt("ship_sink", transform.position, 1f, 0f, 0.7f);
        GameManager.Instance?.ShakeCamera(0.6f, 0.5f);
    }

    public void HideImmediate()
    {
        state = State.Hidden;
        SetHurtboxes(false);
        gameObject.SetActive(false);
    }

    public bool OnTorpedoHit(Torpedo torpedo)
    {
        if (!torpedo.isPlayerTorpedo || !IsVulnerable || hp <= 0f) return true;
        hp = Mathf.Max(0f, hp - torpedo.damage);
        if (flash != null) flash.Flash(new Color(1f, 0.9f, 0.9f), 0.18f);
        SoundManager.PlayAt("hit_ship", torpedo.transform.position, 1f, 0.05f, 0.7f);
        GameManager.Instance?.ShakeCamera(0.2f, 0.2f);
        host.OnBossDamaged();
        return true;
    }

    public void DebugDamage(float amount)
    {
        if (!IsVulnerable) return;
        hp = Mathf.Max(0f, hp - amount);
        if (flash != null) flash.Flash(Color.white, 0.18f);
    }

    void Update()
    {
        if (state == State.Hidden) return;
        t += Time.deltaTime;
        switch (state)
        {
            case State.Emerging:
            {
                float k = Mathf.Clamp01(t / duration);
                float y = Mathf.Lerp(-hiddenDepth, 0f, 1f - (1f - k) * (1f - k)) + Mathf.Sin(k * Mathf.PI) * 2.5f;
                pivot.localPosition = new Vector3(0f, y, 0f);
                if (k >= 1f) { state = State.Surfaced; t = 0f; }
                break;
            }
            case State.Surfaced:
                pivot.localPosition = new Vector3(0f, Mathf.Sin(t * 1.6f) * 0.6f - 0.3f, 0f);
                pivot.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 2f, 0f, Mathf.Sin(t * 0.9f) * 2f);
                if (host.Player != null) FaceToward(host.Player.position, Time.deltaTime * 1.5f);
                break;
            case State.Diving:
            {
                float k = KrakenUtil.Smooth01(t / Mathf.Max(0.01f, duration));
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(0f, -hiddenDepth, k), 0f);
                if (t >= duration) HideImmediate();
                break;
            }
            case State.Dying:
            {
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
                pivot.localPosition = new Vector3(Random.Range(-0.3f, 0.3f) * (1f - k), -hiddenDepth * k * k, 0f);
                pivot.localRotation = Quaternion.AngleAxis(25f * k, transform.InverseTransformDirection(tipAxis));
                if (Random.value < Time.deltaTime * 6f)
                    host.Splash(transform.position + KrakenUtil.Flat(Random.insideUnitSphere) * 16f, 1.5f);
                if (t >= duration) HideImmediate();
                break;
            }
        }
    }

    void FaceToward(Vector3 point, float k)
    {
        Vector3 d = KrakenUtil.Flat(point - transform.position);
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), Mathf.Clamp01(k));
    }

    void SetHurtboxes(bool on)
    {
        if (hurtboxes == null) return;
        foreach (var c in hurtboxes) if (c != null) c.enabled = on;
    }
}
