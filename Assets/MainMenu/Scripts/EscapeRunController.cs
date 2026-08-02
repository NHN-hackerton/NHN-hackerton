using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDogDetective.Data;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 추격 탈출 시퀀스. 오른쪽으로 자동 질주하며 점프로 장애물을 넘고,
    /// 뒤따르는 추격자에게 잡히기 전에 코스 끝까지 간다.
    ///
    /// 난이도는 친밀도 100%를 채운 조직원 수(RunState.MaxedAffinityCount)로 결정된다.
    /// 맵은 하나만 두고 파라미터(코스 길이·장애물 간격·추격자 수·시작 거리)만 바꿔
    /// 엔딩 4종을 커버한다. (연출 에셋 공유, 추격자 수만 분기)
    ///
    /// UI Canvas 위에서 도는 간이 물리 — 기존 화면 전환(SetActive) 방식을 그대로 쓴다.
    /// </summary>
    public class EscapeRunController : MonoBehaviour
    {
        [Header("연출 대상")]
        [Tooltip("달리는 형사. X는 고정, Y만 점프로 움직인다.")]
        [SerializeField] private RectTransform player;
        [Tooltip("장애물이 생성될 부모 (플레이어와 같은 좌표계)")]
        [SerializeField] private RectTransform obstacleLayer;
        [Tooltip("뒤따르는 추격자 무리. 거리에 따라 X가 움직인다.")]
        [SerializeField] private RectTransform chaser;
        [Tooltip("가로로 흐르는 배경 (여러 장을 이어붙여 무한 스크롤). 비어 있어도 된다.")]
        [SerializeField] private RectTransform[] scrollingBackgrounds;

        [Header("플레이어 애니메이션")]
        [Tooltip("달리기 프레임 (번갈아 재생). 비어 있으면 그림 교체 없음.")]
        [SerializeField] private Sprite[] runFrames;
        [Tooltip("공중에 떠 있을 때 쓸 프레임")]
        [SerializeField] private Sprite jumpFrame;
        [Tooltip("달리기 프레임 교체 간격(초)")]
        [SerializeField] private float runFrameInterval = 0.12f;

        [Header("추격자 애니메이션")]
        [Tooltip("추격자 달리기 프레임 (번갈아 재생)")]
        [SerializeField] private Sprite[] chaserRunFrames;
        [Tooltip("추격자가 장애물을 넘을 때 쓸 프레임")]
        [SerializeField] private Sprite chaserJumpFrame;
        [Tooltip("장애물이 이 거리 안에 들어오면 추격자가 자동으로 점프한다")]
        [SerializeField] private float chaserJumpTrigger = 210f;

        /// <summary>추격자 한 명분 아트 (조직원별로 다른 스프라이트).</summary>
        [System.Serializable]
        public class ChaserSkin
        {
            public string label = "조직원";
            public Sprite[] runFrames;
            public Sprite jumpFrame;
            [Tooltip("화면상 키(px)")] public float height = 270f;
        }

        [Header("추가 추격자 (선두 다음으로 줄지어 따라옴)")]
        [Tooltip("친밀 100%를 못 채운 조직원 수만큼 뒤에 붙는다. 선두는 위 chaserRunFrames를 쓴다.")]
        [SerializeField] private ChaserSkin[] extraChaserSkins;
        [Tooltip("추격자끼리 벌어지는 간격(px)")]
        [SerializeField] private float chaserSpacing = 190f;

        [Header("장애물 아트")]
        [Tooltip("장애물 스프라이트 후보. 비어 있으면 색 박스로 생성한다.")]
        [SerializeField] private Sprite[] obstacleSprites;
        [Tooltip("장애물 높이 범위 (px). 폭은 스프라이트 비율에 맞춰 자동 계산.")]
        [SerializeField] private float obstacleMinHeight = 90f;
        [SerializeField] private float obstacleMaxHeight = 150f;
        [Tooltip("장애물 최대 폭. 가로로 넓으면 점프로 넘을 수 없어 이 값에 맞춰 줄인다.")]
        [SerializeField] private float obstacleMaxWidth = 190f;

        [Header("HUD")]
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text chaserText;
        [SerializeField] private TMP_Text messageText;
        [Tooltip("진행도 게이지 (0~1)")]
        [SerializeField] private Image progressFill;

        [Header("조작감")]
        [SerializeField] private float runSpeed = 420f;      // px/초 (장애물이 흘러오는 속도)
        [SerializeField] private float jumpVelocity = 1150f;
        [SerializeField] private float gravity = 3200f;
        [Tooltip("바닥 Y (플레이어 anchoredPosition.y 기준)")]
        [SerializeField] private float groundY = 0f;
        [Tooltip("장애물 히트박스 여유 (작을수록 관대)")]
        [SerializeField] private float hitPadding = 12f;

        [Header("결과 화면")]
        [Tooltip("탈출 성공 시 켤 화면")]
        [SerializeField] private GameObject successScreen;
        [Tooltip("붙잡혔을 때 켤 화면")]
        [SerializeField] private GameObject failScreen;
        [Tooltip("결과 화면이 없을 때 대신 띄울 버튼")]
        [SerializeField] private Button resultButton;
        [SerializeField] private GameObject fallbackScreen;

        // ── 난이도 (친밀 100% 인원 → 추격자 수·코스 길이·장애물 밀도) ──
        struct Difficulty
        {
            public int chasers;         // 추격 인원 (보스 포함)
            public float courseLength;  // 코스 길이 (px)
            public float gapMin, gapMax;// 장애물 간격
            public float startGap;      // 추격자와의 초기 거리
            public float hitPenalty;    // 장애물에 부딪혔을 때 좁혀지는 거리
            public float recoverPerSec; // 초당 벌어지는 거리
            public string flavor;       // 시작 문구
        }

        // 장애물 간격은 점프 체공 거리(약 480px)보다 넉넉히 커야 한다.
        // 안 그러면 착지 전에 다음 장애물이 도착해 점프 자체가 불가능해진다.
        static Difficulty ForMaxedCount(int maxed) => maxed switch
        {
            >= 3 => new Difficulty { chasers = 1, courseLength = 3200f, gapMin = 950f, gapMax = 1200f,
                                     startGap = 260f, hitPenalty = 45f, recoverPerSec = 14f,
                                     flavor = "조직원들이 보스 뒤에서 못 본 척 눈을 감아 준다." },
            2    => new Difficulty { chasers = 2, courseLength = 3600f, gapMin = 850f, gapMax = 1050f,
                                     startGap = 230f, hitPenalty = 55f, recoverPerSec = 11f,
                                     flavor = "정든 둘은 추격에서 빠졌다." },
            1    => new Difficulty { chasers = 3, courseLength = 4000f, gapMin = 750f, gapMax = 950f,
                                     startGap = 200f, hitPenalty = 65f, recoverPerSec = 8f,
                                     flavor = "한 명이 망설이다 멈춰 선다." },
            _    => new Difficulty { chasers = 4, courseLength = 4400f, gapMin = 660f, gapMax = 850f,
                                     startGap = 170f, hitPenalty = 75f, recoverPerSec = 6f,
                                     flavor = "아무도 편들어 주지 않는다. 전력 질주." }
        };

        const string ObstacleName = "Obstacle";

        Image playerImage;
        Image chaserImage;
        float chaserVelocityY;

        /// <summary>선두 뒤에 줄지어 따라오는 추격자들 (런타임 생성).</summary>
        class ExtraChaser
        {
            public RectTransform rt;
            public Image img;
            public ChaserSkin skin;
            public float velocityY;
            public float offset;   // 선두로부터 뒤로 떨어진 거리
        }
        readonly List<ExtraChaser> extras = new();
        const string ExtraChaserName = "ExtraChaser";
        float frameTimer;
        int frameIndex;

        readonly List<RectTransform> obstacles = new();
        Difficulty diff;
        float progress;      // 달린 거리
        float velocityY;     // 점프 속도
        float chaserGap;     // 추격자와의 거리 (0 이하 = 잡힘)
        float nextSpawnAt;   // 다음 장애물 생성 지점
        bool running;

        RunState Run => HearingBattleController.CurrentRun;

        private void OnEnable()
        {
            diff = ForMaxedCount(Run != null ? Run.MaxedAffinityCount : 0);

            progress = 0f;
            velocityY = 0f;
            chaserGap = diff.startGap;
            nextSpawnAt = 600f;   // 첫 장애물은 조금 뒤에
            running = true;

            frameTimer = 0f;
            frameIndex = 0;
            if (playerImage == null && player != null) playerImage = player.GetComponent<Image>();
            if (chaserImage == null && chaser != null) chaserImage = chaser.GetComponent<Image>();
            chaserVelocityY = 0f;
            if (chaser != null) chaser.anchoredPosition = new Vector2(chaser.anchoredPosition.x, groundY);
            BuildExtraChasers();

            ClearObstacles();
            if (player != null) player.anchoredPosition = new Vector2(player.anchoredPosition.x, groundY);
            if (resultButton != null) resultButton.gameObject.SetActive(false);
            if (messageText != null) messageText.text = diff.flavor;

            UpdateHud();
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>한 프레임 진행. (dt를 넘겨받아 테스트에서도 결정적으로 돌릴 수 있게 분리)</summary>
        public void Tick(float dt)
        {
            if (!running) return;

            AdvanceFrameClock(dt);
            HandleJump(dt);
            AnimatePlayer();
            Advance(dt);
            SpawnAndMoveObstacles(dt);
            ScrollBackgrounds(dt);
            UpdateChaser(dt);
            AnimateChaser();
            UpdateExtraChasers(dt);
            UpdateHud();

            if (chaserGap <= 0f) { Caught(); return; }
            if (progress >= diff.courseLength) Escaped();
        }

        // ── 점프 ─────────────────────────────────────────────
        private void HandleJump(float dt)
        {
            if (player == null) return;

            bool grounded = player.anchoredPosition.y <= groundY + 0.01f;
            if (grounded && JumpPressed())
            {
                velocityY = jumpVelocity;
                grounded = false;
            }

            if (!grounded || velocityY > 0f)
            {
                velocityY -= gravity * dt;
                var p = player.anchoredPosition;
                p.y += velocityY * dt;
                if (p.y <= groundY) { p.y = groundY; velocityY = 0f; }
                player.anchoredPosition = p;
            }
        }

        /// <summary>점프 입력. 이 프로젝트는 새 Input System을 쓰므로 그쪽을 우선한다.</summary>
        static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetMouseButtonDown(0);
#endif
        }

        /// <summary>
        /// 달리기 프레임 시계. 플레이어·추격자가 공유하므로 항상 돌아야 한다.
        /// (플레이어가 점프 중이라고 멈추면 추격자들까지 얼어붙는다)
        /// </summary>
        private void AdvanceFrameClock(float dt)
        {
            frameTimer += dt;
            if (frameTimer < runFrameInterval) return;
            frameTimer -= runFrameInterval;
            frameIndex++;
        }

        /// <summary>바닥에선 달리기 프레임 교대, 공중에선 점프 프레임.</summary>
        private void AnimatePlayer()
        {
            if (playerImage == null) return;

            bool grounded = player == null || player.anchoredPosition.y <= groundY + 0.01f;
            if (!grounded && jumpFrame != null) { playerImage.sprite = jumpFrame; return; }

            if (runFrames == null || runFrames.Length == 0) return;
            playerImage.sprite = runFrames[frameIndex % runFrames.Length];
        }

        /// <summary>추격자: 공중이면 점프 프레임, 아니면 달리기 프레임 교대.</summary>
        private void AnimateChaser()
        {
            if (chaserImage == null || chaser == null) return;

            bool grounded = chaser.anchoredPosition.y <= groundY + 0.01f;
            if (!grounded && chaserJumpFrame != null) { chaserImage.sprite = chaserJumpFrame; return; }

            if (chaserRunFrames != null && chaserRunFrames.Length > 0)
                chaserImage.sprite = chaserRunFrames[frameIndex % chaserRunFrames.Length];
        }

        private void Advance(float dt) => progress += runSpeed * dt;

        // ── 장애물 ───────────────────────────────────────────
        private void SpawnAndMoveObstacles(float dt)
        {
            if (obstacleLayer == null) return;

            if (progress >= nextSpawnAt)
            {
                SpawnObstacle();
                nextSpawnAt = progress + Random.Range(diff.gapMin, diff.gapMax);
            }

            float dx = runSpeed * dt;
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                if (o == null) { obstacles.RemoveAt(i); continue; }

                o.anchoredPosition -= new Vector2(dx, 0f);

                if (Overlaps(o)) { OnHit(o); continue; }

                if (o.anchoredPosition.x < -1200f)   // 화면 밖으로 지나감
                {
                    obstacles.RemoveAt(i);
                    Destroy(o.gameObject);
                }
            }
        }

        /// <summary>장애물 한 개 생성. 스프라이트가 있으면 그중 하나를 무작위로 쓴다.</summary>
        private void SpawnObstacle()
        {
            var go = new GameObject(ObstacleName, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(obstacleLayer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            var img = go.GetComponent<Image>();
            float h = Random.Range(obstacleMinHeight, obstacleMaxHeight);
            float w;

            if (obstacleSprites != null && obstacleSprites.Length > 0)
            {
                var sprite = obstacleSprites[Random.Range(0, obstacleSprites.Length)];
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
                // 폭은 원본 비율대로. 단, 너무 넓으면 점프로 넘을 수 없어 폭 기준으로 줄인다.
                float aspect = sprite.rect.width / sprite.rect.height;
                w = h * aspect;
                if (w > obstacleMaxWidth)
                {
                    w = obstacleMaxWidth;
                    h = w / aspect;
                }
            }
            else
            {
                img.color = new Color(0.42f, 0.24f, 0.12f, 1f);   // 아트 없을 때 폴백
                w = Random.Range(60f, 110f);
            }

            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(1250f, groundY);
            obstacles.Add(rt);
        }

        /// <summary>플레이어와 장애물의 사각형 겹침 검사 (같은 좌표계 기준).</summary>
        private bool Overlaps(RectTransform o)
        {
            if (player == null) return false;

            Vector2 pPos = player.anchoredPosition;
            Vector2 pSize = player.sizeDelta;
            Vector2 oPos = o.anchoredPosition;
            Vector2 oSize = o.sizeDelta;

            // 플레이어 pivot은 하단 중앙 가정
            float pLeft = pPos.x - pSize.x * 0.5f + hitPadding;
            float pRight = pPos.x + pSize.x * 0.5f - hitPadding;
            float pBottom = pPos.y;
            float pTop = pPos.y + pSize.y;

            float oLeft = oPos.x - oSize.x * 0.5f;
            float oRight = oPos.x + oSize.x * 0.5f;
            float oBottom = oPos.y;
            float oTop = oPos.y + oSize.y;

            return pRight > oLeft && pLeft < oRight && pTop > oBottom && pBottom < oTop;
        }

        private void OnHit(RectTransform o)
        {
            chaserGap -= diff.hitPenalty;
            if (messageText != null) messageText.text = "부딪혔다! 거리가 좁혀진다";

            obstacles.Remove(o);
            Destroy(o.gameObject);
        }

        // ── 배경 스크롤 ──────────────────────────────────────
        private void ScrollBackgrounds(float dt)
        {
            if (scrollingBackgrounds == null) return;
            float dx = runSpeed * dt * 0.6f;   // 배경은 조금 느리게 (원근감)

            foreach (var bg in scrollingBackgrounds)
            {
                if (bg == null) continue;
                bg.anchoredPosition -= new Vector2(dx, 0f);

                float w = bg.rect.width;
                if (bg.anchoredPosition.x <= -w)
                    bg.anchoredPosition += new Vector2(w * scrollingBackgrounds.Length, 0f);
            }
        }

        // ── 추격자 ───────────────────────────────────────────
        private void UpdateChaser(float dt)
        {
            chaserGap = Mathf.Min(chaserGap + diff.recoverPerSec * dt, diff.startGap);

            if (chaser == null || player == null) return;

            var c = chaser.anchoredPosition;
            c.x = player.anchoredPosition.x - chaserGap;

            // 앞에 장애물이 오면 알아서 뛰어넘는다 (추격자는 부딪히지 않는다)
            bool grounded = c.y <= groundY + 0.01f;
            if (grounded && ObstacleNear(c.x, chaser.sizeDelta.x))
            {
                chaserVelocityY = jumpVelocity;
                grounded = false;
            }

            if (!grounded || chaserVelocityY > 0f)
            {
                chaserVelocityY -= gravity * dt;
                c.y += chaserVelocityY * dt;
                if (c.y <= groundY) { c.y = groundY; chaserVelocityY = 0f; }
            }

            chaser.anchoredPosition = c;
        }

        /// <summary>난이도가 정한 추격 인원(선두 1명 제외)만큼 뒤따르는 추격자를 만든다.</summary>
        private void BuildExtraChasers()
        {
            foreach (var e in extras)
                if (e.rt != null) Destroy(e.rt.gameObject);
            extras.Clear();

            if (chaser == null || extraChaserSkins == null || extraChaserSkins.Length == 0) return;

            int need = Mathf.Max(0, diff.chasers - 1);   // 선두는 이미 씬에 있다
            for (int i = 0; i < need && i < extraChaserSkins.Length; i++)
            {
                var skin = extraChaserSkins[i];
                if (skin == null || skin.runFrames == null || skin.runFrames.Length == 0) continue;

                var go = new GameObject(ExtraChaserName + "_" + skin.label, typeof(RectTransform), typeof(Image));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(chaser.parent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);

                var img = go.GetComponent<Image>();
                img.sprite = skin.runFrames[0];
                img.preserveAspect = true;
                img.raycastTarget = false;

                float h = skin.height > 0f ? skin.height : 260f;
                float aspect = skin.runFrames[0].rect.width / skin.runFrames[0].rect.height;
                rt.sizeDelta = new Vector2(h * aspect, h);
                rt.anchoredPosition = new Vector2(chaser.anchoredPosition.x, groundY);
                rt.SetSiblingIndex(chaser.GetSiblingIndex());   // 선두보다 뒤에 그려지게

                extras.Add(new ExtraChaser { rt = rt, img = img, skin = skin, offset = chaserSpacing * (i + 1) });
            }
        }

        /// <summary>추가 추격자 이동 + 자동 점프.</summary>
        private void UpdateExtraChasers(float dt)
        {
            if (chaser == null) return;
            float leadX = chaser.anchoredPosition.x;

            foreach (var e in extras)
            {
                if (e.rt == null) continue;
                var p = e.rt.anchoredPosition;
                p.x = leadX - e.offset;

                bool grounded = p.y <= groundY + 0.01f;
                if (grounded && ObstacleNear(p.x, e.rt.sizeDelta.x))
                {
                    e.velocityY = jumpVelocity;
                    grounded = false;
                }
                if (!grounded || e.velocityY > 0f)
                {
                    e.velocityY -= gravity * dt;
                    p.y += e.velocityY * dt;
                    if (p.y <= groundY) { p.y = groundY; e.velocityY = 0f; }
                }
                e.rt.anchoredPosition = p;

                // 애니메이션
                bool air = p.y > groundY + 0.01f;
                if (air && e.skin.jumpFrame != null) e.img.sprite = e.skin.jumpFrame;
                else if (e.skin.runFrames.Length > 0) e.img.sprite = e.skin.runFrames[frameIndex % e.skin.runFrames.Length];
            }
        }

        /// <summary>주어진 x 위치 앞쪽에 점프해야 할 장애물이 있는지.</summary>
        private bool ObstacleNear(float x, float width)
        {
            float frontEdge = x + width * 0.5f - hitPadding;
            foreach (var o in obstacles)
            {
                if (o == null) continue;
                float left = o.anchoredPosition.x - o.sizeDelta.x * 0.5f;
                float d = left - frontEdge;
                if (d > 0f && d < chaserJumpTrigger) return true;
            }
            return false;
        }

        private void UpdateHud()
        {
            if (distanceText != null)
                distanceText.text = $"{Mathf.Min(progress, diff.courseLength):0} / {diff.courseLength:0}";
            if (chaserText != null)
                chaserText.text = $"추격 {diff.chasers}명 · 거리 {Mathf.Max(0f, chaserGap):0}";
            if (progressFill != null)
                progressFill.fillAmount = Mathf.Clamp01(progress / diff.courseLength);
        }

        // ── 결과 ─────────────────────────────────────────────
        private void Escaped()
        {
            running = false;
            if (messageText != null) messageText.text = "탈출 성공!";
            GoTo(successScreen);
        }

        private void Caught()
        {
            running = false;
            if (messageText != null) messageText.text = "붙잡혔다 — 작전 실패";
            GoTo(failScreen);
        }

        private void GoTo(GameObject screen)
        {
            if (screen != null)
            {
                gameObject.SetActive(false);
                screen.SetActive(true);
                return;
            }
            // 결과 화면이 아직 없으면 버튼으로 대체
            if (resultButton != null)
            {
                resultButton.onClick.RemoveListener(OnResultClicked);
                resultButton.onClick.AddListener(OnResultClicked);
                resultButton.gameObject.SetActive(true);
            }
        }

        private void OnResultClicked()
        {
            gameObject.SetActive(false);
            if (fallbackScreen != null) fallbackScreen.SetActive(true);
        }

        /// <summary>생성된 장애물만 정리한다. (플레이어·추격자가 같은 부모를 쓰므로 이름으로 구분)</summary>
        private void ClearObstacles()
        {
            foreach (var o in obstacles)
                if (o != null) Destroy(o.gameObject);
            obstacles.Clear();

            if (obstacleLayer == null) return;
            for (int i = obstacleLayer.childCount - 1; i >= 0; i--)
            {
                var child = obstacleLayer.GetChild(i);
                if (child.name == ObstacleName) Destroy(child.gameObject);
            }
        }
    }
}
