using UnityEngine;
using UnityEngine.EventSystems;

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
