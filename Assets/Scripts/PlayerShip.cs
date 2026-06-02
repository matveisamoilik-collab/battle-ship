using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShip : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 20f;
    public float rotationSpeed = 90f;

    [Header("Combat")]
    public GameObject torpedoPrefab;
    public Transform torpedoSpawnPoint;
    public float fireInterval = 2f;
    public GameObject piratShipModel;

    [Header("HP")]
    public float maxHP = 7f;
    public HealthBar healthBar;

    [Header("Mobile Controls")]
    public VirtualJoystick virtualJoystick;
    public FireZone fireZone;

    private float currentHP;
    private float torpedoDamage;
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
        ApplySelectedShip();
    }

    void ApplySelectedShip()
    {
        string selected = PlayerPrefs.GetString("SelectedShip", "blue");
        bool yellow     = selected == "yellow";
        bool yellowRed  = selected == "yellowred";
        bool pirat      = selected == "pirat";

        ShipStats stats = pirat     ? ShipData.Pirate
                        : yellowRed ? ShipData.YellowRed
                        : yellow    ? ShipData.Yellow
                                    : ShipData.Blue;

        moveSpeed     = stats.speed;
        maxHP         = stats.hp;
        currentHP     = maxHP;
        fireInterval  = stats.fireDelay;
        torpedoDamage = stats.torpedoDamage;

        if (pirat)
        {
            ApplyPiratShipVisual();
        }
        else
        {
            Color hullColor  = yellow    ? new Color(1.0f, 0.85f, 0.0f)
                             : yellowRed ? new Color(1.0f, 0.85f, 0.0f)
                                         : new Color(0.30f, 0.40f, 0.70f);
            Color cabinColor = yellowRed ? new Color(0.85f, 0.10f, 0.10f)
                             : yellow    ? new Color(1.0f, 0.85f, 0.0f)
                                         : new Color(0.30f, 0.40f, 0.70f);
            var hull  = transform.Find("Hull")?.GetComponent<MeshRenderer>();
            var cabin = transform.Find("Cabin")?.GetComponent<MeshRenderer>();
            if (hull  != null) hull.material.SetColor("_BaseColor", hullColor);
            if (cabin != null) cabin.material.SetColor("_BaseColor", cabinColor);
        }
        if (healthBar != null) healthBar.SetHealth(currentHP, maxHP);
    }

    void ApplyPiratShipVisual()
    {
        if (piratShipModel == null) return;

        var hull  = transform.Find("Hull");
        var cabin = transform.Find("Cabin");
        if (hull  != null) { var mr = hull.GetComponent<MeshRenderer>();  if (mr) mr.enabled = false; }
        if (cabin != null) { var mr = cabin.GetComponent<MeshRenderer>(); if (mr) mr.enabled = false; }

        var model = Instantiate(piratShipModel, transform);
        model.name = "PiratShipVisual";
        model.transform.localPosition    = Vector3.zero;
        model.transform.localEulerAngles = new Vector3(-90f, 270f, 0f);
        model.transform.localScale       = Vector3.one;

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

        HandleMovement();
        HandleFiring();
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;

        float forward = 0f, turn = 0f;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   forward += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) forward -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn   += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  turn   -= 1f;
        }
        if (virtualJoystick != null)
        {
            forward = Mathf.Clamp(forward + virtualJoystick.Direction.y, -1f, 1f);
            turn    = Mathf.Clamp(turn    + virtualJoystick.Direction.x, -1f, 1f);
        }

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
                        || (mouse != null && mouse.leftButton.isPressed)
                        || (fireZone != null && fireZone.IsPressed);

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
        if (torpedo != null) { torpedo.isPlayerTorpedo = true; torpedo.damage = torpedoDamage; }
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
