using UnityEngine;
using UnityEngine.UI;

namespace TopDogDetective.MainMenu
{
    /// <summary>
    /// 낡은 전등이 깜빡이는 연출. 대상 Image의 밝기를 흔든다.
    ///
    /// 일정한 사인파로 흔들면 "숨쉬는" 느낌이라 전등처럼 안 보인다.
    /// 그래서 대부분은 미세하게 떨리다가(idle), 이따금 확 꺼졌다 켜지는(glitch)
    /// 두 단계로 나눴다. 느와르 간판 조명이 나가려다 버티는 느낌.
    /// </summary>
    public class LampFlicker : MonoBehaviour
    {
        [Tooltip("깜빡일 대상. 비우면 이 오브젝트의 Image를 쓴다.")]
        [SerializeField] private Image target;
        [Tooltip("켜면 색 대신 투명도를 흔든다. 빛 오버레이(글로우)에 쓴다 — " +
                 "화면 전체 밝기를 흔들면 정전처럼 보이고 티도 잘 안 난다.")]
        [SerializeField] private bool driveAlpha;

        [Header("평소 미세한 떨림")]
        [Tooltip("평소 밝기 범위 (1 = 원래 색)")]
        [SerializeField] private float idleMin = 0.94f;
        [SerializeField] private float idleMax = 1.00f;
        [Tooltip("떨림 속도")]
        [SerializeField] private float idleSpeed = 7f;
        // 펄린 노이즈는 0~1을 고르게 쓰지 않고 대부분 0.3~0.7에 몰린다.
        // 그래서 밝기 범위를 넓게 줘도 실제로는 절반도 안 흔들린다.
        // 가운데를 기준으로 값을 벌려(contrast) 지정한 범위를 제대로 쓰게 만든다.
        [Tooltip("노이즈를 범위 끝까지 쓰게 벌리는 정도. 1이면 원래 노이즈, 크면 대비가 세진다.")]
        [SerializeField] private float noiseContrast = 2.8f;

        [Header("이따금 확 깜빡임 (정전처럼 꺼지는 연출)")]
        [Tooltip("끄면 미세한 떨림만 남는다. 빛이 아예 꺼지는 느낌이 싫을 때 끈다.")]
        [SerializeField] private bool useGlitch = true;
        [Tooltip("깜빡임 사이 간격(초) 범위")]
        [SerializeField] private float glitchGapMin = 2.5f;
        [SerializeField] private float glitchGapMax = 7f;
        [Tooltip("깜빡일 때 어두워지는 정도")]
        [SerializeField] private float glitchDark = 0.55f;
        [Tooltip("한 번 깜빡이는 데 걸리는 시간(초)")]
        [SerializeField] private float glitchTime = 0.16f;
        [Tooltip("한 번에 몇 번 연달아 깜빡일지")]
        [SerializeField] private int glitchBurstMax = 3;

        Color baseColor;
        float noiseSeed;
        float nextGlitchAt;
        float glitchUntil;
        int glitchLeft;

        private void Awake()
        {
            if (target == null) target = GetComponent<Image>();
            if (target != null) baseColor = target.color;
            noiseSeed = Random.value * 100f;
        }

        private void OnEnable()
        {
            // 화면을 다시 열었을 때 원래 밝기에서 시작
            if (target != null) target.color = baseColor;
            ScheduleNextGlitch();
        }

        private void OnDisable()
        {
            if (target != null) target.color = baseColor;
        }

        private void ScheduleNextGlitch()
        {
            nextGlitchAt = Time.unscaledTime + Random.Range(glitchGapMin, glitchGapMax);
            glitchLeft = 0;
            glitchUntil = 0f;
        }

        private void Update()
        {
            if (target == null) return;
            float now = Time.unscaledTime;

            // 평소: 펄린 노이즈로 불규칙하게 떨림 (사인파보다 전등답다)
            float n = Mathf.PerlinNoise(noiseSeed, now * idleSpeed);
            n = Mathf.Clamp01((n - 0.5f) * noiseContrast + 0.5f);   // 범위 끝까지 쓰게 벌림
            float k = Mathf.Lerp(idleMin, idleMax, n);

            // 깜빡임 구간 시작
            if (useGlitch && glitchLeft <= 0 && now >= nextGlitchAt)
                glitchLeft = Random.Range(1, glitchBurstMax + 1);

            if (glitchLeft > 0)
            {
                if (now >= glitchUntil)
                {
                    glitchUntil = now + glitchTime;
                    glitchLeft--;
                    if (glitchLeft <= 0) ScheduleNextGlitch();
                }
                // 깜빡이는 동안은 어둡게 (다음 프레임에 복귀하며 튀는 느낌)
                float t = Mathf.PingPong((glitchUntil - now) / glitchTime, 1f);
                k *= Mathf.Lerp(1f, glitchDark, t);
            }

            target.color = driveAlpha
                ? new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k)
                : new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
        }
    }
}
