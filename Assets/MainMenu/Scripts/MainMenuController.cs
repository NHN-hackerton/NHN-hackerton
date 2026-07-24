using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Game";

    public void NewInvestigation()
    {
        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        Debug.LogWarning($"[MainMenu] NEW INVESTIGATION clicked, but scene '{gameSceneName}' is not in Build Settings.");
    }

    public void ChapterSelect()
    {
        Debug.Log("[MainMenu] CHAPTER SELECT is currently locked.");
    }

    public void CaseFiles()
    {
        Debug.Log("[MainMenu] CASE FILES clicked. Feature is not implemented yet.");
    }

    public void Options()
    {
        Debug.Log("[MainMenu] OPTIONS clicked. Feature is not implemented yet.");
    }

    public void Casebook()
    {
        Debug.Log("[MainMenu] CASEBOOK clicked. Feature is not implemented yet.");
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
