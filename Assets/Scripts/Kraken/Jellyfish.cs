using UnityEngine;

// Phase 2B jellyfish. Chases the player half submerged and stings on contact (bounces back,
// then a cooldown). Every few seconds it stops on the surface: slower, a bigger hitbox, glowing
// and ringed - the moment to shoot it. cfg.jellyfishHP torpedo hits kill it.
public class Jellyfish : MonoBehaviour, ITorpedoTarget
{
    public enum State { Rising, Chasing, Surfaced, Dying, Dead }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public Transform      pivot;
    public SphereCollider hitbox;
    public Renderer[]     bodyRenderers;
    public Renderer       highlightRing;
    public Color          glowColor = new Color(1f, 0.5f, 1f);

    BossEncounterController host;
    KrakenLevelConfig       cfg;
    State   state;
    float   t, hp;
    float   surfaceTimer;
    float   contactCooldown;
    Vector3 heading;
    Vector3 knock;            // bounce-back velocity after a sting
    float   baseHitbox;
    float   phase;
    readonly System.Collections.Generic.List<Material> glowMats = new System.Collections.Generic.List<Material>();

    const float SwimY     = -0.7f;
    const float SurfacedY = 0.7f;
    const float RiseTime  = 1.2f;

    public bool IsAlive    => state != State.Dying && state != State.Dead;
    public bool IsSurfaced => state == State.Surfaced;

    public void Init(BossEncounterController host, KrakenLevelConfig cfg, float delay)
    {
        this.host = host;
        this.cfg  = cfg;
        hp    = cfg.jellyfishHP;
        // per-instance materials (made once) for the surfaced glow
        glowMats.Clear();
        if (bodyRenderers != null)
            foreach (var r in bodyRenderers)
                if (r != null)
                    foreach (var m in r.materials)
                        if (m != null && m.HasProperty(EmissionId)) glowMats.Add(m);
        phase = Random.Range(0f, 10f);
        baseHitbox = cfg.jellyfishHitbox;
        if (hitbox != null) hitbox.radius = baseHitbox;
        heading = transform.forward;
        state = State.Rising;
        t = -delay;
        pivot.localPosition = new Vector3(0f, -8f, 0f);
        surfaceTimer = Random.Range(cfg.jellySurfaceIntervalMin, cfg.jellySurfaceIntervalMax);
        SetHighlight(0f);
        if (highlightRing != null) highlightRing.enabled = false;
    }

    public bool OnTorpedoHit(Torpedo torpedo)
    {
        if (!torpedo.isPlayerTorpedo || !IsAlive) return true;
        SoundManager.PlayAt("hit_ship", torpedo.transform.position, 0.9f, 0.1f, 1.5f);
        Damage(torpedo.damage);
        return true;
    }

    public void DebugKill() { if (IsAlive) Damage(hp); }

    void Damage(float amount)
    {
        hp -= amount;
        if (hp <= 0f)
        {
            state = State.Dying; t = 0f;
            if (hitbox != null) hitbox.enabled = false;
            if (highlightRing != null) highlightRing.enabled = false;
            host.Splash(transform.position, 1.2f);
            SoundManager.PlayAt("splash", transform.position, 0.8f, 0.1f, 1.4f);
            host.OnJellyfishKilled(this);
        }
    }

    void Update()
    {
        t += Time.deltaTime;
        float dt = Time.deltaTime;
        switch (state)
        {
            case State.Rising:
            {
                if (t < 0f) return;
                float k = KrakenUtil.Smooth01(t / RiseTime);
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(-8f, SwimY, k), 0f);
                Pulse(1f);
                if (k >= 1f) { state = State.Chasing; t = 0f; }
                break;
            }
            case State.Chasing:
                Move(cfg.jellyfishSpeed, dt);
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(pivot.localPosition.y, SwimY + Mathf.Sin(t * 3f + phase) * 0.15f, dt * 4f), 0f);
                Pulse(1f);
                surfaceTimer -= dt;
                if (surfaceTimer <= 0f) BeginSurfaced();
                break;
            case State.Surfaced:
            {
                Move(cfg.jellyfishSpeed * cfg.jellySurfacedSpeedMul, dt);
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(pivot.localPosition.y, SurfacedY, dt * 5f), 0f);
                Pulse(1.25f);
                float glow = 0.6f + 0.4f * Mathf.Sin(t * 10f);
                SetHighlight(glow * 2.2f);
                if (highlightRing != null)
                {
                    float d = (hitbox != null ? hitbox.radius * 2.2f : 7f) * (0.95f + 0.05f * glow);
                    highlightRing.transform.localScale = new Vector3(d, 0.02f, d);
                }
                if (t >= cfg.jellySurfaceDuration) EndSurfaced();
                break;
            }
            case State.Dying:
            {
                float k = Mathf.Clamp01(t / 0.8f);
                pivot.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, k);
                pivot.localPosition = new Vector3(0f, Mathf.Lerp(pivot.localPosition.y, -6f, k), 0f);
                SetHighlight((1f - k) * 3f);
                if (k >= 1f) { state = State.Dead; gameObject.SetActive(false); }
                break;
            }
        }
    }

    void BeginSurfaced()
    {
        state = State.Surfaced; t = 0f;
        if (hitbox != null) hitbox.radius = baseHitbox * cfg.jellySurfacedHitboxMul;
        if (highlightRing != null) highlightRing.enabled = true;
        host.Splash(transform.position, 0.8f);
        SoundManager.PlayAt("splash", transform.position, 0.5f, 0.1f, 1.6f);
    }

    void EndSurfaced()
    {
        state = State.Chasing; t = 0f;
        if (hitbox != null) hitbox.radius = baseHitbox;
        if (highlightRing != null) highlightRing.enabled = false;
        SetHighlight(0f);
        pivot.localScale = Vector3.one;
        surfaceTimer = Random.Range(cfg.jellySurfaceIntervalMin, cfg.jellySurfaceIntervalMax);
    }

    void Move(float speed, float dt)
    {
        Transform player = host.Player;
        contactCooldown -= dt;
        Vector3 pos = transform.position;

        if (player != null)
        {
            Vector3 to = KrakenUtil.Flat(player.position - pos);
            if (to.sqrMagnitude > 0.01f && contactCooldown <= 0f)
                heading = Vector3.RotateTowards(heading, to.normalized, cfg.jellyfishTurnRate * Mathf.Deg2Rad * dt, 0f);

            // sting on contact
            if (contactCooldown <= 0f && !GameManager.Instance.IsGameOver
                && KrakenUtil.HullDistance(player, pos) <= cfg.SL(cfg.jellyfishContactSL))
            {
                var ship = player.GetComponent<PlayerShip>();
                if (ship != null) ship.TakeDamage(cfg.jellyfishContactDamage);
                SoundManager.PlayAt("hit_ship", pos, 0.9f, 0.1f, 1.7f);
                GameManager.Instance?.ShakeCamera(0.25f, 0.3f);
                contactCooldown = cfg.jellyfishContactCooldown;
                Vector3 away = KrakenUtil.Flat(pos - player.position);
                knock = (away.sqrMagnitude > 0.01f ? away.normalized : -heading) * 18f;
                heading = knock.normalized;
            }
        }

        Vector3 vel = heading * (contactCooldown > 0f ? speed * 0.5f : speed) + knock;
        knock = Vector3.MoveTowards(knock, Vector3.zero, 30f * dt);
        pos += vel * dt;

        // separation from the other jellyfish
        var all = host.Jellies;
        for (int i = 0; i < all.Count; i++)
        {
            var o = all[i];
            if (o == null || o == this || !o.IsAlive) continue;
            Vector3 d = KrakenUtil.Flat(pos - o.transform.position);
            float m = d.magnitude;
            if (m < 5f && m > 0.01f) pos += d / m * (5f - m) * 0.5f;
        }
        // keep out of the throne and inside the arena
        Vector3 fromThrone = KrakenUtil.Flat(pos - host.ArenaCentre);
        float minR = cfg.ThroneRadius + 2f, maxR = host.ArenaRadius - cfg.edgeRockRadius - 2f;
        float r = fromThrone.magnitude;
        if (r < minR && r > 0.01f) pos = host.ArenaCentre + fromThrone / r * minR;
        else if (r > maxR) pos = host.ArenaCentre + fromThrone / r * maxR;
        pos.y = 0f;
        transform.position = pos;
        if (heading.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(heading);
    }

    // bell squash-and-stretch
    void Pulse(float scale)
    {
        float s = Mathf.Sin(t * 4.5f + phase);
        pivot.localScale = new Vector3(1f + 0.08f * s, 1f - 0.1f * s, 1f + 0.08f * s) * scale;
    }

    void SetHighlight(float intensity)
    {
        Color c = glowColor * intensity;
        for (int i = 0; i < glowMats.Count; i++) glowMats[i].SetColor(EmissionId, c);
    }

    void OnDestroy()
    {
        foreach (var m in glowMats) if (m != null) Destroy(m);
    }
}
