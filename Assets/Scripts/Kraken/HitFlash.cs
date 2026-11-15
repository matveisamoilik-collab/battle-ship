using System.Collections.Generic;
using UnityEngine;

// Brief emissive flash on every renderer below this object (torpedo hit feedback).
// Uses per-instance material copies made once in Awake (the L7 skin materials have _EMISSION
// enabled with a black emission colour), so a flash never allocates.
public class HitFlash : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    readonly List<Material> mats = new List<Material>();
    float timeLeft;
    float duration = 0.15f;
    Color color    = Color.white;

    void Awake()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            foreach (var m in r.materials)          // instantiates the materials once
                if (m != null && m.HasProperty(EmissionId)) mats.Add(m);
        }
        enabled = false;
    }

    void OnDestroy()
    {
        foreach (var m in mats) if (m != null) Destroy(m);
    }

    public void Flash(Color c, float time = 0.15f)
    {
        color    = c;
        duration = Mathf.Max(0.01f, time);
        timeLeft = duration;
        enabled  = true;
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { Apply(Color.black); enabled = false; return; }
        Apply(color * (timeLeft / duration));
    }

    void Apply(Color c)
    {
        for (int i = 0; i < mats.Count; i++) mats[i].SetColor(EmissionId, c);
    }
}
