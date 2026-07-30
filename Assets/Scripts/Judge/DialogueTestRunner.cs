using System.Collections;
using UnityEngine;

namespace TopDogDetective.Judge
{
    using Data;

    /// <summary>
    /// 개발용 대화 제출 헬퍼. BattleSmokeTest·MemberAEndToEndDemo가 공유한다.
    /// [주의] 개발 전용. 릴리즈 빌드에 포함되지 않는다.
    /// </summary>
#if UNITY_EDITOR
    public static class DialogueTestRunner
    {
        public static IEnumerator Submit(BattleSession session, IDialogueJudge judge, PlayerUtterance u)
        {
            if (session.IsFinished) yield break;

            if (!session.CanSubmit(u, out string reason))
            {
                Debug.LogWarning($"  [턴 {session.CurrentTurn}] 제출 불가: {reason}");
                yield break;
            }

            int turn = session.CurrentTurn;
            DialogueResult raw = null;
            yield return judge.Judge(session, u, r => raw = r);

            var result = session.ApplyResult(raw, u);

            Debug.Log($"  [턴 {turn}] \"{u.ComposedText}\"\n" +
                      $"    → \"{result.reply}\"\n" +
                      $"    의심 {result.currentSuspicion}% · 친밀 {result.currentAffinity}%" +
                      $"{(result.codeRevealed ? $" · 코드 획득 [{result.revealedValue}]" : "")}" +
                      $" · {result.turnOutcome}");
        }
    }
#endif
}
