using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TopDogDetective.Judge
{
    using Data;

    /// <summary>
    /// 규칙 기반 가짜 판정기. LLM 없이 3턴 루프를 굴리기 위한 개발용 구현.
    ///
    /// 목적:
    ///  1) 프록시·API 없이도 게임이 돌아가게 해 UI 작업을 병렬 진행
    ///  2) 문제 발생 시 "AI 탓인지 내 코드 탓인지" 즉시 구분
    ///  3) 밸런싱 목표(완벽 플레이 = 친밀 100%)를 수치로 검증
    ///
    /// [주의] 이 클래스는 절대 릴리즈 빌드의 기본값이 되면 안 된다.
    ///        전환은 IDialogueJudge 구현체 교체로만 한다.
    /// </summary>
    public class MockDialogueJudge : IDialogueJudge
    {
        public string Name => "Mock (규칙 기반)";

        // ── 튜닝 값 (규격서 3-3·3-4절 기준) ──────────────────
        [Serializable]
        public class Settings
        {
            [Header("응답 지연 (실제 LLM 체감 재현)")]
            public float minDelay = 0.6f;
            public float maxDelay = 1.4f;

            [Header("친밀도")]
            public int affinityOnOpeningFrame = 40;   // 마음을 여는 프레이밍 적중
            public int affinityOnNeutral      = 10;   // 무난한 대화
            public int affinityOnCodePush     = 20;   // 코드 압박(약점 적중 동반)
            public int affinityOnBetrayal     = -15;  // 정 떨어지는 접근

            [Header("의심도")]
            public int suspicionOnNatural   = 1;      // 자연스러운 접근
            public int suspicionOnCodePush  = 8;      // 에둘러 코드 접근
            public int suspicionOnCover     = -12;    // 3턴 무마 성공

            [Header("코드 요구 판정")]
            [Tooltip("이 키워드를 프레이밍 없이 쓰면 '직설적 요구'로 간주")]
            public List<string> codeKeywordIds = new() { "kw_code_digit", "kw_password" };

            [Tooltip("직설 요구 시 발동시킬 저항 id (조직원 데이터에 있어야 함)")]
            public string directDemandResistanceId = "direct_demand";
        }

        public Settings Config { get; }
        readonly System.Random rng;

        public MockDialogueJudge(Settings config = null, int seed = 0)
        {
            Config = config ?? new Settings();
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public IEnumerator Judge(BattleSession session, PlayerUtterance utterance,
                                 Action<DialogueResult> onDone)
        {
            if (session == null || utterance == null)
            {
                onDone?.Invoke(null);   // → ApplyResult가 폴백 처리
                yield break;
            }

            // 실제 LLM 체감을 재현해, 로딩 연출을 미리 검증할 수 있게 한다.
            float delay = Mathf.Lerp(Config.minDelay, Config.maxDelay, (float)rng.NextDouble());
            yield return new WaitForSeconds(delay);

            onDone?.Invoke(Evaluate(session, utterance));
        }

        /// <summary>즉시 판정 (테스트·에디터용). 지연 없이 결과만 계산한다.</summary>
        public DialogueResult Evaluate(BattleSession session, PlayerUtterance utterance)
        {
            var enemy = session.Enemy;
            int turn  = session.CurrentTurn;
            string frame = utterance.FrameId;
            var keywords = utterance.KeywordCardIds ?? new List<string>();

            var result = new DialogueResult
            {
                stateDelta = new DialogueResult.StateDelta()
            };

            // ── 접근 유형 분류 ───────────────────────────────
            bool usesCodeKeyword = keywords.Any(k => Config.codeKeywordIds.Contains(k));
            bool isOpening       = enemy.IsOpeningFrame(frame);
            bool isBetrayal      = enemy.IsBetrayalFrame(frame);
            bool frameHitsWeak   = enemy.weaknesses
                                        .Any(w => w.effectiveFrameIds != null
                                               && w.effectiveFrameIds.Contains(frame));

            // 프레이밍 없이 코드를 요구 = 직설적 요구 → 저항 발동
            bool isDirectDemand = usesCodeKeyword && string.IsNullOrEmpty(frame);

            // ── 약점 적중 ────────────────────────────────────
            if (frameHitsWeak && !isDirectDemand)
            {
                result.weaknessHit = true;
                result.hitWeaknessId = enemy.weaknesses
                    .First(w => w.effectiveFrameIds != null
                             && w.effectiveFrameIds.Contains(frame)).id;
            }

            // ── 저항 발동 ────────────────────────────────────
            if (isDirectDemand && enemy.HasResistance(Config.directDemandResistanceId))
                result.resistanceTriggeredId = Config.directDemandResistanceId;

            // ── 의심도 ───────────────────────────────────────
            // 저항이 발동하면 Resolve가 페널티로 대체하므로 여기 값은 무시된다.
            if (isDirectDemand)
                result.suspicionDelta = DialogueResult.SuspicionDeltaMax;
            else if (usesCodeKeyword)
                result.suspicionDelta = Config.suspicionOnCodePush;
            else if (turn >= BattleSession.MaxTurn)
                result.suspicionDelta = Config.suspicionOnCover;   // 3턴 무마
            else
                result.suspicionDelta = Config.suspicionOnNatural;

            // ── 친밀도 ───────────────────────────────────────
            if (isBetrayal)
                result.affinityDelta = Config.affinityOnBetrayal;
            else if (isOpening)
                result.affinityDelta = Config.affinityOnOpeningFrame;
            else if (usesCodeKeyword && result.weaknessHit)
                result.affinityDelta = Config.affinityOnCodePush;
            else if (isDirectDemand)
                result.affinityDelta = Config.affinityOnBetrayal;
            else
                result.affinityDelta = Config.affinityOnNeutral;

            // ── 코드 획득 ────────────────────────────────────
            // 최종 판단은 Sanitize가 하지만(턴·대질 전제), 여기서도 조건을 맞춰둔다.
            bool wantsCode = usesCodeKeyword && result.weaknessHit
                             && turn >= DialogueResult.MinTurnForCode;

            if (wantsCode && session.ConfrontationSatisfied && !session.CodeAcquired)
            {
                result.codeRevealed  = true;
                result.revealedValue = enemy.secret?.codeValue;
            }

            // ── 상태 변화 ────────────────────────────────────
            if (isOpening)
            {
                result.stateDelta.vigilance = -8;
                result.stateDelta.openness  = 12;
            }
            else if (isDirectDemand)
            {
                result.stateDelta.vigilance = 15;
                result.stateDelta.openness  = -10;
            }

            // ── 발각 ─────────────────────────────────────────
            // Mock에서는 누적 의심으로만 실패하게 두고, 즉시 발각은 만들지 않는다.
            result.exposed = false;

            // ── 대사 ─────────────────────────────────────────
            result.reply  = BuildReply(enemy, result, isDirectDemand, isOpening, isBetrayal);
            result.reason = BuildReason(frame, isDirectDemand, isOpening, isBetrayal,
                                        result.weaknessHit, result.codeRevealed);

            return result;
        }

        // ── 대사 템플릿 ──────────────────────────────────────
        string BuildReply(EnemyData enemy, DialogueResult r,
                          bool direct, bool opening, bool betrayal)
        {
            string who = enemy.displayName;

            if (direct)
                return Pick(
                    "…그건 왜 묻는 겁니까. 형님이 시킨 것도 아닐 텐데.",
                    "그런 건 함부로 입에 올리는 거 아닙니다. 누구 죽는 꼴 보려고요?",
                    "…지금 저 떠보는 겁니까?");

            if (r.codeRevealed)
                return Pick(
                    $"아 그거요? …아, 제가 지금 뭐라고. 못 들은 걸로 해주십쇼.",
                    $"…{r.revealedValue}. 아니 잠깐, 이거 말하면 안 되는 건데.",
                    $"형님이니까 말하는 건데… 제 자리는 {r.revealedValue}입니다.");

            if (betrayal)
                return Pick(
                    "…말씀이 좀 그러시네요.",
                    "제가 그렇게 만만해 보입니까.",
                    "…됐습니다. 그런 얘기는 그만하시죠.");

            if (opening)
                return Pick(
                    "아이고 형님, 그런 말씀을… 제가 뭐 대단한 것도 아닌데요.",
                    "형님이 그렇게 봐주시니 몸 둘 바를 모르겠습니다.",
                    "역시 형님밖에 없습니다. 다들 저 무시하는데.");

            if (r.weaknessHit)
                return Pick(
                    "…뭐, 틀린 말씀은 아니네요.",
                    "그렇게 말씀하시니 좀 그렇긴 합니다만.",
                    "음… 형님 말씀이 맞긴 하죠.");

            return Pick(
                "예, 그렇습니까.",
                "…그렇군요.",
                $"{who}는 잠시 말을 고르는 듯했다.");
        }

        string BuildReason(string frame, bool direct, bool opening, bool betrayal,
                           bool weakHit, bool code)
        {
            var parts = new List<string> { $"frame={frame ?? "없음"}" };
            if (direct)   parts.Add("직설적 코드 요구 → 저항 발동");
            if (opening)  parts.Add("마음을 여는 프레이밍 적중");
            if (betrayal) parts.Add("정 떨어지는 접근");
            if (weakHit)  parts.Add("약점 적중");
            if (code)     parts.Add("코드 흘림");
            return "[MOCK] " + string.Join(" / ", parts);
        }

        string Pick(params string[] options) => options[rng.Next(options.Length)];
    }
}
