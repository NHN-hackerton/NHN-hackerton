using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 테두리(외곽선)만 그리는 UI 그래픽. 문 같은 오브젝트 가장자리를 빛나게 할 때.
/// thickness로 테두리 두께 조절, color 알파를 맥동시키면 은은하게 빛남.
/// </summary>
[AddComponentMenu("UI/Border Glow")]
public class BorderGlow : Image
{
    [SerializeField] private float thickness = 8f;

    public float Thickness { get { return thickness; } set { thickness = Mathf.Max(1f, value); SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float t = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.5f);
        float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;

        AddQuad(vh, new Vector2(x0, y1 - t), new Vector2(x1, y1 - t), new Vector2(x1, y1), new Vector2(x0, y1));       // top
        AddQuad(vh, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y0 + t), new Vector2(x0, y0 + t));       // bottom
        AddQuad(vh, new Vector2(x0, y0 + t), new Vector2(x0 + t, y0 + t), new Vector2(x0 + t, y1 - t), new Vector2(x0, y1 - t)); // left
        AddQuad(vh, new Vector2(x1 - t, y0 + t), new Vector2(x1, y0 + t), new Vector2(x1, y1 - t), new Vector2(x1 - t, y1 - t)); // right
    }

    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert; v.color = color; v.uv0 = new Vector2(0.5f, 0.5f);
        v.position = a; vh.AddVert(v);
        v.position = b; vh.AddVert(v);
        v.position = c; vh.AddVert(v);
        v.position = d; vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
