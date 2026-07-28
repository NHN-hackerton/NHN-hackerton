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

        public static EnemyData Load(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                Debug.LogError("[EnemyDataLoader] enemyId가 비어 있습니다.");
                return null;
            }

            if (!jsonCache.TryGetValue(enemyId, out string jsonText))
            {
                var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{enemyId}");
                if (asset == null)
                {
                    Debug.LogError($"[EnemyDataLoader] Resources/{ResourceFolder}/{enemyId}.json을 찾을 수 없습니다.");
                    return null;
                }
                jsonText = asset.text;
                jsonCache[enemyId] = jsonText;
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
