using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>보상 아이콘과 수량을 표시하고, 완료된 의뢰에서는 미수령 수량과 수령 완료를 구분한다.</summary>
public sealed class KY_QuestRewardSlot : MonoBehaviour
{
    // SW 수정
    [SerializeField] private Image icon;
    [SerializeField] private Image frame;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject receivedCheck;

    public void SetData(KY_QuestRewardData data)
    {
        bool received = data.questCompleted && data.remainingAmount <= 0;
        bool pending = data.questCompleted && data.remainingAmount > 0;
        icon.sprite = data.icon;
        icon.enabled = data.icon != null;
        icon.color = new Color(1f, 1f, 1f, received ? 0.45f : 1f);
        frame.color = pending ? new Color(0.9f, 0.7f, 0.29f, 1f) : new Color(0.3f, 0.78f, 0.8f, 1f);
        amountText.text = $"×{(pending ? data.remainingAmount : data.amount):N0}";
        amountText.color = pending ? new Color(1f, 0.83f, 0.42f, 1f) : Color.white;
        nameText.text = received ? "수령 완료" : pending ? "미수령" : data.name;
        nameText.color = pending ? new Color(1f, 0.83f, 0.42f, 1f) : new Color(0.76f, 0.88f, 0.89f, 1f);
        receivedCheck.SetActive(received);
    }
}
