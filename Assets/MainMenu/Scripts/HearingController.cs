using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 심문(함구령 뚫기) 화면. 열릴 때 탐색에서 모은 단서(키워드 카드)를 하단 카드 슬롯에 채우고,
/// 조직원 표정(기본/화남/의심/신뢰)을 상황에 맞게 바꾼다.
/// 나중에 의심도/친밀도 게이지에 SetExpression을 연결하면 됨.
/// </summary>
public class HearingController : MonoBehaviour
{
    public enum Mood { Neutral, Angry, Doubt, Trust }

    [Tooltip("하단 카드 슬롯의 이름 텍스트들 (좌→우)")]
    [SerializeField] private TMP_Text[] cardLabels;

    [Header("조직원 표정")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite angrySprite;
    [SerializeField] private Sprite doubtSprite;
    [SerializeField] private Sprite trustSprite;

    [Header("테스트")]
    [Tooltip("켜면 화면 열리고 3초 뒤 표정이 순환(테스트용). 실사용 땐 끄기.")]
    [SerializeField] private bool testAutoCycle = false;

    private void OnEnable()
    {
        Refresh();
        SetExpression(Mood.Neutral);
        if (testAutoCycle) StartCoroutine(TestCycle());
    }

    private IEnumerator TestCycle()
    {
        yield return new WaitForSecondsRealtime(3f);
        Mood[] moods = { Mood.Angry, Mood.Doubt, Mood.Trust, Mood.Neutral };
        while (true)
        {
            for (int i = 0; i < moods.Length; i++)
            {
                SetExpression(moods[i]);
                yield return new WaitForSecondsRealtime(1.2f);
            }
        }
    }

    /// <summary>조직원 표정 전환. (게이지/판정 결과에 연결해서 호출)</summary>
    public void SetExpression(Mood mood)
    {
        if (portraitImage == null) return;
        Sprite s = neutralSprite;
        switch (mood)
        {
            case Mood.Angry: s = angrySprite; break;
            case Mood.Doubt: s = doubtSprite; break;
            case Mood.Trust: s = trustSprite; break;
        }
        if (s != null) portraitImage.sprite = s;
    }

    // 인스펙터/이벤트에서 쓰기 쉽게 int 버전도 (0 기본,1 화남,2 의심,3 신뢰)
    public void SetExpression(int mood) => SetExpression((Mood)Mathf.Clamp(mood, 0, 3));

    public void Refresh()
    {
        if (cardLabels == null) return;
        var clues = ExplorationController.CollectedClues;
        for (int i = 0; i < cardLabels.Length; i++)
        {
            if (cardLabels[i] == null) continue;
            cardLabels[i].text = (i < clues.Count) ? clues[i] : "";
        }
    }
}
