using UnityEngine;

// Phase 1B whirlpool: the KrakenWhirlpool.fbx funnel (radius modelRadius at scale 1) with the
// animated ShipButtlr/Whirlpool shader, spun slowly and scaled to the configured radius. The pull
// itself is applied by BossEncounterController through PlayerShip.SetExternalDrift(PullAt(...)).
public class Whirlpool : MonoBehaviour
{
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    public Transform visual;
    public Renderer  surface;
    public float     modelRadius = 30f;
    public float     spinSpeed   = 35f;   // degrees per second (the shader adds the inner swirl)

    KrakenLevelConfig     cfg;
    MaterialPropertyBlock block;
    float radius;
    float age;
    float fadeOut = -1f;    // >= 0 while fading out
    float alpha;
    float nextSound;

    public bool IsActive => gameObject.activeSelf && fadeOut < 0f;
    public Vector3 Centre => KrakenUtil.Flat(transform.position);
    public float Radius => radius;

    public void Init(KrakenLevelConfig cfg)
    {
        this.cfg = cfg;
        block = new MaterialPropertyBlock();
        gameObject.SetActive(false);
    }

    public void Begin(Vector3 centre, float radius)
    {
        transform.position = KrakenUtil.Flat(centre) + Vector3.up * 0.3f;   // funnel bottom stays above the sea plane
        this.radius = radius;
        age = 0f;
        fadeOut = -1f;
        nextSound = 0f;
        gameObject.SetActive(true);
        Apply(0.2f, 0f);
        SoundManager.PlayAt("splash", centre, 1f, 0.05f, 0.5f);
    }

    public void End(float fadeTime = 0.8f)
    {
        if (!gameObject.activeSelf) return;
        fadeOut = Mathf.Max(0.05f, fadeTime);
        age = 0f;
    }

    public void HideImmediate() => gameObject.SetActive(false);

    // Drift velocity toward the centre: strongest in the middle, fading to zero at
    // pullReachMultiplier x radius. Moderate - a blue ship (20 u/s) can always escape.
    public Vector3 PullAt(Vector3 pos)
    {
        if (!IsActive) return Vector3.zero;
        Vector3 d = Centre - KrakenUtil.Flat(pos);
        float dist = d.magnitude;
        if (dist < 0.5f) return Vector3.zero;
        float reachR = radius * cfg.pullReachMultiplier;
        if (dist >= reachR) return Vector3.zero;
        float speed = dist <= radius
            ? Mathf.Lerp(cfg.pullSpeedCentre, cfg.pullSpeedRim, dist / radius)
            : Mathf.Lerp(cfg.pullSpeedRim, 0f, (dist - radius) / (reachR - radius));
        float appear = Mathf.Clamp01(age / 0.5f);
        // slight tangential component so the ship visibly circles
        Vector3 dir = d / dist;
        Vector3 tangent = new Vector3(dir.z, 0f, -dir.x);
        return (dir * 0.85f + tangent * 0.35f).normalized * speed * appear;
    }

    void Update()
    {
        age += Time.deltaTime;
        visual.Rotate(Vector3.up, -spinSpeed * Time.deltaTime, Space.World);
        if (fadeOut >= 0f)
        {
            float k = Mathf.Clamp01(age / fadeOut);
            Apply(Mathf.Lerp(1f, 0.6f, k), 1f - k);
            if (k >= 1f) gameObject.SetActive(false);
            return;
        }
        float appear = KrakenUtil.Smooth01(age / 0.4f);
        Apply(Mathf.Lerp(0.2f, 1f, appear), appear);
        if (age >= nextSound)
        {
            nextSound = age + 0.6f;
            SoundManager.PlayAt("splash", Centre, 0.45f, 0.15f, 0.55f);
        }
    }

    // The funnel mesh is 7 deep, but the sea plane is opaque: squash it almost flat and keep it just
    // above the water (Begin lifts the root 0.3) - the shader spiral sells the depth.
    const float FlatScaleY = 0.025f;

    void Apply(float scaleK, float a)
    {
        float s = radius / Mathf.Max(0.01f, modelRadius) * scaleK;
        visual.localScale = new Vector3(s, FlatScaleY, s);
        alpha = a;
        if (surface != null)
        {
            block.SetFloat(AlphaId, alpha);
            surface.SetPropertyBlock(block);
        }
    }
}
