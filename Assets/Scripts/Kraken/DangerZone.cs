using UnityEngine;

// Telegraph disc on the water: a faint outer disc marks the area and an inner disc grows from the
// centre until it fills the outer one exactly when the attack lands, so the timing is readable on
// a phone. Used for tentacle slams (red), the kraken emergence (red), ink (purple) and rocks
// (orange) landing spots and the throne rise. Pooled by BossEncounterController.
// Prefab: root (this) -> "Outer" + "Inner" flat cylinders (radius 0.5 at scale 1).
public class DangerZone : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public Renderer outer;
    public Renderer inner;

    MaterialPropertyBlock block;
    Color   color;
    float   radius;
    float   duration;
    float   age;

    void Awake() { block = new MaterialPropertyBlock(); }

    public void Begin(Vector3 pos, float radius, float duration, Color color)
    {
        transform.position = new Vector3(pos.x, 0.06f, pos.z);
        this.radius   = radius;
        this.duration = Mathf.Max(0.05f, duration);
        this.color    = color;
        age     = 0f;
        float d = radius * 2f;
        if (outer != null) outer.transform.localScale = new Vector3(d, 0.02f, d);
        Update();
    }

    public float Progress => Mathf.Clamp01(age / duration);

    void Update()
    {
        age += Time.deltaTime;
        float t = Progress;
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * Mathf.Lerp(8f, 22f, t));
        if (outer != null)
        {
            Color c = color; c.a = Mathf.Lerp(0.18f, 0.32f, pulse);
            block.SetColor(BaseColorId, c);
            outer.SetPropertyBlock(block);
        }
        if (inner != null)
        {
            float d = radius * 2f * Mathf.Lerp(0.08f, 1f, t);
            inner.transform.localScale = new Vector3(d, 0.03f, d);
            Color c = color; c.a = Mathf.Lerp(0.35f, 0.6f, t);
            block.SetColor(BaseColorId, c);
            inner.SetPropertyBlock(block);
        }
    }
}
