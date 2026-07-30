using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// 조사 가능한 오브젝트. 은은하게 맥동하는 글로우(하이라이트) + 호버 시 "조사하기" 툴팁 +
/// 클릭 시 조사 완료 → 단서 획득(ExplorationController에 통지).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Hotspot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("단서")]
    [SerializeField] private string clueName = "단서";
    [SerializeField] private string tooltipLabel = "조사하기";   // 호버 툴팁 문구 (문은 "심문하기")
    [SerializeField] private bool addsClue = true;               // false면 단서 안 주고 액션만
    [SerializeField] private UnityEvent onInvestigated;          // 클릭 시 추가 동작(예: 심문 화면 열기)

    public bool AddsClue { get { return addsClue; } }

    [Header("하이라이트")]
    [SerializeField] private Graphic highlight;      // 자식 글로우
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private float minAlpha = 0.22f;
    [SerializeField] private float maxAlpha = 0.55f;
    [SerializeField] private float hoverAlpha = 0.9f;

    private ExplorationController controller;
    private bool investigated = false;
    private bool hovering = false;
    private float t;

    private void Awake()
    {
        controller = GetComponentInParent<ExplorationController>();
    }

    private void Update()
    {
        if (highlight == null) return;
        float a;
        if (investigated) a = 0f;
        else if (hovering) a = hoverAlpha;
        else { t += Time.deltaTime * pulseSpeed; a = Mathf.Lerp(minAlpha, maxAlpha, Mathf.Sin(t) * 0.5f + 0.5f); }
        var c = highlight.color; c.a = a; highlight.color = c;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (investigated) return;
        hovering = true;
        if (controller != null) controller.ShowTooltip(transform, tooltipLabel);
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovering = false;
        if (controller != null) controller.HideTooltip();
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (investigated) return;
        investigated = true;
        hovering = false;
        if (controller != null) { controller.HideTooltip(); if (addsClue) controller.AddClue(clueName); }
        if (onInvestigated != null) onInvestigated.Invoke();
    }
}
