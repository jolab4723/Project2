using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class KY_QuestSlot : MonoBehaviour
{
    [Header("Texts")]
    [FormerlySerializedAs("questNameText")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subText;

    private KY_QuestData questData;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("[KY_QuestSlot] Button 컴포넌트가 없습니다.", this);
            return;
        }

        button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    public void SetData(KY_QuestData data)
    {
        questData = data;

        if (data == null)
        {
            if (titleText != null)
                titleText.text = string.Empty;

            if (subText != null)
                subText.text = string.Empty;

            return;
        }

        if (titleText != null)
            titleText.text = data.questName;

        if (subText != null)
            subText.text = data.description;
    }

    void OnClick()
    {
        if (questData == null)
        {
            Debug.LogWarning("[KY_QuestSlot] 표시할 퀘스트 데이터가 없습니다.", this);
            return;
        }

        if (KY_PopupManager.Instance == null)
        {
            Debug.LogError("[KY_QuestSlot] KY_PopupManager를 찾을 수 없습니다.", this);
            return;
        }

        KY_PopupManager.Instance.ShowQuestDetail(questData);
    }
}
