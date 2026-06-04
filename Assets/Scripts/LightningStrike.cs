using UnityEngine;

public class LightningStrike : MonoBehaviour
{
    [HideInInspector] public GameObject markerPrefab;
    [HideInInspector] public GameObject boltPrefab;
    [HideInInspector] public GameObject effectPrefab;

    private const float Interval      = 15f;
    private const float WarningDelay  =  3f;
    private const float DamageRadius  = 12f;
    private const float StrikeDamage  =  1f;

    // Bottom of the central stationary cloud
    private static readonly Vector3 CloudBase = new Vector3(0f, 17f, 0f);

    private float      _intervalTimer;
    private bool       _striking;
    private float      _strikeTimer;
    private Vector3    _strikePos;
    private GameObject _marker;

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
        {
            if (_marker != null) { Destroy(_marker); _marker = null; }
            return;
        }
        if (GameManager.Instance.PlayingLevel != 4) return;

        if (_striking)
        {
            _strikeTimer += Time.deltaTime;
            if (_strikeTimer >= WarningDelay)
            {
                _striking = false;
                FireLightning();
            }
            return;
        }

        _intervalTimer += Time.deltaTime;
        if (_intervalTimer >= Interval)
        {
            _intervalTimer = 0f;
            BeginWarning();
        }
    }

    void BeginWarning()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) return;

        _strikePos   = new Vector3(playerGO.transform.position.x, 0f, playerGO.transform.position.z);
        _strikeTimer = 0f;
        _striking    = true;

        if (markerPrefab != null)
            _marker = Instantiate(markerPrefab, _strikePos, Quaternion.identity);
    }

    void FireLightning()
    {
        if (_marker != null) { Destroy(_marker); _marker = null; }

        // Spawn 3 bolts from cloud base to strike centre — always visible regardless of player position
        if (boltPrefab != null)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 jitter = i == 0
                    ? Vector3.zero
                    : new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                var boltGO = Instantiate(boltPrefab, Vector3.zero, Quaternion.identity);
                boltGO.GetComponent<LightningBolt>().Init(CloudBase, _strikePos + jitter);
            }
        }

        // Ground particle burst
        if (effectPrefab != null)
            Instantiate(effectPrefab, _strikePos, Quaternion.identity);

        GameManager.Instance?.ShakeCamera(0.4f, 0.35f);

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            float dist = Vector3.Distance(
                new Vector3(playerGO.transform.position.x, 0f, playerGO.transform.position.z),
                _strikePos);
            if (dist <= DamageRadius)
            {
                var ship = playerGO.GetComponent<PlayerShip>()
                           ?? playerGO.GetComponentInParent<PlayerShip>();
                ship?.TakeDamage(StrikeDamage);
            }
        }
    }
}
