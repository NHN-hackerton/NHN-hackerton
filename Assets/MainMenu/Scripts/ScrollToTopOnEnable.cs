using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 스크롤 화면을 열 때 항상 맨 위에서 시작하게 한다.
    /// (닫았다 다시 열면 이전에 내려둔 위치가 남아 있어, 처음 보는 사람이 중간부터 읽게 된다)
    ///
    /// 레이아웃이 잡히기 전에 위치를 넣으면 무시되므로 한 프레임 기다린다.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollToTopOnEnable : MonoBehaviour
    {
        ScrollRect scroll;

        private void Awake() => scroll = GetComponent<ScrollRect>();

        private void OnEnable()
        {
            if (scroll == null) scroll = GetComponent<ScrollRect>();
            SnapTop();
            StartCoroutine(SnapNextFrame());
        }

        private IEnumerator SnapNextFrame()
        {
            yield return null;                       // 레이아웃 계산 후
            if (scroll != null && scroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)scroll.content);
            SnapTop();
        }

        private void SnapTop()
        {
            if (scroll == null) return;
            scroll.velocity = Vector2.zero;          // 관성으로 다시 흘러내리지 않게
            scroll.verticalNormalizedPosition = 1f;  // 1 = 맨 위
        }
    }
}
