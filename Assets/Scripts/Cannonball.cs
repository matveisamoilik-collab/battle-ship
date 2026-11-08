using UnityEngine;

// Level 6: ballistic cannonball fired by a CastleCannon. Flies under gravity (same model as
// VolcanoStone) and resolves on reaching the sea: a hit if it lands on the player's hull,
// otherwise a splash.
// Bomb mode (after the player has destroyed 2 pillars): bigger glowing ball, a red warning
// disc marks the landing spot, and the impact explodes with area damage — BombDamage to the
// player if their ship centre is within BombRadius (half the lightning radius, +15.5 %, +6.3 %, +7.1 %, +0.5 %) or the
// bomb lands on the hull.
public class Cannonball : MonoBehaviour
{
    [HideInInspector] public GameObject splashPrefab;
    [HideInInspector] public GameObject explosionPrefab;
    [HideInInspector] public GameObject bombMarkerPrefab;   // LightningMarker disc (radius 12 at scale 24)
    [HideInInspector] public Material   bombMaterial;

    public const float Gravity    = 14f;
    public const float BombRadius = 7.93f;   // half the 12-unit lightning radius (6) + 15.5 %, + 6.3 %, + 7.1 %, + 0.5 %
    private const float Damage     = 1f;
    private const float BombDamage = 2f;
    private const float BombScale  = 1.7f;   // relative to the plain cannonball
    private const float HullHalfWidth  = 3.0f;  // generous vs the 5-unit-wide hull
    private const float HullHalfLength = 6.5f;
    private const float MaxLifetime    = 8f;

    private Vector3    velocity;
    private float      age;
    private bool       isBomb;
    private GameObject marker;

    public bool IsBomb => isBomb;

    public void Launch(Vector3 initialVelocity) => Launch(initialVelocity, false, Vector3.zero);

    public void Launch(Vector3 initialVelocity, bool bomb, Vector3 target)
    {
        velocity = initialVelocity;
        isBomb   = bomb;
        if (!bomb) return;

        transform.localScale *= BombScale;
        var mr = GetComponent<MeshRenderer>();
        if (mr != null && bombMaterial != null) mr.sharedMaterial = bombMaterial;

        if (bombMarkerPrefab != null)
        {
            marker = Instantiate(bombMarkerPrefab, new Vector3(target.x, 0.06f, target.z), Quaternion.identity);
            // Prefab scale 24 = radius 12; scale the disc to the bomb radius
            float d = BombRadius * 2f;
            marker.transform.localScale = new Vector3(d, marker.transform.localScale.y, d);
        }
    }

    void Update()
    {
        velocity.y -= Gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        age += Time.deltaTime;
        if (transform.position.y <= 0.3f || age > MaxLifetime)
            Impact();
    }

    void OnDestroy()
    {
        if (marker != null) Destroy(marker);
    }

    void Impact()
    {
        Vector3 pos = new Vector3(transform.position.x, 0f, transform.position.z);
        bool hit = false;

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null && GameManager.Instance != null && !GameManager.Instance.IsGameOver)
        {
            Vector3 local = playerGO.transform.InverseTransformPoint(pos);
            bool onHull = Mathf.Abs(local.x) <= HullHalfWidth && Mathf.Abs(local.z) <= HullHalfLength;
            Vector3 pp = playerGO.transform.position; pp.y = 0f;
            bool inBlast = isBomb && (pp - pos).sqrMagnitude <= BombRadius * BombRadius;
            if (onHull || inBlast)
            {
                var ship = playerGO.GetComponent<PlayerShip>();
                if (ship != null)
                {
                    ship.TakeDamage(isBomb ? BombDamage : Damage);
                    hit = true;
                }
            }
        }

        if (isBomb)
        {
            // Explodes whether it hits or not — the blast is the danger zone
            if (explosionPrefab != null)
            {
                var e = Instantiate(explosionPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
                e.transform.localScale = Vector3.one * 2.5f;
            }
            if (splashPrefab != null)
            {
                var s = Instantiate(splashPrefab, pos, Quaternion.identity);
                s.transform.localScale = Vector3.one * 2f;
            }
            SoundManager.PlayAt("big_explosion", pos, 0.8f, 0.1f, 1.1f);
            if (hit) SoundManager.PlayAt("hit_ship", pos, 1f);
            GameManager.Instance?.ShakeCamera(hit ? 0.4f : 0.25f, hit ? 0.5f : 0.25f);
        }
        else if (hit)
        {
            if (explosionPrefab != null) Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            SoundManager.PlayAt("hit_ship", pos, 1f);
            GameManager.Instance?.ShakeCamera(0.3f, 0.35f);
        }
        else
        {
            if (splashPrefab != null) Instantiate(splashPrefab, pos, Quaternion.identity);
            SoundManager.PlayAt("splash", pos, 0.7f, 0.1f, 1.2f);
        }
        Destroy(gameObject);
    }
}
