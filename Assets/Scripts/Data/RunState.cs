using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TopDogDetective.Data
{
    /// <summary>탈출 엔딩 분기. 규격서 v1.4 §3-6. 보스는 항상 추격.</summary>
    public enum EscapeEnding
    {
        A_FullTrust,     // 친밀 100% 3명 → 보스만 추격
        B_TwoFriends,    // 2명 → 보스 + 1인
        C_OneFriend,     // 1명 → 보스 + 2인
        D_Alone          // 0명 → 보스 + 3인
    }

    /// <summary>
    /// 런 전체에 걸쳐 유지되는 상태. 규격서 v1.4 §4.
    /// 전투(BattleSession)를 넘나들며 의심도·조직원별 친밀도·획득 코드를 보관한다.
    ///
    /// [주의] 이 클래스는 게임 진행의 단일 진실 원천(single source of truth)이다.
    /// UI는 이 값을 읽기만 하고, 갱신은 반드시 Commit() 경로로만 이뤄져야 한다.
    /// </summary>
    [Serializable]
    public class RunState
    {
        public const int TotalCodeDigits = 3;
        public const int MaxSuspicion    = GameConstants.MaxGaugeValue;
        public const int TotalEnemies    = 3;

        [SerializeField] int suspicion;                            // 런 누적 의심도 0~100
        readonly Dictionary<string, int> enemyAffinity = new();    // enemyId → 0~100
        readonly Dictionary<int, string> acquiredCodes = new();    // codeIndex(1~3) → 값
        readonly HashSet<string> ownedKeywordIds = new();          // 탐색·대화로 얻은 키워드

        public bool HasBossRoomKey { get; private set; }
        public bool BombDefused    { get; private set; }

        // ── 의심도 ───────────────────────────────────────────
        public int  Suspicion => suspicion;
        public bool IsExposed => suspicion >= MaxSuspicion;

        public void SetSuspicion(int value)
            => suspicion = Mathf.Clamp(value, 0, MaxSuspicion);

        /// <summary>안가 휴식 등으로 의심도 회복. 양수를 넘겨도 감소 처리한다.</summary>
        public void ReduceSuspicion(int amount)
            => SetSuspicion(suspicion - Mathf.Abs(amount));

        // ── 친밀도 ───────────────────────────────────────────
        /// <summary>친밀도 기록이 존재하는지. 초기값 주입 판단에 쓴다(0과 구분).</summary>
        public bool HasAffinityRecord(string enemyId)
            => !string.IsNullOrEmpty(enemyId) && enemyAffinity.ContainsKey(enemyId);

        public int GetAffinity(string enemyId)
            => !string.IsNullOrEmpty(enemyId) && enemyAffinity.TryGetValue(enemyId, out var v) ? v : 0;

        public void SetAffinity(string enemyId, int value)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                Debug.LogWarning("[RunState] enemyId가 비어 친밀도를 저장하지 못했습니다.");
                return;
            }
            enemyAffinity[enemyId] = Mathf.Clamp(value, 0, DialogueResult.AffinityMaxValue);
        }

        public bool IsAffinityMaxed(string enemyId)
            => GetAffinity(enemyId) >= DialogueResult.AffinityMaxValue;

        /// <summary>친밀도 100%를 달성한 조직원 수 (= 추격에서 빠지는 인원).</summary>
        public int MaxedAffinityCount
            => enemyAffinity.Count(kv => kv.Value >= DialogueResult.AffinityMaxValue);

        public IReadOnlyDictionary<string, int> AllAffinity => enemyAffinity;

        // ── 키워드 카드 ──────────────────────────────────────
        public IReadOnlyCollection<string> OwnedKeywords => ownedKeywordIds;

        public bool HasKeyword(string keywordId)
            => !string.IsNullOrEmpty(keywordId) && ownedKeywordIds.Contains(keywordId);

        /// <summary>탐색·대화로 키워드 카드 획득. 중복은 무시된다.</summary>
        public bool AcquireKeyword(string keywordId)
            => !string.IsNullOrEmpty(keywordId) && ownedKeywordIds.Add(keywordId);

        // ── 코드 ─────────────────────────────────────────────
        public bool HasCode(int codeIndex) => acquiredCodes.ContainsKey(codeIndex);

        /// <summary>코드 1자리 획득. 잘못된 인덱스는 경고를 남긴다(조용히 삼키지 않는다).</summary>
        public bool AcquireCode(int codeIndex, string value)
        {
            if (codeIndex < 1 || codeIndex > TotalCodeDigits)
            {
                Debug.LogError($"[RunState] 잘못된 codeIndex={codeIndex} — EnemyData.secret.codeIndex를 확인하세요.");
                return false;
            }
            if (string.IsNullOrEmpty(value))
            {
                Debug.LogError($"[RunState] codeIndex={codeIndex}의 값이 비어 있습니다.");
                return false;
            }

            if (acquiredCodes.TryGetValue(codeIndex, out var exist) && exist != value)
                Debug.LogWarning($"[RunState] codeIndex={codeIndex} 값이 '{exist}'→'{value}'로 덮어써집니다.");

            acquiredCodes[codeIndex] = value;
            return true;
        }

        public string GetCode(int codeIndex)
            => acquiredCodes.TryGetValue(codeIndex, out var v) ? v : null;

        public bool HasAllCodes => acquiredCodes.Count >= TotalCodeDigits;
        public int  AcquiredCodeCount => acquiredCodes.Count;

        /// <summary>보스방 단말기 표시용 문자열. 미획득 자리는 '_'.</summary>
        public string ComposeCode()
            => string.Concat(Enumerable.Range(1, TotalCodeDigits)
                                       .Select(i => GetCode(i) ?? "_"));

        /// <summary>플레이어 입력이 정답인지 검증 (대소문자 무시).</summary>
        public bool VerifyCode(string input)
        {
            if (!HasAllCodes || string.IsNullOrEmpty(input)) return false;
            return string.Equals(input.Trim(), ComposeCode(),
                                 StringComparison.OrdinalIgnoreCase);
        }

        public void GrantBossRoomKey() => HasBossRoomKey = true;

        /// <summary>보스방 진입 조건: 코드 3자리 + 열쇠.</summary>
        public bool CanEnterBossRoom => HasAllCodes && HasBossRoomKey;

        public void MarkBombDefused() => BombDefused = true;

        // ── 엔딩 판정 ────────────────────────────────────────
        public EscapeEnding ResolveEnding() => MaxedAffinityCount switch
        {
            >= 3 => EscapeEnding.A_FullTrust,
            2    => EscapeEnding.B_TwoFriends,
            1    => EscapeEnding.C_OneFriend,
            _    => EscapeEnding.D_Alone
        };

        /// <summary>추격자 수 = 보스(1) + 친밀 미달성 조직원.</summary>
        public int ChaserCount(int totalEnemies = TotalEnemies)
            => 1 + Mathf.Max(0, totalEnemies - MaxedAffinityCount);

        /// <summary>추격에서 빠지는 조직원 id 목록 (연출용).</summary>
        public IEnumerable<string> FriendlyEnemyIds
            => enemyAffinity.Where(kv => kv.Value >= DialogueResult.AffinityMaxValue)
                            .Select(kv => kv.Key);

        // ── 전투 결과 반영 ───────────────────────────────────
        /// <summary>
        /// 한 턴의 판정 결과를 런 상태에 커밋한다.
        /// BattleSession이 호출하므로 UI·파이프라인은 직접 부르지 않는다.
        /// </summary>
        public void Commit(string enemyId, DialogueResult result, int codeIndex)
        {
            if (result == null)
            {
                Debug.LogWarning("[RunState] Commit에 null 결과가 전달됐습니다.");
                return;
            }

            SetSuspicion(result.currentSuspicion);
            SetAffinity(enemyId, result.currentAffinity);

            if (result.codeRevealed && !string.IsNullOrEmpty(result.revealedValue))
                AcquireCode(codeIndex, result.revealedValue);
        }

        public void Reset()
        {
            suspicion = 0;
            enemyAffinity.Clear();
            acquiredCodes.Clear();
            ownedKeywordIds.Clear();
            HasBossRoomKey = false;
            BombDefused    = false;
        }
    }
}
