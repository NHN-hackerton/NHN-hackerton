using System.Collections;
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

        /// <summary>
        /// 길게 울리는 소리(사이렌 등)를 정해진 시간만 재생한다.
        ///
        /// PlayOneShot은 한 번 시작하면 중간에 끊을 수 없어서, 15초짜리 사이렌을 그렇게 틀면
        /// 추격 내내 울린다. 그래서 전용 AudioSource를 만들어 재생하고 시간이 지나면 페이드 아웃한다.
        /// 돌려주는 AudioSource를 들고 있으면 화면이 닫힐 때 더 일찍 끊을 수도 있다.
        /// </summary>
        /// <param name="seconds">이 시간이 지나면 페이드 아웃을 시작한다</param>
        public AudioSource PlayFor(AudioClip clip, float seconds, float volumeScale = 1f, float fadeSeconds = 0.8f)
        {
            if (clip == null) return null;

            var go = new GameObject("Sfx_" + clip.name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = Mathf.Clamp01(Volume * Mathf.Max(0f, volumeScale));
            src.playOnAwake = false;
            src.loop = false;
            src.Play();

            StartCoroutine(StopAfter(src, seconds, fadeSeconds));
            return src;
        }

        private IEnumerator StopAfter(AudioSource src, float seconds, float fadeSeconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (src == null) yield break;

            float from = src.volume;
            for (float t = 0f; t < fadeSeconds && src != null; t += Time.unscaledDeltaTime)
            {
                src.volume = from * (1f - t / fadeSeconds);
                yield return null;
            }
            if (src != null) Destroy(src.gameObject);
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
