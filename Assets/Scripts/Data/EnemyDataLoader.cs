using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopDogDetective.Data
{
    /// <summary>
    /// Resources/Enemies/{enemyId}.json을 EnemyData로 로드한다.
    /// 로드 직후 Validate()를 자동 실행해 데이터 오류를 콘솔에 남기고,
    /// 오류가 하나라도 있으면 깨진 데이터를 조용히 통과시키지 않고 null을 반환한다.
    /// </summary>
    public static class EnemyDataLoader
    {
        const string ResourceFolder = "Enemies";

        // JSON 텍스트만 캐싱한다 — 파싱은 호출마다 새로 해서 인스턴스를 독립적으로 유지한다.
        // (EnemyData.state는 참조형이라, 파싱 결과 자체를 캐싱하면 여러 BattleSession이
        //  같은 인스턴스를 공유해 상태가 오염될 수 있다.)
        static readonly Dictionary<string, string> jsonCache = new();

        // 에디터에서는 캐시를 쓰지 않는다.
        //
        // 이 캐시에는 무효화 경로가 없어서, 밸런싱 중 조직원 JSON을 고쳐도 에디터가
        // 도메인을 리로드하기 전까지 옛 텍스트가 계속 나온다. Play 종료로도,
        // AssetDatabase.Refresh로도 안 지워지고 EditorUtility.RequestScriptReload가
        // 필요했다 — 그 사이 수정 전 데이터로 테스트하고 "고쳐도 효과가 없다"고
        // 잘못 결론 내리게 된다(실제로 겪음). 조직원 JSON은 3개뿐이라 에디터에서
        // 매번 다시 읽어도 비용이 없다. 빌드에서는 그대로 캐싱한다.
        //
        // const가 아니라 static readonly인 이유: const면 빌드 구성에 따라
        // 아래 분기가 상수 조건이 돼 '도달 불가 코드' 경고가 난다.
#if UNITY_EDITOR
        static readonly bool UseJsonCache = false;
#else
        static readonly bool UseJsonCache = true;
#endif

        public static EnemyData Load(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                Debug.LogError("[EnemyDataLoader] enemyId가 비어 있습니다.");
                return null;
            }

            string jsonText = null;
            if (!UseJsonCache || !jsonCache.TryGetValue(enemyId, out jsonText))
            {
                var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{enemyId}");
                if (asset == null)
                {
                    Debug.LogError($"[EnemyDataLoader] Resources/{ResourceFolder}/{enemyId}.json을 찾을 수 없습니다.");
                    return null;
                }
                jsonText = asset.text;
                if (UseJsonCache) jsonCache[enemyId] = jsonText;
            }

            EnemyData enemy;
            try
            {
                enemy = JsonUtility.FromJson<EnemyData>(jsonText);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EnemyDataLoader] {enemyId} JSON 파싱 실패: {e.Message}");
                return null;
            }

            if (enemy == null)
            {
                Debug.LogError($"[EnemyDataLoader] {enemyId} 파싱 결과가 null입니다.");
                return null;
            }

            List<string> errors = enemy.Validate();
            if (errors.Count > 0)
            {
                foreach (var e in errors)
                    Debug.LogError($"[EnemyDataLoader] {enemyId}: {e}");
                Debug.LogError($"[EnemyDataLoader] {enemyId}: 데이터 오류 {errors.Count}건 — 로드를 건너뜁니다.");
                return null;
            }

            return enemy;
        }
    }
}
