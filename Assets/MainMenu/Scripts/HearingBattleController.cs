using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

        /// <summary>어떤 판정기로 심문을 돌릴지. (IDialogueJudge 구현 선택)</summary>
        public enum JudgeMode
        {
            Mock,   // 규칙 기반 가짜 판정 — 즉시 응답, 통신 없음 (UI 작업·시연 안전판)
            Llm     // 프록시 경유 실제 LLM 판정
        }

        [Header("판정기")]
        [Tooltip("Mock = 규칙 기반 가짜 판정(즉시 응답). Llm = 프록시 경유 실제 LLM 판정.")]
        [SerializeField] private JudgeMode judgeMode = JudgeMode.Mock;
        [Tooltip("judge.js 엔드포인트 전체 URL (예: https://xxx.vercel.app/api/judge). " +
                 "비우면 Resources/ProxyConfig.json → 환경변수 TOPDOG_PROXY_URL 순으로 읽는다.")]
        [SerializeField] private string proxyUrl = "";
        // 토큰은 인스펙터에 넣을 수 없게 막아둔다.
        // [SerializeField]로 두면 값을 한 번 넣는 순간 씬 YAML에 평문으로 박혀 그대로 커밋된다.
        // "비워 두세요"라는 안내로는 실수를 못 막으므로, 아예 ProxyConfig에서만 읽는다.
        [Tooltip("판정 요청 타임아웃(초)")]
        [SerializeField] private int judgeTimeoutSeconds = 20;

        [Header("대사 연출")]
        [Tooltip("상대 대사를 한 글자씩 출력한다. 속도는 설정 화면의 '대화 출력 속도'를 따른다.")]
        [SerializeField] private bool typeReply = true;

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
        [Tooltip("새 카드를 얻었을 때만 손패 위에 잠깐 뜨는 문구. 없으면 생략된다.")]
        [SerializeField] private TMP_Text cardNoticeText;
        [Tooltip("새 카드 안내를 몇 초 보여줄지")]
        [SerializeField] private float cardNoticeSeconds = 2.2f;

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

        /// <summary>
        /// 정식으로 통과한(3턴을 발각 없이 마친) 조직원 id.
        ///
        /// "이미 통과한 상대인가"를 RunState.HasCode로 판단하면 안 된다 — 발각으로 실패해도
        /// 중간에 흘러나온 코드는 백엔드가 이미 커밋해 둔 상태라, 재도전 때 통과한 것으로 착각해
        /// 의심도·친밀도를 초기화하지 않는다(=시작부터 발각권). 통과 여부는 여기서 따로 센다.
        /// </summary>
        static readonly HashSet<string> clearedEnemies = new();

        /// <summary>새 런 시작 (챕터 처음부터). 탐색 단서와 함께 초기화된다.</summary>
        public static void ResetRun()
        {
            CurrentRun = null;
            clearedEnemies.Clear();
        }

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

            // 심문은 늘 처음부터 시작한다 — 의심 0, 친밀 0.
            //
            // 의심도: 런에 누적하면 앞 라운드 잔고 때문에 3라운드가 시작부터 발각권에 들어간다.
            //         '그 조직원이 나를 얼마나 의심하는가'이므로 상대가 바뀌면 리셋이 맞다.
            // 친밀도: 재도전(실패 후 단서 다시 모아 재입장)인데 친밀이 남아 있으면
            //         이전 시도의 중간 지점에서 시작해버린다. 1턴 친밀 → 2턴 코드 → 3턴 친밀
            //         3턴 구성이 무의미해지므로 재도전도 0에서 다시 쌓게 한다.
            //
            // 단, 이미 통과한 조직원은 다시 볼 일이 없는 상대다. 그 친밀도(=속내 조각)는 보존한다.
            bool alreadyPassed = clearedEnemies.Contains(enemyId);
            if (!alreadyPassed)
            {
                int prevSusp = run.Suspicion;
                int prevAff  = run.GetAffinity(enemyId);
                run.SetSuspicion(0);
                run.SetAffinity(enemyId, 0);
                if (prevSusp != 0 || prevAff != 0)
                    Debug.Log($"[HearingBattle] {enemy.displayName} 심문 시작 — 의심 {prevSusp}→0, 친밀 {prevAff}→0");
            }

            // 탐색에서 모은 단서를 손패로 넘긴다.
            // TODO: seedKeywords는 탐색을 건너뛴 에디터 테스트용이다. 정식 경로에서는 쓰지 않는다.
            var collected = ExplorationController.CollectedClues;
            if (collected != null && collected.Count > 0)
                foreach (var kw in collected) run.AcquireKeyword(kw);
#if UNITY_EDITOR
            else
                foreach (var kw in seedKeywords) run.AcquireKeyword(kw);
#endif

            session = new BattleSession(enemy, run);
            judge   = CreateJudge();
            busy    = false;
            won     = false;

            if (outcomeText != null) outcomeText.text = "";
            // 이름은 JSON에서 오므로 조사를 받침에 맞춘다 ("신참 조직원와의" → "…원과의")
            TypeReply($"{Korean.Gwa(enemy.displayName)}의 심문을 시작한다.");
            if (resultButton != null) resultButton.gameObject.SetActive(false);
            if (resultOverlay != null) resultOverlay.SetActive(false);
            // 지난 심문의 결과창에서 켜둔 블러가 남아 있으면, 새 심문이 옛 화면 스냅샷 위에서 시작된다
            if (blurOverlay != null) blurOverlay.Hide();
            if (cardNoticeCo != null) { StopCoroutine(cardNoticeCo); cardNoticeCo = null; }
            SetCardNoticeAlpha(0f);
            RefreshHud();
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// 인스펙터 설정대로 판정기를 만든다. (BattleSession은 IDialogueJudge만 알면 되므로 여기서 갈아끼운다)
        ///
        /// URL은 비밀이 아니라 인스펙터에 둬도 되지만, 토큰은 ProxyConfig에서만 읽는다(위 필드 주석 참고).
        /// URL·토큰이 없는데 Llm을 골라 두면 심문 자체가 불가능해지므로 Mock으로 떨어뜨린다
        /// — 시연 중에 통신이 막혀도 게임은 굴러가야 한다.
        /// </summary>
        private IDialogueJudge CreateJudge()
        {
            if (judgeMode == JudgeMode.Mock) return new MockDialogueJudge();

            // 인스펙터 URL은 배포처를 임시로 갈아끼울 때만 쓰는 덮어쓰기 값이다.
            string url   = !string.IsNullOrWhiteSpace(proxyUrl) ? proxyUrl.Trim() : ProxyConfig.Url;
            string token = ProxyConfig.Token;

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            {
                Debug.LogWarning("[HearingBattle] 판정기를 Llm으로 골랐지만 프록시 접속 정보가 없어 Mock으로 진행합니다. " +
                                 ProxyConfig.DescribeProblem());
                return new MockDialogueJudge();
            }

            Debug.Log("[HearingBattle] 실제 LLM 판정으로 심문을 시작합니다.");
            return new LlmDialogueJudge(url, token, judgeTimeoutSeconds);
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
                Typewriter.ShowAll(replyText, $"(제출 불가) {reason}");   // 안내는 즉시
                return;
            }

            StartCoroutine(SubmitRoutine(utterance));
        }

        private IEnumerator SubmitRoutine(PlayerUtterance utterance)
        {
            busy = true;
            Typewriter.ShowAll(replyText, "…");   // 판정 대기 연출 (타이핑 없이)

            DialogueResult raw = null;
            yield return judge.Judge(session, utterance, r => raw = r);

            // 약점을 찌르면 새 키워드 카드가 손패에 들어온다. 판정 전후를 비교해 그것만 골라낸다.
            // (result.weaknessHit만 보면 '이미 가진 카드를 다시 드러낸' 경우까지 새 카드로 알린다)
            var before = new HashSet<string>(run.OwnedKeywords);
            DialogueResult result = session.ApplyResult(raw, utterance);
            var gained = new List<string>();
            foreach (var id in run.OwnedKeywords)
                if (!before.Contains(id)) gained.Add(id);

            busy = false;

            ApplyResultToUi(result, gained);
        }

        private void ApplyResultToUi(DialogueResult result, List<string> gainedCards = null)
        {
            TypeReply(result.reply);   // 상대 대사는 한 글자씩 (설정의 '대화 출력 속도')

            RefreshHud();
            if (hearing != null) hearing.SetExpression(MoodFrom(result));

            // 턴 중간에는 화면을 덮는 안내를 띄우지 않는다.
            // 코드를 챈 것도, 속내를 들은 것도 심문이 끝난 게 아니라 '진행 중'인 사건인데
            // 블러 + 중앙 문구로 띄우면 결과창처럼 보여서 심문이 끝난 줄 알게 된다.
            // 그 둘은 HUD 라벨을 튕겨 시선만 끌고, 판정 문구는 3턴이 끝난 뒤 한 번만 보여준다.
            //
            // 딱 하나 문구로 알리는 건 '새 카드'다. 손패가 늘어난 건 다음 턴에 당장 쓸 수 있는
            // 정보인데, 아래에 카드가 조용히 한 장 늘어나는 것만으로는 알아채기 어렵다.
            if (!session.IsFinished)
            {
                if (result.affinityMaxed) Pulse(affinityText);
                else if (result.codeRevealed) Pulse(codeText);

                if (gainedCards != null && gainedCards.Count > 0) ShowCardNotice(gainedCards);
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
            // 심문 중에는 코드를 보여주지 않는다.
            // 상대가 말끝에 흘리는 건 대사로 읽히면 되고, HUD에 값이 박히면 스포일러다.
            // 게다가 이 코드를 실제로 챘는지는 3턴을 다 돌고 의심도·친밀도를 보고 정해지므로,
            // 중간에 띄우면 나중에 못 챈 경우와 어긋난다. 확정된 값은 결과창에서만 보여준다.
            if (codeText != null)
                codeText.text = session.IsFinished && session.CodeAcquired
                    ? $"코드 [{session.SessionCodeValue}]"
                    : "코드 [ ? ]";

            if (suspicionFill != null) suspicionFill.fillAmount = session.Suspicion / 100f;
            if (affinityFill != null)  affinityFill.fillAmount  = session.Affinity / 100f;
        }

        private void ShowOutcome(TurnOutcome outcome)
        {
            if (outcome == TurnOutcome.Success) { Win(); return; }   // 성공은 자동 전환

            // 코드는 심문 '전체'를 마쳤을 때 주는 것이다 — 중간에 값이 흘러나왔어도
            // 마지막 의심도가 발각선을 넘었으면 빈손으로 나온다. (백엔드 DecideOutcome의 원래 설계)
            // 그래서 여기서 통과시켜 주지 않는다. 다시 들어와 제대로 끝내야 한다.
            string head = outcome switch
            {
                TurnOutcome.FailedExposed => session.CodeAcquired
                    ? "발각됨 — 코드를 들고 나오지 못했다"
                    : "발각됨 — 심문 실패",
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

        /// <summary>3턴을 발각 없이 마치고 코드까지 챘을 때. 결과 문구를 보여준 뒤 다음 챕터로.</summary>
        private void Win()
        {
            if (won) return;
            won = true;
            busy = true;   // 전환 대기 중 추가 제출 차단

            // 정식으로 통과한 상대로 기록한다 (재도전 판정에 쓴다 — 아래 clearedEnemies 주석 참고)
            if (!string.IsNullOrEmpty(enemyId)) clearedEnemies.Add(enemyId);

            string head = $"심문 종료 — 코드 [{session.SessionCodeValue}] 확보!";

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

        Coroutine replyCo;

        /// <summary>
        /// 상대 대사를 한 글자씩 출력한다. 다음 판정이 오면 앞 대사는 끊고 새로 시작한다.
        /// (직접 replyText.text에 넣으면 이전 타이핑이 계속 돌아 글자가 섞인다)
        /// </summary>
        private void TypeReply(string text)
        {
            if (replyText == null) return;
            if (replyCo != null) StopCoroutine(replyCo);

            if (!typeReply || !gameObject.activeInHierarchy)
            {
                Typewriter.ShowAll(replyText, text);
                return;
            }
            replyCo = StartCoroutine(TypeReplyRoutine(text));
        }

        private IEnumerator TypeReplyRoutine(string text)
        {
            yield return Typewriter.Reveal(replyText, text);
            replyCo = null;
        }

        Coroutine cardNoticeCo;

        /// <summary>
        /// 새로 얻은 카드를 손패 위에 잠깐 띄운다. 화면을 덮지 않고(블러 없음) 스스로 사라진다.
        /// </summary>
        private void ShowCardNotice(List<string> ids)
        {
            if (cardNoticeText == null) return;

            var names = new List<string>();
            foreach (var id in ids) names.Add($"〈{HearingCardPanel.CardName(id)}〉");
            cardNoticeText.text = $"새 카드 {string.Join(" ", names)}";

            if (cardNoticeCo != null) StopCoroutine(cardNoticeCo);
            cardNoticeCo = StartCoroutine(CardNoticeRoutine());
        }

        private IEnumerator CardNoticeRoutine()
        {
            var rt = cardNoticeText.rectTransform;
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.DOPunchScale(Vector3.one * 0.2f, 0.35f, 5, 0.6f).SetUpdate(true);

            // 끝에서 서서히 사라지게 — 갑자기 없어지면 못 본 사람은 뜬 줄도 모른다
            SetCardNoticeAlpha(1f);
            yield return new WaitForSecondsRealtime(cardNoticeSeconds);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                SetCardNoticeAlpha(1f - t / 0.4f);
                yield return null;
            }
            SetCardNoticeAlpha(0f);
            cardNoticeCo = null;
        }

        private void SetCardNoticeAlpha(float a)
        {
            if (cardNoticeText == null) return;
            var c = cardNoticeText.color;
            cardNoticeText.color = new Color(c.r, c.g, c.b, a);
        }

        /// <summary>
        /// HUD 라벨을 한 번 튕겨 방금 바뀐 값에 시선을 끈다 (진행을 막지 않는 피드백).
        /// 화면을 덮는 안내 대신 쓰는 것이므로 짧고 가볍게 둔다.
        /// </summary>
        private void Pulse(TMP_Text label)
        {
            if (label == null) return;
            var rt = label.rectTransform;
            rt.DOKill();
            rt.localScale = Vector3.one;
            rt.DOPunchScale(Vector3.one * 0.35f, 0.4f, 6, 0.6f).SetUpdate(true);
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
