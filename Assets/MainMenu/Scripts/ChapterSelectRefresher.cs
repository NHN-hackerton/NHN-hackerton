using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 사건선택 화면에 붙여, 화면이 열릴 때마다 해금 상태를 다시 반영한다.
    /// (컨트롤러는 항상 켜져 있는 MainMenuSystem에 있어서 OnEnable이 오지 않는다.
    ///  엔딩 컷씬이 이 화면을 직접 켜는 경로에서도 자물쇠 표시가 최신이 되도록 한다)
    /// </summary>
    public class ChapterSelectRefresher : MonoBehaviour
    {
        [SerializeField] private ChapterSelectController controller;

        private void OnEnable()
        {
            if (controller == null) controller = FindAnyObjectByType<ChapterSelectController>();
            if (controller != null) controller.RefreshLocks();
        }
    }
}
