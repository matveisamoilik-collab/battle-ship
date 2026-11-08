using UnityEngine;

// Level 6 seagull (model: DreadholdSeagull.fbx from 3d/level6/build_dreadhold.py).
// Flies in from the distance toward the camera, turns round to hover tail-first in front of the
// lens, drops a white blob onto it (→ CameraSplat), then flies off and destroys itself.
// The whole flight is in camera-local space, so the ship/camera moving can't outrun it.
// Wings are flapped in code: WingL/WingR pivot at the shoulder, WingTipL/WingTipR (re-parented
// under them in Awake) pivot at the wrist and follow with a phase lag.
[DefaultExecutionOrder(100)] // after CameraFollow.LateUpdate, so the camera-relative bird doesn't jitter
public class Seagull : MonoBehaviour
{
    public Material poopMaterial;

    const float ApproachTime = 2.8f;
    const float TurnTime     = 0.6f;
    const float HoverTime    = 0.35f;
    const float DropTime     = 0.22f;
    const float LeaveTime    = 2.4f;
    static readonly Vector3 HoverPoint = new Vector3(0f, 0.9f, 4.6f);   // camera-local
    static readonly Vector3 TailLocal  = new Vector3(0f, 0.05f, -0.7f); // bird-local vent

    enum Phase { Approach, Turn, Hover, Drop, Leave }

    class Wing
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 axis;   // in t.parent space; rotating +angle raises the wing
    }

    Wing[] wings = new Wing[0], tips = new Wing[0];
    Camera cam;
    CameraSplat splat;
    Phase phase;
    float phaseTime;
    float side;            // +1 / -1: which side it comes in from and leaves to
    Vector3 start, ctrl, leaveTo;
    Vector3 localPos;      // camera-local
    Quaternion localRot;   // camera-local
    Quaternion turnFrom;
    float flapPhase;
    Transform dropping;
    Vector3 dropFrom, dropTo;
    Vector2 hitViewport;

    public void Init(Camera camera, CameraSplat target)
    {
        cam   = camera;
        splat = target;
    }

    void Awake()
    {
        var w = new System.Collections.Generic.List<Wing>();
        var k = new System.Collections.Generic.List<Wing>();
        foreach (string s in new[] { "L", "R" })
        {
            Transform wing = FindDeep(transform, "Wing" + s);
            Transform tip  = FindDeep(transform, "WingTip" + s);
            if (wing == null) continue;
            if (tip != null) tip.SetParent(wing, true);
            w.Add(MakeWing(wing, tip != null ? tip : wing));
            if (tip != null) k.Add(MakeWing(tip, tip));
        }
        wings = w.ToArray();
        tips  = k.ToArray();
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // The flap axis is worked out from the geometry (which side of the body the wing is on),
    // so it doesn't depend on how the FBX axis conversion landed in the hierarchy.
    Wing MakeWing(Transform t, Transform outer)
    {
        Vector3 c = outer.position;
        var r = outer.GetComponent<Renderer>();
        if (r != null) c = r.bounds.center;
        Vector3 o = transform.InverseTransformPoint(c);
        o.y = 0f;
        Vector3 axisRoot = Vector3.Cross(o.normalized, Vector3.up);
        Vector3 axisWorld = transform.TransformDirection(axisRoot);
        return new Wing { t = t, rest = t.localRotation, axis = t.parent.InverseTransformDirection(axisWorld).normalized };
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) { Destroy(gameObject); return; }

        side    = Random.value < 0.5f ? -1f : 1f;
        start   = new Vector3(side * Random.Range(12f, 20f), Random.Range(6f, 10f), 50f);
        ctrl    = new Vector3(-side * 5f, 6f, 20f);
        leaveTo = HoverPoint + new Vector3(side * 6f, 7f, 30f);
        hitViewport = new Vector2(Random.Range(0.36f, 0.64f), Random.Range(0.40f, 0.62f));
        SetPhase(Phase.Approach);
        Approach(0f);
        Apply();
        SoundManager.Play("seagull_cry", 0.7f, 0.08f);
    }

    void SetPhase(Phase p)
    {
        phase = p;
        phaseTime = 0f;
    }

    void LateUpdate()
    {
        if (cam == null) { Cleanup(); return; }
        float dt = Time.deltaTime;
        phaseTime += dt;
        float flapHz = 3.6f, amp = 32f;

        switch (phase)
        {
            case Phase.Approach:
                Approach(Mathf.Clamp01(phaseTime / ApproachTime));
                if (phaseTime >= ApproachTime) { turnFrom = localRot; SetPhase(Phase.Turn); }
                break;

            case Phase.Turn:
            {
                float t = Mathf.Clamp01(phaseTime / TurnTime);
                float e = t * t * (3f - 2f * t);
                // swing round through the side it came from, ending tail-first, nose up
                Vector3 f0 = turnFrom * Vector3.forward;
                float yaw0 = Mathf.Atan2(f0.x, f0.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(yaw0, 0f);
                if (Mathf.Abs(Mathf.Abs(delta) - 180f) < 30f) delta = side * Mathf.Abs(delta);
                float pitch = Mathf.Lerp(0f, -22f, e);
                float roll  = Mathf.Sin(t * Mathf.PI) * 30f * -Mathf.Sign(delta);
                localRot = Quaternion.Euler(pitch, yaw0 + delta * e, roll);
                localPos = HoverPoint + Vector3.up * Bob();
                flapHz = 5.5f; amp = 40f;
                if (phaseTime >= TurnTime) SetPhase(Phase.Hover);
                break;
            }

            case Phase.Hover:
                localPos = HoverPoint + Vector3.up * Bob();
                flapHz = 6f; amp = 42f;
                if (phaseTime >= HoverTime) BeginDrop();
                break;

            case Phase.Drop:
            {
                float t = Mathf.Clamp01(phaseTime / DropTime);
                localPos = HoverPoint + Vector3.up * (Bob() + 0.12f * Mathf.Sin(t * Mathf.PI)); // little hop as it lets go
                flapHz = 6f; amp = 42f;
                if (dropping != null)
                {
                    Vector3 p = Vector3.Lerp(dropFrom, dropTo, t * t);
                    dropping.position   = cam.transform.TransformPoint(p);
                    dropping.localScale = new Vector3(0.16f, 0.13f, 0.16f) * Mathf.Lerp(1f, 1.6f, t);
                }
                if (phaseTime >= DropTime)
                {
                    if (dropping != null) Destroy(dropping.gameObject);
                    dropping = null;
                    if (splat != null) splat.Splat(hitViewport);
                    SoundManager.Play("poop_splat", 1f, 0.06f);
                    if (GameManager.Instance != null) GameManager.Instance.ShakeCamera(0.15f, 0.06f);
                    SetPhase(Phase.Leave);
                    SoundManager.Play("seagull_cry", 0.55f, 0.05f, 1.15f);
                }
                break;
            }

            case Phase.Leave:
            {
                float t = Mathf.Clamp01(phaseTime / LeaveTime);
                Vector3 from = HoverPoint, prev = localPos;
                localPos = Vector3.Lerp(from, leaveTo, t * t);
                Vector3 v = localPos - prev;
                if (v.sqrMagnitude > 1e-6f)
                    localRot = Quaternion.Slerp(localRot, Quaternion.LookRotation(v, Vector3.up), 6f * dt);
                flapHz = 5f; amp = 38f;
                if (phaseTime >= LeaveTime) { Destroy(gameObject); return; }
                break;
            }
        }

        Flap(flapHz, amp, dt);
        Apply();
    }

    float Bob() => 0.08f * Mathf.Sin(Time.time * 7f);

    // Quadratic Bézier from far away to the hover point, easing out; faces along the path and banks
    void Approach(float t)
    {
        float e = 1f - (1f - t) * (1f - t);
        float u = 1f - e;
        localPos = u * u * start + 2f * u * e * ctrl + e * e * HoverPoint;
        Vector3 tangent = 2f * u * (ctrl - start) + 2f * e * (HoverPoint - ctrl);
        if (tangent.sqrMagnitude < 1e-6f) return;
        var look = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        Vector3 accel = 2f * (start - 2f * ctrl + HoverPoint);
        float bank = Vector3.Dot(accel, look * Vector3.right);
        localRot = look * Quaternion.Euler(0f, 0f, -Mathf.Clamp(bank * 0.6f, -30f, 30f));
    }

    void BeginDrop()
    {
        SetPhase(Phase.Drop);
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Dropping";
        Destroy(go.GetComponent<Collider>());
        var mr = go.GetComponent<MeshRenderer>();
        if (poopMaterial != null) mr.sharedMaterial = poopMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dropping = go.transform;
        dropFrom = cam.transform.InverseTransformPoint(transform.TransformPoint(TailLocal));
        Vector3 hit = cam.ViewportToWorldPoint(new Vector3(hitViewport.x, hitViewport.y, cam.nearClipPlane + 0.15f));
        dropTo = cam.transform.InverseTransformPoint(hit);
        dropping.position   = cam.transform.TransformPoint(dropFrom);
        dropping.localScale = new Vector3(0.16f, 0.13f, 0.16f);
    }

    void Flap(float hz, float amp, float dt)
    {
        flapPhase += 2f * Mathf.PI * hz * dt;
        float inner = 6f + amp * Mathf.Sin(flapPhase);                 // slight dihedral
        float outer = amp * 0.7f * Mathf.Sin(flapPhase - 0.9f);        // tips lag behind
        foreach (var w in wings) w.t.localRotation = Quaternion.AngleAxis(inner, w.axis) * w.rest;
        foreach (var w in tips)  w.t.localRotation = Quaternion.AngleAxis(outer, w.axis) * w.rest;
    }

    void Apply()
    {
        transform.SetPositionAndRotation(cam.transform.TransformPoint(localPos), cam.transform.rotation * localRot);
    }

    void Cleanup()
    {
        if (dropping != null) Destroy(dropping.gameObject);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (dropping != null) Destroy(dropping.gameObject);
    }
}
