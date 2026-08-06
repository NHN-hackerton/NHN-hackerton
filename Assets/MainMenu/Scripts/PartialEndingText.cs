using UnityEngine;
using TMPro;
using TopDogDetective.Data;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 부분 엔딩(탈출 성공 · 조직 미검거)의 멘트를 마음을 얻은 조직원 수에 따라 바꾼다.
    /// CutsceneController와 같은 오브젝트에 붙여, 화면이 열릴 때 lines[0]을 갈아끼운다.
    /// </summary>
    [RequireComponent(typeof(CutsceneController))]
    public class PartialEndingText : MonoBehaviour
    {
        [Tooltip("멘트를 표시하는 텍스트 (CutsceneController가 쓰는 것과 같은 것)")]
        [SerializeField] private TMP_Text dialogueText;

        // 공통 앞부분 — 폭탄은 막았지만 잡아둘 게 없었다
        const string Head =
            "폭탄은 멈췄다. 도시는 아침을 맞았다.\n" +
            "그런데 잡아둘 게 없었다. 증언 없이는 이름도 못 적는다.";

        /// <summary>마음을 얻은 인원 수에 따라 덧붙는 마지막 줄.</summary>
        static string TailFor(int maxed) => maxed switch
        {
            >= 2 => "\n둘은 순순히 손을 내밀었다. 나머지는 그림자 속으로 사라졌다.",
            1    => "\n한 놈만은 마지막까지 내 눈을 피하지 않았다. 나머지는 사라졌다.",
            _    => "\n아무도 내 이름을 기억하지 않았다. 조직은 다시 모일 것이다."
        };

        private void OnEnable() => Apply();

        /// <summary>현재 런 상태를 읽어 멘트를 갱신한다.</summary>
        public void Apply()
        {
            int maxed = CaseFile.TruthCount(HearingBattleController.CurrentRun);
            string text = Head + TailFor(maxed) + "\n— 작전 종료: 도시는 지켰다";

            // CutsceneController가 Show()에서 lines[index]를 쓰므로 원본도 바꿔준다.
            // SetLine이 현재 컷이면 타이핑까지 걸어 주므로, 그때는 여기서 또 넣지 않는다.
            var cc = GetComponent<CutsceneController>();
            if (cc != null) { cc.SetLine(0, text); return; }

            Typewriter.ShowAll(dialogueText, text);
        }
    }
}
