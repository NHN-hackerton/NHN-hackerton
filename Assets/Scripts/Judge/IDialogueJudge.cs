using System;
using System.Collections;

namespace TopDogDetective.Judge
{
    using Data;

    /// <summary>
    /// 대화 판정기. 플레이어의 조립문을 받아 DialogueResult를 만들어 돌려준다.
    ///
    /// 구현체:
    ///  - MockDialogueJudge : 규칙 기반 가짜 판정 (개발·UI 작업용, 즉시 응답)
    ///  - LlmDialogueJudge  : 프록시 경유 실제 LLM 판정
    ///
    /// BattleSession은 이 인터페이스만 알면 되므로, 인스펙터에서 Mock ↔ 실제를
    /// 스위치 하나로 전환할 수 있다.
    ///
    /// [주의] 구현체는 Sanitize/Resolve를 호출하지 않는다.
    ///        검증·누적 계산은 BattleSession.ApplyResult가 전담한다.
    ///        구현체는 "LLM이 채우는 필드"만 채워서 돌려주면 된다.
    /// </summary>
    public interface IDialogueJudge
    {
        /// <summary>
        /// 판정 요청. 코루틴으로 구현해 WebGL에서도 안전하게 동작한다.
        /// </summary>
        /// <param name="session">현재 전투 세션 (턴·상태·컨텍스트 제공)</param>
        /// <param name="utterance">플레이어가 제출한 조립문</param>
        /// <param name="onDone">
        /// 결과 콜백. 실패 시 null을 넘기면 ApplyResult가 폴백을 만든다.
        /// </param>
        IEnumerator Judge(BattleSession session, PlayerUtterance utterance,
                          Action<DialogueResult> onDone);

        /// <summary>디버그 표시용 이름.</summary>
        string Name { get; }
    }
}
