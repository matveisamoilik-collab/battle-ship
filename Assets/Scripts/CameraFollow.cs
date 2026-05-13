using System.Collections;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float distance = 15f;
    public float height = 8f;
    public float smoothSpeed = 5f;

    private Vector3 shakeOffset;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position
                          - target.forward * distance
                          + Vector3.up * height;

        transform.position = Vector3.Lerp(
            transform.position,
            desired,
            smoothSpeed * Time.deltaTime);
        transform.position += shakeOffset;

        transform.LookAt(target.position + Vector3.up * 1f);
    }

    // Uses unscaledDeltaTime so shake still runs when Time.timeScale == 0
    public IEnumerator Shake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 offset = Random.insideUnitSphere * magnitude;
            offset.y = 0f;
            shakeOffset = offset;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }
}
