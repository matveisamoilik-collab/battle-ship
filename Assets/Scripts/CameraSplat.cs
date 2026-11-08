using UnityEngine;
using UnityEngine.UI;

// Level 6: seagull droppings on the camera lens. A full-screen RawImage on the HUD canvas
// (first sibling, so the HUD stays readable on top; raycastTarget off so it never eats taps).
// Every Splat() draws a fresh random splotch into a screen-aspect texture, sized by a search so
// exactly CoverageTarget (45 %) of the screen is covered, keeps it fully opaque for HoldDuration,
// then fades it out over FadeDuration.
[RequireComponent(typeof(RawImage))]
public class CameraSplat : MonoBehaviour
{
    public const float CoverageTarget = 0.45f;
    public const float HoldDuration   = 3f;   // fully opaque
    public const float FadeDuration   = 3f;   // then fades out
    const float PopDuration = 0.12f;
    const int   TexWidth    = 320;
    const int   SearchWidth = 80;   // coarse grid used to find the size that hits the coverage target

    struct Circle  { public Vector2 c; public float r; }
    struct Capsule { public Vector2 a, b; public float r; }

    RawImage  image;
    Texture2D tex;
    float     age = -1f;   // < 0 = hidden

    // Splotch shape, in units of "splat size" (scaled by s), relative to the centre
    float[]   lobeAmp = new float[3], lobePhase = new float[3];
    Circle[]  drops;
    Capsule[] arms;
    Vector2   centre;      // in normalised screen units: x 0..aspect, y 0..1
    float     aspect;
    float     noiseSeed;

    public float CoveredFraction { get; private set; }   // measured on the final texture

    void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
    }

    void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    // viewport: where the dropping hit the lens (0..1, 0..1)
    public void Splat(Vector2 viewport)
    {
        aspect = Screen.height > 0 ? Mathf.Clamp((float)Screen.width / Screen.height, 1f, 3f) : 16f / 9f;
        int w = TexWidth, h = Mathf.Max(32, Mathf.RoundToInt(TexWidth / aspect));
        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex != null) Destroy(tex);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        centre = new Vector2(viewport.x * aspect, viewport.y);
        RandomShape();
        float s = FindScale();
        Paint(s);

        image.texture = tex;
        image.color   = Color.white;
        image.enabled = true;
        age = 0f;
    }

    void Update()
    {
        if (age < 0f) return;
        age += Time.unscaledDeltaTime;   // keeps fading even while the end panel pauses the game

        float pop = Mathf.Clamp01(age / PopDuration);
        transform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, 1f - (1f - pop) * (1f - pop));

        float a = 1f - Mathf.Clamp01((age - HoldDuration) / FadeDuration);
        image.color = new Color(1f, 1f, 1f, a);
        if (a <= 0f)
        {
            image.enabled = false;
            transform.localScale = Vector3.one;
            age = -1f;
        }
    }

    // ---- shape ---------------------------------------------------------------------------------

    void RandomShape()
    {
        for (int i = 0; i < 3; i++)
        {
            lobeAmp[i]   = Random.Range(0.05f, 0.18f) / (i + 1);
            lobePhase[i] = Random.Range(0f, Mathf.PI * 2f);
        }
        noiseSeed = Random.Range(0f, 500f);

        var armList  = new System.Collections.Generic.List<Capsule>();
        var dropList = new System.Collections.Generic.List<Circle>();

        // Splash arms ending in fat droplets
        int armCount = Random.Range(7, 11);
        for (int i = 0; i < armCount; i++)
        {
            float ang = (i + Random.Range(-0.3f, 0.3f)) / armCount * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float len = Random.Range(1.25f, 1.8f);
            armList.Add(new Capsule { a = dir * 0.7f, b = dir * len, r = Random.Range(0.07f, 0.12f) });
            dropList.Add(new Circle { c = dir * len, r = Random.Range(0.13f, 0.21f) });
        }
        // Runny drips down the lens
        int dripCount = Random.Range(2, 5);
        for (int i = 0; i < dripCount; i++)
        {
            float x = Random.Range(-0.65f, 0.65f);
            float len = Random.Range(1.4f, 2.3f);
            armList.Add(new Capsule { a = new Vector2(x, -0.4f), b = new Vector2(x, -len), r = Random.Range(0.055f, 0.085f) });
            dropList.Add(new Circle { c = new Vector2(x, -len), r = Random.Range(0.10f, 0.14f) });
        }
        // Loose specks around it
        int speckCount = Random.Range(8, 14);
        for (int i = 0; i < speckCount; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            dropList.Add(new Circle { c = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(1.7f, 2.7f),
                                      r = Random.Range(0.03f, 0.10f) });
        }
        arms  = armList.ToArray();
        drops = dropList.ToArray();
    }

    // Signed distance-like value (normalised screen units): > 0 inside the splotch
    float Field(Vector2 p, float s)
    {
        Vector2 d = p - centre;
        float dist = d.magnitude;
        float th = Mathf.Atan2(d.y, d.x);
        float rad = 1f + lobeAmp[0] * Mathf.Sin(3f * th + lobePhase[0])
                       + lobeAmp[1] * Mathf.Sin(5f * th + lobePhase[1])
                       + lobeAmp[2] * Mathf.Sin(9f * th + lobePhase[2]);
        float best = s * rad - dist;
        if (best > 0f) return best;

        Vector2 q = d / s;   // shape space
        for (int i = 0; i < drops.Length; i++)
            best = Mathf.Max(best, (drops[i].r - (q - drops[i].c).magnitude) * s);
        for (int i = 0; i < arms.Length; i++)
        {
            Vector2 ab = arms[i].b - arms[i].a;
            float t = Mathf.Clamp01(Vector2.Dot(q - arms[i].a, ab) / ab.sqrMagnitude);
            best = Mathf.Max(best, (arms[i].r - (q - (arms[i].a + ab * t)).magnitude) * s);
        }
        return best;
    }

    float Coverage(float s, int w)
    {
        int h = Mathf.Max(8, Mathf.RoundToInt(w / aspect));
        int inside = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Field(new Vector2((x + 0.5f) / w * aspect, (y + 0.5f) / h), s) > 0f) inside++;
        return (float)inside / (w * h);
    }

    // Binary search for the splat size whose on-screen coverage is CoverageTarget
    float FindScale()
    {
        float lo = 0.05f, hi = 1.5f;
        for (int i = 0; i < 12; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (Coverage(mid, SearchWidth) < CoverageTarget) lo = mid; else hi = mid;
        }
        return 0.5f * (lo + hi);
    }

    void Paint(float s)
    {
        int w = tex.width, h = tex.height;
        var px = new Color32[w * h];
        float pxSize = 1f / h;   // one texel in normalised units
        var white  = new Color(0.98f, 0.97f, 0.92f);
        var rim    = new Color(0.80f, 0.80f, 0.72f);
        var green  = new Color(0.60f, 0.62f, 0.48f);
        var grit   = new Color(0.33f, 0.31f, 0.26f);
        Vector2 gloss = centre + new Vector2(-0.3f, 0.32f) * s;
        int inside = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) / w * aspect, (y + 0.5f) / h);
                float f = Field(p, s);
                float alpha = Mathf.Clamp01(f / (1.3f * pxSize) + 0.5f);
                if (alpha <= 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                if (f > 0f) inside++;

                // thick creamy middle, greyer rim, greenish streaks, dark grit, a wet highlight
                float depth = Mathf.Clamp01(f / (0.18f * s));
                Color c = Color.Lerp(rim, white, Mathf.Sqrt(depth));
                float n = Mathf.PerlinNoise(noiseSeed + p.x * 9f, p.y * 9f);
                if (n > 0.58f) c = Color.Lerp(c, green, Mathf.Clamp01((n - 0.58f) * 4f) * 0.8f);
                float g = Mathf.PerlinNoise(noiseSeed * 1.7f + p.x * 55f, 31f + p.y * 55f);
                if (g > 0.78f && depth > 0.15f) c = Color.Lerp(c, grit, Mathf.Clamp01((g - 0.78f) * 8f));
                float gd = (p - gloss).magnitude / (0.22f * s);
                if (gd < 1f) c = Color.Lerp(c, Color.white, (1f - gd) * 0.85f);

                c.a = alpha;
                px[y * w + x] = c;
            }
        }
        CoveredFraction = (float)inside / (w * h);
        tex.SetPixels32(px);
        tex.Apply(false);
    }
}
