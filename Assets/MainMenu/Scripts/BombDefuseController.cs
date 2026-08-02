using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDogDetective.Data;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 보스방 폭탄 해제 단말기. 확보한 코드 3자리를 순서대로 입력해 해제한다.
    /// 키패드 버튼은 런타임에 생성하고(문자 = 조직원들에게서 뜯어낸 코드값 + 더미),
    /// 정답은 RunState.VerifyCode가 판정한다.
    /// </summary>
    public class BombDefuseController : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("입력 중인 코드 (예: K7_)")]
        [SerializeField] private TMP_Text displayText;
        [Tooltip("안내·결과 문구")]
        [SerializeField] private TMP_Text messageText;
        [Tooltip("포스트잇의 조합 순서 힌트")]
        [SerializeField] private TMP_Text hintText;

        [Header("입력")]
        [Tooltip("키패드 버튼이 생성될 부모")]
        [SerializeField] private RectTransform keypadContainer;
        [SerializeField] private Button backspaceButton;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_FontAsset font;

        [Header("타임어택")]
        [Tooltip("제한 시간(초). 짧을수록 긴박하다. 인스펙터에서 조절.")]
        [SerializeField] private float timeLimit = 10f;
        [Tooltip("남은 시간 표시 (0:00)")]
        [SerializeField] private TMP_Text timerText;
        [Tooltip("이 시간 이하로 남으면 타이머가 빨갛게 깜빡인다")]
        [SerializeField] private float dangerTime = 5f;

        [Header("해제 후")]
        [Tooltip("해제 성공 시 켤 화면 (탈출 시퀀스 등). 없으면 이 화면만 닫힌다.")]
        [SerializeField] private GameObject nextScreen;
        [Tooltip("닫기(뒤로) 시 돌아갈 화면")]
        [SerializeField] private GameObject bossRoom;
        [Tooltip("시간 초과(폭발) 시 켤 화면. 없으면 '다시 시작' 버튼이 뜬다.")]
        [SerializeField] private GameObject failScreen;

        [Header("다시 시작 (폭발 후)")]
        [Tooltip("폭발 시 중앙에 뜨는 버튼. 평소 숨김.")]
        [SerializeField] private Button restartButton;
        [Tooltip("다시 시작 시 돌아갈 화면 (사건 선택 등)")]
        [SerializeField] private GameObject restartScreen;

        // 키패드에 깔 문자 — 정답 3자리 + 헷갈리게 하는 더미
        static readonly string[] KeyChars =
        { "K", "Q", "7", "3", "M", "9", "B", "4", "X", "2", "R", "8" };

        readonly StringBuilder input = new StringBuilder();
        bool defused;
        bool exploded;
        float remaining;

        /// <summary>타이머가 도는 중인지 (해제·폭발 전).</summary>
        bool Ticking => !defused && !exploded;

        RunState Run => HearingBattleController.CurrentRun;

        private void OnEnable()
        {
            input.Length = 0;
            defused = false;
            exploded = false;
            remaining = timeLimit;   // 화면 열 때마다 리셋 (재도전)

            if (backspaceButton != null)
            {
                backspaceButton.onClick.RemoveListener(Backspace);
                backspaceButton.onClick.AddListener(Backspace);
            }
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(Submit);
                submitButton.onClick.AddListener(Submit);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(Restart);
                restartButton.onClick.AddListener(Restart);
                restartButton.gameObject.SetActive(false);   // 폭발 전엔 숨김
            }

            BuildKeypad();
            ShowHint();
            UpdateDisplay();
            UpdateTimer();
            if (messageText != null) messageText.text = "코드 세 자리를 입력하세요.";
        }

        private void Update()
        {
            if (!Ticking) return;

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                UpdateTimer();
                Explode();
                return;
            }
            UpdateTimer();
        }

        private void UpdateTimer()
        {
            if (timerText == null) return;
            int total = Mathf.CeilToInt(remaining);
            timerText.text = string.Format("{0}:{1:00}", total / 60, total % 60);

            // 위험 구간에서 빨갛게 깜빡임
            if (remaining <= dangerTime)
            {
                bool on = Mathf.Repeat(remaining, 0.6f) > 0.3f;
                timerText.color = on ? new Color(1f, 0.25f, 0.2f) : new Color(0.6f, 0.15f, 0.12f);
            }
            else timerText.color = new Color(1f, 0.72f, 0.3f);
        }

        /// <summary>시간 초과 — 폭탄이 터진다.</summary>
        private void Explode()
        {
            exploded = true;
            if (messageText != null) messageText.text = "시간 초과 — 폭탄이 터졌다…";
            if (submitButton != null) submitButton.interactable = false;
            if (closeButton != null) closeButton.gameObject.SetActive(false);
            StartCoroutine(ExplodedRoutine());
        }

        private IEnumerator ExplodedRoutine()
        {
            yield return new WaitForSecondsRealtime(1.4f);

            gameObject.SetActive(false);
            if (bossRoom != null) bossRoom.SetActive(false);

            if (failScreen != null) failScreen.SetActive(true);   // 폭발 엔딩
            else if (restartButton != null)                        // 엔딩 화면 없을 때만 폴백
            {
                gameObject.SetActive(true);
                var lbl = restartButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = "다시 시작";
                restartButton.gameObject.SetActive(true);
            }
        }

        /// <summary>다시 시작: 런 상태·수집 단서를 비우고 처음 화면으로 돌아간다.</summary>
        public void Restart()
        {
            HearingBattleController.ResetRun();          // 코드·친밀·의심 초기화
            ExplorationController.CollectedClues.Clear(); // 모은 단서 초기화

            if (restartButton != null) restartButton.gameObject.SetActive(false);
            gameObject.SetActive(false);
            if (bossRoom != null) bossRoom.SetActive(false);
            if (restartScreen != null) restartScreen.SetActive(true);
        }

        /// <summary>포스트잇 힌트 = 조합 순서만. 값(정답)은 보여주지 않는다 — 플레이어가 기억해야 한다.</summary>
        private void ShowHint()
        {
            if (hintText == null) return;
            hintText.text = "순서: 신참 → 금고지기 → 측근";
        }

        private void BuildKeypad()
        {
            if (keypadContainer == null) return;
            for (int i = keypadContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(keypadContainer.GetChild(i).gameObject);

            var grid = keypadContainer.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = keypadContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.cellSize = new Vector2(96f, 96f);

            foreach (var ch in KeyChars)
            {
                string c = ch;
                MakeKey(c).onClick.AddListener(() => Append(c));
            }
        }

        private Button MakeKey(string label)
        {
            var go = new GameObject("Key_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(keypadContainer, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.16f, 0.11f, 0.06f, 0.95f);

            var textGO = new GameObject("Label", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var rt = textGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            if (font != null) tmp.font = font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10; tmp.fontSizeMax = 44;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.98f, 0.86f, 0.55f);

            return go.GetComponent<Button>();
        }

        private void Append(string c)
        {
            if (!Ticking) return;
            if (input.Length >= RunState.TotalCodeDigits) return;
            input.Append(c);
            UpdateDisplay();
        }

        private void Backspace()
        {
            if (!Ticking || input.Length == 0) return;
            input.Length -= 1;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (displayText == null) return;
            var sb = new StringBuilder();
            for (int i = 0; i < RunState.TotalCodeDigits; i++)
                sb.Append(i < input.Length ? input[i].ToString() : "_").Append(' ');
            displayText.text = sb.ToString().TrimEnd();

            if (submitButton != null)
                submitButton.interactable = Ticking && input.Length == RunState.TotalCodeDigits;
        }

        private void Submit()
        {
            if (!Ticking || Run == null) return;

            if (!Run.HasAllCodes)
            {
                if (messageText != null) messageText.text = "아직 코드를 다 알아내지 못했다.";
                return;
            }

            if (Run.VerifyCode(input.ToString()))
            {
                defused = true;
                Run.MarkBombDefused();
                if (messageText != null) messageText.text = "해제 성공 — 폭탄이 멈췄다!";
                if (submitButton != null) submitButton.interactable = false;
                if (closeButton != null) closeButton.gameObject.SetActive(false);   // 해제 후엔 돌아갈 곳 없음
                StartCoroutine(DefusedRoutine());
            }
            else
            {
                if (messageText != null) messageText.text = "틀렸다. 다시 확인해라.";
                input.Length = 0;
                UpdateDisplay();
            }
        }

        private IEnumerator DefusedRoutine()
        {
            yield return new WaitForSecondsRealtime(1.6f);
            gameObject.SetActive(false);
            if (nextScreen != null) nextScreen.SetActive(true);
        }

        /// <summary>닫기 — 보스방으로 돌아간다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
            if (bossRoom != null) bossRoom.SetActive(true);
        }
    }
}
