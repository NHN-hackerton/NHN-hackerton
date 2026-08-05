using UnityEngine;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>강아지 발바닥(패드 + 발가락 4개) 모양 UI 그래픽. 발자국 트레일용.</summary>
    [AddComponentMenu("UI/Paw Icon")]
    public class PawIcon : Image
    {
        [SerializeField, Range(0.05f, 0.4f)] private float padRadiusX = 0.22f;
        [SerializeField, Range(0.05f, 0.4f)] private float padRadiusY = 0.17f;
        [SerializeField, Range(0.03f, 0.2f)] private float toeRadius = 0.085f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float w = r.width, h = r.height;
            Vector2 c = r.center;
            // 패드 (아래, 큰 타원)
            AddEllipse(vh, c + new Vector2(0f, -h * 0.16f), w * padRadiusX, h * padRadiusY);
            // 발가락 4개
            float tr = w * toeRadius;
            AddEllipse(vh, c + new Vector2(-w * 0.26f, h * 0.10f), tr, tr);
            AddEllipse(vh, c + new Vector2(-w * 0.09f, h * 0.28f), tr, tr);
            AddEllipse(vh, c + new Vector2(w * 0.09f, h * 0.28f), tr, tr);
            AddEllipse(vh, c + new Vector2(w * 0.26f, h * 0.10f), tr, tr);
        }

        private void AddEllipse(VertexHelper vh, Vector2 center, float rx, float ry)
        {
            const int seg = 16;
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert; v.color = color; v.uv0 = new Vector2(0.5f, 0.5f);
            v.position = center; vh.AddVert(v);
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                v.position = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                vh.AddVert(v);
            }
            for (int i = 0; i < seg; i++) vh.AddTriangle(start, start + 1 + i, start + 2 + i);
        }
    }
}
