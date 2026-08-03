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

        [Header("챕터2 해금 (진엔딩 보상)")]
        [Tooltip("해금되면 이 자물쇠/잠김 표시를 끈다")]
        [SerializeField] private GameObject chapter2Lock;
        [Tooltip("해금 상태를 알려줄 텍스트 (챕터2 부제)")]
        [SerializeField] private TMPro.TMP_Text chapter2Sub;
        [Tooltip("해금된 챕터2를 눌렀을 때 띄울 안내")]
        [SerializeField] private TMPro.TMP_Text noticeText;

        [Header("씬")]
        [SerializeField] private string gameSceneName = "Game";

        const string Chapter2UnlockedKey = "TopDog.Chapter2Unlocked";

        /// <summary>진엔딩을 본 적이 있는가 (플레이를 넘겨 유지된다).</summary>
        public static bool Chapter2Unlocked
        {
            get => PlayerPrefs.GetInt(Chapter2UnlockedKey, 0) == 1;
            private set { PlayerPrefs.SetInt(Chapter2UnlockedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>진엔딩 컷씬이 끝나면 호출 — 챕터2를 해금한다.</summary>
        public static void UnlockChapter2() => Chapter2Unlocked = true;

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
            RefreshLocks();
        }

        /// <summary>해금 상태를 화면에 반영한다.</summary>
        private void RefreshLocks()
        {
            bool unlocked = Chapter2Unlocked;
            if (chapter2Lock != null) chapter2Lock.SetActive(!unlocked);
            if (chapter2Sub != null)
                chapter2Sub.text = unlocked ? "해금됨 — 다음 사건 준비 중" : "🔒 잠김";
            if (noticeText != null) noticeText.text = "";
        }

        public void Back()
        {
            if (chapterScreen != null) chapterScreen.SetActive(false);
            if (menuScreen != null) menuScreen.SetActive(true);
        }

        private void SelectChapter1()
        {
            if (chapterScreen != null) chapterScreen.SetActive(false);
            // 새 런 시작 — 이전 플레이의 코드·친밀·단서 정리
            HearingBattleController.ResetRun();
            ExplorationController.CollectedClues.Clear();

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
            // 챕터2는 진엔딩을 보면 해금된다 (콘텐츠는 준비 중)
            if (noticeText != null)
                noticeText.text = Chapter2Unlocked
                    ? "챕터 2 — 다음 사건은 준비 중입니다. 기다려 주세요."
                    : "🔒 잠겨 있습니다. 진엔딩을 보면 다음 사건이 열립니다.";
            Debug.Log("[ChapterSelect] 🔒 아직 잠겨 있는 챕터입니다.");
        }
    }
}
