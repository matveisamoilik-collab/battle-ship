using UnityEngine;

// Spins the shark ship's radar antenna around its local Y axis.
public class RadarSpin : MonoBehaviour
{
    public float degreesPerSecond = 120f;

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
