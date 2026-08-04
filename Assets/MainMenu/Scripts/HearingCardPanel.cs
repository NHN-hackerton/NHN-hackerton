using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDogDetective.Data;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 심문 화면 카드 UI. 보유 키워드 카드 + 프레이밍 카드(14종)를 런타임에 생성해
    /// 선택 → 조립 → 제출을 HearingBattleController.Submit에 넘긴다.
    /// (씬에 카드를 일일이 두지 않고 데이터로 생성 — 유지보수·안정성)
    /// </summary>
    public class HearingCardPanel : MonoBehaviour
    {
        [SerializeField] private HearingBattleController battle;
        [SerializeField] private RectTransform keywordContainer;
        [SerializeField] private RectTransform framingContainer;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_Text composeText;
        [SerializeField] private TMP_FontAsset font;

        // 프레이밍 카드 표시 순서 (규격서 §0)
        static readonly string[] FrameOrder =
        {
            FrameIds.SmallTalk, FrameIds.Praise, FrameIds.Empathy, FrameIds.Complicity,
            FrameIds.Sympathy, FrameIds.Authority, FrameIds.Urgency, FrameIds.FalsePremise,
            FrameIds.SelfDisclosure, FrameIds.Reciprocity, FrameIds.InformationLeak,
            FrameIds.Confrontation, FrameIds.Reassurance, FrameIds.TopicShift
        };

        // 키워드 표시명 (실제 카드 데이터 붙기 전까지 임시)
        static readonly Dictionary<string, string> KeywordNames = new()
        {
            { "kw_rookie_pride", "자부심" },
            { "kw_code_digit",   "금고번호" },
            { "kw_password",     "비밀번호" },
            { "kw_drink",          "한잔 약속" },
            { "kw_boss_grievance", "보스 뒷담화" },
            { "kw_boss_distrust",  "보스 불신" },
            { "kw_keeper_slip",    "금고지기 실토" },
        };

        readonly HashSet<string> selectedKeywords = new();
        string selectedFrame;
        readonly Dictionary<string, Image> keywordCards = new();
        readonly Dictionary<string, Image> framingCards = new();

        static readonly Color CardNormal   = new Color(0.20f, 0.12f, 0.05f, 0.95f);
        static readonly Color CardSelected  = new Color(0.85f, 0.62f, 0.25f, 1f);
        static readonly Color CardDisabled  = new Color(0.14f, 0.10f, 0.07f, 0.6f);

        // 손패(배경 없는 글씨만) 카드의 선택 표시는 글씨색으로 한다
        static readonly Color TextNormal   = new Color(0.98f, 0.90f, 0.70f, 1f);   // 평소: 크림
        static readonly Color TextSelected = new Color(1f,    0.86f, 0.30f, 1f);   // 선택: 밝은 금색
        static readonly Color TextDisabled = new Color(0.55f, 0.48f, 0.40f, 0.7f); // 종료: 흐림

        private void OnEnable()
        {
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmit);
                submitButton.onClick.AddListener(OnSubmit);
                var lbl = submitButton.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = "제출";
            }
            if (battle != null) battle.OnStateChanged += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (battle != null) battle.OnStateChanged -= Rebuild;
        }

        public void Rebuild()
        {
            selectedKeywords.Clear();
            selectedFrame = null;
            BuildKeywordHand();
            BuildFramingCards();
            UpdateVisual();
            UpdateCompose();
        }

        // 배경 9칸 슬롯의 x-범위 (KeywordContainer 폭 기준 0~1). 카드를 여기에 딱 맞춘다.
        static readonly float[,] SlotX = {
            {0.00f,0.077f},{0.112f,0.194f},{0.228f,0.312f},{0.342f,0.428f},{0.458f,0.541f},
            {0.574f,0.658f},{0.690f,0.774f},{0.807f,0.889f},{0.921f,1.00f}
        };

        // 카드 라벨 줄바꿈을 예쁘게: 공백은 그 자리에서 끊고, 공백 없는 짝수 글자는 한가운데(2/2)에서 끊는다.
        static string WrapLabel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf(' ') >= 0) return s.Replace(" ", "\n");   // "한잔 약속" → 한잔/약속
            if (s.Length == 4) return s.Substring(0, 2) + "\n" + s.Substring(2);   // "비밀번호" → 비밀/번호
            return s;   // 3자 이하(자부심 등)는 한 줄로
        }

        private void BuildKeywordHand()
        {
            keywordCards.Clear();
            if (keywordContainer == null) return;
            ClearChildren(keywordContainer);
            // 레이아웃 그룹 제거 — 카드를 배경 슬롯 좌표에 직접 앉힌다
            var hlg = keywordContainer.GetComponent<HorizontalLayoutGroup>(); if (hlg != null) DestroyImmediate(hlg);
            var glg = keywordContainer.GetComponent<GridLayoutGroup>();       if (glg != null) DestroyImmediate(glg);

            var owned = (battle != null && battle.Session != null)
                ? battle.Session.Run.OwnedKeywords : null;
            if (owned == null) return;

            int i = 0;
            foreach (var id in owned)
            {
                if (i >= SlotX.GetLength(0)) break;   // 최대 9칸
                string label = KeywordNames.TryGetValue(id, out var n) ? n : id;
                string kid = id;
                var img = MakeCard(keywordContainer, WrapLabel(label), 0f, 0f);
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(SlotX[i, 0], 0.06f);
                rt.anchorMax = new Vector2(SlotX[i, 1], 0.94f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                img.color = new Color(0f, 0f, 0f, 0f);   // 배경 없이 글씨만 (투명이어도 클릭됨)
                var lbl = img.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                {
                    lbl.fontSizeMax = 30; lbl.fontStyle = FontStyles.Bold;
                    lbl.enableWordWrapping = false;   // 내가 넣은 \n만 쓰고, TMP가 4자를 3/1로 또 쪼개지 않게
                }
                img.GetComponent<Button>().onClick.AddListener(() => ToggleKeyword(kid));
                keywordCards[kid] = img;
                i++;
            }
        }

        private void BuildFramingCards()
        {
            framingCards.Clear();
            if (framingContainer == null) return;
            ClearChildren(framingContainer);
            EnsureFramingRow(framingContainer);

            foreach (var fid in FrameOrder)
            {
                string f = fid;
                var img = MakeCard(framingContainer, FrameIds.ToDisplayName(fid), 0f, 0f); // grid가 셀 크기 지정
                img.GetComponent<Button>().onClick.AddListener(() => SelectFrame(f));
                framingCards[f] = img;
            }
        }

        private void ToggleKeyword(string id)
        {
            if (!selectedKeywords.Remove(id)) selectedKeywords.Add(id);
            UpdateVisual();
            UpdateCompose();
        }

        private void SelectFrame(string f)
        {
            selectedFrame = (selectedFrame == f) ? null : f;
            UpdateVisual();
            UpdateCompose();
        }

        private void UpdateVisual()
        {
            bool finished = battle == null || battle.IsFinished;
            foreach (var kv in keywordCards)   // 배경은 투명 — 글씨색으로 선택/비활성 표시
            {
                var lbl = kv.Value.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                    lbl.color = finished ? TextDisabled
                        : (selectedKeywords.Contains(kv.Key) ? TextSelected : TextNormal);
            }
            foreach (var kv in framingCards)
                kv.Value.color = finished ? CardDisabled
                    : (selectedFrame == kv.Key ? CardSelected : CardNormal);

            if (submitButton != null)
            {
                var u = BuildUtterance();
                submitButton.interactable = !finished && battle.CanSubmit(u, out _);
            }
        }

        private void UpdateCompose()
        {
            if (composeText == null) return;
            var u = BuildUtterance();
            composeText.text = string.IsNullOrEmpty(u.ComposedText) ? "카드를 골라 문장을 만드세요" : u.ComposedText;
        }

        private PlayerUtterance BuildUtterance()
        {
            var kws = new List<string>(selectedKeywords);
            string frameName = string.IsNullOrEmpty(selectedFrame) ? "" : FrameIds.ToDisplayName(selectedFrame);
            var names = new List<string>();
            foreach (var id in kws) names.Add(KeywordNames.TryGetValue(id, out var n) ? n : id);
            string composed = "";
            if (names.Count > 0) composed += string.Join(", ", names);
            if (!string.IsNullOrEmpty(frameName)) composed += (composed.Length > 0 ? " — " : "") + frameName;
            return new PlayerUtterance
            {
                KeywordCardIds = kws,
                FrameId = selectedFrame,
                ComposedText = composed
            };
        }

        private void OnSubmit()
        {
            if (battle == null) return;
            battle.Submit(BuildUtterance());
            // 결과는 비동기 — 판정 끝나면 OnStateChanged로 Rebuild됨
        }

        // ── UI 생성 헬퍼 ─────────────────────────────────────
        private Image MakeCard(RectTransform parent, string label, float w, float h)
        {
            var go = new GameObject("Card_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = CardNormal;
            go.AddComponent<Button>();
            go.AddComponent<UiClickSound>();   // 카드도 클릭음 (런타임 생성이라 씬에서 못 붙임)
            if (w > 0f && h > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = w; le.preferredHeight = h;
            }
            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)lgo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(6, 4); lrt.offsetMax = new Vector2(-6, -4);
            var tmp = lgo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.color = new Color(0.98f, 0.90f, 0.70f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 6; tmp.fontSizeMax = 16;
            return img;
        }

        private static void ClearChildren(Transform t)
        {
            // 즉시 삭제 — 지연 Destroy면 같은 프레임 Rebuild 시 옛 카드가 남아 중복된다.
            for (int i = t.childCount - 1; i >= 0; i--)
                DestroyImmediate(t.GetChild(i).gameObject);
        }

        private static void EnsureHorizontal(RectTransform t)
        {
            if (t.GetComponent<GridLayoutGroup>() is GridLayoutGroup g) DestroyImmediate(g);
            var h = t.GetComponent<HorizontalLayoutGroup>();
            if (h == null) h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 32; h.childAlignment = TextAnchor.MiddleLeft;   // 왼쪽 슬롯부터 채움
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        }

        private static void EnsureFramingRow(RectTransform t)
        {
            // 14장을 한 줄로 — 가로로 균등 분할해 컨테이너 폭을 채운다.
            if (t.GetComponent<GridLayoutGroup>() is GridLayoutGroup g) DestroyImmediate(g);
            var h = t.GetComponent<HorizontalLayoutGroup>();
            if (h == null) h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
        }
    }
}
