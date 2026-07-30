using UnityEngine;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 별/반짝임(빛) 모양을 그리는 UI 그래픽.
    /// points=4면 반짝이는 빛(sparkle), 5면 일반 별. innerRatio로 뾰족함 조절.
    /// </summary>
    [AddComponentMenu("UI/Star Image")]
    public class StarImage : Image
    {
        [SerializeField] private int points = 4;
        [SerializeField, Range(0.05f, 0.9f)] private float innerRatio = 0.38f;

        public int Points { get { return points; } set { points = Mathf.Max(3, value); SetVerticesDirty(); } }
        public float InnerRatio { get { return innerRatio; } set { innerRatio = Mathf.Clamp(value, 0.05f, 0.9f); SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = outer * innerRatio;

            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.position = center; v.uv0 = new Vector2(0.5f, 0.5f);
            vh.AddVert(v);

            int p = Mathf.Max(3, points);
            int n = p * 2;
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.PI / 2f + (Mathf.PI * 2f * i) / n; // 위쪽부터 시작
                float r = (i % 2 == 0) ? outer : inner;
                Vector2 pt = center + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                v.position = pt;
                v.uv0 = new Vector2(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, pt.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, pt.y));
                vh.AddVert(v);
            }
            for (int i = 0; i < n; i++)
                vh.AddTriangle(0, i + 1, (i + 1) % n + 1);
        }
    }
}
