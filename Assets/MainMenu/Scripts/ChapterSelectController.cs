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
        [Tooltip("챕터3 자물쇠. 아직 해금 조건이 없는 챕터라 항상 켜 둔다.")]
        [SerializeField] private GameObject chapter3Lock;
        [Tooltip("해금 상태를 알려줄 텍스트 (챕터2 부제)")]
        [SerializeField] private TMPro.TMP_Text chapter2Sub;
        [Tooltip("잠긴 챕터를 눌렀을 때 띄울 안내")]
        [SerializeField] private TMPro.TMP_Text noticeText;
        [Tooltip("챕터2 버튼. 해금 전후로 안내 문구가 달라져서 따로 받는다.")]
        [SerializeField] private Button chapter2Button;
        [Tooltip("챕터2 미리보기 이미지. 잠겨 있으면 어둡게, 해금되면 밝게 보여준다.")]
        [SerializeField] private Image chapter2Thumb;
        [Tooltip("잠긴 미리보기의 밝기 (1 = 원래 색)")]
        [SerializeField, Range(0f, 1f)] private float lockedThumbBrightness = 0.42f;

        [Header("씬")]
        [SerializeField] private string gameSceneName = "Game";

        const string Chapter2UnlockedKey = "TopDog.Chapter2Unlocked";

        Color thumbBaseColor;   // 씬에 지정된 미리보기 원래 색 (밝기 배율의 기준)
        bool thumbBaseCached;

        /// <summary>진엔딩을 본 적이 있는가 (플레이를 넘겨 유지된다).</summary>
        public static bool Chapter2Unlocked
        {
            get => PlayerPrefs.GetInt(Chapter2UnlockedKey, 0) == 1;
            private set { PlayerPrefs.SetInt(Chapter2UnlockedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>진엔딩 컷씬이 끝나면 호출 — 챕터2를 해금한다.</summary>
        public static void UnlockChapter2() => Chapter2Unlocked = true;

        // 이 해금은 PlayerPrefs에 남아 게임을 다시 켜도 유지된다(=보상). 그래서 테스트로 진엔딩을
        // 한 번 보면 그 PC에서는 계속 해금 상태다. 시연 전에 잠긴 상태로 되돌리려면 아래를 쓴다.
        /// <summary>챕터2를 다시 잠근다 (시연·테스트용).</summary>
        [ContextMenu("챕터2 해금 초기화")]
        public void ResetChapter2Lock()
        {
            Chapter2Unlocked = false;
            RefreshLocks();
            Debug.Log("[ChapterSelect] 챕터2 해금 초기화 — 다시 잠김");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Top Dog/챕터2 해금 초기화")]
        private static void ResetChapter2LockMenu()
        {
            Chapter2Unlocked = false;
            Debug.Log("[ChapterSelect] 챕터2 해금 초기화 — 다시 잠김 (에디터 메뉴)");
        }
#endif

        private void Start()
        {
            if (chapter1Button != null) chapter1Button.onClick.AddListener(SelectChapter1);
            if (backButton != null)     backButton.onClick.AddListener(Back);
            if (mapBackButton != null)  mapBackButton.onClick.AddListener(BackFromMap);
            // 챕터2는 해금 여부에 따라 안내가 달라지므로 따로 붙인다.
            // (lockedButtons에 챕터2가 같이 들어 있어도 이쪽이 뒤에 걸려 덮어쓰지 않게 구분)
            if (lockedButtons != null)
                foreach (var b in lockedButtons)
                {
                    if (b == null) continue;
                    bool isChapter2 = chapter2Button != null && b == chapter2Button;
                    if (isChapter2) b.onClick.AddListener(OnChapter2Clicked);
                    else            b.onClick.AddListener(OnLockedClicked);
                }
            if (chapter2Button != null && (lockedButtons == null || System.Array.IndexOf(lockedButtons, chapter2Button) < 0))
                chapter2Button.onClick.AddListener(OnChapter2Clicked);

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

        /// <summary>해금 상태를 화면에 반영한다. (엔딩에서 바로 돌아올 때도 호출된다)</summary>
        public void RefreshLocks()
        {
            bool unlocked = Chapter2Unlocked;
            if (chapter2Lock != null) chapter2Lock.SetActive(!unlocked);
            if (chapter2Sub != null)
                // 이모지는 폰트(Galmuri11)에 없어 네모로 뜨므로 쓰지 않는다
                chapter2Sub.text = unlocked ? "해금됨 — 다음 사건 준비 중" : "잠김";

            // 미리보기: 잠겨 있으면 어둡게 깔아두고, 해금되면 원래 색으로 드러낸다.
            // 흰색·알파1로 덮어쓰면 씬에 잡아둔 틴트와 투명도가 사라지므로,
            // 처음 색을 한 번 기억해 두고 거기에 밝기 배율만 곱한다. (툴팁의 "1 = 원래 색"과 일치)
            if (chapter2Thumb != null)
            {
                if (!thumbBaseCached) { thumbBaseColor = chapter2Thumb.color; thumbBaseCached = true; }
                float k = unlocked ? 1f : lockedThumbBrightness;
                chapter2Thumb.color = new Color(thumbBaseColor.r * k,
                                                thumbBaseColor.g * k,
                                                thumbBaseColor.b * k,
                                                thumbBaseColor.a);
            }

            // 챕터3은 아직 해금 조건이 없다. 자물쇠를 항상 켜 둬서 챕터2와 같은 잠김 표현을 쓴다.
            // (해금 조건이 생기면 위 챕터2처럼 조건에 따라 켜고 끄면 된다)
            if (chapter3Lock != null) chapter3Lock.SetActive(true);

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

        /// <summary>챕터2 — 진엔딩 전이면 조건을, 해금 뒤엔 준비 중임을 알린다.</summary>
        private void OnChapter2Clicked()
        {
            // 해금 조건은 '진엔딩' = 세 조직원의 속내 조각을 다 모은 상태(수사기록 완성)다.
            // "완료하세요"는 그냥 클리어로 읽히므로 조건을 그대로 적어 준다.
            string msg = Chapter2Unlocked
                ? "아직 준비 중인 챕터입니다."
                : "챕터 1 수사기록을 완성하세요.";
            if (noticeText != null) noticeText.text = msg;
            Debug.Log("[ChapterSelect] 챕터2 — " + msg);
        }

        /// <summary>해금 조건 자체가 아직 없는 챕터(3).</summary>
        private void OnLockedClicked()
        {
            if (noticeText != null)
                noticeText.text = "아직 준비 중인 챕터입니다.";
            Debug.Log("[ChapterSelect] 아직 준비 중인 챕터입니다.");
        }
    }
}
