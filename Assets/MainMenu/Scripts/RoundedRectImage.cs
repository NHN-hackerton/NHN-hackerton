using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Rounded Rect Image")]
public class RoundedRectImage : Image
{
protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.height * 0.16f, rect.width * 0.16f);
        Vector2 center = rect.center;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = center;
        vertex.uv0 = new Vector2(0.5f, 0.5f);
        vh.AddVert(vertex);

        Vector2[] cornerCenters =
        {
            new Vector2(rect.xMax - radius, rect.yMin + radius),
            new Vector2(rect.xMax - radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMax - radius),
            new Vector2(rect.xMin + radius, rect.yMin + radius)
        };

        float[] startAngles = { -90f, 0f, 90f, 180f };
        int boundaryCount = 0;

        for (int corner = 0; corner < 4; corner++)
        {
            for (int segment = 0; segment <= 10; segment++)
            {
                float t = segment / 10f;
                float angle = (startAngles[corner] + t * 90f) * Mathf.Deg2Rad;
                Vector2 point = cornerCenters[corner] + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius);

                vertex.position = point;
                vertex.uv0 = new Vector2(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                vh.AddVert(vertex);
                boundaryCount++;
            }
        }

        for (int i = 0; i < boundaryCount; i++)
        {
            int current = i + 1;
            int next = ((i + 1) % boundaryCount) + 1;
            vh.AddTriangle(0, current, next);
        }
    }
}
