using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 효과음 재생기. 버튼 클릭음처럼 짧은 소리를 한 곳에서 낸다.
    /// 음량은 설정 화면의 효과음 슬라이더(PlayerPrefs)를 읽는다.
    /// </summary>
    public class SfxManager : MonoBehaviour
    {
        public static SfxManager Instance { get; private set; }

        [Tooltip("버튼 클릭음")]
        [SerializeField] private AudioClip clickClip;
        [Tooltip("효과음 기준 음량. 설정 슬라이더를 100%로 둬도 이 값이 상한이 된다.")]
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.5f;
        [Tooltip("같은 소리가 이 시간 안에 겹쳐 나지 않게 막는다(초). 연타 시 소리가 뭉치는 것 방지.")]
        [SerializeField] private float minInterval = 0.04f;

        AudioSource source;
        float lastClickTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        float Volume => Mathf.Clamp01(SettingsController.SfxVolume) * baseVolume;

        /// <summary>버튼 클릭음. 연타로 소리가 뭉치지 않게 최소 간격을 둔다.</summary>
        public void PlayClick()
        {
            if (source == null || clickClip == null) return;
            // 시간 정지 연출이 들어와도 UI 소리는 나야 하므로 unscaledTime 기준
            if (Time.unscaledTime - lastClickTime < minInterval) return;
            lastClickTime = Time.unscaledTime;
            source.PlayOneShot(clickClip, Volume);
        }

        /// <summary>
        /// 임의의 효과음을 한 번 재생한다. 연타 방어는 걸지 않는다 —
        /// 폭발음처럼 화면 전환에 맞춰 한 번 울려야 하는 소리가 클릭음에 막히면 안 된다.
        /// </summary>
        /// <param name="volumeScale">이 소리만 조절할 배율 (원본 크기가 곡마다 달라서)</param>
        public void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip, Volume * Mathf.Max(0f, volumeScale));
        }

        /// <summary>설정 화면에서 슬라이더를 움직일 때 미리듣기용.</summary>
        public void PreviewClick(float sliderValue)
        {
            if (source == null || clickClip == null) return;
            lastClickTime = Time.unscaledTime;
            source.PlayOneShot(clickClip, Mathf.Clamp01(sliderValue) * baseVolume);
        }
    }
}
