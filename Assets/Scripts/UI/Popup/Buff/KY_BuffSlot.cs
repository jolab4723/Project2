using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using ItemSystem;

// 버프 표시 팝업의 슬롯 코드입니다.
public class KY_BuffSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("구성요소")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI stackText;

    private BuffInstance myData;

    public event Action<BuffInstance> OnSlotHoverEnter;
    public event Action OnSlotHoverExit;

    // 슬롯이 처음 생성되거나, 목록 구성이 바뀌었을 때 호출 (Instantiate 직후 등)
    public void Render(BuffInstance data)
    {
        myData = data;

        iconImage.sprite = data.source.BuffIcon;

        // BuffDisplayName은 SO에 적힌 한국어 원문이라 언어를 따라가지 않는다. HUD 버프 아이콘 툴팁과
        // 같은 경로(BuffLabelDatabase/UniqueEffectLabelDatabase 조회)로 현재 언어 이름을 받는다.
        // 스택은 아래 stackText가 따로 표시하므로 BuildName이 아니라 BuildBaseName을 쓴다.
        nameText.text = BuffTextComposer.BuildBaseName(data.source);

        bool isPermanent = data.source.IsPermanent;
        timeText.gameObject.SetActive(!isPermanent);

        bool hasStack = data.stackCount > 1;
        stackText.gameObject.SetActive(hasStack);
        if (hasStack)
            stackText.text = "x" + data.stackCount;

        RefreshTimeOnly(); // 초기 표시도 시간 텍스트를 채워야 하니 같이 호출
    }

    // 슬롯 구성은 그대로, 남은 시간만 매 프레임 갱신할 때 호출 (Instantiate 없이 가벼움)
    public void RefreshTimeOnly()
    {
        if (myData == null || myData.source.IsPermanent)
            return;

        timeText.text = FormatTime(myData.remainingTime);
    }

    string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    public void OnPointerEnter(PointerEventData eventData) => OnSlotHoverEnter?.Invoke(myData);
    public void OnPointerExit(PointerEventData eventData) => OnSlotHoverExit?.Invoke();
}