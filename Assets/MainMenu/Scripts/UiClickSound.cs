using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 클릭 소리를 내는 컴포넌트. 버튼·카드·핫스팟 어디든 붙이면 된다.
    /// 비활성(interactable=false) 버튼은 소리를 내지 않는다 — 눌린 것처럼 들리면 오해를 준다.
    /// </summary>
    public class UiClickSound : MonoBehaviour, IPointerClickHandler
    {
        Selectable selectable;
        bool cached;

        public void OnPointerClick(PointerEventData e)
        {
            if (!cached) { selectable = GetComponent<Selectable>(); cached = true; }
            if (selectable != null && !selectable.interactable) return;
            if (SfxManager.Instance != null) SfxManager.Instance.PlayClick();
        }
    }
}
