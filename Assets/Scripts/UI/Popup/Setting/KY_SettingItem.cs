using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>설정 항목 호버 시 해당 항목의 설명을 설명 패널에 표시한다.</summary>
public class KY_SettingItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea]
    [Tooltip("다국어 조회에 실패했을 때 쓰는 원문. descriptionKey가 비어 있으면 이 값이 그대로 나온다.")]
    public string description;

    [Header("다국어(비워두면 description을 그대로 사용)")]
    [SerializeField] private string descriptionKey;
    [SerializeField] private UILabelDatabaseSO labelDatabase;

    public void OnPointerEnter(PointerEventData eventData)
    {
        string localizedDescription = (labelDatabase != null && !string.IsNullOrEmpty(descriptionKey))
            ? labelDatabase.GetLabel(descriptionKey)
            : description;

        KY_SettingDescriptionPanel.Instance.Show(localizedDescription);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        KY_SettingDescriptionPanel.Instance.Hide();
    }
}
