using System.Collections;
using TMPro;
using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 대사를 한 글자씩 드러내는 연출. 속도는 설정 화면의 "대화 출력 속도"를 따른다
    /// (SettingsController.DialogueCharsPerSecond — 느리게 25 / 보통 50 / 빠르게 90자per초).
    ///
    /// 글자를 한 자씩 이어 붙이는 대신 TMP의 maxVisibleCharacters를 늘린다.
    /// 문장 전체를 먼저 배치해 두므로 줄바꿈 위치가 중간에 바뀌며 글자가 튀지 않는다.
    ///
    /// [주의] 이 방식은 label에 '보이는 글자 수' 상태를 남긴다. 다른 코드가 label.text를
    ///        직접 넣으면 앞부분만 보이는 사고가 나므로, 그럴 때는 ShowAll()을 쓴다.
    /// </summary>
    public static class Typewriter
    {
        /// <summary>타이핑 없이 즉시 전체를 보여준다. (label.text 직접 대입 대신 이걸 쓴다)</summary>
        public static void ShowAll(TMP_Text label, string text)
        {
            if (label == null) return;
            label.maxVisibleCharacters = int.MaxValue;
            label.text = text;
        }

        /// <summary>
        /// 한 글자씩 드러낸다. 코루틴이므로 호출한 쪽이 StartCoroutine으로 돌리고,
        /// 다음 대사를 넣기 전에 StopCoroutine으로 끊어 준다.
        /// </summary>
        public static IEnumerator Reveal(TMP_Text label, string text)
        {
            if (label == null) yield break;

            label.maxVisibleCharacters = 0;
            label.text = text;
            label.ForceMeshUpdate();                       // 글자 수를 세려면 배치가 끝나야 한다

            int total = label.textInfo.characterCount;
            if (total <= 0) { label.maxVisibleCharacters = int.MaxValue; yield break; }

            float cps = Mathf.Max(1f, SettingsController.DialogueCharsPerSecond);
            float shown = 0f;
            while (shown < total)
            {
                // 화면 전환·타임어택이 모두 realtime 기준이라 여기도 unscaled로 센다
                shown += cps * Time.unscaledDeltaTime;
                label.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);
                yield return null;
            }
            label.maxVisibleCharacters = int.MaxValue;      // 이후 다른 대사가 들어와도 안 잘리게
        }
    }
}
