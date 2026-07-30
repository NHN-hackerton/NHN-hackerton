using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자식 발자국(PawIcon)들의 알파를 파도처럼 순차로 밝혔다 어둡혔다 해서
/// "이 방향(문)으로 가봐" 흐르는 트레일 효과를 낸다.
/// </summary>
public class PawTrail : MonoBehaviour
{
    [SerializeField] private float speed = 1.4f;       // 파도 속도
    [SerializeField] private float phaseGap = 0.5f;    // 발자국 사이 위상차
    [SerializeField] private float baseAlpha = 0.12f;
    [SerializeField] private float peakAlpha = 0.7f;

    private Graphic[] paws;
    private float t;

    private void Awake()
    {
        paws = GetComponentsInChildren<Graphic>();
    }

    private void Update()
    {
        if (paws == null || paws.Length == 0) return;
        t += Time.deltaTime * speed;
        for (int i = 0; i < paws.Length; i++)
        {
            float phase = t - i * phaseGap;
            float a = Mathf.Lerp(baseAlpha, peakAlpha, Mathf.Sin(phase) * 0.5f + 0.5f);
            var c = paws[i].color; c.a = a; paws[i].color = c;
        }
    }
}
