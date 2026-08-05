using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    public class MainMenuIconHoverAnimation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private bool rotateWhileHovered;
        [SerializeField] private float rotationSpeed = 180f;
        [SerializeField] private float swapDuration = 0.14f;

        private bool hovering;
        private Coroutine swapRoutine;

        private void Awake()
        {
            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>();

            if (iconImage != null && normalSprite != null)
            {
                iconImage.sprite = normalSprite;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (rotateWhileHovered && hovering && iconImage != null)
                iconImage.rectTransform.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            if (hoverSprite != null)
                StartSwap(hoverSprite);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (iconImage != null)
                iconImage.rectTransform.localRotation = Quaternion.identity;
            if (normalSprite != null)
                StartSwap(normalSprite);
        }

        private void OnDisable()
        {
            hovering = false;
            if (swapRoutine != null)
                StopCoroutine(swapRoutine);
            if (iconImage != null)
            {
                iconImage.rectTransform.localScale = Vector3.one;
                iconImage.rectTransform.localRotation = Quaternion.identity;
                if (normalSprite != null)
                    iconImage.sprite = normalSprite;
            }
        }

        private void StartSwap(Sprite target)
        {
            if (!isActiveAndEnabled || iconImage == null)
                return;
            if (swapRoutine != null)
                StopCoroutine(swapRoutine);
            swapRoutine = StartCoroutine(SwapSprite(target));
        }

        private IEnumerator SwapSprite(Sprite target)
        {
            float half = Mathf.Max(0.01f, swapDuration * 0.5f);
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                iconImage.rectTransform.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.82f, 0.82f, 1f), t);
                yield return null;
            }

            iconImage.sprite = target;
            elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                iconImage.rectTransform.localScale = Vector3.Lerp(new Vector3(0.82f, 0.82f, 1f), Vector3.one, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }

            iconImage.rectTransform.localScale = Vector3.one;
            swapRoutine = null;
        }
    }
}
