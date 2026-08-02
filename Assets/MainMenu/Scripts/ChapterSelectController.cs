using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 사건(챕터) 선택 화면 제어. 챕터1은 해금(클릭 시 게임 시작), 챕터2·3은 잠김.
    /// 메인 메뉴의 CHAPTER SELECT 버튼이 Open()을 호출한다.
    /// </summary>
    public class ChapterSelectController : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private GameObject chapterScreen;   // 열고 닫을 사건선택 화면 루트
        [SerializeField] private GameObject menuScreen;       // 뒤로갔을 때 다시 보일 메뉴

        [Header("버튼")]
        [SerializeField] private Button chapter1Button;       // 해금된 챕터
        [SerializeField] private Button[] lockedButtons;      // 잠긴 챕터(2,3)
        [SerializeField] private Button backButton;

        [Header("챕터1 맵")]
        [SerializeField] private GameObject chapter1Map;      // 챕터1 탐색 맵 오버레이
        [SerializeField] private GameObject introCutscene;    // 있으면 챕터1 시작 시 먼저 재생(끝나면 Chapter1Map)
        [SerializeField] private Button mapBackButton;        // 맵에서 사건선택으로 돌아가기

        [Header("씬")]
        [SerializeField] private string gameSceneName = "Game";

        private void Start()
        {
            if (chapter1Button != null) chapter1Button.onClick.AddListener(SelectChapter1);
            if (backButton != null)     backButton.onClick.AddListener(Back);
            if (mapBackButton != null)  mapBackButton.onClick.AddListener(BackFromMap);
            if (lockedButtons != null)
                foreach (var b in lockedButtons)
                    if (b != null) b.onClick.AddListener(OnLockedClicked);

            if (chapterScreen != null) chapterScreen.SetActive(false);
            if (chapter1Map != null) chapter1Map.SetActive(false);
        }

        /// <summary>메인 메뉴 CHAPTER SELECT 버튼이 호출.</summary>
        public void Open()
        {
            if (menuScreen != null) menuScreen.SetActive(false);
            if (chapterScreen != null) chapterScreen.SetActive(true);
        }

        public void Back()
        {
            if (chapterScreen != null) chapterScreen.SetActive(false);
            if (menuScreen != null) menuScreen.SetActive(true);
        }

        private void SelectChapter1()
        {
            if (chapterScreen != null) chapterScreen.SetActive(false);
            HearingBattleController.ResetRun();   // 새 런 시작 — 이전 플레이의 코드·친밀 정리

            // 컷씬이 연결돼 있으면 먼저 재생 (컷씬이 끝나면 nextScreen=Chapter1Map로 진행)
            if (introCutscene != null)
            {
                introCutscene.SetActive(true);
                return;
            }
            // 컷씬 없으면 바로 탐색 맵
            if (chapter1Map != null)
            {
                chapter1Map.SetActive(true);
                return;
            }
            // 폴백: 게임 씬이 있으면 로드
            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
                return;
            }
            Debug.Log("[ChapterSelect] 챕터1 시작 — 맵/씬이 아직 연결되지 않았어요.");
        }

        /// <summary>챕터1 맵에서 사건 선택 화면으로 돌아가기.</summary>
        public void BackFromMap()
        {
            if (chapter1Map != null) chapter1Map.SetActive(false);
            if (chapterScreen != null) chapterScreen.SetActive(true);
        }

        private void OnLockedClicked()
        {
            Debug.Log("[ChapterSelect] 🔒 아직 잠겨 있는 챕터입니다.");
        }
    }
}
