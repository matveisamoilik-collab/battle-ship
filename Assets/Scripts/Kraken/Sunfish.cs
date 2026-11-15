using UnityEngine;

// Phase 2A sunfish (mola mola). Surfaces inside the arena, drifts slowly and never attacks.
// cfg.sunfishHP torpedo hits roll it over and sink it.
public class Sunfish : MonoBehaviour, ITorpedoTarget
{
    public enum State { Hidden, Rising, Drifting, Dying }

    public Transform  pivot;
    public Collider[] hurtboxes;
    public HitFlash   flash;
    public float      hiddenDepth = 12f;
    public float      surfaceY    = 2.6f;   // body centre height while drifting (most of the disc above water)

    BossEncounterController host;
    KrakenLevelConfig       cfg;
    State   state = State.Hidden;
    float   t, hp;
    Vector3 heading;
    float   nextTurn;

    public bool  IsAlive => hp > 0f;
    public float HP      => hp;
    public State CurrentState => state;

    public void Init(BossEncounterController host, KrakenLevelConfig cfg)
    {
        this.host = host;
        this.cfg  = cfg;
        HideImmediate();
    }

    public void Surface(Vector3 pos)
    {
        hp = cfg.sunfishHP;
        transform.position = KrakenUtil.Flat(pos);
        float a = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, a, 0f);
        heading = transform.forward;
        gameObject.SetActive(true);
        pivot.localPosition = new Vector3(0f, -hiddenDepth, 0f);
        pivot.localRotation = Quaternion.identity;
        SetHurtboxes(true);
        state = State.Rising; t = 0f;
        host.Splash(transform.position, 2f);
        SoundManager.PlayAt("splash", transform.position, 0.9f, 0.1f, 0.9f);
    }

    public void HideImmediate()
    {
        state = State.Hidden;
        hp = 0f;
        SetHurtboxes(false);
        gameObject.SetActive(false);
    }

    public bool OnTorpedoHit(Torpedo torpedo)
    {
        if (!torpedo.isPlayerTorpedo || hp <= 0f) return true;
        SoundManager.PlayAt("hit_ship", torpedo.transform.position, 1f, 0.05f, 1.2f);
        DebugDamage(torpedo.damage);
        host.OnBossDamaged();
        return true;
    }

    public void DebugDamage(float amount)
    {
        if (hp <= 0f || state == State.Hidden) return;
        hp = Mathf.Max(0f, hp - amount);
        if (flash != null) flash.Flash(Color.white, 0.18f);
        if (hp <= 0f)
        {
            SetHurtboxes(false);
            state = State.Dying; t = 0f;
            host.Splash(transform.position, 2.5f);
            SoundManager.PlayAt("ship_sink", transform.position, 0.8f, 0f, 1.3f);
        }
    }

    void Update()
    {
        if (state == State.Hidden) return;
        t += Time.deltaTime;
        switch (state)
        {
            case State.Rising:
            {
                float k = KrakenUtil.Smooth01(t / Mathf.Max(0.01f, cfg.sunfishRiseTime));
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(-hiddenDepth, surfaceY, k), 0f);
                if (k >= 1f) { state = State.Drifting; t = 0f; nextTurn = Random.Range(2f, 4f); }
                break;
            }
            case State.Drifting:
                Drift();
                break;
            case State.Dying:
            {
                float k = Mathf.Clamp01(t / 1.6f);
                pivot.localRotation = Quaternion.Euler(0f, 0f, 90f * KrakenUtil.Smooth01(k * 1.5f));
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(surfaceY, -hiddenDepth, k * k), 0f);
                if (k >= 1f) HideImmediate();
                break;
            }
        }
    }

    void Drift()
    {
        nextTurn -= Time.deltaTime;
        if (nextTurn <= 0f)
        {
            nextTurn = Random.Range(2.5f, 5f);
            heading = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * heading;
        }
        // stay inside the arena: steer back toward the centre near the edge
        Vector3 toCentre = KrakenUtil.Flat(host.ArenaCentre - transform.position);
        if (toCentre.magnitude > host.ArenaRadius * 0.75f)
            heading = Vector3.RotateTowards(heading, toCentre.normalized, Time.deltaTime * 1.5f, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * 1.2f);
        transform.position += transform.forward * cfg.sunfishSpeed * Time.deltaTime;
        pivot.localPosition = new Vector3(0f, surfaceY + Mathf.Sin(t * 1.3f) * 0.25f, 0f);
        pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f) * 6f);
    }

    void SetHurtboxes(bool on)
    {
        if (hurtboxes == null) return;
        foreach (var c in hurtboxes) if (c != null) c.enabled = on;
    }
}
