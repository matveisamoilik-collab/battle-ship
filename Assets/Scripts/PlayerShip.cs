using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShip : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 15f;
    public float rotationSpeed = 90f;

    [Header("Combat")]
    public GameObject torpedoPrefab;
    public Transform torpedoSpawnPoint;
    public float fireInterval = 2f;

    [Header("HP")]
    public float maxHP = 245f;
    public HealthBar healthBar;

    private float currentHP;
    private float lastFireTime;
    private IslandData[] islands;
    private Transform enemy;
    private float lastShakeTime = -999f;

    void Start()
    {
        currentHP = maxHP;
        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);
        islands = FindObjectsByType<IslandData>(FindObjectsSortMode.None);
        var enemyGO = GameObject.FindWithTag("Enemy");
        if (enemyGO != null) enemy = enemyGO.transform;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        HandleMovement();
        HandleFiring();
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float forward = 0f, turn = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) forward += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) forward -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) turn -= 1f;

        transform.Rotate(Vector3.up, turn * rotationSpeed * Time.deltaTime);
        transform.position += transform.forward * forward * moveSpeed * Time.deltaTime;

        // Keep ship inside arena bounds and flat on water
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -95f, 95f);
        pos.z = Mathf.Clamp(pos.z, -95f, 95f);
        pos.y = 0f;
        transform.position = pos;
        transform.position = PushOutOfIslands(transform.position);
        transform.position = PushOutOfShip(transform.position, enemy);
    }

    void TryShake()
    {
        if (Time.time - lastShakeTime < 0.4f) return;
        lastShakeTime = Time.time;
        GameManager.Instance?.ShakeCamera(0.35f, 1.5f);
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
            TryShake();
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
                TryShake();
            }
        }
        return pos;
    }

    void HandleFiring()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        bool firePressed = (kb != null && kb.spaceKey.isPressed)
                        || (mouse != null && mouse.leftButton.isPressed);

        if (firePressed && Time.time >= lastFireTime + fireInterval)
        {
            FireTorpedo();
            lastFireTime = Time.time;
        }
    }

    void FireTorpedo()
    {
        if (torpedoPrefab == null || torpedoSpawnPoint == null) return;
        GameObject t = Instantiate(torpedoPrefab,
                                   torpedoSpawnPoint.position,
                                   torpedoSpawnPoint.rotation);
        Torpedo torpedo = t.GetComponent<Torpedo>();
        if (torpedo != null) torpedo.isPlayerTorpedo = true;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        currentHP = Mathf.Max(0f, currentHP);
        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);
        if (currentHP <= 0f && GameManager.Instance != null)
            GameManager.Instance.PlayerDefeated();
    }
}
