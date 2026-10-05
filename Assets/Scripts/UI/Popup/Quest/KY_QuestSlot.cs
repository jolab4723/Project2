using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

/// <summary>퀘스트 하나의 제목과 요약을 표시하고 상세 팝업 열기를 요청한다.</summary>
public class KY_QuestSlot : MonoBehaviour
{
    [Header("Texts")]
    [FormerlySerializedAs("questNameText")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subText;

    private KY_QuestData questData;
    private UnityEngine.UI.Button button;
    private QuestLabelDatabaseSO questLabels;

    /// <summary>버튼 클릭 이벤트를 연결한다.</summary>
    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        // SW 수정 : 공용 라벨을 재사용하고 신규 상태 문구가 없으면 한국어로 표시한다.
        questLabels = Resources.Load<QuestLabelDatabaseSO>("DataFiles/QuestData/3. GeneratedAssets/QuestLabelDatabase");

        if (button == null)
        {
            Debug.LogError("[KY_QuestSlot] Button 컴포넌트가 없습니다.", this);
            return;
        }

        button.onClick.AddListener(OnClick);
    }

    /// <summary>파괴될 때 버튼 이벤트를 해제한다.</summary>
    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    /// <summary>슬롯에 표시할 퀘스트 데이터를 설정한다.</summary>
    public void SetData(KY_QuestData data)
    {
        questData = data;

        if (data == null)
        {
            if (titleText != null)
                titleText.text = string.Empty;

            if (subText != null)
                subText.text = string.Empty;

            if (button != null)
                button.interactable = false;

            return;
        }

        if (titleText != null)
            titleText.text = (data.questName ?? string.Empty) + (data.isCompleted
                ? " · " + Label("quest_ui.objective_completed", "목표 달성") : string.Empty);

        if (subText != null)
            subText.text = data.isCompleted
                ? data.rewardPending ? Label("quest_ui.reward_pending", "보상 대기") : Label("quest_ui.reward_received", "수령 완료")
                : string.IsNullOrWhiteSpace(data.objectiveTypeLabel) ? data.description ?? string.Empty : data.objectiveTypeLabel;

        if (button != null)
            button.interactable = true;
    }

    /// <summary>누락된 상태 라벨의 키 문자열이 화면에 노출되지 않게 한다.</summary>
    private string Label(string key, string fallback)
    {
        string value = questLabels != null ? questLabels.GetLabel(key) : null;
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    /// <summary>현재 슬롯의 퀘스트 상세 팝업을 연다.</summary>
    private void OnClick()
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
