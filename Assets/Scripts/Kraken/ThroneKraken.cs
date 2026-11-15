using UnityEngine;

// Phase 2 kraken king on his throne (KrakenThrone.fbx + KrakenKing.fbx). Rises in the arena
// centre, is shielded while any jellyfish lives, then throws rocks with two smaller tentacles
// (TentacleAttacker, driven by BossEncounterController) until cfg.throneHP torpedo hits sink it.
// Its IslandData is enabled once risen, so the ship collides with it (and the L5+ island-crash
// rule applies).
public class ThroneKraken : MonoBehaviour, ITorpedoTarget
{
    public enum State { Hidden, Rising, Risen, Sinking }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public Transform          pivot;
    public Collider[]         hurtboxes;
    public HitFlash           flash;
    public IslandData         islandData;
    public Renderer           shield;
    public TentacleAttacker[] throwers;
    public float              hiddenDepth = 42f;
    public Color              shieldColor = new Color(0.35f, 0.85f, 1f, 0.28f);

    BossEncounterController host;
    KrakenLevelConfig       cfg;
    State state = State.Hidden;
    float t, duration;
    float hp;
    bool  shielded;
    float shieldPop = -1f;    // >= 0 while the shield bursts
    float shieldHit;          // flash timer when a torpedo is blocked
    MaterialPropertyBlock block;

    public float HP        => hp;
    public bool  IsAlive   => hp > 0f;
    public bool  IsRisen   => state == State.Risen;
    public bool  Shielded  => shielded;
    public State CurrentState => state;

    public void Init(BossEncounterController host, KrakenLevelConfig cfg)
    {
        this.host = host;
        this.cfg  = cfg;
        block = new MaterialPropertyBlock();
        if (islandData != null) islandData.radius = cfg.ThroneRadius;
        foreach (var th in throwers) if (th != null) th.Init(host, cfg, transform.position + transform.forward * 50f);
        HideImmediate();
    }

    public void Rise(float time)
    {
        hp = cfg.throneHP;
        gameObject.SetActive(true);
        pivot.localPosition = new Vector3(0f, -hiddenDepth, 0f);
        pivot.localRotation = Quaternion.identity;
        SetHurtboxes(false);
        SetShield(false, instant: true);
        state = State.Rising; t = 0f; duration = time;
        SoundManager.PlayAt("big_explosion", transform.position, 0.9f, 0f, 0.45f);
        GameManager.Instance?.ShakeCamera(time, 0.25f);
    }

    public void ShowRisenImmediate()
    {
        hp = cfg.throneHP;
        gameObject.SetActive(true);
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
        state = State.Risen;
        SetHurtboxes(true);
        if (islandData != null) islandData.enabled = true;
        host.OnThroneRisen();
    }

    public void Sink(float time)
    {
        if (state == State.Hidden) return;
        SetHurtboxes(false);
        SetShield(false, instant: true);
        if (islandData != null) islandData.enabled = false;
        foreach (var th in throwers) if (th != null) th.Sink(time * 0.6f);
        state = State.Sinking; t = 0f; duration = time;
        SoundManager.PlayAt("big_explosion", transform.position, 1f, 0f, 0.5f);
        SoundManager.PlayAt("ship_sink", transform.position, 1f, 0f, 0.6f);
        GameManager.Instance?.ShakeCamera(1.2f, 0.45f);
    }

    public void HideImmediate()
    {
        state = State.Hidden;
        hp = 0f;
        SetHurtboxes(false);
        SetShield(false, instant: true);
        if (islandData != null) islandData.enabled = false;
        foreach (var th in throwers) if (th != null) th.HideImmediate();
        gameObject.SetActive(false);
    }

    public void SetShield(bool on, bool instant = false)
    {
        if (shield == null) { shielded = on; return; }
        if (on)
        {
            shielded = true;
            shieldPop = -1f;
            shield.gameObject.SetActive(true);
            shield.transform.localScale = Vector3.one * ShieldDiameter;
            if (!instant) SoundManager.PlayAt("splash", transform.position, 0.8f, 0f, 1.8f);
        }
        else
        {
            bool wasOn = shielded;
            shielded = false;
            if (instant || !wasOn) { shield.gameObject.SetActive(false); shieldPop = -1f; }
            else
            {
                shieldPop = 0f;
                SoundManager.PlayAt("big_explosion", transform.position, 0.7f, 0f, 1.6f);
                GameManager.Instance?.ShakeCamera(0.3f, 0.3f);
            }
        }
    }

    float ShieldDiameter => cfg != null ? (cfg.ThroneRadius + 4f) * 2f : 36f;

    public bool OnTorpedoHit(Torpedo torpedo)
    {
        if (!torpedo.isPlayerTorpedo || state != State.Risen || hp <= 0f) return true;
        if (shielded)
        {
            shieldHit = 0.25f;
            SoundManager.PlayAt("hit_island", torpedo.transform.position, 1f, 0.05f, 1.7f);
            host.OnShieldBlocked(torpedo.transform.position);
            return true;
        }
        hp = Mathf.Max(0f, hp - torpedo.damage);
        if (flash != null) flash.Flash(new Color(1f, 0.9f, 0.9f), 0.18f);
        SoundManager.PlayAt("hit_ship", torpedo.transform.position, 1f, 0.05f, 0.7f);
        GameManager.Instance?.ShakeCamera(0.2f, 0.2f);
        host.OnBossDamaged();
        return true;
    }

    // Second jellyfish wave: restores some HP (capped at max)
    public void Heal(float amount)
    {
        if (state != State.Risen) return;
        hp = Mathf.Min(cfg.throneHP, hp + amount);
        if (flash != null) flash.Flash(new Color(0.3f, 1f, 0.4f), 0.6f);
        host.OnBossDamaged();
    }

    public void DebugDamage(float amount)
    {
        if (state != State.Risen || shielded) return;
        hp = Mathf.Max(0f, hp - amount);
        if (flash != null) flash.Flash(Color.white, 0.18f);
    }

    void Update()
    {
        if (state == State.Hidden) return;
        t += Time.deltaTime;
        switch (state)
        {
            case State.Rising:
            {
                float k = KrakenUtil.Smooth01(t / Mathf.Max(0.01f, duration));
                pivot.localPosition = new Vector3(Random.Range(-0.2f, 0.2f) * (1f - k), Mathf.Lerp(-hiddenDepth, 0f, k), 0f);
                if (Random.value < Time.deltaTime * 8f)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    host.Splash(transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * cfg.ThroneRadius, 1.6f);
                }
                if (k >= 1f)
                {
                    state = State.Risen; t = 0f;
                    pivot.localPosition = Vector3.zero;
                    SetHurtboxes(true);
                    if (islandData != null) islandData.enabled = true;
                    host.OnThroneRisen();
                }
                break;
            }
            case State.Risen:
                pivot.localPosition = new Vector3(0f, Mathf.Sin(t * 0.9f) * 0.25f, 0f);
                break;
            case State.Sinking:
            {
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
                pivot.localPosition = new Vector3(Random.Range(-0.25f, 0.25f) * (1f - k), -hiddenDepth * k * k, 0f);
                pivot.localRotation = Quaternion.Euler(10f * k, 0f, 6f * k);
                if (Random.value < Time.deltaTime * 10f)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    host.Splash(transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * cfg.ThroneRadius, 2f);
                }
                if (k >= 1f) HideImmediate();
                break;
            }
        }
        UpdateShield();
    }

    void UpdateShield()
    {
        if (shield == null || !shield.gameObject.activeSelf) return;
        Color c = shieldColor;
        if (shieldPop >= 0f)
        {
            shieldPop += Time.deltaTime;
            float k = Mathf.Clamp01(shieldPop / 0.45f);
            shield.transform.localScale = Vector3.one * ShieldDiameter * (1f + 0.5f * k);
            c.a = shieldColor.a * (1f - k);
            if (k >= 1f) { shield.gameObject.SetActive(false); shieldPop = -1f; }
        }
        else
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            shieldHit = Mathf.Max(0f, shieldHit - Time.deltaTime);
            c.a = shieldColor.a * (0.75f + 0.35f * pulse) + shieldHit * 1.6f;
            shield.transform.localScale = Vector3.one * ShieldDiameter * (1f + 0.015f * pulse);
        }
        block.SetColor(BaseColorId, c);
        shield.SetPropertyBlock(block);
    }

    void SetHurtboxes(bool on)
    {
        if (hurtboxes == null) return;
        foreach (var c in hurtboxes) if (c != null) c.enabled = on;
    }
}
