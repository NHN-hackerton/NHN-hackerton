using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TopDogDetective.Data
{
    /// <summary>전투 진행 단계. 3비트 구조(기획서 §4).</summary>
    public enum BattlePhase
    {
        Probe    = 1,   // 1턴 간보기      — 친밀 ↑ (저위험)
        Strike   = 2,   // 2턴 핵심 찌르기 — 코드 획득
        Cover    = 3,   // 3턴 무마·이탈   — 의심 ↓ · 친밀 ↑
        Finished = 4
    }

    /// <summary>플레이어가 제출한 조립문 (UI → 세션).</summary>
    public class PlayerUtterance
    {
        public List<string> KeywordCardIds = new();
        public string FrameId;        // 프레이밍 카드 (없을 수 있음)
        public string FreeTail;       // 자유 어미·어구
        public string ComposedText;   // readback으로 완성된 최종 문장

        /// <summary>집중력 소모량. 미지정(0 이하)이면 카드 수로 자동 계산한다.</summary>
        public int FocusCost;

        /// <summary>기본 비용 = 키워드 카드 수 + 프레이밍 카드(있으면 1).</summary>
        public int ResolveFocusCost()
        {
            if (FocusCost > 0) return FocusCost;
            int cost = KeywordCardIds?.Count ?? 0;
            if (!string.IsNullOrEmpty(FrameId)) cost += 1;
            return Mathf.Max(1, cost);   // 최소 1
        }
    }

    /// <summary>파이프라인이 시스템 프롬프트에 주입할 런타임 상태.</summary>
    public class BattleContext
    {
        public int Turn;
        public int MaxTurn;
        public string Phase;
        public int Suspicion;
        public int Affinity;
        public bool CodeAlreadyGot;
        public bool ConfrontationSatisfied;
        public int Vigilance;
        public int Openness;
        public IReadOnlyList<string> Transcript;
        public string PlayerText;
        public string UsedFrameId;
        public List<string> UsedKeywords;
    }

    /// <summary>
    /// 조직원 1명과의 3턴 전투 상태 관리. 기획서 v5.1 · 규격서 v1.4.
    ///
    /// 역할:
    ///  - 턴 진행(1→3)·페이즈·집중력 관리
    ///  - 제출 전 가드레일(미보유 키워드 차단, 집중력 검사)
    ///  - 대질 전제(측근) 검사 후 판정에 전달
    ///  - LLM 결과를 Sanitize → Resolve → RunState.Commit 순으로 처리
    ///  - 전투 종료·열쇠 지급 판정
    ///
    /// 이 클래스는 LLM 호출을 직접 하지 않는다. 호출은 파이프라인이 담당하고,
    /// 세션은 "무엇을 보낼지"와 "받은 결과를 어떻게 반영할지"만 책임진다.
    /// </summary>
    public class BattleSession
    {
        public const int MaxTurn      = 3;
        public const int FocusPerTurn = 3;   // 턴당 집중력 (기획서 §4)

        public EnemyData Enemy { get; }
        public RunState  Run   { get; }

        public int         CurrentTurn { get; private set; } = 1;
        public int         FocusRemaining { get; private set; } = FocusPerTurn;
        public bool        IsFinished  { get; private set; }
        public TurnOutcome LastOutcome { get; private set; } = TurnOutcome.Continue;

        public BattlePhase Phase => IsFinished
            ? BattlePhase.Finished
            : (BattlePhase)Mathf.Clamp(CurrentTurn, 1, MaxTurn);

        /// <summary>이 전투에서 확보한 코드 값 (없으면 null).</summary>
        public string SessionCodeValue { get; private set; }
        public bool   CodeAcquired => !string.IsNullOrEmpty(SessionCodeValue);

        public int Affinity  => Run.GetAffinity(Enemy.id);
        public int Suspicion => Run.Suspicion;

        /// <summary>대질 전제 충족 여부 (측근 전용 가드). 전제가 없으면 항상 true.</summary>
        public bool ConfrontationSatisfied
            => Enemy.IsConfrontationSatisfied(ownedKeywordIds);

        readonly HashSet<string> ownedKeywordIds;
        readonly List<string> transcript = new();   // LLM 컨텍스트용 대화 기록

        public IReadOnlyList<string> Transcript => transcript;

        public BattleSession(EnemyData enemy, RunState run, IEnumerable<string> ownedKeywords = null)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Run   = run   ?? throw new ArgumentNullException(nameof(run));

            // 보유 키워드는 인자 우선, 없으면 런 상태에서 가져온다.
            ownedKeywordIds = new HashSet<string>(ownedKeywords ?? Run.OwnedKeywords);

            // 초기 친밀도는 '기록이 아예 없을 때'만 주입한다.
            // (0으로 떨어진 재도전에서 초기값으로 되돌아가는 버그 방지)
            if (!Run.HasAffinityRecord(Enemy.id))
                Run.SetAffinity(Enemy.id, Enemy.affinity?.initial ?? 0);

            // 이 조직원의 코드를 이전 세션에서 이미 획득했다면 세션 상태에도 반영한다.
            SessionCodeValue = Run.GetCode(Enemy.secret?.codeIndex ?? 0);
        }

        // ── 제출 전 검사 (코드 가드레일) ─────────────────────
        /// <summary>
        /// 제출 가능 여부. 미보유 키워드·집중력 초과를 차단한다(환각 방지·탐색 강제).
        /// </summary>
        public bool CanSubmit(PlayerUtterance utterance, out string reason)
        {
            reason = null;

            if (IsFinished)        { reason = "이미 종료된 대화입니다.";       return false; }
            if (utterance == null) { reason = "조립문이 없습니다.";            return false; }

            if (string.IsNullOrWhiteSpace(utterance.ComposedText))
            { reason = "문장이 비어 있습니다.";  return false; }

            if (utterance.KeywordCardIds != null)
            {
                foreach (var id in utterance.KeywordCardIds)
                {
                    if (string.IsNullOrEmpty(id))
                    { reason = "빈 키워드 카드가 포함돼 있습니다."; return false; }

                    if (!ownedKeywordIds.Contains(id))
                    { reason = $"보유하지 않은 키워드 카드입니다: {id}"; return false; }
                }

                // 같은 카드를 중복 배치하는 것 방지
                if (utterance.KeywordCardIds.Distinct().Count() != utterance.KeywordCardIds.Count)
                { reason = "같은 키워드 카드를 중복 사용했습니다."; return false; }
            }

            if (!string.IsNullOrEmpty(utterance.FrameId) && !FrameIds.IsValid(utterance.FrameId))
            { reason = $"알 수 없는 프레이밍 카드입니다: {utterance.FrameId}"; return false; }

            int cost = utterance.ResolveFocusCost();
            if (cost > FocusRemaining)
            { reason = $"집중력이 부족합니다. (필요 {cost} / 남은 {FocusRemaining})"; return false; }

            return true;
        }

        // ── LLM에 보낼 컨텍스트 ──────────────────────────────
        public BattleContext BuildContext(PlayerUtterance utterance) => new()
        {
            Turn                   = CurrentTurn,
            MaxTurn                = MaxTurn,
            Phase                  = Phase.ToString(),
            Suspicion              = Run.Suspicion,
            Affinity               = Affinity,
            CodeAlreadyGot         = CodeAcquired,
            ConfrontationSatisfied = ConfrontationSatisfied,
            Vigilance              = Enemy.state?.vigilance ?? 0,
            Openness               = Enemy.state?.openness  ?? 0,
            Transcript             = transcript,
            PlayerText             = utterance?.ComposedText ?? string.Empty,
            UsedFrameId            = utterance?.FrameId,
            UsedKeywords           = utterance?.KeywordCardIds ?? new List<string>()
        };

        // ── 판정 결과 반영 ───────────────────────────────────
        /// <summary>
        /// LLM 응답(또는 null)을 받아 이번 턴을 마무리한다.
        /// 파이프라인은 파싱만 하고, 검증·계산·커밋은 전부 여기서 처리한다.
        /// </summary>
        public DialogueResult ApplyResult(DialogueResult raw, PlayerUtterance utterance)
        {
            if (IsFinished)
            {
                Debug.LogWarning("[BattleSession] 종료된 전투에 결과가 전달됐습니다.");
                return raw;
            }

            int prevSuspicion = Run.Suspicion;
            int prevAffinity  = Affinity;

            var result = raw ?? DialogueResult.Fallback(
                prevSuspicion, prevAffinity, CurrentTurn, MaxTurn, SessionCodeValue);

            // 순서 고정: 정합성 보정 → 누적 계산
            result.Sanitize(Enemy, CurrentTurn, CodeAcquired, ConfrontationSatisfied);
            result.Resolve(prevSuspicion, prevAffinity, Enemy,
                           CurrentTurn, MaxTurn, SessionCodeValue);

            // 이번 턴 신규 획득 반영 (Resolve의 heldCodeValue와 일치)
            if (result.codeRevealed && !string.IsNullOrEmpty(result.revealedValue))
                SessionCodeValue = result.revealedValue;

            // 약점 적중 시 키워드 지급 (정보 흘리기·대질 — 이 조직원에게서 얻은 정보를
            // 측근의 requiredConfrontationKeywords에 꽂아 넣는 통로)
            if (result.weaknessHit && !string.IsNullOrEmpty(result.hitWeaknessId))
            {
                string revealedKeywordId = Enemy.FindWeakness(result.hitWeaknessId)?.revealsKeywordId;
                if (!string.IsNullOrEmpty(revealedKeywordId))
                    Run.AcquireKeyword(revealedKeywordId);
            }

            // 런 상태 커밋 (의심·친밀·코드)
            Run.Commit(Enemy.id, result, Enemy.secret?.codeIndex ?? 0);

            // 대화 기록
            if (utterance != null && !string.IsNullOrWhiteSpace(utterance.ComposedText))
                transcript.Add($"플레이어: {utterance.ComposedText}");
            if (!string.IsNullOrEmpty(result.reply))
                transcript.Add($"{Enemy.displayName}: {result.reply}");

            // 집중력 차감
            if (utterance != null)
                FocusRemaining = Mathf.Max(0, FocusRemaining - utterance.ResolveFocusCost());

            LastOutcome = result.turnOutcome;

            if (result.turnOutcome != TurnOutcome.Continue)
            {
                Finish(result.turnOutcome);
            }
            else
            {
                CurrentTurn++;
                FocusRemaining = FocusPerTurn;   // 매 턴 리셋
            }

            return result;
        }

        void Finish(TurnOutcome outcome)
        {
            IsFinished  = true;
            LastOutcome = outcome;

            // 성공 시 보스방 열쇠 지급 (측근만 보유)
            if (outcome == TurnOutcome.Success && (Enemy.secret?.hasBossRoomKey ?? false))
                Run.GrantBossRoomKey();
        }

        /// <summary>재도전(FailedTimeout 이후). 런 상태(의심·친밀)는 유지된다.</summary>
        public BattleSession CreateRetry(IEnumerable<string> ownedKeywords = null)
            => new(Enemy, Run, ownedKeywords);
    }
}
