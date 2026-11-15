using UnityEngine;

// Small shared helpers for the Level 7 encounter.
public static class KrakenUtil
{
    // Player hull footprint (Hull cube 5 x 12 in BuildShipGO)
    public const float HullHalfWidth  = 2.5f;
    public const float HullHalfLength = 6f;

    // Distance (XZ) from a world point to the ship's hull rectangle; 0 when the point is on the hull
    public static float HullDistance(Transform ship, Vector3 point)
    {
        Vector3 local = ship.InverseTransformPoint(new Vector3(point.x, ship.position.y, point.z));
        float dx = Mathf.Max(Mathf.Abs(local.x) - HullHalfWidth, 0f);
        float dz = Mathf.Max(Mathf.Abs(local.z) - HullHalfLength, 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    public static void DrawCircle(Vector3 centre, float radius, Color color, int segments = 64)
    {
        Gizmos.color = color;
        Vector3 prev = centre + new Vector3(0f, 0f, radius);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 p = centre + new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
