using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 휴식 팝업의 표시와 버튼 흐름을 담당한다.
/// 실제 체력·포션·크레딧 처리는 외부 시스템에서 계산한 값을 SetRecoveryPreview로 주입한다.
/// </summary>
public class KY_RestPopup : MonoBehaviour
{
    [Header("표시 텍스트")]
    [SerializeField] private TMP_Text healthRecoveryText;
    [SerializeField] private TMP_Text potionRecoveryText;
    [SerializeField] private TMP_Text costText;

    [Header("완료 알림(팝업이 닫히며 나오는 것이기에 팝업 외부에서 연결)")]
    [Tooltip("팝업 바깥에서 완료 문구와 배경을 함께 표시할 패널")]
    [SerializeField] private GameObject completionMessagePanel;
    [SerializeField] private TMP_Text completionMessageText;
    [Min(0f)] [SerializeField] private float completionMessageDuration = 2.5f;

    [Header("버튼")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private int healthAmount;
    private int potionAmount;
    private int cost;
    private int currentCredits;
    private Coroutine completionMessageRoutine;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancelClicked);

        HideCompletionMessage();
    }

    private void OnDestroy()
    {
        if (completionMessageRoutine != null)
            StopCoroutine(completionMessageRoutine);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
    }

    private void Update()
    {
        if (gameObject.activeSelf && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleCancelClicked();
        }
    }

    /// <summary>팝업을 열고 현재 회복 미리보기 값을 표시한다.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        HideCompletionMessage();
        RefreshView();
    }

    /// <summary>팝업을 닫는다.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>외부 회복 시스템이 계산한 미리보기 값을 주입한다.</summary>
    public void SetRecoveryPreview(int health, int potions, int requiredCost, int credits)
    {
        healthAmount = Mathf.Max(0, health);
        potionAmount = Mathf.Max(0, potions);
        cost = Mathf.Max(0, requiredCost);
        currentCredits = Mathf.Max(0, credits);
        RefreshView();
    }

    private void RefreshView()
    {
        if (healthRecoveryText != null)
            healthRecoveryText.text = $"체력 회복     +{healthAmount}";

        if (potionRecoveryText != null)
            potionRecoveryText.text = $"포션 충전     +{potionAmount}";

        if (costText != null)
            costText.text = $"필요 크레딧     {cost}";

        if (confirmButton != null)
            confirmButton.interactable = currentCredits >= cost;
    }

    private void HandleConfirmClicked()
    {
        if (currentCredits < cost)
            return;

        if (completionMessageText != null)
            completionMessageText.text =
                $"{cost} 크레딧을 소비하여 체력 {healthAmount}, 포션 {potionAmount}개를 채웠습니다.";

        if (completionMessagePanel != null)
            completionMessagePanel.SetActive(true);

        if (completionMessageRoutine != null)
            StopCoroutine(completionMessageRoutine);

        completionMessageRoutine = StartCoroutine(HideCompletionMessageAfterDelay());
    }

    private void HandleCancelClicked()
    {
        Close();
    }

    private void HideCompletionMessage()
    {
        if (completionMessageRoutine != null)
        {
            StopCoroutine(completionMessageRoutine);
            completionMessageRoutine = null;
        }

        if (completionMessagePanel != null)
            completionMessagePanel.SetActive(false);
    }

    private IEnumerator HideCompletionMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(completionMessageDuration);
        completionMessageRoutine = null;
        HideCompletionMessage();
    }
}
