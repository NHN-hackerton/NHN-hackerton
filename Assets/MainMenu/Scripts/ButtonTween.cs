using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 버튼 손맛 연출 (DOTween) — 호버에 살짝 커지고, 누를 때 눌리고, 뗄 때 통 튀어오른다.
    /// 이 게임은 화면 전환·타임어택이 realtime 기준이라 모든 트윈을 SetUpdate(true)로 돌린다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonTween : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("마우스를 올렸을 때 배율")]
        [SerializeField] private float hoverScale = 1.05f;
        [Tooltip("누르고 있을 때 배율")]
        [SerializeField] private float pressScale = 0.94f;
        [Tooltip("손을 뗄 때 튀어오르는 세기 (0이면 튐 없음)")]
        [SerializeField] private float punchStrength = 0.12f;
        [Tooltip("호버·누름 전환 시간(초)")]
        [SerializeField] private float moveTime = 0.10f;
        [Tooltip("튀어오르는 연출 시간(초)")]
        [SerializeField] private float punchTime = 0.22f;

        RectTransform rt;
        Button button;
        Vector3 baseScale;
        Tween current;
        bool hovering;

        private void Awake() => EnsureInit();

        // 결과 버튼처럼 비활성으로 시작하는 버튼은 Awake가 아직 안 돌 수 있다.
        // 그 상태에서 포인터 이벤트가 들어오면 참조가 비어 터지므로 지연 초기화한다.
        private void EnsureInit()
        {
            if (rt != null) return;
            rt = (RectTransform)transform;
            baseScale = rt.localScale;
            button = GetComponent<Button>();
        }

        // 버튼이 자기 화면을 끄는 경우가 많다. 트윈이 중간에 끊기면 크기가 어긋난 채로
        // 남으므로 반드시 죽이고 원복한다. (다음에 이 화면을 열었을 때 찌그러져 보이는 걸 막음)
        private void OnDisable()
        {
            current?.Kill();
            current = null;
            hovering = false;
            if (rt != null) rt.localScale = baseScale;
        }

        private void OnDestroy() => current?.Kill();

        bool Usable => button == null || button.interactable;

        public void OnPointerEnter(PointerEventData e)
        {
            EnsureInit();
            hovering = true;
            if (Usable) ScaleTo(hoverScale, moveTime);
        }

        public void OnPointerExit(PointerEventData e)
        {
            EnsureInit();
            hovering = false;
            // 나갈 때는 Usable을 보지 않는다. 커서를 올린 뒤 버튼이 interactable=false가 되면
            // OnDisable도 안 불려서(비활성화가 아니라 상호작용만 끈 것) 확대된 채로 굳는다.
            ScaleTo(1f, moveTime);
        }

        public void OnPointerDown(PointerEventData e)
        {
            EnsureInit();
            if (Usable) ScaleTo(pressScale, moveTime * 0.6f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            EnsureInit();
            if (!Usable) return;

            current?.Kill();
            rt.localScale = baseScale * (hovering ? hoverScale : 1f);
            current = rt.DOPunchScale(baseScale * punchStrength, punchTime, vibrato: 6, elasticity: 0.6f)
                        .SetUpdate(true);
        }

        private void ScaleTo(float target, float time)
        {
            current?.Kill();
            current = rt.DOScale(baseScale * target, time)
                        .SetEase(Ease.OutCubic)
                        .SetUpdate(true);
        }
    }
}
