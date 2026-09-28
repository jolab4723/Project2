using UnityEngine;

/// <summary>
/// 휴식 NPC. 상호작용하면(YJ_ClickNPC.onClicked에 이 컴포넌트의 Interact()를 연결) 휴식 팝업(KY_RestPopup)을
/// 열고 실제 회복 미리보기 값을 계산해 넣는다. QuestBoardNPC와 같은 관례로, 팝업이 비활성 상태여도
/// FindFirstObjectByType(Include)로 찾아 직접 연다.
///
/// 팝업의 KY_RestPopup.OnConfirmed(확인 버튼이 비용 검사를 통과했을 때)를 구독해서 실제 크레딧 차감,
/// 체력 회복, 포션 충전을 적용한다 - 팝업 자체는 표시/버튼 흐름만 담당하고 실제 처리는 여기서 한다.
///
/// 휴식 규칙: 비용 = creditPerAct × 현재 액트 번호(고정), 체력 = 최대 체력의 baseHealPercent%
/// (캠프 회복 패시브 해금 시 패시브 수치), 포션 = potionRechargeAmount회 충전. 둘 다 최대치까지만 채운다.
///
/// !! 회복 비율/비용 수치는 RestData.xlsx에서 관리한다(DataLoader/Rest Data 파이프라인 → RestDatabaseSO).
///    아래 SerializeField 값은 DB를 못 찾았을 때만 쓰는 폴백이다.
/// </summary>
public class CampRestNPC : MonoBehaviour
{
    [Header("휴식 밸런스 데이터 (비워두면 Resources에서 자동으로 찾아 쓴다)")]
    [SerializeField] private RestDatabaseSO restDatabase;

    [Header("데이터를 못 찾았을 때 쓰는 폴백 값")]
    [Tooltip("캠프 회복 증가 패시브 미해금 시 기본 회복 비율(최대 체력 대비 %)")]
    [SerializeField] private float baseHealPercent = 25f;
    [Tooltip("휴식 1회 비용 = 이 값 × 현재 액트 번호")]
    [SerializeField] private int creditPerAct = 500;
    [Tooltip("휴식 1회로 채우는 포션 충전 수")]
    [SerializeField] private int potionRechargeAmount = 3;

    private const string RestDatabaseResourcePath = "DataFiles/RestData/3. GeneratedAssets/RestDatabase";

    private KY_RestPopup restPopup;
    private int pendingHealthAmount;
    private int pendingPotionAmount;
    private int pendingCost;

    private void Awake()
    {
        if (restDatabase == null)
            restDatabase = Resources.Load<RestDatabaseSO>(RestDatabaseResourcePath);

        restPopup = FindFirstObjectByType<KY_RestPopup>(FindObjectsInactive.Include);
        if (restPopup != null)
            restPopup.OnConfirmed += HandleConfirmed;
    }

    private void OnDestroy()
    {
        if (restPopup != null)
            restPopup.OnConfirmed -= HandleConfirmed;
    }

    public void Interact()
    {
        if (restPopup == null)
        {
            Debug.LogWarning("[CampRestNPC] KY_RestPopup을 씬에서 찾지 못했습니다.");
            return;
        }

        WBH_PlayerStatus status = GetPlayerStatus();
        PlayerWallet wallet = InventoryController.Instance != null ? InventoryController.Instance.PlayerWallet : null;
        if (status == null || wallet == null)
        {
            Debug.LogWarning("[CampRestNPC] 플레이어 상태 또는 지갑을 찾지 못했습니다.");
            return;
        }

        RestDatabaseSO.RestEntry settings = restDatabase != null ? restDatabase.Default : null;
        float basePercent = settings != null ? settings.baseHealPercent : baseHealPercent;
        int costPerAct = settings != null ? settings.creditPerAct : creditPerAct;
        int rechargeAmount = settings != null ? settings.potionRechargeAmount : potionRechargeAmount;

        float healPercent = PassiveSkillManager.Instance != null && PassiveSkillManager.Instance.CampHealPercent > 0f
            ? PassiveSkillManager.Instance.CampHealPercent
            : basePercent;

        // 회복량은 최대 체력의 healPercent%이되, 부족한 체력만큼만 채운다.
        int missingHealth = Mathf.Max(0, Mathf.RoundToInt(status.MaxHealth - status.CurrentHp));
        int healthAmount = Mathf.Min(missingHealth, Mathf.RoundToInt(status.MaxHealth * healPercent / 100f));

        // 포션은 rechargeAmount만큼 채우되, 최대 충전량을 넘지 않는다.
        int potionAmount = 0;
        if (PotionUseManager.Instance != null)
        {
            int missingCharges = Mathf.Max(0, PotionUseManager.Instance.MaxCharges - PotionUseManager.Instance.CurrentCharges);
            potionAmount = Mathf.Min(missingCharges, Mathf.Max(0, rechargeAmount));
        }

        // 비용은 회복량과 무관한 고정값(액트 번호 × creditPerAct). 채울 것이 하나도 없으면 받지 않는다.
        int cost = healthAmount > 0 || potionAmount > 0
            ? Mathf.Max(0, costPerAct) * GetCurrentActNumber()
            : 0;

        pendingHealthAmount = healthAmount;
        pendingPotionAmount = potionAmount;
        pendingCost = cost;

        restPopup.SetRecoveryPreview(healthAmount, potionAmount, cost, wallet.Gold);

        // KY_PopupManager 스택에 올려서 연다. 직접 Open()하면 매니저가 이 팝업이 열린 걸 몰라서
        // ESC 한 번에 팝업이 닫히면서 일시정지까지 같이 열렸다(다른 팝업과 동일하게 매니저를 탄다).
        if (KY_PopupManager.Instance != null)
            KY_PopupManager.Instance.Show(PopupType.Rest);
        else
            restPopup.Open();
    }

    /// <summary>확인 버튼이 비용 검사를 통과했을 때(KY_RestPopup.OnConfirmed) 실제 처리를 적용한다.</summary>
    private void HandleConfirmed()
    {
        PlayerWallet wallet = InventoryController.Instance != null ? InventoryController.Instance.PlayerWallet : null;
        if (wallet == null || !wallet.TrySpendGold(pendingCost))
            return;

        if (pendingHealthAmount > 0)
        {
            WBH_PlayerStatus status = GetPlayerStatus();
            status?.Heal(pendingHealthAmount);
        }

        if (pendingPotionAmount > 0 && PotionUseManager.Instance != null)
            PotionUseManager.Instance.RechargePotions(pendingPotionAmount);
    }

    /// <summary>
    /// 스테이지 저장의 현재 액트 번호(Act1=1, Act2=2, Act3=3). 저장이 없거나 읽지 못하면(씬 직접 실행) 1.
    /// 캠프 씬의 YJ_StageSaveService를 쓰고, 없으면 포탈과 같은 방식으로 이 오브젝트에 붙여 조회만 한다.
    /// </summary>
    private int GetCurrentActNumber()
    {
        YJ_StageSaveService saveService = FindFirstObjectByType<YJ_StageSaveService>();
        if (saveService == null)
            saveService = gameObject.AddComponent<YJ_StageSaveService>();

        if (!saveService.HasSaveFile || !saveService.TryLoadSaveData(out StageMapSaveData map) || map == null)
            return 1;

        return Mathf.Max(1, (int)map.act);
    }

    private static WBH_PlayerStatus GetPlayerStatus()
    {
        return PlayerStatManager.Instance != null ? PlayerStatManager.Instance.GetComponent<WBH_PlayerStatus>() : null;
    }
}
