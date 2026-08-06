using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 조사 가능한 오브젝트. 은은하게 맥동하는 글로우(하이라이트) + 호버 시 "조사하기" 툴팁 +
    /// 클릭 시 조사 완료 → 단서 획득(ExplorationController에 통지).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Hotspot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("단서")]
        [SerializeField] private string clueName = "단서";           // 토스트/표시용 이름
        [SerializeField] private string keywordId = "";              // 실제 키워드 카드 ID (예: kw_rookie_pride) — 심문 손패로 전달
        [SerializeField] private string tooltipLabel = "조사하기";   // 호버 툴팁 문구 (문은 "심문하기")
        [SerializeField] private bool addsClue = true;               // false면 단서 안 주고 액션만
        [SerializeField] private bool requiresAllClues = false;      // true면(심문 문) 단서 다 모아야 열림
        [SerializeField] private string lockedMessage = "더 조사해보세요";  // 덜 모았을 때 밑에 뜨는 문구
        [SerializeField] private UnityEvent onInvestigated;          // 클릭 시 추가 동작(예: 심문 화면 열기)

        public bool AddsClue { get { return addsClue; } }

        [Header("하이라이트")]
        [SerializeField] private Graphic highlight;      // 자식 글로우
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField] private float minAlpha = 0.22f;
        [SerializeField] private float maxAlpha = 0.55f;
        [SerializeField] private float hoverAlpha = 0.9f;

        private ExplorationController controller;
        private bool investigated = false;
        private bool hovering = false;
        private float t;

        private void Awake()
        {
            controller = GetComponentInParent<ExplorationController>(true);
        }

        // 맵에 (다시) 들어올 때마다 조사 상태를 리셋 → 불빛/발자국 되살아나고 다시 클릭 가능(재도전 대응)
        private void OnEnable()
        {
            investigated = false;
            hovering = false;
            if (controller == null) controller = GetComponentInParent<ExplorationController>(true);   // 놓쳤으면 재확보
        }

        private void Update()
        {
            if (highlight == null) return;
            float a;
            if (investigated) a = 0f;
            else if (hovering) a = hoverAlpha;
            else { t += Time.deltaTime * pulseSpeed; a = Mathf.Lerp(minAlpha, maxAlpha, Mathf.Sin(t) * 0.5f + 0.5f); }
            var c = highlight.color; c.a = a; highlight.color = c;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (investigated) return;
            hovering = true;
            if (controller != null) controller.ShowTooltip(transform, tooltipLabel);
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovering = false;
            if (controller != null) controller.HideTooltip();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (investigated) return;
            if (controller == null) controller = GetComponentInParent<ExplorationController>(true);

            // 심문 문: 단서를 다 모으기 전엔 못 들어감 — 문은 잠긴 채 안내만
            if (requiresAllClues && controller != null && !controller.AllCluesFound)
            {
                controller.HideTooltip();
                controller.ShowMessage(lockedMessage);
                return;   // investigated 세팅 안 함 → 다시 조사하고 재클릭 가능
            }

            investigated = true;
            hovering = false;
            if (controller != null) { controller.HideTooltip(); if (addsClue) controller.AddClue(keywordId, clueName); }
            if (onInvestigated != null) onInvestigated.Invoke();
        }
    }
}
