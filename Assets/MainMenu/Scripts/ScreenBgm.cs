using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 화면 루트에 붙여, 그 화면이 켜질 때 배경음악을 지정한다.
    /// 여러 화면에 같은 클립을 넣으면 오가도 곡이 끊기지 않는다 (로비↔사건선택).
    /// 클립을 비워두면 음악을 끈다.
    /// </summary>
    public class ScreenBgm : MonoBehaviour
    {
        [Tooltip("이 화면에서 재생할 곡. 비우면 음악을 멈춘다.")]
        [SerializeField] private AudioClip clip;
        [Tooltip("이 트랙만 조절할 배율. 곡마다 녹음 레벨이 달라서 필요하다.")]
        [SerializeField, Range(0f, 2f)] private float volumeScale = 1f;
        [Tooltip("클립이 비었을 때 정말 멈출지. 끄면 이전 곡을 그대로 이어 간다.")]
        [SerializeField] private bool stopWhenEmpty = false;

        private void OnEnable()
        {
            var bgm = BgmManager.Instance;
            if (bgm == null) return;
            if (clip != null) bgm.Play(clip, volumeScale);
            else if (stopWhenEmpty) bgm.Stop();
        }
    }
}
