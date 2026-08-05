using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 화면이 열릴 때 효과음을 한 번 재생한다. (폭발 엔딩 같은 일회성 연출용)
    /// 음량은 SfxManager가 설정 화면의 효과음 슬라이더를 반영해 계산한다.
    /// </summary>
    public class ScreenSfx : MonoBehaviour
    {
        [Tooltip("화면이 열릴 때 한 번 재생할 소리")]
        [SerializeField] private AudioClip clip;
        // SfxManager의 기준 음량(0.12)은 버튼 클릭음에 맞춰 낮게 잡혀 있다.
        // 폭발처럼 한 번 크게 울려야 하는 소리는 여기서 배율로 끌어올린다.
        [Tooltip("이 소리만 조절할 배율. 1보다 크게 두면 기준 음량보다 크게 난다.")]
        [SerializeField, Range(0f, 5f)] private float volumeScale = 1f;

        private void OnEnable()
        {
            if (clip == null) return;
            var sfx = SfxManager.Instance;
            if (sfx != null) sfx.Play(clip, volumeScale);
        }
    }
}
