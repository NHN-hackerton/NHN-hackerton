using UnityEngine;

namespace TopDogDetective.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        public void CaseFiles()
        {
            Debug.Log("[MainMenu] CASE FILES clicked. Feature is not implemented yet.");
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
