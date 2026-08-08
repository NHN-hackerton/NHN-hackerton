using UnityEngine;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 설정 화면 제어. 사운드(전체/배경음악/효과음) 슬라이더 + 대화 출력 속도 선택 +
    /// 적용/뒤로가기 버튼을 담당한다.
    ///
    /// 저장은 PlayerPrefs. 값은 static 프로퍼티로 노출해 다른 시스템(오디오·대화)이 읽어간다.
    /// 아직 오디오 에셋이 없으므로 전체 음량만 AudioListener.volume에 미리 반영하고,
    /// AudioMixer가 연결되면(masterMixer 등) 그쪽으로 라우팅한다.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private GameObject settingsScreen;   // 열고 닫을 설정 화면 루트

        // 설정창은 두 모습으로 쓴다.
        //   메인 메뉴에서 열면  : 나가기가 필요 없으므로 액자 2개짜리 배경 (setting1_bk)
        //   게임 도중에 열면    : 나가기까지 들어간 액자 3개짜리 배경 (setting_bk)
        // 액자가 배경 그림에 그려져 있어서, 배경을 바꿀 때 버튼 위치도 같이 옮겨야 한다.
        [Header("배경 변형")]
        [SerializeField] private UnityEngine.UI.Image panelBackground;
        [SerializeField] private Sprite menuBackground;   // setting1_bk (액자 2개)
        [SerializeField] private Sprite gameBackground;   // setting_bk  (액자 3개)
        // 위치는 코드가 건드리지 않는다 — 씬에서 액자에 맞춰 놓은 그대로 쓴다.
        // 배경마다 액자 수·자리가 달라서, 변형별 버튼을 따로 두고 켜고 끄기만 한다.
        [Tooltip("메인 메뉴 변형에서 켤 버튼들 (액자 2개용)")]
        [SerializeField] private GameObject[] menuRowButtons;
        [Tooltip("게임 중 변형에서 켤 버튼들 (액자 3개용)")]
        [SerializeField] private GameObject[] gameRowButtons;
        [Tooltip("메뉴 변형의 적용 / 뒤로가기 — 게임 변형 것과 같은 동작을 붙인다")]
        [SerializeField] private Button menuApplyButton;
        [SerializeField] private Button menuBackButton;

        [Header("사운드 슬라이더 (0~1)")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("대화 출력 속도 (0=느리게 1=보통 2=빠르게)")]
        [SerializeField] private Button[] speedButtons = new Button[3];
        [SerializeField] private Image[] speedButtonBackgrounds = new Image[3];
        [SerializeField] private Color speedNormalColor = new Color(0.10f, 0.09f, 0.08f, 0.85f);
        [SerializeField] private Color speedSelectedColor = new Color(0.78f, 0.60f, 0.32f, 1f);

        [Header("하단 버튼")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button backButton;

        // 게임 도중 빠져나갈 길이 여기밖에 없다. 탐색 맵·심문·보스방에는 나가기 버튼이 없고,
        // 설정창은 모든 화면에서 열리므로 한 곳에 두면 전부 커버된다.
        [Header("메뉴로 나가기 (진행 중인 런을 버린다)")]
        [SerializeField] private Button quitButton;
        [Tooltip("돌아갈 화면 (메인 메뉴)")]
        [SerializeField] private GameObject menuScreen;
        [Tooltip("나갈 때도 계속 켜 둘 것 (공용 배경 등)")]
        [SerializeField] private GameObject[] keepActive;
        [Tooltip("실수로 눌러 런이 날아가지 않게, 한 번 더 눌러야 실행된다")]
        [SerializeField] private float confirmSeconds = 3f;

        string quitLabelDefault;
        float quitArmedUntil;

        // ---- 저장 키 ----
        private const string KeyMaster = "settings.masterVol";
        private const string KeyBgm    = "settings.bgmVol";
        private const string KeySfx    = "settings.sfxVol";
        private const string KeySpeed  = "settings.dialogueSpeed";

        // ---- 편집 중(pending) 값 : 적용 전까지 저장되지 않음 ----
        private float pendingMaster, pendingBgm, pendingSfx;
        private int pendingSpeed;

        // =========================================================
        // 다른 시스템이 읽어가는 static 접근자
        // =========================================================
        public static float MasterVolume => PlayerPrefs.GetFloat(KeyMaster, 1f);
        public static float BgmVolume     => PlayerPrefs.GetFloat(KeyBgm, 1f);
        public static float SfxVolume     => PlayerPrefs.GetFloat(KeySfx, 1f);
        public static int   DialogueSpeed => PlayerPrefs.GetInt(KeySpeed, 1);

        /// <summary>대화 출력 속도 → 초당 글자 수. 대화 시스템이 이 값을 쓰면 됨.</summary>
        public static float DialogueCharsPerSecond
        {
            get
            {
                switch (DialogueSpeed)
                {
                    case 0: return 25f;  // 느리게
                    case 2: return 90f;  // 빠르게
                    default: return 50f; // 보통
                }
            }
        }

        private void Awake()
        {
            // 저장된 전체 음량을 부팅 시 미리 반영
            ApplyMasterToEngine(MasterVolume);
        }

        private void Start()
        {
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (bgmSlider != null)    bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (sfxSlider != null)    sfxSlider.onValueChanged.AddListener(OnSfxChanged);

            for (int i = 0; i < speedButtons.Length; i++)
            {
                int idx = i; // 클로저 캡처 주의
                if (speedButtons[i] != null)
                    speedButtons[i].onClick.AddListener(() => OnSpeedSelected(idx));
            }

            if (applyButton != null) applyButton.onClick.AddListener(Apply);
            if (backButton != null)  backButton.onClick.AddListener(Back);
            // 메뉴 변형 버튼은 게임 변형과 같은 동작 (복제본이라 리스너가 따로 필요하다)
            if (menuApplyButton != null) menuApplyButton.onClick.AddListener(Apply);
            if (menuBackButton != null)  menuBackButton.onClick.AddListener(Back);
            if (quitButton != null)
            {
                var lbl = quitButton.GetComponentInChildren<TMPro.TMP_Text>();
                quitLabelDefault = lbl != null ? lbl.text : "메뉴로 나가기";
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            if (settingsScreen != null) settingsScreen.SetActive(false);
        }

        // =========================================================
        // 열기 / 닫기
        // =========================================================
        /// <summary>메인 메뉴 OPTIONS 버튼이 호출.</summary>
        public void Open()
        {
            // 오버레이 방식: 다른 화면은 그대로 두고 설정창만 위에 띄운다.
            // (설정 화면은 불투명 풀스크린 + 맨 위 렌더라 아래 화면을 덮는다)
            LoadIntoPending();
            SyncUIFromPending();
            ApplyPanelVariant();
            if (settingsScreen != null) settingsScreen.SetActive(true);
        }

        /// <summary>적용: pending 값을 저장하고 화면 유지.</summary>
        public void Apply()
        {
            PlayerPrefs.SetFloat(KeyMaster, pendingMaster);
            PlayerPrefs.SetFloat(KeyBgm, pendingBgm);
            PlayerPrefs.SetFloat(KeySfx, pendingSfx);
            PlayerPrefs.SetInt(KeySpeed, pendingSpeed);
            PlayerPrefs.Save();
            ApplyMasterToEngine(pendingMaster);
            if (BgmManager.Instance != null) BgmManager.Instance.ApplyVolume();   // 재생 중인 곡에 즉시 반영
            Debug.Log("[Settings] 적용됨 " +
                      $"(master={pendingMaster:0.00}, bgm={pendingBgm:0.00}, sfx={pendingSfx:0.00}, speed={pendingSpeed})");
        }

        /// <summary>뒤로가기: 저장 안 한 변경은 되돌리고 설정창만 닫는다(아래 화면 복귀).</summary>
        /// <summary>
        /// 어디서 열었는지에 따라 배경과 하단 버튼 배치를 맞춘다.
        /// 메인 메뉴에서는 나가기 버튼을 감춘다 — 이미 메뉴에 있으니 누를 이유가 없다.
        /// </summary>
        private void ApplyPanelVariant()
        {
            bool inMenu = menuScreen != null && menuScreen.activeInHierarchy;

            if (panelBackground != null)
            {
                var spr = inMenu ? menuBackground : gameBackground;
                if (spr != null) panelBackground.sprite = spr;
            }

            SetRow(menuRowButtons, inMenu);
            SetRow(gameRowButtons, !inMenu);
        }

        static void SetRow(GameObject[] row, bool on)
        {
            if (row == null) return;
            foreach (var g in row) if (g != null) g.SetActive(on);
        }

        /// <summary>나가기 1번째 클릭은 확인 요청, 2번째가 실행. (실수로 런을 날리지 않게)</summary>
        private void OnQuitClicked()
        {
            var lbl = quitButton != null ? quitButton.GetComponentInChildren<TMPro.TMP_Text>() : null;

            if (Time.unscaledTime <= quitArmedUntil)
            {
                if (lbl != null) lbl.text = quitLabelDefault;
                quitArmedUntil = 0f;
                QuitToMenu();
                return;
            }

            quitArmedUntil = Time.unscaledTime + confirmSeconds;
            if (lbl != null) lbl.text = "정말 나가기?";
        }

        private void Update()
        {
            // 확인 시간이 지나면 문구를 되돌린다 (누른 걸 잊고 나중에 또 누르면 바로 나가버리므로)
            if (quitArmedUntil > 0f && Time.unscaledTime > quitArmedUntil)
            {
                quitArmedUntil = 0f;
                var lbl = quitButton != null ? quitButton.GetComponentInChildren<TMPro.TMP_Text>() : null;
                if (lbl != null) lbl.text = quitLabelDefault;
            }
        }

        /// <summary>진행 중인 런을 버리고 메인 메뉴로 돌아간다.</summary>
        public void QuitToMenu()
        {
            HearingBattleController.ResetRun();             // 코드·친밀·의심·통과 기록 초기화
            ExplorationController.CollectedClues.Clear();   // 모은 단서 초기화

            // 진행 중이던 화면이 남아 있으면 클릭을 먹거나 위에 겹쳐 보인다.
            // 화면이 나중에 추가돼도 빠지지 않게, 메뉴와 예외 목록만 남기고 전부 끈다.
            if (menuScreen != null && menuScreen.transform.parent != null)
            {
                foreach (Transform t in menuScreen.transform.parent)
                {
                    if (t.gameObject == menuScreen) continue;
                    bool keep = false;
                    if (keepActive != null)
                        foreach (var k in keepActive) if (k != null && k == t.gameObject) keep = true;
                    if (!keep) t.gameObject.SetActive(false);
                }
                menuScreen.SetActive(true);
            }

            if (settingsScreen != null) settingsScreen.SetActive(false);
            Debug.Log("[Settings] 메뉴로 나가기 — 진행 중이던 런을 버렸다");
        }

        public void Back()
        {
            // pending을 저장값으로 되돌리고 엔진 상태도 복구
            ApplyMasterToEngine(MasterVolume);
            if (BgmManager.Instance != null) BgmManager.Instance.ApplyVolume();   // 미리듣기 취소
            if (settingsScreen != null) settingsScreen.SetActive(false);
        }

        // =========================================================
        // 입력 콜백 (pending만 갱신, 저장은 Apply에서)
        // =========================================================
        private void OnMasterChanged(float v)
        {
            pendingMaster = v;
            ApplyMasterToEngine(v); // 미리듣기
        }

        private void OnBgmChanged(float v)
        {
            pendingBgm = v;
            if (BgmManager.Instance != null) BgmManager.Instance.PreviewVolume(v);   // 미리듣기
        }

        private void OnSfxChanged(float v)
        {
            pendingSfx = v;
            // 효과음은 지속음이 아니라서 움직이는 동안 들려줘야 감이 온다.
            // 드래그 중에도 계속 불리므로, 겹쳐 울리지 않게 PreviewClick 쪽에서 간격을 둔다.
            if (SfxManager.Instance != null) SfxManager.Instance.PreviewClick(v);
        }

        private void OnSpeedSelected(int idx)
        {
            pendingSpeed = Mathf.Clamp(idx, 0, 2);
            RefreshSpeedButtons();
        }

        // =========================================================
        // 내부 헬퍼
        // =========================================================
        private void LoadIntoPending()
        {
            pendingMaster = MasterVolume;
            pendingBgm = BgmVolume;
            pendingSfx = SfxVolume;
            pendingSpeed = DialogueSpeed;
        }

        private void SyncUIFromPending()
        {
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(pendingMaster);
            if (bgmSlider != null)    bgmSlider.SetValueWithoutNotify(pendingBgm);
            if (sfxSlider != null)    sfxSlider.SetValueWithoutNotify(pendingSfx);
            RefreshSpeedButtons();
        }

        private void RefreshSpeedButtons()
        {
            for (int i = 0; i < speedButtonBackgrounds.Length; i++)
            {
                if (speedButtonBackgrounds[i] == null) continue;
                speedButtonBackgrounds[i].color = (i == pendingSpeed) ? speedSelectedColor : speedNormalColor;
            }
        }

        private void ApplyMasterToEngine(float v)
        {
            // 오디오 에셋/믹서가 붙기 전까지의 임시 반영.
            AudioListener.volume = Mathf.Clamp01(v);
        }
    }
}
