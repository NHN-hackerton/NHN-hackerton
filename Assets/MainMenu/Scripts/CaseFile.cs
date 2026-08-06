using System.Collections.Generic;
using TopDogDetective.Data;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 수사기록. 조직원 3인 × 조각 3종(물증·자백·속내) = 9칸.
    ///
    /// 조각은 별도로 저장하지 않고 <see cref="RunState"/>에서 파생시킨다.
    /// (단일 진실 원천을 늘리지 않는다 — 친밀·코드는 이미 RunState가 갖고 있다)
    ///
    ///  · 물증: 그 라운드 탐색에서 얻는 키워드를 전부 모았는가
    ///  · 자백: 그 조직원의 코드 자리를 확보했는가
    ///  · 속내: 그 조직원 친밀 100% — 진엔딩의 조건
    /// </summary>
    public static class CaseFile
    {
        public enum Kind { Evidence, Confession, Truth }   // 물증 · 자백 · 속내

        /// <summary>조각 한 칸.</summary>
        public class Slot
        {
            public Kind kind;
            public string title;
            public string body;        // 채웠을 때 읽히는 내용
            public bool filled;
        }

        /// <summary>조직원 한 명분 기록.</summary>
        public class Entry
        {
            public string enemyId;
            public string displayName;
            public int codeIndex;
            public string[] clueKeywords;   // 그 라운드 탐색 키워드 (물증 조건)
            public Slot[] slots;
        }

        // ── 조직원별 조각 원문 ────────────────────────────────
        static readonly string[] EnemyIds = { "member_a_rookie", "member_b_keeper", "member_c_lieutenant" };
        static readonly string[] Names    = { "신참", "금고지기", "보스 측근" };
        static readonly int[] CodeIndexes = { 1, 2, 3 };

        static readonly string[][] ClueSets = {
            new[] { "kw_code_digit", "kw_rookie_pride", "kw_password" },
            new[] { "kw_code_digit", "kw_drink", "kw_boss_grievance" },
            new[] { "kw_boss_distrust", "kw_keeper_slip", "kw_code_digit" }
        };

        static readonly string[] EvidenceTitles = { "쪼개진 세 자리", "금고 옆 술병", "찢긴 보고서" };
        static readonly string[] EvidenceBodies = {
            "코드는 한 놈이 통째로 모르게 세 자리로 쪼개져 있었다. 서로를 못 믿는 조직이라는 뜻이다.",
            "금고 옆에 빈 병이 줄지어 있었다. 이 놈은 여기서 혼자 마신다. 지켜야 할 게 아니라 버티고 있었다.",
            "찢긴 보고서에 같은 이름이 반복됐다. 보스는 실패를 기록으로 남기지 않는다. 사람으로 남긴다."
        };

        static readonly string[] ConfessionTitles = { "신참의 한 자리", "금고지기의 한 자리", "측근의 한 자리" };
        static readonly string[] ConfessionBodies = {
            "\"…K. 아니 잠깐, 이거 말하면 안 되는 건데.\" 자기 입으로 말하고 자기가 더 놀랐다.",
            "\"두 번째 자리는 7이야. …형님, 이거 나한테 들었다고 하지 마십쇼.\" 술기운이 반, 지친 게 반이었다.",
            "\"Q.\" 딱 한 글자였다. 그러고는 오래 아무 말도 하지 않았다."
        };

        static readonly string[] TruthTitles = { "신참의 자부심", "금고지기의 밤", "측근의 균열" };
        static readonly string[] TruthBodies = {
            "조직에서 처음으로 자기 이름을 불러준 게 보스였다고 했다. 그래서 못 빠져나온 거였다.",
            "보스는 실수하면 사람을 지운다더라. 그 얘길 하면서 저 놈은 자기 손을 봤다.",
            "보스를 위해 다 버렸는데, 보스는 자기 이름조차 기억하지 못했다."
        };

        /// <summary>현재 런 상태를 읽어 수사기록 3인분을 만든다.</summary>
        public static List<Entry> Build(RunState run)
        {
            var list = new List<Entry>();
            for (int i = 0; i < EnemyIds.Length; i++)
            {
                bool allClues = true;
                foreach (var kw in ClueSets[i])
                    if (run == null || !run.HasKeyword(kw)) { allClues = false; break; }

                bool hasCode = run != null && run.HasCode(CodeIndexes[i]);
                bool maxed   = run != null && run.IsAffinityMaxed(EnemyIds[i]);

                list.Add(new Entry
                {
                    enemyId = EnemyIds[i],
                    displayName = Names[i],
                    codeIndex = CodeIndexes[i],
                    clueKeywords = ClueSets[i],
                    slots = new[]
                    {
                        new Slot { kind = Kind.Evidence,   title = EvidenceTitles[i],   body = EvidenceBodies[i],   filled = allClues },
                        new Slot { kind = Kind.Confession, title = ConfessionTitles[i], body = ConfessionBodies[i], filled = hasCode },
                        new Slot { kind = Kind.Truth,      title = TruthTitles[i],      body = TruthBodies[i],      filled = maxed }
                    }
                });
            }
            return list;
        }

        /// <summary>속내 조각 개수 = 마음을 얻은 조직원 수. 3이면 진엔딩.</summary>
        public static int TruthCount(RunState run)
            => run == null ? 0 : run.MaxedAffinityCount;

        /// <summary>진엔딩 자격: 세 명 모두의 속내를 들었는가.</summary>
        public static bool TrueEndingUnlocked(RunState run)
            => TruthCount(run) >= RunState.TotalEnemies;

        /// <summary>채운 칸 / 전체 칸.</summary>
        public static void CountSlots(RunState run, out int filled, out int total)
        {
            filled = 0; total = 0;
            foreach (var e in Build(run))
                foreach (var s in e.slots) { total++; if (s.filled) filled++; }
        }

        public static string KindLabel(Kind k) => k switch
        {
            Kind.Evidence   => "물증",
            Kind.Confession => "자백",
            _               => "속내"
        };
    }
}
