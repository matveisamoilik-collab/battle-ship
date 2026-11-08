using System.Collections;
using UnityEngine;

// Level 6 controller on the Dreadhold island root. Tracks the three pillars; each collapse
// tips the island toward the lost support and spawns an enemy boat. When the last pillar
// falls the island shakes, sinks into the sea and the level is won.
public class CastleIsland : MonoBehaviour
{
    public CastlePillar[] pillars;
    public Transform      sinkRoot;      // island + castle + cannons visual
    public GameObject     splashPrefab;

    private const float TiltPerPillar  = 2f;
    private const float TiltTime       = 0.8f;
    private const float BoatSpawnDist  = 12f;
    private const float ShakeTime      = 2f;
    private const float ShakeAmplitude = 0.4f;
    private const float SinkTime       = 6f;
    private const float SinkDepth      = 45f;
    private const float SinkExtraTilt  = 6f;

    private int        alive;
    private int        total;

    public const int BombsAfterPillars = 2;     // cannons switch to bombs once this many pillars fell
    public int  PillarsDestroyed => total - alive;
    public bool BombMode => PillarsDestroyed >= BombsAfterPillars;
    private Quaternion tilt = Quaternion.identity;
    private Quaternion sinkBaseRot;
    private Vector3    sinkBasePos;
    private Coroutine  tiltRoutine;

    void Start()
    {
        if (sinkRoot != null)
        {
            sinkBaseRot = sinkRoot.localRotation;
            sinkBasePos = sinkRoot.localPosition;
        }
        alive = 0;
        foreach (var p in pillars)
        {
            if (p == null) continue;
            p.island = this;
            alive++;
        }
        total = alive;
        GameManager.Instance?.SetObjective(alive);
    }

    public void OnPillarCollapsed(CastlePillar pillar)
    {
        alive = Mathf.Max(0, alive - 1);
        GameManager.Instance?.SetObjective(alive);

        Vector3 outward = pillar.transform.position - transform.position;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;

        // The island sags toward the support it just lost
        Vector3 axis = Vector3.Cross(Vector3.up, outward);
        Quaternion target = Quaternion.AngleAxis(TiltPerPillar, axis) * tilt;
        if (tiltRoutine != null) StopCoroutine(tiltRoutine);
        tiltRoutine = StartCoroutine(TiltTo(target));

        if (alive > 0)
        {
            Vector3 spawn = new Vector3(pillar.transform.position.x, 0f, pillar.transform.position.z)
                          + outward * BoatSpawnDist;
            GameManager.Instance?.SpawnLevel6Boat(spawn, Quaternion.LookRotation(outward));
        }
        else
        {
            StartCoroutine(Finale());
        }
    }

    IEnumerator TiltTo(Quaternion target)
    {
        Quaternion start = tilt;
        for (float t = 0f; t < TiltTime; t += Time.deltaTime)
        {
            tilt = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, t / TiltTime));
            ApplyPose(Vector3.zero);
            yield return null;
        }
        tilt = target;
        ApplyPose(Vector3.zero);
        tiltRoutine = null;
    }

    void ApplyPose(Vector3 offset)
    {
        if (sinkRoot == null) return;
        sinkRoot.localRotation = tilt * sinkBaseRot;
        sinkRoot.localPosition = sinkBasePos + offset;
    }

    IEnumerator Finale()
    {
        GameManager.Instance?.BeginLevel6Finale();
        if (tiltRoutine != null) { StopCoroutine(tiltRoutine); tiltRoutine = null; }

        // 1. Shake
        GameManager.Instance?.ShakeCamera(ShakeTime, 0.5f);
        SoundManager.PlayAt("big_explosion", transform.position, 1f, 0f, 0.5f);
        for (float t = 0f; t < ShakeTime; t += Time.deltaTime)
        {
            float amp = ShakeAmplitude * (0.5f + t / ShakeTime);
            ApplyPose(new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp) * 0.5f, Random.Range(-amp, amp)));
            yield return null;
        }

        // 2. Sink, tipping a little further over
        SoundManager.PlayAt("ship_sink", transform.position, 1f, 0f, 0.6f);
        var data = GetComponent<IslandData>();
        Quaternion startTilt = tilt;
        Vector3 tipAxis = Random.insideUnitSphere; tipAxis.y = 0f;
        tipAxis = tipAxis.sqrMagnitude > 0.01f ? tipAxis.normalized : Vector3.right;
        float nextSplash = 0f;
        for (float t = 0f; t < SinkTime; t += Time.deltaTime)
        {
            float k = t / SinkTime;
            float ease = k * k;
            tilt = Quaternion.AngleAxis(SinkExtraTilt * k, tipAxis) * startTilt;
            float jitter = ShakeAmplitude * 0.5f * (1f - k);
            ApplyPose(new Vector3(Random.Range(-jitter, jitter), -SinkDepth * ease, Random.Range(-jitter, jitter)));
            if (data != null) data.radius = Mathf.Lerp(24f, 0f, k); // ships may sail over the sunken island

            if (t >= nextSplash && splashPrefab != null)
            {
                nextSplash = t + 0.25f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(20f, 28f);
                Instantiate(splashPrefab, transform.position + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r), Quaternion.identity);
            }
            if (t > 1f && Random.value < Time.deltaTime * 1.5f)
                GameManager.Instance?.ShakeCamera(0.3f, 0.3f);
            yield return null;
        }
        if (sinkRoot != null) sinkRoot.gameObject.SetActive(false);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        GameManager.Instance?.Level6Cleared();
    }
}
