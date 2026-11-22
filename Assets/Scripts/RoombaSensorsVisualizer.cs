using UnityEngine;

/// <summary>
/// Visualizes the green raycast sensors and detection circles around the Roomba robot,
/// identical to the visual in the reference picture.
/// Draws in both Scene View (Gizmos) and Game View (GL lines & rings).
/// </summary>
public class RoombaSensorsVisualizer : MonoBehaviour
{
    [Header("Sensor Configuration")]
    public int rayCount = 14;
    public float maxRayDistance = 4.5f;
    public float rayHeight = 0.08f;
    public LayerMask detectionLayers;

    [Header("Visual Styling")]
    public Color sensorColor = new Color(0.2f, 0.95f, 0.35f, 0.9f);
    public float hitCircleRadius = 0.12f;

    private Material lineMaterial;

    void Awake()
    {
        // Simple unlit material for runtime Game View GL drawing
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader != null)
        {
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    void OnRenderObject()
    {
        if (lineMaterial == null) return;

        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(sensorColor);

        Vector3 origin = transform.position + Vector3.up * rayHeight;
        float angleStep = 360f / rayCount;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * angleStep;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            Vector3 targetPoint;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRayDistance, detectionLayers))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = origin + direction * maxRayDistance;
            }

            // Ray line
            GL.Vertex(origin);
            GL.Vertex(targetPoint);

            // Ring at endpoint
            DrawGLRing(targetPoint, hitCircleRadius, 12);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void DrawGLRing(Vector3 center, float radius, int segments)
    {
        float anglePerSeg = 360f / segments;
        for (int s = 0; s < segments; s++)
        {
            float a1 = s * anglePerSeg * Mathf.Deg2Rad;
            float a2 = (s + 1) * anglePerSeg * Mathf.Deg2Rad;

            Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0.01f, Mathf.Sin(a1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0.01f, Mathf.Sin(a2) * radius);

            GL.Vertex(p1);
            GL.Vertex(p2);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = sensorColor;
        Vector3 origin = transform.position + Vector3.up * rayHeight;
        float angleStep = 360f / rayCount;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * angleStep;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxRayDistance, detectionLayers))
            {
                Gizmos.DrawLine(origin, hit.point);
                Gizmos.DrawWireSphere(hit.point, hitCircleRadius);
            }
            else
            {
                Vector3 end = origin + direction * maxRayDistance;
                Gizmos.DrawLine(origin, end);
                Gizmos.DrawWireSphere(end, hitCircleRadius * 0.7f);
            }
        }
    }

    void OnDestroy()
    {
        if (lineMaterial != null) DestroyImmediate(lineMaterial);
    }
}
