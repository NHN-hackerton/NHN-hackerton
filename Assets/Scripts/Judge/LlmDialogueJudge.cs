using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TopDogDetective.Judge
{
    using Data;

    /// <summary>
    /// 프록시(Vercel) 경유 실제 LLM 판정. proxy/api/judge.js에 { systemPrompt, userMessage }를
    /// POST하고, { text } 응답을 DialogueResult로 파싱해 돌려준다.
    ///
    /// [주의] 이 클래스는 Sanitize/Resolve를 호출하지 않는다.
    ///        검증·누적 계산은 BattleSession.ApplyResult가 전담한다.
    ///        네트워크·파싱 실패 시 onDone(null)을 넘기면 ApplyResult가 폴백을 만든다.
    /// </summary>
    public class LlmDialogueJudge : IDialogueJudge
    {
        public string Name => "LLM (Claude, 프록시 경유)";

        // 프록시 자체 타임아웃(judge.js REQUEST_TIMEOUT_MS = 20초)보다 여유 있게 잡는다.
        public const int DefaultTimeoutSeconds = 25;

        // LLM이 형식에 안 맞는 텍스트를 준 경우에만 재시도한다 (네트워크·타임아웃 실패는 재시도 안 함 —
        // 같은 문제가 반복될 뿐이고, 그만큼 대기 시간과 토큰만 더 든다).
        public const int MaxAttempts = 2;

        readonly string proxyUrl;
        readonly string proxyToken;
        readonly int timeoutSeconds;

        /// <param name="proxyUrl">judge.js 엔드포인트 전체 URL (예: https://xxx.vercel.app/api/judge)</param>
        /// <param name="proxyToken">프록시의 PROXY_TOKEN과 동일한 값. X-Proxy-Token 헤더로 전송된다.</param>
        /// <param name="timeoutSeconds">Unity 쪽 요청 타임아웃.</param>
        public LlmDialogueJudge(string proxyUrl, string proxyToken, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (string.IsNullOrEmpty(proxyUrl))
                throw new ArgumentException("proxyUrl이 비어 있습니다.", nameof(proxyUrl));
            if (string.IsNullOrEmpty(proxyToken))
                throw new ArgumentException("proxyToken이 비어 있습니다. 프록시의 PROXY_TOKEN과 동일한 값을 넘기세요.", nameof(proxyToken));
            this.proxyUrl = proxyUrl;
            this.proxyToken = proxyToken;
            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>한 번의 시도 결과. Retryable=true면 LLM 출력 형식 문제 — 재시도할 가치가 있다.</summary>
        readonly struct AttemptOutcome
        {
            public readonly DialogueResult Result;
            public readonly bool Retryable;
            public AttemptOutcome(DialogueResult result, bool retryable)
            {
                Result = result;
                Retryable = retryable;
            }
        }

        public IEnumerator Judge(BattleSession session, PlayerUtterance utterance,
                                 Action<DialogueResult> onDone)
        {
            if (session == null || utterance == null)
            {
                onDone?.Invoke(null);
                yield break;
            }

            var context = session.BuildContext(utterance);
            string requestJson = JsonUtility.ToJson(new ProxyRequestBody
            {
                systemPrompt = BuildSystemPrompt(session.Enemy),
                userMessage  = BuildUserMessage(context)
            });

            AttemptOutcome outcome = default;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                yield return SendOnce(requestJson, o => outcome = o);

                if (outcome.Result != null || !outcome.Retryable)
                    break;

                if (attempt < MaxAttempts)
                    Debug.LogWarning($"[LlmDialogueJudge] 응답 파싱 실패 — 재시도 {attempt}/{MaxAttempts}");
            }

            onDone?.Invoke(outcome.Result);
        }

        /// <summary>요청 1회 왕복. 네트워크·프록시 실패는 Retryable=false로 즉시 포기시킨다.</summary>
        IEnumerator SendOnce(string requestJson, Action<AttemptOutcome> onAttemptDone)
        {
            using var www = new UnityWebRequest(proxyUrl, UnityWebRequest.kHttpVerbPOST);
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("X-Proxy-Token", proxyToken);
            www.timeout = timeoutSeconds;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LlmDialogueJudge] 요청 실패({www.result}): {www.error}");
                onAttemptDone(new AttemptOutcome(null, retryable: false));
                yield break;
            }

            ProxyResponseBody response;
            try
            {
                response = JsonUtility.FromJson<ProxyResponseBody>(www.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LlmDialogueJudge] 프록시 응답 파싱 실패: {e.Message}");
                onAttemptDone(new AttemptOutcome(null, retryable: false));
                yield break;
            }

            if (response == null || string.IsNullOrEmpty(response.text))
            {
                Debug.LogWarning("[LlmDialogueJudge] 프록시 응답에 text가 없습니다.");
                onAttemptDone(new AttemptOutcome(null, retryable: false));
                yield break;
            }

            var result = ParseDialogueResult(response.text);
            if (result == null)
                Debug.LogWarning($"[LlmDialogueJudge] LLM 응답 JSON 파싱 실패. 원문: {response.text}");

            // LLM 출력 자체가 형식에 안 맞았을 때만 재시도 대상 — 같은 입력이라도 매번 다른 텍스트가 나온다.
            onAttemptDone(new AttemptOutcome(result, retryable: result == null));
        }

        // ── 요청/응답 봉투 ───────────────────────────────────
        [Serializable]
        class ProxyRequestBody
        {
            public string systemPrompt;
            public string userMessage;
        }

        [Serializable]
        class ProxyResponseBody
        {
            public string text;
        }

        // ── DialogueResult 파싱 ──────────────────────────────
        static DialogueResult ParseDialogueResult(string raw)
        {
            string json = ExtractJson(raw);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonUtility.FromJson<DialogueResult>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LlmDialogueJudge] DialogueResult 파싱 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>마크다운 코드펜스·설명 텍스트가 섞여 와도 첫 '{'~마지막 '}'만 추출한다.</summary>
        static string ExtractJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string trimmed = raw.Trim();
            if (trimmed.StartsWith("```"))
            {
                int firstNewline = trimmed.IndexOf('\n');
                if (firstNewline >= 0) trimmed = trimmed[(firstNewline + 1)..];
                int fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (fenceEnd >= 0) trimmed = trimmed[..fenceEnd];
                trimmed = trimmed.Trim();
            }

            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start < 0 || end < 0 || end < start) return null;

            return trimmed[start..(end + 1)];
        }

        // ── 시스템 프롬프트 (조직원 페르소나·규칙, 조직원 무관 공통 템플릿) ──
        static string BuildSystemPrompt(EnemyData enemy)
        {
            var p = enemy.persona;
            var s = enemy.secret;
            var sb = new StringBuilder();

            sb.AppendLine($"너는 잠입 게임 \"Top Dog Detective\" 속 고양이 조직원 \"{enemy.displayName}\"이다. " +
                          "플레이어(위장 잠입한 개 경찰)와 대화한다.");
            sb.AppendLine();

            sb.AppendLine("[페르소나]");
            sb.AppendLine($"나이: {p?.age}");
            sb.AppendLine($"성격: {p?.personality}");
            sb.AppendLine($"말투: {p?.speechStyle}");
            sb.AppendLine($"플레이어 인식: {p?.relationToPlayer}");
            sb.AppendLine();

            sb.AppendLine("[함구령 — 반드시 지켜야 하는 비밀]");
            sb.AppendLine(s?.gagOrder);
            sb.AppendLine($"너는 코드 {s?.codeIndex}번째 자리 값 \"{s?.codeValue}\"를 알고 있다. " +
                          "절대 먼저 말하지 않는다. 아래 [약점]을 정확히 찌르는 대화에만 무너질 수 있다.");
            sb.AppendLine();

            sb.AppendLine("[약점 — 정확히 찌르면 무너질 수 있음]");
            foreach (var w in enemy.weaknesses ?? new List<EnemyData.Weakness>())
                sb.AppendLine($"- {w.label} ({w.id}): {w.trigger}");
            sb.AppendLine();

            if (enemy.resistances is { Count: > 0 })
            {
                sb.AppendLine("[저항 — 이런 접근엔 오히려 경계함]");
                foreach (var r in enemy.resistances)
                    sb.AppendLine($"- {r.label} ({r.id}): {r.trigger}");
                sb.AppendLine();
            }

            sb.AppendLine("[친밀 — 마음을 여는/닫는 접근]");
            sb.AppendLine($"마음을 여는 접근: {string.Join(", ", enemy.affinity?.openingFrameIds ?? new List<string>())}");
            sb.AppendLine($"정 떨어지는 접근: {string.Join(", ", enemy.affinity?.betrayalFrameIds ?? new List<string>())}");
            sb.AppendLine();

            if (enemy.RequiresConfrontation)
            {
                sb.AppendLine("[대질 전제]");
                sb.AppendLine("너는 정면 설득만으로는 절대 무너지지 않는다. 플레이어가 다른 조직원에게서 " +
                              "얻은 정보로 너를 대질(추궁)해야만 흔들릴 수 있다.");
                sb.AppendLine();
            }

            sb.AppendLine("[판정 규칙]");
            sb.AppendLine("- weaknessHit: 플레이어 접근이 위 약점을 정확히 찔렀으면 true");
            sb.AppendLine("- hitWeaknessId: weaknessHit=true일 때만 해당 약점 id, 아니면 필드 생략");
            sb.AppendLine("- resistanceTriggeredId: 위 저항 중 하나가 발동했으면 해당 id, 아니면 필드 생략");
            sb.AppendLine("- codeRevealed: 이번 발화로 코드를 실제로 흘렸다고 판단하면 true " +
                          "(턴 2 이상 + weaknessHit=true일 때만 가능)");
            sb.AppendLine($"- revealedValue: codeRevealed=true일 때만 \"{s?.codeValue}\" 값을 넣는다");
            sb.AppendLine("- suspicionDelta: 이번 턴 의심도 증감. 범위 " +
                          $"{DialogueResult.SuspicionDeltaMin}~+{DialogueResult.SuspicionDeltaMax}");
            sb.AppendLine("- affinityDelta: 이번 턴 친밀도 증감. 범위 " +
                          $"{DialogueResult.AffinityDeltaMin}~+{DialogueResult.AffinityDeltaMax}");
            sb.AppendLine("- exposed: 정체가 명백히 드러난 경우에만 true (극히 예외적인 경우만). " +
                          "애매하면 반드시 false — 코드가 아니라 캐릭터가 확신할 만큼 결정적인 순간만 인정한다");
            sb.AppendLine("- exposureReason: exposed=true일 때만 필수. 정확히 무엇 때문에 들켰는지 " +
                          "플레이어가 읽을 한 문장으로 명시 (예: \"경찰 신분증을 실수로 보여줌\"). " +
                          "exposed=false면 필드 생략");
            sb.AppendLine("- stateDelta.vigilance / stateDelta.openness: 경계심·개방도 증감치");
            sb.AppendLine($"- reply: 캐릭터 대사. 최대 3문장, {DialogueResult.MaxReplyLength}자 이내");
            sb.AppendLine();

            sb.AppendLine("[출력 형식]");
            sb.AppendLine("다른 설명이나 마크다운 없이, 아래 형식의 JSON 객체 하나만 출력하라. " +
                          "해당 없는 선택 필드는 생략한다(null을 넣지 않는다).");
            sb.AppendLine(@"{
  ""reply"": ""..."",
  ""weaknessHit"": true,
  ""hitWeaknessId"": ""..."",
  ""resistanceTriggeredId"": ""..."",
  ""codeRevealed"": false,
  ""revealedValue"": ""..."",
  ""suspicionDelta"": 0,
  ""affinityDelta"": 0,
  ""exposed"": false,
  ""exposureReason"": ""..."",
  ""stateDelta"": { ""vigilance"": 0, ""openness"": 0 },
  ""reason"": ""...""
}");

            return sb.ToString();
        }

        // ── 유저 메시지 (턴별 상황 + 플레이어 발화) ───────────
        static string BuildUserMessage(BattleContext ctx)
        {
            var sb = new StringBuilder();

            sb.AppendLine("[현재 상황]");
            sb.AppendLine($"턴: {ctx.Turn}/{ctx.MaxTurn} ({ctx.Phase})");
            sb.AppendLine($"누적 의심도: {ctx.Suspicion}/100");
            sb.AppendLine($"이 조직원과의 친밀도: {ctx.Affinity}/100");
            sb.AppendLine($"이미 코드 획득함: {ctx.CodeAlreadyGot}");
            sb.AppendLine($"대질 전제 충족: {ctx.ConfrontationSatisfied}");
            sb.AppendLine($"경계심: {ctx.Vigilance}/100, 개방도: {ctx.Openness}/100");
            sb.AppendLine();

            if (ctx.Transcript is { Count: > 0 })
            {
                sb.AppendLine("[지금까지 대화]");
                foreach (var line in ctx.Transcript)
                    sb.AppendLine(line);
                sb.AppendLine();
            }

            sb.AppendLine("[플레이어의 이번 발화]");
            sb.AppendLine($"사용한 프레이밍: {(string.IsNullOrEmpty(ctx.UsedFrameId) ? "없음" : ctx.UsedFrameId)}");
            sb.AppendLine($"사용한 키워드 카드: " +
                          $"{(ctx.UsedKeywords is { Count: > 0 } ? string.Join(", ", ctx.UsedKeywords) : "없음")}");
            sb.AppendLine($"최종 문장: \"{ctx.PlayerText}\"");
            sb.AppendLine();
            sb.AppendLine("위 발화에 캐릭터로서 반응하고, 판정 결과를 JSON으로만 출력하라.");

            return sb.ToString();
        }
    }
}
