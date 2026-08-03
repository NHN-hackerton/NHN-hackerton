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
        [Tooltip("추격 무리를 화면상 추가로 뒤로 밀어내는 거리. 게임 수치(추격 거리)는 건드리지 않고 보기만 벌린다.")]
        [SerializeField] private float chaserVisualOffset = 300f;

        [Header("연출")]
        [Tooltip("발밑 그림자 스프라이트. 비어 있으면 그림자 없음.")]
        [SerializeField] private Sprite shadowSprite;
        [Tooltip("그림자 폭 = 캐릭터 폭 × 이 값")]
        [SerializeField] private float shadowWidthScale = 0.75f;
        [Tooltip("뒤쪽 추격자를 어둡게 (0=그대로, 1=완전 검정). 0이면 명암 없음.")]
        [SerializeField] private float depthDarken = 0f;

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

        [Header("시작 연출 (중앙까지 달려나가기)")]
        [Tooltip("형사가 자리를 잡을 x 위치. 여기 도착하면 장애물이 나오기 시작한다.")]
        [SerializeField] private float playerHomeX = 820f;
        [Tooltip("시작 지점에서 위 위치까지 달려가는 속도 (평소보다 빠르게)")]
        [SerializeField] private float introDashSpeed = 900f;
        [Tooltip("장애물이 생성되는 x (화면 오른쪽 밖)")]
        [SerializeField] private float obstacleSpawnX = 2100f;

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
            >= 3 => new Difficulty { chasers = 1, courseLength = 9800f, gapMin = 950f, gapMax = 1200f,
                                     startGap = 260f, hitPenalty = 45f, recoverPerSec = 11f,
                                     flavor = "조직원들이 보스 뒤에서 못 본 척 눈을 감아 준다." },
            2    => new Difficulty { chasers = 2, courseLength = 10400f, gapMin = 850f, gapMax = 1050f,
                                     startGap = 230f, hitPenalty = 55f, recoverPerSec = 9f,
                                     flavor = "정든 둘은 추격에서 빠졌다." },
            1    => new Difficulty { chasers = 3, courseLength = 11000f, gapMin = 750f, gapMax = 950f,
                                     startGap = 200f, hitPenalty = 65f, recoverPerSec = 7f,
                                     flavor = "한 명이 망설이다 멈춰 선다." },
            _    => new Difficulty { chasers = 4, courseLength = 11500f, gapMin = 660f, gapMax = 850f,
                                     startGap = 170f, hitPenalty = 75f, recoverPerSec = 5.5f,
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
            public RectTransform shadow;
        }

        RectTransform playerShadow, chaserShadow;
        const string ShadowName = "Shadow";
        readonly List<ExtraChaser> extras = new();
        const string ExtraChaserName = "ExtraChaser";

        float playerStartX;   // 씬에 배치된 원래 x (되돌리기용)
        bool intro;           // 중앙까지 달려가는 중

        // 장애물 셔플백: 한 바퀴 안에서 모든 종류가 한 번씩 나오고, 같은 게 연달아 나오지 않는다
        readonly List<int> obstacleBag = new();
        int lastObstacleIndex = -1;
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
            obstacleBag.Clear();
            lastObstacleIndex = -1;
            if (playerImage == null && player != null) playerImage = player.GetComponent<Image>();
            if (chaserImage == null && chaser != null) chaserImage = chaser.GetComponent<Image>();
            chaserVelocityY = 0f;
            if (chaser != null) chaser.anchoredPosition = new Vector2(chaser.anchoredPosition.x, groundY);
            playerShadow = MakeShadow(player);
            chaserShadow = MakeShadow(chaser);
            BuildExtraChasers();

            ClearObstacles();
            if (player != null)
            {
                if (playerStartX == 0f) playerStartX = player.anchoredPosition.x;   // 최초 1회 기억
                player.anchoredPosition = new Vector2(playerStartX, groundY);
                intro = playerStartX < playerHomeX - 1f;
            }
            else intro = false;
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
            RunIntro(dt);
            HandleJump(dt);
            AnimatePlayer();
            Advance(dt);
            SpawnAndMoveObstacles(dt);
            ScrollBackgrounds(dt);
            UpdateChaser(dt);
            AnimateChaser();
            UpdateExtraChasers(dt);
            UpdateShadow(playerShadow, player);
            UpdateShadow(chaserShadow, chaser);
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

        /// <summary>시작 연출: 형사가 자기 자리(중앙)까지 빠르게 달려나간다. 그동안 장애물은 안 나온다.</summary>
        private void RunIntro(float dt)
        {
            if (!intro || player == null) return;

            var p = player.anchoredPosition;
            p.x = Mathf.MoveTowards(p.x, playerHomeX, introDashSpeed * dt);
            player.anchoredPosition = p;

            if (p.x >= playerHomeX - 0.5f)
            {
                intro = false;
                nextSpawnAt = progress + 200f;   // 자리 잡은 직후 첫 장애물
            }
        }

        private void Advance(float dt) => progress += runSpeed * dt;

        // ── 장애물 ───────────────────────────────────────────
        private void SpawnAndMoveObstacles(float dt)
        {
            if (obstacleLayer == null) return;

            if (!intro && progress >= nextSpawnAt)   // 시작 연출 중엔 장애물 없음
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

                if (o.anchoredPosition.x < -400f)   // 화면 왼쪽 밖으로 지나감
                {
                    obstacles.RemoveAt(i);
                    Destroy(o.gameObject);
                }
            }
        }

        /// <summary>
        /// 다음에 쓸 장애물 인덱스. 셔플백에서 하나씩 꺼내므로 종류가 골고루 나오고,
        /// 백을 새로 채울 때 직전 것과 겹치면 뒤로 밀어 연속 등장을 막는다.
        /// </summary>
        private int NextObstacleIndex()
        {
            int count = obstacleSprites.Length;
            if (count == 1) return 0;

            if (obstacleBag.Count == 0)
            {
                for (int i = 0; i < count; i++) obstacleBag.Add(i);
                // 셔플
                for (int i = obstacleBag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (obstacleBag[i], obstacleBag[j]) = (obstacleBag[j], obstacleBag[i]);
                }
                // 첫 장이 직전과 같으면 뒤쪽과 교환
                if (obstacleBag[0] == lastObstacleIndex && obstacleBag.Count > 1)
                    (obstacleBag[0], obstacleBag[obstacleBag.Count - 1]) = (obstacleBag[obstacleBag.Count - 1], obstacleBag[0]);
            }

            int pick = obstacleBag[0];
            obstacleBag.RemoveAt(0);
            lastObstacleIndex = pick;
            return pick;
        }

        /// <summary>장애물 한 개 생성. 스프라이트가 있으면 셔플백에서 골라 쓴다.</summary>
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
                var sprite = obstacleSprites[NextObstacleIndex()];
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
            rt.anchoredPosition = new Vector2(obstacleSpawnX, groundY);   // 화면 오른쪽 밖에서 등장
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
            c.x = player.anchoredPosition.x - chaserGap - chaserVisualOffset;

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

        /// <summary>발밑 그림자를 만든다. 캐릭터보다 먼저 그려지도록 형제 순서를 앞으로 보낸다.</summary>
        private RectTransform MakeShadow(RectTransform owner)
        {
            if (shadowSprite == null || owner == null) return null;

            var old = owner.parent.Find(ShadowName + "_" + owner.name);
            if (old != null) Destroy(old.gameObject);

            var go = new GameObject(ShadowName + "_" + owner.name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(owner.parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float w = owner.sizeDelta.x * shadowWidthScale;
            rt.sizeDelta = new Vector2(w, w * 0.26f);

            var img = go.GetComponent<Image>();
            img.sprite = shadowSprite;
            img.raycastTarget = false;
            rt.SetSiblingIndex(0);   // 모든 캐릭터 뒤에
            return rt;
        }

        /// <summary>그림자를 캐릭터 발밑에 붙인다. 공중에 뜰수록 작아지고 흐려진다.</summary>
        private void UpdateShadow(RectTransform shadow, RectTransform owner)
        {
            if (shadow == null || owner == null) return;

            float airHeight = Mathf.Max(0f, owner.anchoredPosition.y - groundY);
            float t = Mathf.Clamp01(airHeight / 420f);          // 점프 최고점 기준
            float shrink = Mathf.Lerp(1f, 0.55f, t);

            float w = owner.sizeDelta.x * shadowWidthScale * shrink;
            shadow.sizeDelta = new Vector2(w, w * 0.26f);
            shadow.anchoredPosition = new Vector2(owner.anchoredPosition.x, groundY + 14f);

            var img = shadow.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = Mathf.Lerp(1f, 0.35f, t);
                img.color = c;
            }
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

                // 뒤로 갈수록 어둡게 (붉은 조명 속 원근감)
                float dark = 1f - depthDarken * (i + 1);
                img.color = new Color(dark, dark * 0.97f, dark * 0.95f, 1f);

                extras.Add(new ExtraChaser {
                    rt = rt, img = img, skin = skin,
                    offset = chaserSpacing * (i + 1),
                    shadow = MakeShadow(rt)
                });
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
                UpdateShadow(e.shadow, e.rt);

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
