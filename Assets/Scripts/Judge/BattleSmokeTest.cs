using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDogDetective.Judge
{
    using Data;

    /// <summary>
    /// UI 없이 3턴 루프를 검증하는 스모크 테스트.
    /// 빈 GameObject에 붙이고 Play를 누르면 콘솔에 전 과정이 찍힌다.
    ///
    /// 검증 항목:
    ///  1) 탐색 → 키워드 → 3턴 대화 → 코드 획득이 끊김 없이 도는가
    ///  2) 완벽 플레이(1턴 친밀 → 2턴 코드 → 3턴 친밀) 시 친밀도 100% 도달하는가
    ///  3) 직설적 요구 시 저항이 발동하고 코드가 막히는가
    ///  4) 측근 대질 전제 미충족 시 코드가 차단되는가
    ///
    /// [주의] 개발 전용. 릴리즈 씬에 넣지 말 것.
    /// </summary>
#if UNITY_EDITOR
    public class BattleSmokeTest : MonoBehaviour
    {
        [SerializeField] bool runOnStart = true;

        void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        IEnumerator RunAll()
        {
            Debug.Log("═══ 스모크 테스트 시작 ═══");

            yield return TestPerfectPlay();
            yield return TestDirectDemand();
            yield return TestConfrontationGuard();

            Debug.Log("═══ 스모크 테스트 종료 ═══");
        }

        // ── 시나리오 1: 완벽 플레이 ──────────────────────────
        IEnumerator TestPerfectPlay()
        {
            Debug.Log("\n▶ [1] 완벽 플레이 — 1턴 친밀 / 2턴 코드 / 3턴 친밀");

            var run = new RunState();
            var enemy = MakeRookie();

            run.AcquireKeyword("kw_rookie_pride");
            run.AcquireKeyword("kw_code_digit");

            var session = new BattleSession(enemy, run);
            var judge = new MockDialogueJudge(seed: 42);

            // 1턴 — 치켜세우기 (친밀)
            yield return DialogueTestRunner.Submit(session, judge, new PlayerUtterance
            {
                KeywordCardIds = new List<string> { "kw_rookie_pride" },
                FrameId = FrameIds.Praise,
                ComposedText = "요즘 애들 중에 너만큼 야무진 놈이 없대."
            });

            // 2턴 — 코드 압박 (약점 적중 프레이밍 + 코드 키워드)
            yield return DialogueTestRunner.Submit(session, judge, new PlayerUtterance
            {
                KeywordCardIds = new List<string> { "kw_code_digit" },
                FrameId = FrameIds.Complicity,
                ComposedText = "우리끼리니까 하는 말인데, 네 자리 하나만 확인하자."
            });

            // 3턴 — 잡담으로 무마 (친밀 + 의심 감소)
            yield return DialogueTestRunner.Submit(session, judge, new PlayerUtterance
            {
                KeywordCardIds = new List<string> { "kw_rookie_pride" },
                FrameId = FrameIds.SmallTalk,
                ComposedText = "아무튼 오늘 고생했다. 나중에 밥이나 한번 먹자."
            });

            Debug.Log($"  → 결과: {session.LastOutcome} / 친밀 {run.GetAffinity(enemy.id)}% " +
                      $"/ 의심 {run.Suspicion}% / 코드 {run.ComposeCode()}");

            if (run.IsAffinityMaxed(enemy.id))
                Debug.Log("  ✅ 친밀도 100% 달성 — 밸런싱 목표 충족");
            else
                Debug.LogWarning($"  ⚠️ 친밀도 {run.GetAffinity(enemy.id)}% — 목표(100%) 미달. " +
                                 "MockDialogueJudge.Settings 수치 조정 필요");
        }

        // ── 시나리오 2: 직설적 요구 ──────────────────────────
        IEnumerator TestDirectDemand()
        {
            Debug.Log("\n▶ [2] 직설적 요구 — 저항 발동·코드 차단 확인");

            var run = new RunState();
            var enemy = MakeRookie();
            run.AcquireKeyword("kw_code_digit");

            var session = new BattleSession(enemy, run);
            var judge = new MockDialogueJudge(seed: 7);

            for (int i = 0; i < 3 && !session.IsFinished; i++)
            {
                yield return DialogueTestRunner.Submit(session, judge, new PlayerUtterance
                {
                    KeywordCardIds = new List<string> { "kw_code_digit" },
                    FrameId = null,   // 프레이밍 없이 = 직설
                    ComposedText = "너 비밀번호 한 자리 안다며, 알려줘."
                });
            }

            Debug.Log($"  → 결과: {session.LastOutcome} / 의심 {run.Suspicion}% " +
                      $"/ 코드 획득: {session.CodeAcquired}");

            if (!session.CodeAcquired) Debug.Log("  ✅ 직설 요구로는 코드가 뚫리지 않음");
            else Debug.LogError("  ❌ 직설 요구로 코드가 뚫렸습니다 — 가드 확인 필요");
        }

        // ── 시나리오 3: 측근 대질 가드 ───────────────────────
        IEnumerator TestConfrontationGuard()
        {
            Debug.Log("\n▶ [3] 측근 대질 전제 — 미충족 시 코드 차단 확인");

            var run = new RunState();
            var aide = MakeAide();
            run.AcquireKeyword("kw_code_digit");   // 대질 키워드는 일부러 미보유

            var session = new BattleSession(aide, run);
            var judge = new MockDialogueJudge(seed: 13);

            Debug.Log($"  대질 전제 충족: {session.ConfrontationSatisfied} (false 여야 정상)");

            for (int i = 0; i < 3 && !session.IsFinished; i++)
            {
                yield return DialogueTestRunner.Submit(session, judge, new PlayerUtterance
                {
                    KeywordCardIds = new List<string> { "kw_code_digit" },
                    FrameId = FrameIds.Sympathy,
                    ComposedText = "사정이 급해서 그러는데 한 자리만…"
                });
            }

            if (!session.CodeAcquired) Debug.Log("  ✅ 대질 전제 없이는 측근이 뚫리지 않음");
            else Debug.LogError("  ❌ 대질 전제 없이 코드가 뚫렸습니다 — 가드 확인 필요");
        }

        // ── 테스트용 데이터 ──────────────────────────────────
        static EnemyData MakeRookie() => EnemyDataLoader.Load("member_a_rookie");

        static EnemyData MakeAide()
        {
            var aide = MakeRookie();
            aide.id = "member_c_aide";
            aide.displayName = "보스 측근";
            aide.tier = 3;
            aide.difficulty = 3;
            aide.secret.codeIndex = 3;
            aide.secret.codeValue = "9";
            aide.secret.hasBossRoomKey = true;
            aide.affinity.openingFrameIds = new List<string> { FrameIds.Sympathy };
            aide.requiredConfrontationKeywords =
                new List<string> { "kw_boss_distrust", "kw_keeper_slip" };
            return aide;
        }
    }
#endif
}
