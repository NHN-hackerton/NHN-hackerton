using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDogDetective.Data;
using TopDogDetective.Judge;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 심문(함구령 뚫기) 전투 진행. 백엔드 파이프라인(BattleSession/IDialogueJudge)을
    /// 감싸 UI와 잇는다. 한 턴 = CanSubmit → Judge → ApplyResult 순.
    ///
    /// 이 컨트롤러는 "제출문(PlayerUtterance)을 받아 판정을 돌리고 결과로 화면을 갱신"만
    /// 책임진다. 카드 선택·조립 UI는 나중에 이 Submit()에 물린다.
    /// </summary>
    public class HearingBattleController : MonoBehaviour
    {
        [Header("전투 대상")]
        [SerializeField] private string enemyId = "member_a_rookie";
        [Tooltip("탐색 연동 전까지 임시로 지급할 보유 키워드 (Mock 루프 검증용)")]
        [SerializeField] private string[] seedKeywords = { "kw_rookie_pride", "kw_code_digit" };

        [Header("연동")]
        [Tooltip("표정 전환용. 없으면 표정은 생략된다.")]
        [SerializeField] private HearingController hearing;

        [Header("HUD 텍스트 (없으면 생략)")]
        [SerializeField] private TMP_Text suspicionText;
        [SerializeField] private TMP_Text affinityText;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text focusText;
        [SerializeField] private TMP_Text codeText;
        [SerializeField] private TMP_Text replyText;
        [SerializeField] private TMP_Text outcomeText;

        [Header("게이지 바 (fillAmount 0~1, 없으면 생략)")]
        [SerializeField] private Image suspicionFill;
        [SerializeField] private Image affinityFill;

        [Header("결과 → 다음 (심문 종료 시)")]
        [Tooltip("결과 표시 중 배경을 덮는 어두운 판. 결과 문구/버튼만 도드라지게 한다.")]
        [SerializeField] private GameObject resultOverlay;
        [Tooltip("문구를 띄울 때 배경을 흐리게 만드는 판.")]
        [SerializeField] private ScreenBlurOverlay blurOverlay;
        [Tooltip("중앙 결과 버튼. 평소 숨김, 종료 시 표시.")]
        [SerializeField] private Button resultButton;
        [Tooltip("실패 시 돌아갈 탐색 맵 (Chapter1Map)")]
        [SerializeField] private GameObject searchMap;
        [Tooltip("성공 시 갈 다음 챕터 맵 (Chapter2Map)")]
        [SerializeField] private GameObject nextChapter;

        private bool won;   // 코드 확보 성공 처리 완료 (중복 방지)

        /// <summary>런(챕터 1회 플레이) 전체가 공유하는 상태. 라운드를 넘겨 코드·친밀·의심이 누적된다.
        /// (심문마다 새로 만들면 확보한 코드가 사라져 보스방 해제가 불가능해진다)</summary>
        public static RunState CurrentRun { get; private set; }

        /// <summary>새 런 시작 (챕터 처음부터). 탐색 단서와 함께 초기화된다.</summary>
        public static void ResetRun() => CurrentRun = null;

        private EnemyData enemy;
        private RunState run;
        private BattleSession session;
        private IDialogueJudge judge;
        private bool busy;   // 판정 대기 중 중복 제출 방지

        public BattleSession Session => session;
        public bool IsBusy => busy;
        public bool IsFinished => session == null || session.IsFinished;

        /// <summary>세션 시작·턴 판정 완료 시 발생. 카드 UI가 손패를 갱신하는 신호.</summary>
        public event System.Action OnStateChanged;

        private void OnEnable()
        {
            if (resultButton != null)
            {
                resultButton.onClick.RemoveListener(OnResultClicked);
                resultButton.onClick.AddListener(OnResultClicked);
            }
            StartBattle();
        }

        /// <summary>세션을 새로 만들고 화면을 초기화한다.</summary>
        public void StartBattle()
        {
            enemy = EnemyDataLoader.Load(enemyId);
            if (enemy == null)
            {
                Debug.LogError($"[HearingBattle] 조직원 '{enemyId}' 로드 실패 — 전투를 시작할 수 없습니다.");
                return;
            }

            // 런 상태는 라운드를 넘겨 유지한다 (확보한 코드·친밀·의심 누적)
            if (CurrentRun == null) CurrentRun = new RunState();
            run = CurrentRun;

            // 의심도는 조직원별로 0에서 시작한다.
            // (런에 누적하면 앞 라운드 잔고 때문에 3라운드가 시작부터 발각권에 들어간다.
            //  의심도는 '그 조직원이 나를 얼마나 의심하는가'이므로 상대가 바뀌면 리셋이 맞다)
            if (run.Suspicion != 0)
            {
                Debug.Log($"[HearingBattle] {enemy.displayName} 심문 시작 — 의심도 {run.Suspicion} → 0");
                run.SetSuspicion(0);
            }

            var collected = ExplorationController.CollectedClues;
            if (collected != null && collected.Count > 0)
                foreach (var kw in collected) run.AcquireKeyword(kw);   // 탐색에서 모은 카드
            else
                foreach (var kw in seedKeywords) run.AcquireKeyword(kw); // 탐색 안 거쳤을 때 테스트 폴백

            session = new BattleSession(enemy, run);
            judge   = new MockDialogueJudge();
            busy    = false;
            won     = false;

            if (outcomeText != null) outcomeText.text = "";
            if (replyText != null)   replyText.text = $"{enemy.displayName}와의 심문을 시작한다.";
            if (resultButton != null) resultButton.gameObject.SetActive(false);
            if (resultOverlay != null) resultOverlay.SetActive(false);
            RefreshHud();
            OnStateChanged?.Invoke();
        }

        /// <summary>제출 가능 여부(카드 UI 버튼 활성/비활성용).</summary>
        public bool CanSubmit(PlayerUtterance utterance, out string reason)
        {
            reason = null;
            if (session == null || busy || session.IsFinished) return false;
            return session.CanSubmit(utterance, out reason);
        }

        /// <summary>제출문 하나를 판정에 넣고 결과로 화면을 갱신한다. (카드 UI가 이걸 호출)</summary>
        public void Submit(PlayerUtterance utterance)
        {
            if (session == null) { Debug.LogWarning("[HearingBattle] 세션이 없습니다."); return; }
            if (busy || session.IsFinished) return;

            if (!session.CanSubmit(utterance, out string reason))
            {
                if (replyText != null) replyText.text = $"(제출 불가) {reason}";
                return;
            }

            StartCoroutine(SubmitRoutine(utterance));
        }

        private IEnumerator SubmitRoutine(PlayerUtterance utterance)
        {
            busy = true;
            if (replyText != null) replyText.text = "…";   // 판정 대기 연출

            DialogueResult raw = null;
            yield return judge.Judge(session, utterance, r => raw = r);

            DialogueResult result = session.ApplyResult(raw, utterance);
            busy = false;

            ApplyResultToUi(result);
        }

        private void ApplyResultToUi(DialogueResult result)
        {
            if (replyText != null) replyText.text = result.reply;

            RefreshHud();
            if (hearing != null) hearing.SetExpression(MoodFrom(result));

            if (outcomeText != null && !session.IsFinished)
            {
                if (result.affinityMaxed)
                    ShowFlash($"{enemy.displayName}의 속내를 들었다 — 조각 획득!");
                else if (result.codeRevealed && !string.IsNullOrEmpty(result.revealedValue))
                    ShowFlash($"코드 확보: {result.revealedValue}\n남은 턴에 마음을 얻어라", 3f);
            }

            // 코드를 얻어도 3턴까지 간다 (기획서 §4: 간보기 → 찌르기 → 무마·이탈).
            // 마지막 턴이 친밀도를 채우는 자리이므로, 여기서 끊으면 '속내 조각'을 얻을 수 없다.
            if (session.IsFinished)
                ShowOutcome(session.LastOutcome);

            OnStateChanged?.Invoke();
        }

        private void RefreshHud()
        {
            if (session == null) return;

            if (suspicionText != null) suspicionText.text = $"의심 {session.Suspicion}%";
            if (affinityText != null)  affinityText.text  = $"{session.Affinity}%";
            if (turnText != null)      turnText.text      = $"{session.CurrentTurn}/{BattleSession.MaxTurn}턴";
            if (focusText != null)     focusText.text     = $"집중 {session.FocusRemaining}/{BattleSession.FocusPerTurn}";
            if (codeText != null)      codeText.text      = session.CodeAcquired ? $"코드 [{session.SessionCodeValue}]" : "코드 [ _ ]";

            if (suspicionFill != null) suspicionFill.fillAmount = session.Suspicion / 100f;
            if (affinityFill != null)  affinityFill.fillAmount  = session.Affinity / 100f;
        }

        private void ShowOutcome(TurnOutcome outcome)
        {
            if (outcome == TurnOutcome.Success) { Win(); return; }   // 성공은 자동 전환

            // 코드를 이미 챘으면 발각돼도 라운드는 통과시킨다.
            // (백엔드 DecideOutcome은 의심도만 보고 발각을 내므로, 코드 확보가 무시된다)
            // 대가는 친밀 100을 못 채워 '속내 조각'을 놓치는 것 — 진엔딩이 막힌다.
            if (session != null && session.CodeAcquired) { Win(outcome == TurnOutcome.FailedExposed); return; }

            // 실패(발각·시간초과) → "다시 돌아가기" 버튼
            string head = outcome switch
            {
                TurnOutcome.FailedExposed => "발각됨 — 심문 실패",
                TurnOutcome.FailedTimeout => "시간 초과 — 코드 미확보 (재도전 가능)",
                _                         => outcomeText != null ? outcomeText.text : ""
            };
            RevealResult(head + AffinityNote, "다시 돌아가기");
        }

        /// <summary>배경을 먼저 흐리게 떠 두고, 다음 프레임에 결과 문구·버튼을 올린다.</summary>
        private void RevealResult(string text, string buttonLabel)
        {
            StartCoroutine(RevealRoutine(text, buttonLabel));
        }

        private IEnumerator RevealRoutine(string text, string buttonLabel)
        {
            // 문구가 블러 배경에 같이 찍히면 글자가 두 번 겹쳐 보이므로 캡처를 먼저 끝낸다
            if (blurOverlay != null)
            {
                blurOverlay.Show();
                yield return null;
            }
            if (outcomeText != null) outcomeText.text = text;
            if (resultOverlay != null) resultOverlay.SetActive(true);
            if (resultButton != null)
            {
                var lbl = resultButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = buttonLabel;
                resultButton.gameObject.SetActive(true);
            }
        }

        /// <summary>코드 확보 = 성공. 결과 문구를 보여준 뒤 다음 챕터로.</summary>
        /// <param name="exposed">막판에 발각됐는가 (코드는 이미 챈 상태)</param>
        private void Win(bool exposed = false)
        {
            if (won) return;
            won = true;
            busy = true;   // 전환 대기 중 추가 제출 차단

            // 발각 통과 경로에서는 백엔드 Finish가 Success가 아니라 열쇠를 주지 않으므로 직접 지급
            if (exposed && (enemy?.secret?.hasBossRoomKey ?? false))
                CurrentRun?.GrantBossRoomKey();

            string head = exposed
                ? $"들켰지만 코드 [{session.SessionCodeValue}]는 챘다"
                : $"심문 종료 — 코드 [{session.SessionCodeValue}] 확보!";

            // 코드를 충분히 확인한 뒤 직접 넘어가게 (자동 전환 X)
            if (resultButton != null)
                RevealResult(head + AffinityNote, "빠져나가기");
            else
            {
                if (resultOverlay != null) resultOverlay.SetActive(true);
                if (outcomeText != null) outcomeText.text = head + AffinityNote;
                StartCoroutine(WinRoutine());   // 버튼이 없으면 기존 자동 전환 폴백
            }
        }

        Coroutine flashCo;

        /// <summary>턴 중 안내 문구를 잠깐 띄웠다가 지운다 (심문이 끝난 게 아니므로 남겨두지 않는다).</summary>
        private void ShowFlash(string text, float seconds = 4f)
        {
            if (outcomeText == null) return;
            if (flashCo != null) StopCoroutine(flashCo);
            flashCo = StartCoroutine(FlashRoutine(text, seconds));
        }

        private IEnumerator FlashRoutine(string text, float seconds)
        {
            // 문구를 넣기 전에 배경을 흐리게 떠 둔다 (문구가 블러에 같이 찍히지 않도록)
            if (blurOverlay != null)
            {
                blurOverlay.Show();
                yield return null;
            }
            outcomeText.text = text;

            yield return new WaitForSecondsRealtime(seconds);

            bool finished = session != null && session.IsFinished;
            if (!finished)
            {
                outcomeText.text = "";
                if (blurOverlay != null) blurOverlay.Hide();
            }
            flashCo = null;
        }

        /// <summary>친밀 100% 달성 여부에 따라 결과창에 덧붙일 문구 (속내 조각 안내).</summary>
        private string AffinityNote
        {
            get
            {
                if (session == null) return "";
                // 이모지는 폰트에 없어 네모로 뜨므로 쓰지 않는다
                return session.Affinity >= 100
                    ? "\n<size=70%>속내 조각 획득 — 증언을 남겼다</size>"
                    : "\n<size=70%>속내 조각 없음 — 마음을 얻지 못했다</size>";
            }
        }

        private IEnumerator WinRoutine()
        {
            yield return new WaitForSecondsRealtime(5f);   // 확보한 코드를 충분히 읽을 시간
            gameObject.SetActive(false);                      // 심문 화면 닫기
            if (nextChapter != null) nextChapter.SetActive(true);
        }

        /// <summary>결과 버튼 클릭: 성공이면 다음 챕터로, 실패면 탐색 맵으로 복귀(재도전).</summary>
        private void OnResultClicked()
        {
            gameObject.SetActive(false);
            if (won)
            {
                if (nextChapter != null) nextChapter.SetActive(true);
            }
            else if (searchMap != null) searchMap.SetActive(true);
        }

        /// <summary>결과를 조직원 표정으로 매핑. (의심/친밀/발각 흐름 기반 휴리스틱)</summary>
        private HearingController.Mood MoodFrom(DialogueResult result)
        {
            // 화남: 발각·저항 발동·정 떨어지는(배신) 접근 → 조직원이 빡침
            if (result.exposed || result.turnOutcome == TurnOutcome.FailedExposed
                || !string.IsNullOrEmpty(result.resistanceTriggeredId)
                || result.affinityDelta < 0)
                return HearingController.Mood.Angry;

            // 신뢰: 약점 적중·코드 흘림·친밀 만렙
            if (result.codeRevealed || result.affinityMaxed || result.weaknessHit)
                return HearingController.Mood.Trust;

            // 의심: 의심도 상승
            if (result.suspicionDelta > 0)
                return HearingController.Mood.Doubt;

            return HearingController.Mood.Neutral;
        }

        // ── 임시 테스트용 프리셋 (카드 UI 붙기 전까지 루프 검증) ──
        //    버튼 onClick / ContextMenu / 코드에서 공통 호출.
        public void DebugSubmitTurn(int idx)
        {
            PlayerUtterance u = idx switch
            {
                0 => new PlayerUtterance
                {
                    KeywordCardIds = new List<string> { "kw_rookie_pride" },
                    FrameId = FrameIds.Praise,
                    ComposedText = "요즘 애들 중에 너만큼 야무진 놈이 없대."
                },
                1 => new PlayerUtterance
                {
                    KeywordCardIds = new List<string> { "kw_code_digit" },
                    FrameId = FrameIds.Complicity,
                    ComposedText = "우리끼리니까 하는 말인데, 네 자리 하나만 확인하자."
                },
                _ => new PlayerUtterance
                {
                    KeywordCardIds = new List<string> { "kw_rookie_pride" },
                    FrameId = FrameIds.SmallTalk,
                    ComposedText = "아무튼 오늘 고생했다. 나중에 밥이나 한번 먹자."
                }
            };
            Submit(u);
        }

        [ContextMenu("테스트: 1턴 치켜세우기")] private void TestTurn0() => DebugSubmitTurn(0);
        [ContextMenu("테스트: 2턴 코드 압박")]  private void TestTurn1() => DebugSubmitTurn(1);
        [ContextMenu("테스트: 3턴 잡담 무마")]  private void TestTurn2() => DebugSubmitTurn(2);
    }
}
