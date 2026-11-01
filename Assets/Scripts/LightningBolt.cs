using System.Collections;
using UnityEngine;

public class LightningBolt : MonoBehaviour
{
    private LineRenderer _lr;
    private Vector3      _source;
    private Vector3      _target;
    private const int    SegmentCount = 8; // source + 6 mid + target

    // Damage settings — only one bolt per strike deals damage (so each flash = 1 hit, not 3)
    private bool    _dealsDamage;
    private Vector3 _damageCenter;
    private float   _damageRadius;
    private float   _damage;

    public void Init(Vector3 source, Vector3 target)
    {
        _source = source;
        _target = target;
        _lr = GetComponent<LineRenderer>();
        _lr.positionCount = SegmentCount;
        _lr.enabled = false;
        StartCoroutine(Flash());
    }

    // Each flash damages the player if they are inside the (former) red circle,
    // even though the marker has already disappeared.
    public void SetDamage(Vector3 center, float radius, float damage)
    {
        _dealsDamage  = true;
        _damageCenter = new Vector3(center.x, 0f, center.z);
        _damageRadius = radius;
        _damage       = damage;
    }

    IEnumerator Flash()
    {
        for (int i = 0; i < 7; i++)
        {
            DrawBolt();
            _lr.enabled = true;
            if (_dealsDamage) TryDamagePlayer();
            yield return new WaitForSecondsRealtime(0.12f);
            _lr.enabled = false;
            yield return new WaitForSecondsRealtime(0.23f);
        }
        Destroy(gameObject);
    }

    void TryDamagePlayer()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) return;

        Vector3 p = playerGO.transform.position;
        float dist = Vector3.Distance(new Vector3(p.x, 0f, p.z), _damageCenter);
        if (dist > _damageRadius) return;

        var ship = playerGO.GetComponent<PlayerShip>()
                   ?? playerGO.GetComponentInParent<PlayerShip>();
        ship?.TakeDamage(_damage);
    }

    void DrawBolt()
    {
        _lr.SetPosition(0, _source);
        _lr.SetPosition(SegmentCount - 1, _target);

        for (int i = 1; i < SegmentCount - 1; i++)
        {
            float t    = (float)i / (SegmentCount - 1);
            // Jitter fades to zero at both endpoints
            float fade = Mathf.Sin(t * Mathf.PI);
            var mid    = Vector3.Lerp(_source, _target, t);
            var perp   = Random.insideUnitCircle * 3.5f * fade;
            mid.x += perp.x;
            mid.z += perp.y;
            _lr.SetPosition(i, mid);
        }
    }
}
