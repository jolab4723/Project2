using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_QuestSlot : MonoBehaviour
{
    public TextMeshProUGUI questNameText;

    private KY_QuestData questData;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    public void SetData(KY_QuestData data)
    {
        questData = data;
        questNameText.text = data.questName;
    }

    void OnClick()
    {
        KY_PopupManager.Instance.ShowQuestDetail(questData);
    }
}