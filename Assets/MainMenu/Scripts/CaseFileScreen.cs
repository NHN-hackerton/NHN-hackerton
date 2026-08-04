using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDogDetective.Data;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 수사기록 화면. 조직원 3인 × 조각 3종(물증·자백·속내)을 얼굴 3조각으로 보여준다.
    /// 못 얻은 조각은 흑백, 얻은 조각은 컬러 — 셋 다 모으면 얼굴이 완성된다.
    /// 조각을 누르면 아래에 내용이 펼쳐진다.
    /// </summary>
    public class CaseFileScreen : MonoBehaviour
    {
        /// <summary>
        /// 조직원 한 명분 초상화 조각 스프라이트 (위→아래 3장).
        /// 초상화 전체를 3등분한 것으로, 못 얻은 조각은 블러판을 쓴다.
        /// 블러는 초상화 전체에 한 번 걸고 나서 자르므로, 아무것도 못 알아낸 조직원은
        /// 이음선 없는 '전체가 흐린 초상화'로 보인다.
        /// (gray 필드명은 씬 연결을 깨지 않으려고 유지 — 실제 내용은 블러)
        /// </summary>
        [System.Serializable]
        public class FaceSet
        {
            public string label = "조직원";
            [Tooltip("위에서부터 1·2·3 조각 (컬러)")]   public Sprite[] color = new Sprite[3];
            [Tooltip("같은 순서의 블러 조각")]          public Sprite[] gray  = new Sprite[3];
        }

        [Header("조직원별 얼굴 조각 (신참·금고지기·측근 순)")]
        [SerializeField] private FaceSet[] faces = new FaceSet[3];

        [Header("배치")]
        [Tooltip("행이 생성될 부모")]
        [SerializeField] private RectTransform rowContainer;
        [Tooltip("얼굴 한 칸의 화면 크기(px)")]
        [SerializeField] private float faceSize = 200f;

        [Header("텍스트")]
        [SerializeField] private TMP_Text progressText;
        [Tooltip("조각을 누르면 제목이 뜨는 곳")]
        [SerializeField] private TMP_Text detailTitle;
        [Tooltip("조각 내용이 뜨는 곳")]
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private TMP_FontAsset font;

        [Header("닫기")]
        [SerializeField] private Button closeButton;
        [Tooltip("닫으면 돌아갈 화면")]
        [SerializeField] private GameObject returnScreen;

        static readonly Color Cream   = new Color(0.98f, 0.90f, 0.70f);
        static readonly Color Dim     = new Color(0.55f, 0.48f, 0.40f);
        static readonly Color Gold    = new Color(1f, 0.82f, 0.35f);
        const string RowName = "CaseRow";

        RunState Run => HearingBattleController.CurrentRun;

        private void OnEnable()
        {
            // 어느 화면에서 열어도 위에 뜨게 한다 (형제 순서가 낮으면 다른 화면에 가려 안 보인다)
            transform.SetAsLastSibling();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
            Rebuild();
        }

        /// <summary>현재 런 상태로 기록을 다시 그린다.</summary>
        public void Rebuild()
        {
            if (rowContainer == null) return;

            for (int i = rowContainer.childCount - 1; i >= 0; i--)
                if (rowContainer.GetChild(i).name.StartsWith(RowName))
                    DestroyImmediate(rowContainer.GetChild(i).gameObject);

            var entries = CaseFile.Build(Run);
            for (int i = 0; i < entries.Count; i++)
                BuildRow(entries[i], i);

            CaseFile.CountSlots(Run, out int filled, out int total);
            if (progressText != null)
            {
                int truth = CaseFile.TruthCount(Run);
                string tail = CaseFile.TrueEndingUnlocked(Run)
                    ? "  —  세 명 모두의 속내를 들었다"
                    : $"  —  속내 {truth}/3";
                progressText.text = $"수사기록  {filled}/{total}{tail}";
            }

            ShowDetail(null, null);
        }

        private void BuildRow(CaseFile.Entry entry, int rowIndex)
        {
            // 가로 3열 배치 — 열마다 [얼굴 3조각] + [이름] + [조각 3줄]
            float colWidth = rowContainer.rect.width / 3f;

            var row = new GameObject(RowName + "_" + entry.displayName, typeof(RectTransform));
            var rrt = row.GetComponent<RectTransform>();
            rrt.SetParent(rowContainer, false);
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.sizeDelta = new Vector2(colWidth, rowContainer.rect.height);
            rrt.anchoredPosition = new Vector2(rowIndex * colWidth, 0f);

            // 위: 얼굴 3조각 (열 가운데)
            var faceGO = new GameObject("Face", typeof(RectTransform));
            var frt = faceGO.GetComponent<RectTransform>();
            frt.SetParent(rrt, false);
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.sizeDelta = new Vector2(faceSize, faceSize);
            frt.anchoredPosition = new Vector2((colWidth - faceSize) * 0.5f, 0f);

            var set = (faces != null && rowIndex < faces.Length) ? faces[rowIndex] : null;
            float bandH = faceSize / 3f;
            for (int b = 0; b < 3; b++)
            {
                var slot = entry.slots[b];   // 0=물증(위) 1=자백(중간) 2=속내(아래)
                var band = new GameObject("Band" + (b + 1), typeof(RectTransform), typeof(Image), typeof(Button));
                var brt = band.GetComponent<RectTransform>();
                brt.SetParent(frt, false);
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.sizeDelta = new Vector2(faceSize, bandH);
                brt.anchoredPosition = new Vector2(0f, -b * bandH);

                var img = band.GetComponent<Image>();
                if (set != null && set.color != null && b < set.color.Length)
                    img.sprite = slot.filled ? set.color[b] : (set.gray != null && b < set.gray.Length ? set.gray[b] : set.color[b]);
                img.color = Color.white;

                var captured = slot;
                var who = entry.displayName;
                band.GetComponent<Button>().onClick.AddListener(() => ShowDetail(who, captured));
            }

            // 얼굴 아래: 이름 + 조각 3줄 (열 가운데 정렬)
            float textX = (colWidth - faceSize) * 0.5f;
            var nameT = MakeText(rrt, entry.displayName, new Vector2(textX, -faceSize - 14f), 34,
                                 Cream, TextAlignmentOptions.Center, faceSize);
            for (int b = 0; b < 3; b++)
            {
                var slot = entry.slots[b];
                string mark = slot.filled ? "◆" : "◇";
                string title = slot.filled ? slot.title : "???";
                var t = MakeText(rrt, $"{mark} {CaseFile.KindLabel(slot.kind)}  {title}",
                                 new Vector2(textX, -faceSize - 60f - b * 34f), 24,
                                 slot.filled ? Cream : Dim, TextAlignmentOptions.Center, faceSize);
                if (slot.kind == CaseFile.Kind.Truth && slot.filled) t.color = Gold;
            }
        }

        private TMP_Text MakeText(RectTransform parent, string text, Vector2 pos, int size,
                                  Color color, TextAlignmentOptions align, float width)
        {
            var go = new GameObject("T", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, size + 12f);
            rt.anchoredPosition = pos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>조각 클릭 시 아래 상세란에 내용 표시. null이면 안내문.</summary>
        private void ShowDetail(string who, CaseFile.Slot slot)
        {
            if (slot == null)
            {
                if (detailTitle != null) detailTitle.text = "조각을 눌러 내용을 확인하세요";
                if (detailBody != null) detailBody.text = "";
                return;
            }
            if (!slot.filled)
            {
                if (detailTitle != null) detailTitle.text = $"{who} — {CaseFile.KindLabel(slot.kind)} (미확보)";
                if (detailBody != null)
                    detailBody.text = slot.kind switch
                    {
                        CaseFile.Kind.Evidence   => "탐색에서 단서를 전부 모으면 채워진다.",
                        CaseFile.Kind.Confession => "심문에서 코드 한 자리를 뜯어내면 채워진다.",
                        _                        => "친밀도 100%를 채워 마음을 얻으면 채워진다."
                    };
                return;
            }
            if (detailTitle != null) detailTitle.text = $"{who} — {slot.title}";
            if (detailBody != null) detailBody.text = slot.body;
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (returnScreen != null) returnScreen.SetActive(true);
        }
    }
}
