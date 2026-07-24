using System;
using UnityEngine;

namespace TopDogDetective.Data
{
    /// <summary>턴 종료 결과. 규격서 v1.4 §3-5.</summary>
    public enum TurnOutcome
    {
        Continue,       // 다음 턴 진행
        Success,        // 코드 획득 후 정상 종료
        FailedExposed,  // 발각 → 게임오버
        FailedTimeout   // 3턴 소진·코드 미획득 (재도전 가능)
    }

    /// <summary>
    /// LLM 판정 결과 + 코드가 계산한 최종 상태. 규격서 v1.4 기준.
    ///
    /// [원칙]
    ///  1) LLM은 delta만 반환. 누적값(의심·친밀·상태)은 코드가 계산 (LLM 산수 신뢰 금지).
    ///  2) 저항 발동 시 LLM delta를 무시하고 시스템 페널티로 override (중복 가산 방지).
    ///  3) 호출 순서: Sanitize(...) → Resolve(...) → UI 전달.
    ///  4) 모든 게이지는 0~100 스케일 (기획서 v5와 동일).
    /// </summary>
    [Serializable]
    public class DialogueResult
    {
        // ── LLM이 채우는 필드 ────────────────────────────────
        public string reply;
        public bool   weaknessHit;
        public string hitWeaknessId;
        public string resistanceTriggeredId;
        public bool   codeRevealed;            // 이번 턴 신규 획득
        public string revealedValue;
        public int    suspicionDelta;          // -10 ~ +20
        public int    affinityDelta;           // -20 ~ +45  (v1.4)
        public bool   exposed;
        public StateDelta stateDelta;

        /// 🚫 DEBUG ONLY — UI 노출 금지. 릴리즈에서는 로그도 남기지 않는다.
        public string reason;

        // ── 코드가 계산해 채우는 필드 (UI는 이걸 쓴다) ────────
        public string      heldCodeValue;      // 현재 보유 코드 값 (획득 후 유지)
        public int         currentSuspicion;   // 누적 의심도 0~100
        public int         currentAffinity;    // 현재 친밀도 0~100 (v1.4)
        public bool        affinityMaxed;      // 100% 도달 (v1.4)
        public StateDelta  currentState;
        public TurnOutcome turnOutcome = TurnOutcome.Continue;

        [Serializable]
        public class StateDelta
        {
            public int vigilance;
            public int openness;
        }

        // ── 규격서 §3: 코드가 강제하는 규칙 (100 스케일) ─────
        public const int SuspicionDeltaMin = -10;
        public const int SuspicionDeltaMax = 20;
        public const int AffinityDeltaMin  = -20;
        public const int AffinityDeltaMax  = 45;
        public const int TurnSuspicionCap  = 30;   // 턴당 의심 '증가' 상한
        public const int ExposureThreshold = GameConstants.MaxGaugeValue;  // 발각 임계치 (= 100%)
        public const int AffinityMaxValue  = GameConstants.MaxGaugeValue;
        public const int MaxReplyLength    = 120;
        public const int TurnBaseSuspicion = 3;    // 매 턴 기본 가산
        public const int MinTurnForCode    = 2;    // 코드 획득 가능 최소 턴

        /// <summary>
        /// LLM 응답 보정 + 필드 정합성 강제. 파싱 직후 반드시 호출.
        /// </summary>
        /// <param name="enemy">해당 조직원 (id·저항 검증). null 허용</param>
        /// <param name="currentTurn">현재 턴 (1부터)</param>
        /// <param name="alreadyRevealed">이 조직원의 코드를 이미 획득했는지</param>
        /// <param name="confrontationSatisfied">
        /// 대질 전제 충족 여부. false면 코드 획득을 무조건 차단(측근 가드).
        /// 대질 전제가 없는 조직원은 항상 true를 넘긴다.
        /// </param>
        public void Sanitize(EnemyData enemy, int currentTurn, bool alreadyRevealed,
                             bool confrontationSatisfied = true)
        {
            reply ??= string.Empty;
            stateDelta ??= new StateDelta();

            // ── 약점 정합성 ─────────────────────────────────
            if (!weaknessHit)
                hitWeaknessId = null;

            if (!string.IsNullOrEmpty(hitWeaknessId)
                && enemy != null && !enemy.HasWeakness(hitWeaknessId))
            {
                hitWeaknessId = null;
                weaknessHit   = false;
            }

            // ── 저항 정합성 (null이면 페널티 0이 구조적으로 보장) ──
            if (!string.IsNullOrEmpty(resistanceTriggeredId)
                && enemy != null && !enemy.HasResistance(resistanceTriggeredId))
            {
                resistanceTriggeredId = null;
            }

            // ── 코드 획득 정합성 ────────────────────────────
            //   ① 턴 2 이상 + 약점 적중일 때만
            if (codeRevealed && (currentTurn < MinTurnForCode || !weaknessHit))
                codeRevealed = false;

            //   ② 대질 전제 미충족이면 절대 불가 (측근 가드)
            if (codeRevealed && !confrontationSatisfied)
                codeRevealed = false;

            //   ③ 이미 획득했으면 '신규 획득'으로 다시 오지 않는다
            if (alreadyRevealed)
                codeRevealed = false;

            //   ④ 획득했다면서 값이 비어 있으면 강등
            if (codeRevealed && string.IsNullOrEmpty(revealedValue))
                codeRevealed = false;

            if (!codeRevealed)
                revealedValue = null;

            // ── 수치 클램프 ─────────────────────────────────
            suspicionDelta = Mathf.Clamp(suspicionDelta, SuspicionDeltaMin, SuspicionDeltaMax);
            affinityDelta  = Mathf.Clamp(affinityDelta,  AffinityDeltaMin,  AffinityDeltaMax);

            // 최후 안전장치: reply 길이 (1차 제어는 프롬프트)
            if (reply.Length > MaxReplyLength)
                reply = reply.Substring(0, MaxReplyLength - 1) + "…";
        }

        /// <summary>
        /// 누적값 계산 + turnOutcome 결정. Sanitize() 다음에 호출.
        /// </summary>
        /// <param name="prevSuspicion">런 누적 의심도 (0~100)</param>
        /// <param name="prevAffinity">이 조직원의 기존 친밀도 (0~100)</param>
        /// <param name="enemy">조직원 데이터. null 허용</param>
        /// <param name="currentTurn">현재 턴 (1부터)</param>
        /// <param name="maxTurn">최대 턴 (기본 3)</param>
        /// <param name="sessionCodeValue">
        /// 이 전투에서 '이번 턴 반영 전'까지 확보한 코드 값 (없으면 null).
        /// 이번 턴 신규 획득분은 codeRevealed로 별도 반영한다.
        /// </param>
        public void Resolve(int prevSuspicion, int prevAffinity, EnemyData enemy,
                            int currentTurn, int maxTurn, string sessionCodeValue)
        {
            stateDelta ??= new StateDelta();

            int resistancePenalty = enemy?.GetResistancePenalty(resistanceTriggeredId) ?? 0;

            // ── 의심도 ───────────────────────────────────────
            // 저항 발동 시 LLM delta 무시하고 페널티로 대체 (중복 가산 방지)
            int applied = resistancePenalty > 0 ? resistancePenalty : suspicionDelta;

            if (applied >= 0)
            {
                // 증가분에만 상한을 건다 (기본 가산 포함). 감소는 그대로.
                int rise = Mathf.Min(applied + TurnBaseSuspicion, TurnSuspicionCap);
                currentSuspicion = Mathf.Clamp(prevSuspicion + rise, 0, ExposureThreshold);
            }
            else
            {
                // 무마 등 감소 턴: 기본 가산과 상쇄해 순변화 반영
                int change = applied + TurnBaseSuspicion;
                currentSuspicion = Mathf.Clamp(prevSuspicion + change, 0, ExposureThreshold);
            }

            // ── 친밀도 ───────────────────────────────────────
            currentAffinity = Mathf.Clamp(prevAffinity + affinityDelta, 0, AffinityMaxValue);
            affinityMaxed   = currentAffinity >= AffinityMaxValue;

            // ── 조직원 상태 (null 방어) ──────────────────────
            var st = enemy?.state;
            if (st != null)
            {
                st.vigilance = Mathf.Clamp(st.vigilance + stateDelta.vigilance, 0, 100);
                st.openness  = Mathf.Clamp(st.openness  + stateDelta.openness,  0, 100);
                currentState = new StateDelta { vigilance = st.vigilance, openness = st.openness };
            }
            else
            {
                currentState = new StateDelta
                {
                    vigilance = Mathf.Clamp(stateDelta.vigilance, 0, 100),
                    openness  = Mathf.Clamp(stateDelta.openness,  0, 100)
                };
                Debug.LogWarning("[DialogueResult] enemy/state가 null — 상태 갱신 생략");
            }

            // ── 보유 코드 (이번 턴 신규 획득 우선, 없으면 기존 유지) ──
            heldCodeValue = codeRevealed && !string.IsNullOrEmpty(revealedValue)
                            ? revealedValue
                            : sessionCodeValue;

            // ── 턴 결과 ──────────────────────────────────────
            turnOutcome = DecideOutcome(currentSuspicion, exposed, currentTurn, maxTurn,
                                        !string.IsNullOrEmpty(heldCodeValue));
        }

        static TurnOutcome DecideOutcome(int suspicion, bool isExposed,
                                         int currentTurn, int maxTurn, bool hasCode)
        {
            if (isExposed || suspicion >= ExposureThreshold)
                return TurnOutcome.FailedExposed;

            if (currentTurn >= maxTurn)
                return hasCode ? TurnOutcome.Success : TurnOutcome.FailedTimeout;

            return TurnOutcome.Continue;
        }

        /// <summary>
        /// 파싱 실패·통신 오류 시 폴백. 마지막 턴에 호출돼도 Continue로 새지 않는다.
        /// 오류는 플레이어 책임이 아니므로 의심·친밀 변동을 주지 않는다.
        /// </summary>
        public static DialogueResult Fallback(int prevSuspicion, int prevAffinity,
                                              int currentTurn, int maxTurn,
                                              string sessionCodeValue, string message = null)
        {
            bool hasCode = !string.IsNullOrEmpty(sessionCodeValue);
            return new DialogueResult
            {
                reply                 = message ?? "…무슨 소리야, 그게.",
                weaknessHit           = false,
                hitWeaknessId         = null,
                resistanceTriggeredId = null,
                codeRevealed          = false,
                revealedValue         = null,
                suspicionDelta        = 0,
                affinityDelta         = 0,
                exposed               = false,
                heldCodeValue         = sessionCodeValue,
                currentSuspicion      = Mathf.Clamp(prevSuspicion, 0, ExposureThreshold),
                currentAffinity       = Mathf.Clamp(prevAffinity, 0, AffinityMaxValue),
                affinityMaxed         = prevAffinity >= AffinityMaxValue,
                stateDelta            = new StateDelta(),
                currentState          = new StateDelta(),
                turnOutcome           = DecideOutcome(prevSuspicion, false, currentTurn, maxTurn, hasCode),
                reason                = "FALLBACK: 응답 파싱 실패 또는 통신 오류"
            };
        }
    }
}
