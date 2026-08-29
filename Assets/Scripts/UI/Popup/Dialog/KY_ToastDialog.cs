using DG.Tweening;
using UnityEngine;
using TMPro;

// 자동 소멸형 알림. 버튼/콜백 없이 메시지만 표시하고, 일정 시간 후 스스로 닫힘.
// 열고 닫힐 때 KY_CurtainEffect(세로 커튼 연출)를 재사용함.
public class KY_ToastDialog : KY_PopupBase
{
    [Header("Text")]
    [SerializeField] private TMP_Text messageText;

    [Header("Effect")]
    [SerializeField] private KY_CurtainEffect curtainEffect; // axis = Vertical로 세팅해두면 됨

    [Header("Timing")]
    [SerializeField] private float displayDuration = 1.5f; // 화면에 떠있는 시간

    private Tween autoCloseTween;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show(string message)
    {
        autoCloseTween?.Kill();

        messageText.text = message;
        Open(); // KY_PopupBase - SetActive(true)

        curtainEffect.Open(); // 커튼 펼쳐짐

        autoCloseTween = DOVirtual.DelayedCall(displayDuration, () =>
        {
            curtainEffect.Close().OnComplete(() => Close()); // 압축 끝나면 진짜로 꺼짐
        });
    }
}