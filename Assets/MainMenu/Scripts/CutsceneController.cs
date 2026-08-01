using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 인트로 컷씬. 프레임(이미지+대사)을 순서대로 보여주고 ❯ 버튼으로 넘긴다.
    /// 컷 전환 시 페이드 아웃 → 교체 → 페이드 인. 마지막 컷 다음엔 nextScreen을 켜고 닫는다.
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        [Header("표시 대상")]
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextButton;

        [Header("컷씬 내용 (프레임 순서대로)")]
        [SerializeField] private Sprite[] frames;
        [SerializeField, TextArea(2, 4)] private string[] lines;

        [Header("페이드")]
        [Tooltip("이미지+대사+버튼을 감싸는 CanvasGroup (검정 배경 위에서 페이드)")]
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private float fadeDuration = 0.35f;

        [Header("끝난 뒤")]
        [Tooltip("마지막 컷 다음에 켤 화면 (없으면 컷씬만 닫힘)")]
        [SerializeField] private GameObject nextScreen;
        [SerializeField] private UnityEvent onFinished;

        private int index;
        private bool transitioning;

        private void OnEnable()
        {
            index = 0;
            transitioning = false;
            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(Next);
                nextButton.onClick.AddListener(Next);
            }
            Show();
            if (fadeGroup != null) fadeGroup.alpha = 1f;  // 첫 컷은 페이드인 없이 바로 (전환/마지막 페이드는 유지)
        }

        private void Show()
        {
            if (frames == null || frames.Length == 0) return;
            if (image != null && index < frames.Length) image.sprite = frames[index];
            if (dialogueText != null)
                dialogueText.text = (lines != null && index < lines.Length) ? lines[index] : "";
        }

        /// <summary>❯ 버튼 / 클릭으로 다음 컷.</summary>
        public void Next()
        {
            if (transitioning) return;
            if (fadeGroup == null)   // 페이드 없으면 즉시 전환
            {
                index++;
                if (index >= (frames != null ? frames.Length : 0)) { Finish(); return; }
                Show();
                return;
            }
            StartCoroutine(NextRoutine());
        }

        private IEnumerator NextRoutine()
        {
            transitioning = true;
            yield return Fade(1f, 0f);            // 현재 컷 페이드 아웃

            index++;
            int count = frames != null ? frames.Length : 0;
            if (index >= count) { Finish(); yield break; }   // 페이드 아웃된 채 다음 화면으로

            Show();                                // 안 보이는 동안 다음 컷으로 교체
            yield return Fade(0f, 1f);            // 페이드 인
            transitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (fadeGroup == null) yield break;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(from, to, fadeDuration <= 0f ? 1f : t / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = to;
        }

        private void Finish()
        {
            onFinished?.Invoke();
            if (nextScreen != null) nextScreen.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
