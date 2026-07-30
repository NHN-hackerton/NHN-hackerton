using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화살표(→) 아이콘. 샤프트(막대) + 삼각 머리. RectTransform 회전으로 방향 조절.
/// 문 "들어가기/심문하기" 표시용.
/// </summary>
[AddComponentMenu("UI/Arrow Icon")]
public class ArrowIcon : Image
{
    [SerializeField, Range(0.1f, 0.6f)] private float shaftThickness = 0.32f; // 높이 대비 막대 두께
    [SerializeField, Range(0.3f, 0.8f)] private float headWidth = 0.45f;       // 너비 대비 머리 크기

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float cy = r.center.y;
        float th = r.height * shaftThickness;
        float headX = r.xMax - r.width * headWidth;

        AddQuad(vh,
            new Vector2(r.xMin, cy - th / 2f),
            new Vector2(headX, cy - th / 2f),
            new Vector2(headX, cy + th / 2f),
            new Vector2(r.xMin, cy + th / 2f));

        AddTri(vh,
            new Vector2(headX, r.yMin + r.height * 0.06f),
            new Vector2(r.xMax, cy),
            new Vector2(headX, r.yMax - r.height * 0.06f));
    }

    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert; v.color = color; v.uv0 = new Vector2(0.5f, 0.5f);
        v.position = a; vh.AddVert(v); v.position = b; vh.AddVert(v);
        v.position = c; vh.AddVert(v); v.position = d; vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }

    private void AddTri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
    {
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert; v.color = color; v.uv0 = new Vector2(0.5f, 0.5f);
        v.position = a; vh.AddVert(v); v.position = b; vh.AddVert(v); v.position = c; vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
    }
}
