using UnityEngine;

public class BotShip : MonoBehaviour
{
    public enum AIState { APPROACH, FLANK, RETREAT }

    [Header("Movement")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 60f;

    [Header("Combat")]
    public GameObject torpedoPrefab;
    public Transform torpedoSpawnPoint;

    [Header("HP")]
    public float maxHP = 245f;
    public HealthBar healthBar;

    private float currentHP;
    private AIState state = AIState.APPROACH;
    private Transform player;
    private float nextFireTime;
    private float retreatTimer;
    private float flankAngle;
    private IslandData[] islands;

    void Start()
    {
        currentHP = maxHP;
        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);

        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) player = playerGO.transform;

        nextFireTime = Time.time + 2f;
        islands = FindObjectsByType<IslandData>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (player == null) return;

        UpdateState();
        ExecuteState();
        ClampToBounds();
        HandleFire();
    }

    void UpdateState()
    {
        float dist = Vector3.Distance(transform.position, player.position);
        float hpPercent = currentHP / maxHP;

        // RETREAT overrides everything; count down then return to APPROACH
        if (state == AIState.RETREAT)
        {
            retreatTimer -= Time.deltaTime;
            if (retreatTimer <= 0f) state = AIState.APPROACH;
            return;
        }

        if (hpPercent < 0.3f)
        {
            state = AIState.RETREAT;
            retreatTimer = 3f;
            return;
        }

        if (state == AIState.APPROACH && dist < 45f)
            state = AIState.FLANK;
        else if (state == AIState.FLANK && dist > 55f)
            state = AIState.APPROACH;
    }

    void ExecuteState()
    {
        switch (state)
        {
            case AIState.APPROACH:
                RotateToward(player.position);
                MoveForward();
                break;

            case AIState.FLANK:
                // Orbit at 40-unit radius around the player
                flankAngle += 45f * Time.deltaTime;
                Vector3 orbitOffset = new Vector3(
                    Mathf.Sin(flankAngle * Mathf.Deg2Rad),
                    0f,
                    Mathf.Cos(flankAngle * Mathf.Deg2Rad)) * 40f;
                RotateToward(player.position + orbitOffset);
                MoveForward();
                // Keep bow pointed at player regardless of travel direction
                RotateToward(player.position);
                break;

            case AIState.RETREAT:
                Vector3 awayDir = (transform.position - player.position).normalized;
                RotateToward(transform.position + awayDir * 10f);
                MoveForward();
                break;
        }
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

    void MoveForward()
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
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

    void HandleFire()
    {
        if (Time.time < nextFireTime) return;
        if (torpedoPrefab == null || torpedoSpawnPoint == null) return;

        // Aim directly at player position at fire time
        Vector3 aimDir = (player.position - torpedoSpawnPoint.position);
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
