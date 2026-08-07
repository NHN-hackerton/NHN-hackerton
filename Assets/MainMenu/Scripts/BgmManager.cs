using DG.Tweening;
using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 배경음악 재생기. 화면마다 붙은 <see cref="ScreenBgm"/>이 이걸 불러 곡을 바꾼다.
    /// 같은 곡을 다시 요청하면 이어서 재생한다 — 로비↔사건선택처럼 오가는 화면에서 곡이 처음부터
    /// 다시 시작되면 끊긴 느낌이 나기 때문이다.
    /// 음량은 설정 화면의 BGM 슬라이더(PlayerPrefs)를 읽는다.
    /// </summary>
    public class BgmManager : MonoBehaviour
    {
        public static BgmManager Instance { get; private set; }

        [Tooltip("곡을 바꿀 때 겹쳐 넘기는 시간(초)")]
        [SerializeField] private float fadeTime = 0.8f;
        [Tooltip("배경음악 기준 음량. 설정 슬라이더를 100%로 둬도 이 값이 상한이 된다 " +
                 "(BGM은 대사·효과음보다 낮게 깔려야 한다)")]
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.35f;

        AudioSource source;
        AudioClip current;
        Tween fade;
        float clipScale = 1f;   // 트랙별 보정 (곡마다 녹음 레벨이 달라서)
        bool stopping;          // 정지 페이드가 도는 중 (음량을 되돌리면 안 되는 구간)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            fade?.Kill();
        }

        /// <summary>설정 슬라이더 × 기준 음량 × 트랙 보정. (전체 음량은 AudioListener가 따로 처리한다)</summary>
        float TargetVolume => Mathf.Clamp01(SettingsController.BgmVolume) * baseVolume * clipScale;

        /// <summary>곡을 재생한다. 이미 같은 곡이면 이어서 재생한다.</summary>
        /// <param name="volumeScale">이 트랙만 더 줄이거나 키울 배율</param>
        public void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (source == null) return;
            if (clip == null) { Stop(); return; }
            clipScale = Mathf.Max(0f, volumeScale);
            if (clip == current && source.isPlaying) { ApplyVolume(); return; }

            current = clip;
            stopping = false;   // 정지 페이드 중이었다면 이 재생이 덮어쓴다
            fade?.Kill();

            source.clip = clip;
            source.volume = 0f;
            source.Play();
            fade = DOTween.To(() => source.volume, v => source.volume = v, TargetVolume, fadeTime)
                          .SetUpdate(true);
        }

        public void Stop()
        {
            if (source == null) return;
            fade?.Kill();
            current = null;
            stopping = true;
            fade = DOTween.To(() => source.volume, v => source.volume = v, 0f, fadeTime)
                          .SetUpdate(true)
                          .OnComplete(() => { source.Stop(); stopping = false; });
        }

        /// <summary>설정 적용/취소 후 호출 — 저장된 값으로 음량을 되돌린다.</summary>
        public void ApplyVolume()
        {
            if (source == null) return;
            // 정지 페이드 중에 음량을 되돌리면, 트윈이 죽어 OnComplete의 source.Stop()이 영영 안 불린다.
            // 꺼지던 곡이 제 음량으로 되살아나 다음 화면까지 따라온다.
            if (stopping) return;
            fade?.Kill();
            source.volume = TargetVolume;
        }

        /// <summary>
        /// 설정 화면에서 슬라이더를 움직이는 동안 쓰는 미리듣기.
        /// 저장값(PlayerPrefs)이 아니라 넘겨받은 슬라이더 값을 즉시 반영한다.
        /// </summary>
        public void PreviewVolume(float sliderValue)
        {
            if (source == null) return;
            if (stopping) return;   // 위와 같은 이유 — 꺼지는 중인 곡은 건드리지 않는다
            fade?.Kill();
            source.volume = Mathf.Clamp01(sliderValue) * baseVolume * clipScale;
        }
    }
}
