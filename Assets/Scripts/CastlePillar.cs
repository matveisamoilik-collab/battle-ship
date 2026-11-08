using UnityEngine;

// Level 6: one of the three glowing stone pillars that hold up the Dreadhold island.
// Player torpedoes call TakeHit(). Each of the first 4 hits cracks one drum (bottom → top),
// turns its rune band dark and knocks off debris; the 5th hit topples the whole pillar.
public class CastlePillar : MonoBehaviour
{
    public const int MaxHits = 5;

    [Header("Model parts (bottom → top)")]
    public Transform[] drums;          // intact drums
    public Transform[] damagedDrums;   // cracked variants, hidden until hit
    public Transform   capital;
    public Transform   baseStone;
    public Renderer    glowRing;
    public Transform   marker;

    [Header("Materials / effects")]
    public Material   runeMaterial;    // saved L6_Rune asset (emissive) — instanced per pillar
    public Material   runeDeadMaterial;
    public Mesh       chunkMesh;
    public Material   chunkMaterial;
    public GameObject splashPrefab;
    public GameObject explosionPrefab;

    [HideInInspector] public CastleIsland island;

    public bool IsDestroyed => hits >= MaxHits;

    private const float TiltPerHit  = 1.5f;
    private const float MarkerY     = 11f;
    private static readonly Color RuneColor = new Color(1f, 0.55f, 0.1f);

    private int        hits;
    private Material   runeLit;
    private Quaternion baseRot;
    private Vector3    outward = Vector3.forward;
    private Vector3    tiltAxis = Vector3.right;
    private Vector3    markerBase;

    void Start()
    {
        baseRot = transform.rotation;
        Vector3 center = island != null ? island.transform.position : Vector3.zero;
        outward = transform.position - center;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;
        tiltAxis = Vector3.Cross(Vector3.up, outward); // positive angle tips the top outward

        if (marker != null) markerBase = marker.localPosition;

        // One emissive rune material per pillar so its glow can pulse independently
        if (runeMaterial != null)
        {
            runeLit = new Material(runeMaterial);
            runeLit.EnableKeyword("_EMISSION");
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                SwapMaterial(r, runeMaterial, runeLit);
        }
    }

    void Update()
    {
        if (IsDestroyed) return;
        float t = Time.time;
        if (runeLit != null)
        {
            float pulse = 1.6f + 0.9f * Mathf.Sin(t * 4f + transform.position.x);
            runeLit.SetColor("_EmissionColor", RuneColor * pulse);
        }
        if (marker != null)
        {
            marker.localPosition = markerBase + Vector3.up * Mathf.Sin(t * 2.5f) * 0.5f;
            marker.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        }
    }

    public void TakeHit()
    {
        if (IsDestroyed) return;
        hits++;

        Vector3 toPlayer = outward;
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            toPlayer = playerGO.transform.position - transform.position;
            toPlayer.y = 0f;
            toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : outward;
        }

        if (hits < MaxHits)
        {
            CrackDrum(hits - 1);
            SpawnDebris(hits - 1, toPlayer, Random.Range(3, 5));
            transform.rotation = Quaternion.AngleAxis(TiltPerHit * hits, tiltAxis) * baseRot;
            SoundManager.PlayAt("hit_island", transform.position, 1f, 0.05f, 0.8f);
            GameManager.Instance?.ShakeCamera(0.25f, 0.25f);
        }
        else
        {
            Collapse(toPlayer);
        }
    }

    void CrackDrum(int i)
    {
        if (drums != null && i < drums.Length && drums[i] != null)
            drums[i].gameObject.SetActive(false);
        if (damagedDrums != null && i < damagedDrums.Length && damagedDrums[i] != null)
        {
            var d = damagedDrums[i];
            d.gameObject.SetActive(true);
            var r = d.GetComponent<Renderer>();
            if (r != null && runeDeadMaterial != null)
            {
                SwapMaterial(r, runeLit, runeDeadMaterial);
                SwapMaterial(r, runeMaterial, runeDeadMaterial);
            }
        }
    }

    void SpawnDebris(int drumIndex, Vector3 dir, int count)
    {
        if (chunkMesh == null) return;
        Transform src = (drums != null && drumIndex < drums.Length && drums[drumIndex] != null)
                        ? drums[drumIndex] : transform;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("PillarChunk");
            go.transform.position = src.position + dir * 2f + Vector3.up * Random.Range(-0.3f, 0.3f);
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * Random.Range(0.6f, 1.3f);
            go.AddComponent<MeshFilter>().sharedMesh = chunkMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = chunkMaterial;
            var fp = go.AddComponent<FallingPiece>();
            Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-2.5f, 2.5f);
            fp.velocity  = dir * Random.Range(3f, 6f) + side + Vector3.up * Random.Range(3f, 6f);
            fp.spinAxis  = Random.onUnitSphere;
            fp.spinSpeed = Random.Range(180f, 420f);
        }
    }

    void Collapse(Vector3 toPlayer)
    {
        foreach (var c in GetComponents<Collider>()) c.enabled = false;
        var data = GetComponent<IslandData>();
        if (data != null) Destroy(data); // ships can sail through the gap now
        if (marker   != null) marker.gameObject.SetActive(false);
        if (glowRing != null) glowRing.gameObject.SetActive(false);

        SoundManager.PlayAt("big_explosion", transform.position, 1f, 0f, 0.8f);
        SoundManager.PlayAt("hit_island", transform.position, 1f);
        GameManager.Instance?.ShakeCamera(0.6f, 0.6f);
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position + Vector3.up * 1.5f + toPlayer * 2f, Quaternion.identity);
        SpawnDebris(0, toPlayer, 6);

        // Topple every remaining piece outward, higher pieces travelling further
        int n = drums != null ? drums.Length : 0;
        for (int i = 0; i < n; i++)
        {
            Transform piece = null;
            if (damagedDrums != null && i < damagedDrums.Length && damagedDrums[i] != null && damagedDrums[i].gameObject.activeSelf)
                piece = damagedDrums[i];
            else if (drums[i] != null && drums[i].gameObject.activeSelf)
                piece = drums[i];
            if (piece != null) Drop(piece, i, splash: true);
        }
        if (capital   != null) Drop(capital, n, splash: true);
        if (baseStone != null)
        {
            baseStone.SetParent(null, true);
            var fp = baseStone.gameObject.AddComponent<FallingPiece>();
            fp.velocity = Vector3.down * 0.5f;
        }

        island?.OnPillarCollapsed(this);
        Destroy(gameObject, 0.1f);
    }

    void Drop(Transform piece, int level, bool splash)
    {
        piece.SetParent(null, true);
        var fp = piece.gameObject.AddComponent<FallingPiece>();
        fp.velocity     = outward * (1.5f + level * 0.9f) + Vector3.up * 0.8f
                        + Vector3.Cross(Vector3.up, outward) * Random.Range(-0.6f, 0.6f);
        fp.spinAxis     = tiltAxis;
        fp.spinSpeed    = 35f + level * 22f;
        fp.delay        = level * 0.07f;
        fp.splashPrefab = splash ? splashPrefab : null;
        fp.playSound    = splash && level % 2 == 0;
    }

    static void SwapMaterial(Renderer r, Material from, Material to)
    {
        if (r == null || from == null || to == null) return;
        var mats = r.sharedMaterials;
        bool changed = false;
        for (int k = 0; k < mats.Length; k++)
            if (mats[k] == from) { mats[k] = to; changed = true; }
        if (changed) r.sharedMaterials = mats;
    }
}
