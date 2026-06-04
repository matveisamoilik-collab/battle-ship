using System.Collections;
using UnityEngine;

public class LightningBolt : MonoBehaviour
{
    private LineRenderer _lr;
    private Vector3      _source;
    private Vector3      _target;
    private const int    SegmentCount = 8; // source + 6 mid + target

    public void Init(Vector3 source, Vector3 target)
    {
        _source = source;
        _target = target;
        _lr = GetComponent<LineRenderer>();
        _lr.positionCount = SegmentCount;
        _lr.enabled = false;
        StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        for (int i = 0; i < 6; i++)
        {
            DrawBolt();
            _lr.enabled = true;
            yield return new WaitForSecondsRealtime(0.10f);
            _lr.enabled = false;
            yield return new WaitForSecondsRealtime(0.23f);
        }
        Destroy(gameObject);
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
