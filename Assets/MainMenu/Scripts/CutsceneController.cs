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
        [Tooltip("켜면 이 컷씬이 열릴 때 첫 컷도 페이드인한다 (기본은 바로 뜸)")]
        [SerializeField] private bool fadeInOnStart = false;

        [Header("끝난 뒤")]
        [Tooltip("마지막 컷 다음에 켤 화면 (없으면 컷씬만 닫힘)")]
        [SerializeField] private GameObject nextScreen;
        [SerializeField] private UnityEvent onFinished;

        [Header("대사 연출")]
        [Tooltip("대사를 한 글자씩 출력한다. 속도는 설정 화면의 '대화 출력 속도'를 따른다.")]
        [SerializeField] private bool typeDialogue = true;

        private int index;
        private bool transitioning;
        private bool typing;      // 대사가 아직 다 나오지 않았는가

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
            if (fadeGroup != null)
            {
                if (fadeInOnStart) StartCoroutine(FadeInRoutine());  // 이 컷씬만 첫 컷 페이드인
                else fadeGroup.alpha = 1f;                            // 기본: 바로 뜸
            }
        }

        private IEnumerator FadeInRoutine()
        {
            transitioning = true;         // 페이드인 도중 넘어가기 방지
            fadeGroup.alpha = 0f;
            yield return Fade(0f, 1f);
            transitioning = false;
        }

        /// <summary>대사 한 줄을 런타임에 갈아끼운다 (엔딩 분기 문구용).</summary>
        public void SetLine(int i, string text)
        {
            if (lines == null || i < 0 || i >= lines.Length) return;
            lines[i] = text;
            if (i == index) TypeLine(text);
        }

        private void Show()
        {
            if (frames == null || frames.Length == 0) return;
            if (image != null && index < frames.Length) image.sprite = frames[index];
            TypeLine((lines != null && index < lines.Length) ? lines[index] : "");
        }

        Coroutine typeCo;

        /// <summary>대사를 한 글자씩 드러낸다. (설정의 '대화 출력 속도'를 따른다)</summary>
        private void TypeLine(string text)
        {
            if (dialogueText == null) return;
            if (typeCo != null) StopCoroutine(typeCo);

            if (!typeDialogue || !gameObject.activeInHierarchy)
            {
                Typewriter.ShowAll(dialogueText, text);
                return;
            }
            typeCo = StartCoroutine(TypeRoutine(text));
        }

        private IEnumerator TypeRoutine(string text)
        {
            typing = true;
            yield return Typewriter.Reveal(dialogueText, text);
            typing = false;
            typeCo = null;
        }

        /// <summary>타이핑 중이면 즉시 전문을 보여준다. (읽기 빠른 사람이 기다리지 않게)</summary>
        private void FinishTyping()
        {
            if (typeCo != null) { StopCoroutine(typeCo); typeCo = null; }
            typing = false;
            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = int.MaxValue;
        }

        /// <summary>❯ 버튼 / 클릭으로 다음 컷.</summary>
        public void Next()
        {
            if (transitioning) return;

            // 아직 타이핑 중이면 이번 클릭은 '건너뛰기'다 — 못 읽은 대사를 넘겨버리지 않는다
            if (typing) { FinishTyping(); return; }
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

            // 마지막 컷은 페이드 아웃하지 않고 곧바로 다음 화면으로 넘긴다.
            // 컷 사이에서는 검게 사라졌다가 다음 컷이 이어받지만, 마지막엔 이어받을 게 없어서
            // '검은 화면이 잠깐 뜬 다음 맵이 나타나는' 끊김으로 보인다.
            int count = frames != null ? frames.Length : 0;
            if (index + 1 >= count) { Finish(); yield break; }

            yield return Fade(1f, 0f);            // 현재 컷 페이드 아웃
            index++;
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
