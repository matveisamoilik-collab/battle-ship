using UnityEngine;

public class CloudDrifter : MonoBehaviour
{
    public float orbitRadius;
    public float orbitSpeed;      // degrees per second; negative = clockwise
    public float orbitAngle;      // current angle in degrees
    public float orbitHeight;
    public float selfRotateSpeed; // degrees per second around own Y axis

    void Update()
    {
        orbitAngle += orbitSpeed * Time.deltaTime;
        float rad = orbitAngle * Mathf.Deg2Rad;
        transform.position = new Vector3(
            Mathf.Sin(rad) * orbitRadius,
            orbitHeight,
            Mathf.Cos(rad) * orbitRadius
        );

        if (selfRotateSpeed != 0f)
            transform.Rotate(0f, selfRotateSpeed * Time.deltaTime, 0f, Space.World);
    }
}
