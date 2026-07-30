using System;
using System.Collections.Generic;
using System.Linq;

namespace TopDogDetective.Data
{
    /// <summary>프레이밍 카드 ID. 규격서 §0(2026-07-24 회의 확정, 6종 추가). 표시명은 UI에서만 치환.</summary>
    public static class FrameIds
    {
        public const string SmallTalk       = "small_talk";        // 친근한 잡담
        public const string Praise          = "praise";            // 치켜세우기
        public const string Empathy         = "empathy";           // 불만 공감
        public const string Complicity      = "complicity";        // 공범 의식
        public const string Sympathy        = "sympathy";          // 동정심 유발
        public const string Authority       = "authority";         // 권위 사칭
        public const string Urgency         = "urgency";           // 긴급 상황
        public const string FalsePremise    = "false_premise";     // 거짓 전제
        public const string SelfDisclosure  = "self_disclosure";   // 자기 고백
        public const string Reciprocity     = "reciprocity";       // 거래제안
        public const string InformationLeak = "information_leak";  // 정보 흘리기
        public const string Confrontation   = "confrontation";     // 대질
        public const string Reassurance     = "reassurance";       // 안심시키기
        public const string TopicShift      = "topic_shift";       // 화제 전환

        public static readonly HashSet<string> All = new()
        {
            SmallTalk, Praise, Empathy, Complicity,
            Sympathy, Authority, Urgency, FalsePremise,
            SelfDisclosure, Reciprocity, InformationLeak,
            Confrontation, Reassurance, TopicShift
        };

        static readonly Dictionary<string, string> DisplayNames = new()
        {
            [SmallTalk]       = "친근한 잡담",
            [Praise]          = "치켜세우기",
            [Empathy]         = "불만 공감",
            [Complicity]      = "공범 의식",
            [Sympathy]        = "동정심 유발",
            [Authority]       = "권위 사칭",
            [Urgency]         = "긴급 상황",
            [FalsePremise]    = "거짓 전제",
            [SelfDisclosure]  = "자기 고백",
            [Reciprocity]     = "거래제안",
            [InformationLeak] = "정보 흘리기",
            [Confrontation]   = "대질",
            [Reassurance]     = "안심시키기",
            [TopicShift]      = "화제 전환",
        };

        /// <summary>ID → 표시명. 미등록이면 ID를 그대로 반환.</summary>
        public static string ToDisplayName(string frameId)
            => frameId != null && DisplayNames.TryGetValue(frameId, out var n) ? n : frameId;

        public static bool IsValid(string frameId)
            => !string.IsNullOrEmpty(frameId) && All.Contains(frameId);
    }

    /// <summary>
    /// 조직원 1명의 정의. 규격서 v1.4 기준.
    /// 로드 직후 반드시 Validate()로 데이터 오류를 잡는다.
    /// </summary>
    [Serializable]
    public class EnemyData
    {
        public const int MinDifficulty = 1;
        public const int MaxDifficulty = 3;
        public const int MinCodeIndex  = 1;
        public const int MaxCodeIndex  = 3;
        public const int MinTier       = 1;   // A
        public const int MaxTier       = 3;   // C

        public string id;
        public string displayName;
        public int    tier;          // 등장 순서 (A=1, B=2, C=3)
        public int    difficulty;    // 1~3 (UI에서 ★로 치환)

        public Persona  persona;
        public Secret   secret;
        public List<Weakness>   weaknesses  = new();
        public List<Resistance> resistances = new();
        public AffinityConfig   affinity;
        public EnemyState state;

        /// <summary>
        /// 대질 전제 (측근 전용, 선택). 여기에 나열된 키워드 카드를 하나라도
        /// 보유하지 않으면 코드가 절대 뚫리지 않도록 코드가 강제한다.
        /// null 또는 빈 목록이면 대질 전제 없음(일반 조직원).
        /// </summary>
        public List<string> requiredConfrontationKeywords = new();

        [Serializable]
        public class Persona
        {
            public string age;
            public string personality;
            public string speechStyle;
            public string relationToPlayer;
        }

        [Serializable]
        public class Secret
        {
            public int    codeIndex;        // 1~3
            public string codeValue;        // 영문자/숫자 1글자
            public string gagOrder;         // 함구령 내용
            public bool   hasBossRoomKey;   // 측근 1명만 true
        }

        [Serializable]
        public class Weakness
        {
            public string id;
            public string label;
            public string trigger;
            public List<string> effectiveFrameIds = new();  // 코드를 흘리는 통로

            /// <summary>
            /// 이 약점이 적중하면 지급되는 키워드 카드 id (선택).
            /// "정보 흘리기·대질" — 다른 조직원에게 얻은 정보를 측근
            /// requiredConfrontationKeywords에 꽂아 넣는 통로. 없으면 지급 없음.
            /// </summary>
            public string revealsKeywordId;
        }

        [Serializable]
        public class Resistance
        {
            public string id;
            public string label;
            public string trigger;
            public int    suspicionPenalty;  // 0~30 (100 스케일)
        }

        /// <summary>친밀도 설정 (v1.4 신규).</summary>
        [Serializable]
        public class AffinityConfig
        {
            public int initial;                            // 초기 친밀도 0~100
            public List<string> openingFrameIds  = new();  // 마음을 여는 프레이밍
            public List<string> betrayalFrameIds = new();  // 정이 떨어지는 프레이밍
        }

        [Serializable]
        public class EnemyState
        {
            public int vigilance;   // 경계심 0~100
            public int openness;    // 개방도 0~100
        }

        // ── 조회 ─────────────────────────────────────────────
        public bool HasWeakness(string weaknessId)
            => !string.IsNullOrEmpty(weaknessId)
               && weaknesses != null && weaknesses.Exists(w => w.id == weaknessId);

        public Weakness FindWeakness(string weaknessId)
            => string.IsNullOrEmpty(weaknessId) || weaknesses == null
               ? null : weaknesses.Find(w => w.id == weaknessId);

        public bool HasResistance(string resistanceId)
            => !string.IsNullOrEmpty(resistanceId)
               && resistances != null && resistances.Exists(r => r.id == resistanceId);

        public Resistance FindResistance(string resistanceId)
            => string.IsNullOrEmpty(resistanceId) || resistances == null
               ? null : resistances.Find(r => r.id == resistanceId);

        /// <summary>저항 페널티. 미발동·미등록이면 0.</summary>
        public int GetResistancePenalty(string resistanceId)
            => FindResistance(resistanceId)?.suspicionPenalty ?? 0;

        public bool IsOpeningFrame(string frameId)
            => !string.IsNullOrEmpty(frameId)
               && (affinity?.openingFrameIds?.Contains(frameId) ?? false);

        public bool IsBetrayalFrame(string frameId)
            => !string.IsNullOrEmpty(frameId)
               && (affinity?.betrayalFrameIds?.Contains(frameId) ?? false);

        /// <summary>대질 전제가 있는 조직원(측근)인지.</summary>
        public bool RequiresConfrontation
            => requiredConfrontationKeywords != null
               && requiredConfrontationKeywords.Count > 0;

        /// <summary>
        /// 대질 전제 충족 여부. 필요한 키워드를 하나라도 보유하면 true.
        /// 전제가 없으면 항상 true.
        /// </summary>
        public bool IsConfrontationSatisfied(ICollection<string> ownedKeywordIds)
        {
            if (!RequiresConfrontation) return true;
            if (ownedKeywordIds == null || ownedKeywordIds.Count == 0) return false;
            return requiredConfrontationKeywords.Any(ownedKeywordIds.Contains);
        }

        /// <summary>UI 표시용 별 문자열.</summary>
        public string DifficultyStars
            => new string('★', Math.Clamp(difficulty, MinDifficulty, MaxDifficulty));

        // ── 유효성 검사 (로드 직후 호출) ──────────────────────
        /// <summary>데이터 오류 목록. 비어 있으면 정상.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            string who = string.IsNullOrEmpty(id) ? "(id 없음)" : id;

            if (string.IsNullOrEmpty(id))          errors.Add("id가 비어 있음");
            if (string.IsNullOrEmpty(displayName)) errors.Add($"{who}: displayName이 비어 있음");

            if (tier < MinTier || tier > MaxTier)
                errors.Add($"{who}: tier={tier} — {MinTier}~{MaxTier} 범위여야 함 (A=1, B=2, C=3)");

            if (difficulty < MinDifficulty || difficulty > MaxDifficulty)
                errors.Add($"{who}: difficulty={difficulty} — {MinDifficulty}~{MaxDifficulty} 범위여야 함");

            if (persona == null) errors.Add($"{who}: persona 누락");
            if (state   == null) errors.Add($"{who}: state 누락");
            else
            {
                if (state.vigilance < 0 || state.vigilance > 100)
                    errors.Add($"{who}: state.vigilance={state.vigilance} — 0~100");
                if (state.openness < 0 || state.openness > 100)
                    errors.Add($"{who}: state.openness={state.openness} — 0~100");
            }

            // secret
            if (secret == null)
            {
                errors.Add($"{who}: secret 누락");
            }
            else
            {
                if (secret.codeIndex < MinCodeIndex || secret.codeIndex > MaxCodeIndex)
                    errors.Add($"{who}: codeIndex={secret.codeIndex} — {MinCodeIndex}~{MaxCodeIndex}이어야 함");
                if (string.IsNullOrEmpty(secret.codeValue) || secret.codeValue.Length != 1)
                    errors.Add($"{who}: codeValue는 1글자여야 함 (현재: '{secret.codeValue}')");
                if (string.IsNullOrEmpty(secret.gagOrder))
                    errors.Add($"{who}: gagOrder가 비어 있음");
                if (secret.hasBossRoomKey && !RequiresConfrontation)
                    errors.Add($"{who}: hasBossRoomKey=true(측근)인데 requiredConfrontationKeywords가 " +
                                "비어 있음 — 정면 설득으로 뚫림");
            }

            // weaknesses
            if (weaknesses == null || weaknesses.Count == 0)
            {
                errors.Add($"{who}: weaknesses가 최소 1개 필요");
            }
            else
            {
                var seen = new HashSet<string>();
                foreach (var w in weaknesses)
                {
                    if (w == null) { errors.Add($"{who}: weakness 항목이 null"); continue; }
                    if (!ValidateEntryId(who, "weakness", w.id, seen, errors)) continue;
                    if (string.IsNullOrEmpty(w.trigger)) errors.Add($"{who}/{w.id}: trigger가 비어 있음");

                    if (w.effectiveFrameIds == null || w.effectiveFrameIds.Count == 0)
                    {
                        errors.Add($"{who}/{w.id}: effectiveFrameIds가 비어 있음");
                        continue;
                    }
                    foreach (var f in w.effectiveFrameIds.Where(f => !FrameIds.IsValid(f)))
                        errors.Add($"{who}/{w.id}: 알 수 없는 frameId '{f}' — 표시명 대신 ID를 썼는지 확인");
                }
            }

            // resistances
            if (resistances != null)
            {
                var seen = new HashSet<string>();
                foreach (var r in resistances)
                {
                    if (r == null) { errors.Add($"{who}: resistance 항목이 null"); continue; }
                    if (!ValidateEntryId(who, "resistance", r.id, seen, errors)) continue;
                    if (r.suspicionPenalty < 0 || r.suspicionPenalty > 30)
                        errors.Add($"{who}/{r.id}: suspicionPenalty={r.suspicionPenalty} — 0~30 (100 스케일)");
                }
            }

            // affinity (v1.4)
            if (affinity == null)
            {
                errors.Add($"{who}: affinity 블록 누락 (v1.4 필수)");
            }
            else
            {
                if (affinity.initial < 0 || affinity.initial > 100)
                    errors.Add($"{who}: affinity.initial={affinity.initial} — 0~100");

                if (affinity.openingFrameIds == null || affinity.openingFrameIds.Count == 0)
                    errors.Add($"{who}: affinity.openingFrameIds가 비어 있음 — 친밀 100% 달성 불가");

                foreach (var f in (affinity.openingFrameIds ?? new List<string>())
                                  .Where(f => !FrameIds.IsValid(f)))
                    errors.Add($"{who}: 알 수 없는 openingFrameId '{f}'");

                foreach (var f in (affinity.betrayalFrameIds ?? new List<string>())
                                  .Where(f => !FrameIds.IsValid(f)))
                    errors.Add($"{who}: 알 수 없는 betrayalFrameId '{f}'");
            }

            return errors;
        }

        /// <summary>id 비어있음·중복 체크 공통 처리. false면 호출부에서 해당 항목을 건너뛴다.</summary>
        static bool ValidateEntryId(string who, string entryLabel, string id, HashSet<string> seen, List<string> errors)
        {
            if (string.IsNullOrEmpty(id))
            {
                errors.Add($"{who}: {entryLabel}.id가 비어 있음");
                return false;
            }
            if (!seen.Add(id))
                errors.Add($"{who}: {entryLabel}.id 중복 — {id}");
            return true;
        }
    }
}
