using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDogDetective.Judge
{
    using Data;

    /// <summary>
    /// 조직원 A(신참) end-to-end 데모. Resources/Enemies/member_a_rookie.json을 로드해
    /// 실제 배포된 LLM 프록시로 3턴 대화를 돌리고, 코드 획득 + 친밀도 상승을 검증한다.
    ///
    /// [주의] 실제 Claude API를 호출해 토큰을 소모한다.
    ///        runOnStart는 기본 false — 씬에 두고 Play만 눌러도 자동 실행되지 않는다.
    ///        인스펙터에서 컴포넌트 우클릭 → "Run End-to-End (LLM 호출, 토큰 소모됨)"로 직접 실행한다.
    ///        proxyToken은 씬 파일에 남지 않도록 인스펙터가 아니라 환경변수(PROXY_TOKEN)로 넘긴다.
    ///        터미널에서 `export PROXY_TOKEN=...` 설정 후 그 터미널에서 Unity를 실행하면 된다.
    /// </summary>
#if UNITY_EDITOR
    public class MemberAEndToEndDemo : MonoBehaviour
    {
        const string ProxyTokenEnvVar = "PROXY_TOKEN";

        [SerializeField] string proxyUrl = "";
        [SerializeField] bool runOnStart = false;

        void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        [ContextMenu("Run End-to-End (LLM 호출, 토큰 소모됨)")]
        public void RunFromInspector() => StartCoroutine(Run());

        IEnumerator Run()
        {
            string proxyToken = Environment.GetEnvironmentVariable(ProxyTokenEnvVar);

            if (string.IsNullOrWhiteSpace(proxyUrl))
            {
                Debug.LogError("[MemberAEndToEndDemo] proxyUrl이 비어 있습니다. " +
                               "인스펙터에 배포된 judge.js URL을 입력하세요.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(proxyToken))
            {
                Debug.LogError($"[MemberAEndToEndDemo] 환경변수 {ProxyTokenEnvVar}이 설정되지 않았습니다. " +
                               $"터미널에서 export {ProxyTokenEnvVar}=... 설정 후 그 터미널에서 Unity를 실행하세요.");
                yield break;
            }

            var enemy = EnemyDataLoader.Load("member_a_rookie");
            if (enemy == null)
            {
                Debug.LogError("[MemberAEndToEndDemo] member_a_rookie 데이터를 불러오지 못했습니다.");
                yield break;
            }

            Debug.Log("═══ 조직원 A end-to-end (실제 LLM) 시작 ═══");

            var run = new RunState();
            run.AcquireKeyword("kw_rookie_pride");
            run.AcquireKeyword("kw_code_digit");

            var session = new BattleSession(enemy, run);
            var judge = new LlmDialogueJudge(proxyUrl, proxyToken);

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

            if (run.HasCode(enemy.secret.codeIndex))
                Debug.Log("  ✅ 코드 1자리 획득 성공");
            else
                Debug.LogWarning("  ⚠️ 코드 미획득 — 시스템 프롬프트 튜닝이 필요할 수 있음");

            if (run.IsAffinityMaxed(enemy.id))
                Debug.Log("  ✅ 친밀도 100% 달성 — 밸런싱 목표 충족");
            else
                Debug.LogWarning($"  ⚠️ 친밀도 {run.GetAffinity(enemy.id)}% — 목표(100%) 미달");

            Debug.Log("═══ 조직원 A end-to-end 종료 ═══");
        }
    }
#endif
}
