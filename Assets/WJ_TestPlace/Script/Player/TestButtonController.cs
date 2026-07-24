using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Mirror.NetworkRuntimeProfiler;

/// <summary>
/// 테스트용 버튼 5개(인벤토리 팝업, 레벨업, 레벨 초기화, 아이템 드랍, 아이템 획득)에
/// 연결할 메서드를 모아둔 컨트롤러. 각 버튼의 OnClick()에 해당 메서드를 연결해서 사용.
/// </summary>
public class TestButtonController : MonoBehaviour
{
    [Header("인벤토리 팝업")]
    [Tooltip("켰다 껐다 할 인벤토리 UI 루트 오브젝트")]
    [SerializeField] private GameObject inventoryPopup;

    [Header("패시브 팝업")]
    [SerializeField] private GameObject passivePopup;

    [Header("아이템 드랍 / 획득")]
    [SerializeField] private ItemSystemController itemSystemController;

    [Tooltip("드랍 테스트에 사용할 아이템 정의")]
    [SerializeField] private ItemDefinitionSO testDropItem;

    [Tooltip("비워두면 이 오브젝트의 위치에 드랍")]
    [SerializeField] private Transform dropPoint;

    [Tooltip("드랍된 아이템이 없을 때 비활성화할 획득 버튼")]
    [SerializeField] private Button getItemBtn;

    [Tooltip("IItemReceiver를 구현한 컴포넌트(예: InventoryController). 비워두면 로그만 출력.")]
    [SerializeField] private MonoBehaviour receiverBehaviour;

    [Tooltip("총 공격력 실시간 표시용 텍스트")]
    [SerializeField] private TextMeshProUGUI AttackText;

    [Header("Key Mapping")]

    private IItemReceiver Receiver => receiverBehaviour as IItemReceiver;


    private void Start()
    {
        if (receiverBehaviour != null && Receiver == null)
            Debug.LogWarning($"[TestButtonController] {receiverBehaviour.GetType().Name}은(는) IItemReceiver를 구현하지 않았습니다.");

        var stat = PlayerStatManager.Instance.Stat;
    }

    
    public void OnClickInventoryPopup()
    {
        if (inventoryPopup == null)
        {
            Debug.LogWarning("[TestButtonController] inventoryPopup이 연결되지 않았습니다.");
            return;
        }

        inventoryPopup.SetActive(!inventoryPopup.activeSelf);
    }

    public void OnClickPassivePopup()
    {
        if (passivePopup == null)
        {
            Debug.LogWarning("[TestButtonController] passivePopup이 연결되지 않았습니다.");
            return;
        }

        passivePopup.SetActive(!passivePopup.activeSelf);
    }

    /// <summary>2. levelUpBtn - 캐릭터 레벨업</summary>
    public void OnClickLevelUp()
    {
        if (PlayerStatManager.Instance == null)
        {
            Debug.LogWarning("[TestButtonController] PlayerStatManager.Instance가 없습니다.");
            return;
        }

        PlayerStatManager.Instance.LevelUp();
    }

    /// <summary>3. levelClear - 캐릭터 레벨을 1로 초기화</summary>
    public void OnClickLevelClear()
    {
        if (PlayerStatManager.Instance == null)
        {
            Debug.LogWarning("[TestButtonController] PlayerStatManager.Instance가 없습니다.");
            return;
        }

        PlayerStatManager.Instance.ResetLevel();
    }

    /// <summary>4. dropItemBtn - 아이템 생성 및 드랍</summary>
    public void OnClickDropItem()
    {
        if (itemSystemController == null)
        {
            Debug.LogWarning("[TestButtonController] itemSystemController가 연결되지 않았습니다.");
            return;
        }

        if (testDropItem == null)
        {
            Debug.LogWarning("[TestButtonController] testDropItem이 연결되지 않았습니다.");
            return;
        }

        // 아직 안 주운 이전 드랍이 남아있으면, 월드에 고아 오브젝트가 쌓이지 않도록 먼저 정리.
        if (itemSystemController.LastSpawnedPickup != null)
            Destroy(itemSystemController.LastSpawnedPickup);

        Vector3 pos = dropPoint != null ? dropPoint.position : transform.position;

        ItemSystemController.Instance.DropGeneratedItem(testDropItem, pos);
    }

    private void Update()
    {
        if (PlayerStatManager.Instance == null || PlayerStatManager.Instance.Stat == null)
            return;

        var stat = PlayerStatManager.Instance.Stat;
        AttackText.text = $"실시간 총 공격력 : {stat.attackPower}";
    }
}
