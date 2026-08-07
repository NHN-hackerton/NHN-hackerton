using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 화면을 그대로 캡처해 잘게 줄였다가 다시 늘려(=블러) 배경에 깔아준다.
    /// URP 2D UI 위에서 실제 가우시안 블러를 쓰려면 풀스크린 패스를 붙여야 해서,
    /// 캡처 → 축소 → 쌍선형 확대로 같은 결과를 값싸게 낸다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ScreenBlurOverlay : MonoBehaviour
    {
        [Tooltip("줄일 목표 가로 픽셀. 작을수록 더 흐려진다. 화면 크기와 무관하게 흐림 정도가 일정해진다.")]
        [SerializeField] private int blurWidth = 120;
        [Tooltip("1이면 원본 밝기, 낮추면 어두워진다")]
        [SerializeField] private float brightness = 0.5f;
        [Tooltip("캡처하는 순간만 잠깐 숨길 것들 — 안내 문구가 배경에 같이 찍히는 걸 막는다")]
        [SerializeField] private GameObject[] hideWhileCapturing;

        private Image img;
        private Texture2D blurred;
        private Coroutine co;
        // 캡처하려고 잠깐 숨긴 것들. 지역변수로 두면 캡처 도중 코루틴이 끊겼을 때
        // 목록째 사라져 그 오브젝트들이 영영 꺼진 채로 남는다.
        private readonly List<GameObject> hidden = new();

        private void Awake()
        {
            img = GetComponent<Image>();
            img.raycastTarget = false;   // 배경이므로 클릭을 막지 않는다
            img.enabled = false;
        }

        /// <summary>지금 화면을 흐리게 떠서 깔아준다.</summary>
        public void Show()
        {
            if (!gameObject.activeInHierarchy) return;
            if (co != null) StopCoroutine(co);
            RestoreHidden();                     // 앞 캡처가 끊겼다면 먼저 되살린다
            co = StartCoroutine(ShowRoutine());
        }

        /// <summary>블러를 걷어낸다.</summary>
        public void Hide()
        {
            if (co != null) { StopCoroutine(co); co = null; }
            RestoreHidden();
            if (img != null) img.enabled = false;
            Release();
        }

        /// <summary>캡처하려고 숨겨둔 오브젝트를 원래대로 켠다.</summary>
        private void RestoreHidden()
        {
            foreach (var g in hidden) if (g != null) g.SetActive(true);
            hidden.Clear();
        }

        private IEnumerator ShowRoutine()
        {
            if (img == null) img = GetComponent<Image>();
            img.enabled = false;   // 이전 블러가 다시 찍히지 않도록

            if (hideWhileCapturing != null)
                foreach (var g in hideWhileCapturing)
                    if (g != null && g.activeSelf) { g.SetActive(false); hidden.Add(g); }

            yield return new WaitForEndOfFrame();   // 이 프레임 렌더가 끝난 뒤에 캡처

            Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
            RestoreHidden();
            if (full == null) { co = null; yield break; }

            int w = Mathf.Clamp(blurWidth, 24, full.width);
            int h = Mathf.Max(8, Mathf.RoundToInt(w * full.height / (float)full.width));
            full.filterMode = FilterMode.Bilinear;

            var rt = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(full, rt);                                  // 축소 = 주변 픽셀 평균 = 블러

            Release();
            blurred = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            blurred.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            blurred.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(full);

            blurred.filterMode = FilterMode.Bilinear;                 // 늘릴 때 부드럽게
            blurred.wrapMode = TextureWrapMode.Clamp;

            img.sprite = Sprite.Create(blurred, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = new Color(brightness, brightness, brightness, 1f);
            img.enabled = true;
            co = null;
        }

        private void Release()
        {
            if (img != null && img.sprite != null) { Destroy(img.sprite); img.sprite = null; }
            if (blurred != null) { Destroy(blurred); blurred = null; }
        }

        private void OnDisable() { Hide(); }
    }
}
