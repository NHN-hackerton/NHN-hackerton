using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 진엔딩 화면에 붙여, 엔딩을 본 시점에 챕터2를 해금한다.
    /// (컷씬을 끝까지 보지 않고 나가도 이미 자격은 증명했으므로 화면이 열릴 때 지급)
    /// </summary>
    public class TrueEndingReward : MonoBehaviour
    {
        private void OnEnable()
        {
            ChapterSelectController.UnlockChapter2();
            Debug.Log("[TrueEnding] 진엔딩 도달 — 챕터 2 해금");
        }
    }
}
