using TMPro;
using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 대사 길이에 맞춰 세로로 자라는 말풍선.
    ///
    /// 글자를 줄이는 대신 풍선을 키운다 — LLM 대사는 길이가 들쭉날쭉해서
    /// 폰트를 자동 축소하면 대사마다 글자 크기가 달라져 읽기 나쁘다.
    ///
    /// 아래로 자란다. 원래 배경 아트에 그려진 말풍선과 꼬리를 덮어야 하므로
    /// 최소 높이는 그 그림을 가릴 만큼 잡아 둔다(minHeight).
    /// </summary>
    [ExecuteAlways]
    public class SpeechBubble : MonoBehaviour
    {
        [Tooltip("풍선 몸통 (9-slice). 비우면 이 오브젝트의 RectTransform을 쓴다.")]
        [SerializeField] private RectTransform body;
        [Tooltip("몸통 아래에 붙는 꼬리. 몸통이 자라면 따라 내려간다.")]
        [SerializeField] private RectTransform tail;
        [Tooltip("대사 텍스트")]
        [SerializeField] private TMP_Text label;

        [Header("여백")]
        [Tooltip("글자와 풍선 테두리 사이 좌우 여백")]
        [SerializeField] private float padX = 26f;
        [Tooltip("위아래 여백")]
        [SerializeField] private float padY = 18f;

        [Header("높이")]
        [Tooltip("짧은 대사일 때의 높이. 배경에 그려진 말풍선+꼬리를 덮을 만큼은 되어야 한다.")]
        [SerializeField] private float minHeight = 130f;
        [Tooltip("아무리 길어도 이 높이는 넘지 않는다 (초상화를 가리지 않게)")]
        [SerializeField] private float maxHeight = 300f;

        string lastText;
        float lastWidth;

        private void OnEnable() => Refresh();

        private void LateUpdate()
        {
            if (label == null) return;
            // 대사가 바뀌었을 때만 다시 잰다 (타이핑 중에는 text가 그대로라 매 프레임 계산하지 않는다)
            if (label.text == lastText && Mathf.Approximately(Rect.rect.width, lastWidth)) return;
            Refresh();
        }

        RectTransform Rect => body != null ? body : (RectTransform)transform;

        /// <summary>지금 대사에 맞춰 풍선 높이와 꼬리 위치를 맞춘다.</summary>
        public void Refresh()
        {
            if (label == null) return;
            var rt = Rect;
            lastText = label.text;
            lastWidth = rt.rect.width;

            float innerW = Mathf.Max(1f, rt.rect.width - padX * 2f);
            float need = label.GetPreferredValues(label.text, innerW, 0f).y;
            float h = Mathf.Clamp(need + padY * 2f, minHeight, maxHeight);

            // 위쪽 모서리를 고정하고 아래로만 자란다 (풍선 윗변이 화면 위에 붙어 있어 위로는 여유가 없다)
            var size = rt.sizeDelta;
            rt.pivot = new Vector2(rt.pivot.x, 1f);
            rt.sizeDelta = new Vector2(size.x, h);

            // 글자는 여백만큼 안쪽에
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(padX, padY);
            lrt.offsetMax = new Vector2(-padX, -padY);

            // 꼬리는 몸통 아래 가운데에 매단다
            if (tail != null)
            {
                tail.anchorMin = new Vector2(0.5f, 0f);
                tail.anchorMax = new Vector2(0.5f, 0f);
                tail.pivot = new Vector2(0.5f, 1f);
                tail.anchoredPosition = new Vector2(0f, tailOverlap);
            }
        }

        [Tooltip("꼬리를 몸통 쪽으로 겹쳐 올릴 픽셀 (이음매를 가린다)")]
        [SerializeField] private float tailOverlap = 14f;
    }
}
