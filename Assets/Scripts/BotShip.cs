using UnityEngine;

public class BotShip : MonoBehaviour
{
    public enum AIState { AIM, REPOSITION }

    [Header("Movement")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 60f;

    [Header("Combat")]
    public GameObject torpedoPrefab;
    public Transform torpedoSpawnPoint;

    [Header("HP")]
    public float maxHP = 245f;
    public HealthBar healthBar;

    [Header("Level 3 Visual")]
    public GameObject piratShipModel;

    private const float MinPlayerDist = 18f; // 1.5 hull lengths — bot never crosses this
    private const float FiringArc = 10f;     // fire only when player is within this bow cone

    private float currentHP;
    private AIState state = AIState.AIM;
    private Transform player;
    private float nextFireTime;
    private float postFireDist;
    private Quaternion postFireTarget;
    private IslandData[] islands;

    void Start()
    {
        currentHP = maxHP;
        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);

        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) player = playerGO.transform;

        nextFireTime = Time.time + 2f;
        islands = FindObjectsByType<IslandData>(FindObjectsSortMode.None);

        if (GameManager.Instance != null && GameManager.Instance.PlayingLevel == 3)
            ApplyPiratShipVisual();
    }

    void ApplyPiratShipVisual()
    {
        if (piratShipModel == null) return;

        // Hide original box geometry — keep Hull BoxCollider for torpedo hits
        var hull  = transform.Find("Hull");
        var cabin = transform.Find("Cabin");
        if (hull  != null) { var mr = hull.GetComponent<MeshRenderer>();  if (mr) mr.enabled = false; }
        if (cabin != null) { var mr = cabin.GetComponent<MeshRenderer>(); if (mr) mr.enabled = false; }

        var model = Instantiate(piratShipModel, transform);
        model.name = "PiratShipVisual";
        model.transform.localPosition    = Vector3.zero;
        model.transform.localEulerAngles = Vector3.zero;
        model.transform.localScale       = Vector3.one;

        // Measure world-space AABB
        Bounds wb = new Bounds();
        bool hasMesh = false;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var wc = mf.transform.TransformPoint(
                    b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz)));
                if (!hasMesh) { wb = new Bounds(wc, Vector3.zero); hasMesh = true; }
                else wb.Encapsulate(wc);
            }
        }

        // Scale longest XZ axis to match hull length (12 units)
        float longest = Mathf.Max(wb.size.x, wb.size.z);
        float s = (hasMesh && longest > 0.0001f) ? 12f / longest : 1f;
        model.transform.localScale    = new Vector3(s, s, s);
        model.transform.localPosition = new Vector3(0f, -wb.min.y * s, 0f);

        foreach (var col in model.GetComponentsInChildren<Collider>())
            Destroy(col);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (player == null) return;

        ExecuteState();
        ClampToBounds();
    }

    void ExecuteState()
    {
        switch (state)
        {
            case AIState.AIM:
                // Turn bow onto the player and close in — but never inside MinPlayerDist.
                RotateToward(player.position);
                MoveForwardClamped();
                if (Time.time >= nextFireTime && PlayerInFiringArc())
                {
                    FireTorpedo();
                    BeginReposition();
                }
                break;

            case AIState.REPOSITION:
                // Post-fire: hold the peel-off heading and drive 2 hull lengths forward.
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, postFireTarget, rotationSpeed * Time.deltaTime);
                postFireDist -= MoveForwardClamped();
                if (postFireDist <= 0f) state = AIState.AIM;
                break;
        }
    }

    bool PlayerInFiringArc()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) return false;
        return Vector3.Angle(transform.forward, toPlayer) <= FiringArc;
    }

    void BeginReposition()
    {
        // "Turn a bit": peel off roughly perpendicular to the player (random side, small jitter)
        // so the bot circles the player rather than charging straight in.
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
        float side = Random.value > 0.5f ? 1f : -1f;
        Vector3 tangent = new Vector3(-toPlayer.z, 0f, toPlayer.x) * side;
        Vector3 peelDir = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * tangent;
        postFireTarget = Quaternion.LookRotation(peelDir);
        postFireDist = 24f; // 2 blue hull lengths
        state = AIState.REPOSITION;
    }

    // Steps forward, but never lets the bot cross inside MinPlayerDist of the player —
    // a step that would penetrate is projected onto the 18-unit ring (the bot slides / circles).
    // Returns the distance actually travelled this frame.
    float MoveForwardClamped()
    {
        Vector3 start = transform.position;
        Vector3 desired = start + transform.forward * moveSpeed * Time.deltaTime;
        desired.y = 0f;

        Vector3 p = new Vector3(player.position.x, 0f, player.position.z);
        if (Vector3.Distance(desired, p) < MinPlayerDist)
        {
            Vector3 dir = desired - p;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = start - p;
            desired = p + dir.normalized * MinPlayerDist;
        }

        transform.position = desired;
        return Vector3.Distance(start, transform.position);
    }

    void RotateToward(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot,
            rotationSpeed * Time.deltaTime);
    }

    void ClampToBounds()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -95f, 95f);
        pos.z = Mathf.Clamp(pos.z, -95f, 95f);
        pos.y = 0f;
        transform.position = pos;
        transform.position = PushOutOfIslands(transform.position);
        transform.position = PushOutOfShip(transform.position, player);
    }

    Vector3 PushOutOfShip(Vector3 pos, Transform other)
    {
        if (other == null) return pos;
        const float combinedRadius = 12f; // 6 (self) + 6 (other)
        float dx = pos.x - other.position.x;
        float dz = pos.z - other.position.z;
        float distSq = dx * dx + dz * dz;
        if (distSq < combinedRadius * combinedRadius && distSq > 0.0001f)
        {
            float dist = Mathf.Sqrt(distSq);
            pos.x += dx / dist * (combinedRadius - dist);
            pos.z += dz / dist * (combinedRadius - dist);
        }
        return pos;
    }

    Vector3 PushOutOfIslands(Vector3 pos)
    {
        const float shipRadius = 6f; // hull half-length — prevents front/back penetration
        foreach (var island in islands)
        {
            if (island == null) continue;
            float dx = pos.x - island.transform.position.x;
            float dz = pos.z - island.transform.position.z;
            float distSq = dx * dx + dz * dz;
            float minDist = island.radius + shipRadius;
            if (distSq < minDist * minDist && distSq > 0.0001f)
            {
                float dist = Mathf.Sqrt(distSq);
                pos.x += dx / dist * (minDist - dist);
                pos.z += dz / dist * (minDist - dist);
            }
        }
        return pos;
    }

    void FireTorpedo()
    {
        if (torpedoPrefab == null || torpedoSpawnPoint == null) return;

        // Aim at the player — only reachable when the player is within the bow arc.
        Vector3 aimDir = player.position - torpedoSpawnPoint.position;
        aimDir.y = 0f;
        if (aimDir.sqrMagnitude < 0.01f) return;
        Quaternion aimRot = Quaternion.LookRotation(aimDir.normalized);

        GameObject t = Instantiate(torpedoPrefab, torpedoSpawnPoint.position, aimRot);
        Torpedo torpedo = t.GetComponent<Torpedo>();
        if (torpedo != null) torpedo.isPlayerTorpedo = false;

        nextFireTime = Time.time + Random.Range(1.6f, 2.4f);
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        currentHP = Mathf.Max(0f, currentHP);
        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);
        if (currentHP <= 0f && GameManager.Instance != null)
            GameManager.Instance.BotDefeated();
    }
}
