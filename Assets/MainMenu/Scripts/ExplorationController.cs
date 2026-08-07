using UnityEngine;
using TMPro;
using System.Collections;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 은신처 탐색 총괄: 단서 카운터 + "〈단서〉 단서에 추가됨" 토스트 + "조사하기" 툴팁 관리.
    /// Chapter1Map 루트에 붙이고, 자식 Hotspot들이 자동 집계된다.
    /// </summary>
    public class ExplorationController : MonoBehaviour
    {
        [Header("카운터 (HUD 안내문)")]
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private string counterFormat = "단서를 수집하세요 ({0}/{1})";

        [Header("토스트 (좌하단)")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float toastDuration = 2f;

        [Header("조사하기 툴팁 (공용)")]
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, 46f);

        private int found = 0;
        private int total = 0;
        private Coroutine toastCo;

        /// <summary>탐색으로 획득한 단서(키워드 카드) 목록. 심문 화면 손패가 이걸 읽는다.</summary>
        public static readonly System.Collections.Generic.List<string> CollectedClues = new System.Collections.Generic.List<string>();

        // 맵에 들어올 때마다 초기화한다. Start로 두면 한 번만 돌아서,
        // 재도전으로 맵에 다시 들어왔을 때 Hotspot은 조사 상태가 풀리는데(각자 OnEnable)
        // 카운터와 단서 목록은 그대로 남아 단서가 두 번씩 쌓인다. (8/4, 카드 중복)
        private void OnEnable()
        {
            CollectedClues.Clear();
            found = 0;
            total = 0;
            foreach (var h in GetComponentsInChildren<Hotspot>(true))
                if (h.AddsClue) total++;   // 단서 주는 핫스팟만 집계 (문 등 제외)
            UpdateCounter();
            if (toastRoot != null) toastRoot.SetActive(false);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }

        private void UpdateCounter()
        {
            if (counterText != null) counterText.text = string.Format(counterFormat, found, total);
        }

        /// <summary>단서를 다 모았는지 (문 입장 게이트용). 단서가 없는 맵은 항상 true.</summary>
        public bool AllCluesFound => total <= 0 || found >= total;

        /// <summary>Hotspot이 조사됐을 때 호출. keywordId는 심문 손패로, displayName은 토스트 표시용.</summary>
        public void AddClue(string keywordId, string displayName)
        {
            found = Mathf.Min(found + 1, Mathf.Max(total, 1));   // 카운터가 total을 넘지 않게
            if (!string.IsNullOrEmpty(keywordId) && !CollectedClues.Contains(keywordId))
                CollectedClues.Add(keywordId);                    // 같은 단서를 두 번 담지 않는다
            UpdateCounter();
            ShowMessage("〈" + displayName + "〉 단서에 추가됨");
        }

        /// <summary>좌하단 토스트에 임의 메시지 표시 (예: "더 조사해보세요"). 카운터는 안 건드린다.</summary>
        public void ShowMessage(string msg)
        {
            if (toastText != null) toastText.text = msg;
            if (toastRoot != null)
            {
                if (toastCo != null) StopCoroutine(toastCo);
                toastCo = StartCoroutine(ToastRoutine());
            }
        }

        private IEnumerator ToastRoutine()
        {
            toastRoot.SetActive(true);
            yield return new WaitForSecondsRealtime(toastDuration);
            toastRoot.SetActive(false);
        }

        public void ShowTooltip(Transform at, string label)
        {
            if (tooltip == null) return;
            var tmp = tooltip.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = label;
            tooltip.gameObject.SetActive(true);
            tooltip.position = at.position + new Vector3(tooltipOffset.x, tooltipOffset.y, 0f);
        }

        public void HideTooltip()
        {
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }
    }
}
