using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 엔딩 화면에 붙여, 열릴 때 뒤에 남아 있는 게임 화면들을 전부 끈다.
    /// (경로에 따라 보스방·해제화면·탐색맵이 켜진 채로 남아, 엔딩이 끝난 뒤 다시 보이는 것을 막는다)
    /// </summary>
    public class EndingScreenCleanup : MonoBehaviour
    {
        [Tooltip("엔딩이 열릴 때 강제로 끌 화면들")]
        [SerializeField] private GameObject[] screensToClose;

        private void OnEnable()
        {
            if (screensToClose == null) return;
            foreach (var s in screensToClose)
                if (s != null && s != gameObject) s.SetActive(false);
        }
    }
}
