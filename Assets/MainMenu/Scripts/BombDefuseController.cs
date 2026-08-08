using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDogDetective.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 보스방 폭탄 해제 단말기. 확보한 코드 3자리를 키보드로 직접 쳐서 해제한다.
    /// (화면 키패드를 누르는 방식이었으나, 타자로 치는 긴박함을 살리려고 키보드 입력으로 바꿨다.
    ///  그래서 정답 문자를 키패드에 깔아둘 필요도 없어졌다 — 아무 영문·숫자나 칠 수 있다.)
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
        [Tooltip("한 글자 지우기 (Backspace 키와 같은 일)")]
        [SerializeField] private Button backspaceButton;
        [Tooltip("확인 (Enter 키와 같은 일)")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;

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

        readonly StringBuilder input = new StringBuilder();
        bool defused;
        bool exploded;
        float remaining;
        bool keyboardBound;

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

            BindKeyboard();
            ShowHint();
            UpdateDisplay();
            UpdateTimer();
            if (messageText != null)
                messageText.text = keyboardBound
                    ? "키보드로 코드 세 자리를 입력하세요.\n(Enter 확인 · Backspace 지우기)"
                    : "키보드를 연결해 주세요.";
        }

        private void OnDisable() => UnbindKeyboard();

        private void Update()
        {
            if (!Ticking) return;

            ReadKeyboardKeys();

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

            if (failScreen != null)     // 폭발 엔딩
            {
                gameObject.SetActive(false);
                if (bossRoom != null) bossRoom.SetActive(false);
                failScreen.SetActive(true);
                yield break;
            }

            // 엔딩 화면이 없을 때의 폴백 — 이 화면을 껐다 켜면 안 된다.
            // SetActive(true)가 OnEnable을 다시 불러 exploded=false, remaining=timeLimit으로
            // 초기화되므로, "다시 시작" 버튼 뒤에서 타이머가 다시 돌다 또 터진다.
            // 화면은 켜 둔 채 폭발 상태를 유지하고 버튼만 띄운다.
            if (restartButton != null)
            {
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

        // ── 키보드 입력 ──────────────────────────────────────
        // 이 프로젝트는 Input System(New)만 켜져 있어서(activeInputHandler=1) Input.inputString이
        // 예외를 던진다. 글자는 Keyboard.current.onTextInput으로 받고, 레거시 빌드도 굴러가게 양쪽을 둔다.

        private void BindKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            if (keyboardBound) return;
            if (Keyboard.current == null)
            {
                // 키보드가 없으면 코드를 넣을 방법이 없다. 안내 문구가 이유를 대신 말해 준다.
                Debug.LogWarning("[BombDefuse] 키보드를 찾지 못했습니다 — 코드를 입력할 수 없습니다.");
                return;
            }
            Keyboard.current.onTextInput += OnTextInput;
            keyboardBound = true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            keyboardBound = true;   // 레거시는 Input.inputString을 매 프레임 읽으므로 붙일 게 없다
#endif
        }

        private void UnbindKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            if (!keyboardBound) return;
            // 화면이 꺼진 뒤에도 타자가 들어오면 안 된다. 그 사이 장치가 바뀌었을 수 있어 null 검사.
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
            keyboardBound = false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>키보드가 흘려주는 글자 — 영문·숫자 한 글자만 대문자로 받는다.</summary>
        private void OnTextInput(char c)
        {
            if (!Ticking) return;
            if (c > 127 || !char.IsLetterOrDigit(c)) return;   // 한글·기호·제어문자는 버린다
            AppendTyped(char.ToUpperInvariant(c).ToString());
        }
#endif

        /// <summary>글자가 아닌 키(지우기·확인)는 눌린 프레임에 읽는다.</summary>
        private void ReadKeyboardKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) Backspace();
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) SubmitTyped();
#elif ENABLE_LEGACY_INPUT_MANAGER
            foreach (char c in Input.inputString)
            {
                if (c == '\b') { Backspace(); continue; }
                if (c == '\n' || c == '\r') { SubmitTyped(); continue; }
                if (c > 127 || !char.IsLetterOrDigit(c)) continue;
                AppendTyped(char.ToUpperInvariant(c).ToString());
            }
#endif
        }

        /// <summary>키보드로 한 글자 — 버튼의 UiClickSound 대신 여기서 소리를 낸다.</summary>
        private void AppendTyped(string c)
        {
            int before = input.Length;
            Append(c);
            if (input.Length != before && SfxManager.Instance != null) SfxManager.Instance.PlayClick();
        }

        /// <summary>Enter — 세 자리를 다 채웠을 때만 확인한다 (빈칸이 오답으로 날아가면 억울하다).</summary>
        private void SubmitTyped()
        {
            if (input.Length == RunState.TotalCodeDigits) Submit();
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
            if (bossRoom != null) bossRoom.SetActive(false);   // 보스방을 끄지 않으면 엔딩 뒤에 다시 보인다
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
