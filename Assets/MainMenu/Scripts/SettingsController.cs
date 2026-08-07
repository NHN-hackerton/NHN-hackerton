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
