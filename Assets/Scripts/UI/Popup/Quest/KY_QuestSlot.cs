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

    // WJ 이우진 추가(2026-10-06): 목표 달성 시 아이콘 교체, 보상까지 모두 받으면 테두리·하이라이트를 회색으로.
    [Header("State Visual")]
    [Tooltip("진행 상태 아이콘. 목표를 달성하면 completedIcon으로 바뀌고, 진행 중이면 원래 이미지로 돌아간다.")]
    [SerializeField] private UnityEngine.UI.Image stateIcon;
    [SerializeField] private Sprite completedIcon;
    [Tooltip("KY_ButtonColorEffect 대상이 아닌 테두리 장식(Line_Left/Right 등). 효과 대상(Outline·아이콘)은 효과 쪽에서 함께 바뀐다.")]
    [SerializeField] private UnityEngine.UI.Graphic[] extraBorderGraphics;
    [SerializeField] private Color rewardReceivedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color rewardReceivedHoverColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    private KY_QuestData questData;
    private UnityEngine.UI.Button button;
    private QuestLabelDatabaseSO questLabels;
    private KY_ButtonColorEffect colorEffect;
    private Sprite defaultStateIcon;
    private Color[] defaultBorderColors;

    /// <summary>버튼 클릭 이벤트를 연결한다.</summary>
    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        colorEffect = GetComponent<KY_ButtonColorEffect>();
        // 슬롯은 풀에서 재사용되므로 처음 모습을 기억해 두고 진행 중 상태로 돌아갈 때 복원한다.
        defaultStateIcon = stateIcon != null ? stateIcon.sprite : null;
        if (extraBorderGraphics != null)
        {
            defaultBorderColors = new Color[extraBorderGraphics.Length];
            for (int i = 0; i < extraBorderGraphics.Length; i++)
                defaultBorderColors[i] = extraBorderGraphics[i] != null ? extraBorderGraphics[i].color : Color.white;
        }
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

            ApplyStateVisual(false, false);
            return;
        }

        ApplyStateVisual(data.isCompleted, data.isCompleted && !data.rewardPending);

        // SW 수정 : 목표 완료 여부와 보상 수령 상태를 구분해 표시한다.
        if (titleText != null)
        {
            titleText.text = data.questName ?? string.Empty;
            if (data.isCompleted)
                titleText.text += " · " + Label("quest_ui.objective_completed", "목표 달성");
        }

        if (subText != null)
        {
            if (data.isCompleted)
            {
                subText.text = data.rewardPending
                    ? Label("quest_ui.reward_pending", "보상 대기")
                    : Label("quest_ui.reward_received", "수령 완료");
            }
            else
            {
                subText.text = string.IsNullOrWhiteSpace(data.objectiveTypeLabel)
                    ? data.description ?? string.Empty
                    : data.objectiveTypeLabel;
            }
        }

        if (button != null)
            button.interactable = true;
    }

    /// <summary>
    /// WJ 이우진 추가(2026-10-06): 목표 달성이면 완료 아이콘, 보상까지 모두 받았으면 테두리·하이라이트를 회색으로 바꾼다.
    /// 그 외에는 처음 이미지·색으로 되돌린다(풀 재사용 대비).
    /// </summary>
    private void ApplyStateVisual(bool objectiveCompleted, bool allRewardsReceived)
    {
        if (stateIcon != null)
            stateIcon.sprite = objectiveCompleted && completedIcon != null ? completedIcon : defaultStateIcon;

        if (colorEffect != null)
        {
            if (allRewardsReceived)
                colorEffect.SetStateColors(rewardReceivedColor, rewardReceivedHoverColor);
            else
                colorEffect.ResetStateColors();
        }

        if (extraBorderGraphics == null || defaultBorderColors == null)
            return;

        for (int i = 0; i < extraBorderGraphics.Length; i++)
        {
            if (extraBorderGraphics[i] != null)
                extraBorderGraphics[i].color = allRewardsReceived ? rewardReceivedColor : defaultBorderColors[i];
        }
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
