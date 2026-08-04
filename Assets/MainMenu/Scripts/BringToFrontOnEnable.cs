using UnityEngine;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 열릴 때마다 형제 순서를 맨 뒤로 보내 화면 위에 뜨게 한다.
    /// 이 게임은 한 캔버스에 화면을 겹쳐 쌓기 때문에, 형제 순서가 낮으면
    /// 켜져 있어도 다른 화면에 가려 안 보인다. (수사기록·도움말처럼 어디서나 열리는 창에 필요)
    /// </summary>
    public class BringToFrontOnEnable : MonoBehaviour
    {
        private void OnEnable() => transform.SetAsLastSibling();
    }
}
