using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RoundedRectImage))]
public class MainMenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Color hoverColor = new Color(0.86f, 0.74f, 0.56f, 0.16f);
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float animationDuration = 0.08f;

    private RoundedRectImage targetImage;
    private RectTransform rectTransform;
    private Vector3 baseScale;
    private bool hovering;
    private Coroutine scaleRoutine;

private void Awake()
    {
        rectTransform = (RectTransform)transform;
        baseScale = rectTransform.localScale;
        var image = GetComponent<RoundedRectImage>();
        image.color = Color.clear;
        image.raycastTarget = true;
    }

private void EnsureInitialized()
    {
        if (targetImage == null)
            targetImage = GetComponent<RoundedRectImage>();
        if (rectTransform == null)
        {
            rectTransform = (RectTransform)transform;
            baseScale = rectTransform.localScale;
        }
    }


public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        var image = GetComponent<RoundedRectImage>();
        if (image != null)
            image.color = hoverColor;
    }

public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        var image = GetComponent<RoundedRectImage>();
        if (image != null)
            image.color = Color.clear;
        EnsureInitialized();
        AnimateScale(baseScale);
    }

public void OnPointerDown(PointerEventData eventData)
    {
        EnsureInitialized();
        AnimateScale(baseScale * pressedScale);
    }

public void OnPointerUp(PointerEventData eventData)
    {
        var image = GetComponent<RoundedRectImage>();
        if (image != null)
            image.color = hovering ? hoverColor : Color.clear;
        EnsureInitialized();
        AnimateScale(baseScale);
    }

    private void OnDisable()
    {
        if (targetImage != null)
            targetImage.color = Color.clear;
        if (rectTransform != null)
            rectTransform.localScale = baseScale;
    }

    private void AnimateScale(Vector3 target)
    {
        if (!isActiveAndEnabled)
            return;
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleTo(target));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = rectTransform.localScale;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = animationDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / animationDuration);
            rectTransform.localScale = Vector3.LerpUnclamped(start, target, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }
        rectTransform.localScale = target;
        scaleRoutine = null;
    }
}
