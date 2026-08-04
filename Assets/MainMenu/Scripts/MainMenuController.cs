using UnityEngine;

namespace TopDogDetective.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("로비의 '수사 기록' 버튼으로 열 화면")]
        [SerializeField] private GameObject caseFileScreen;

        public void CaseFiles()
        {
            if (caseFileScreen == null)
            {
                Debug.LogWarning("[MainMenu] 수사기록 화면이 연결되지 않았습니다.");
                return;
            }
            // 로비는 그대로 두고 위에 덮는다 (닫으면 다시 로비가 보인다)
            caseFileScreen.SetActive(true);
        }

        public void Exit()
        {
#if UNITY_EDITOR
            Debug.Log("[MainMenu] EXIT clicked. Application.Quit() would run in a build.");
#else
            Application.Quit();
#endif
        }
    }
}
