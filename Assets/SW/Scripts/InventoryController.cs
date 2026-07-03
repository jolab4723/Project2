using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryController : MonoBehaviour, IItemReceiver
{
    public static InventoryController Instance { get; private set; }
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private InventoryGrid playerGrid;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private RectTransform dragLayer;


    public InventoryGrid PlayerGrid => playerGrid;

    public TextMeshProUGUI logText;
    public EquipSlotUI hoveredEquipSlot;
    public EquipSlotUI[] allEquipSlots;
    public GameObject itemUIPrefab;

    public RectTransform highlightRect;
    public Image highlightImage;
    public TextMeshProUGUI goldText;

    void Awake()
    {
        Instance = this;
        RefreshGoldText(playerWallet.Gold);
    }
    private void OnEnable()
    {
        playerWallet.OnGoldChanged += RefreshGoldText;
        RefreshGoldText(playerWallet.Gold);
    }

    private void OnDisable()
    {
        playerWallet.OnGoldChanged -= RefreshGoldText;
    }

    public bool AddItem(ItemInstance itemData)
    {
        if (itemData == null || itemData.definition == null)
        {
            Debug.LogWarning("[InventoryController] AddItem에 유효하지 않은 itemData가 전달되었습니다.");
            return false;
        }

        var data = itemData.definition;
        if (!playerGrid.FindEmptySpace(data.itemWidth, data.itemHeight, out int x, out int y))
        {
            PrintLog("인벤토리가 꽉 찼습니다!");
            return false;
        }

        InventoryItem item = new InventoryItem(itemData);
        playerGrid.PlaceItem(item, x, y);

        if (!SpawnItemUI(item))
        {
            // UI 생성이 실패했으면 그리드 데이터도 되돌려서 데이터-화면 불일치를 막는다.
            playerGrid.RemoveItem(item);
            PrintLog($"{data.itemName} 아이템 UI 생성에 실패했습니다.");
            return false;
        }

        PrintLog($"{data.itemName} 아이템을 획득했습니다. 위치 : {x}, {y}");
        return true;
    }

    /// <summary>
    /// itemUIPrefab을 생성해 화면에 표시. 성공 여부를 반환한다.
    /// </summary>
    public bool SpawnItemUI(InventoryItem itemData)
    {
        if (itemUIPrefab == null)
        {
            Debug.LogWarning("[InventoryController] itemUIPrefab이 비어있습니다. 인스펙터에서 연결해주세요.");
            return false;
        }

        // 1. 바탕화면(itemsContainer)의 자식으로 프리팹(그림)을 생성합니다.
        GameObject newObj = Instantiate(itemUIPrefab, playerGrid.ItemsContainer);

        // 2. 방금 만든 그림의 ItemUI 스크립트를 가져옵니다.
        ItemUI ui = newObj.GetComponent<ItemUI>();

        if (ui == null)
        {
            Debug.LogWarning("[InventoryController] itemUIPrefab에 ItemUI 컴포넌트가 없습니다.");
            Destroy(newObj);
            return false;
        }

        // 3. 데이터를 넘겨주어 스스로 크기와 위치를 맞추게 합니다.
        ui.Setup(itemData, playerGrid);
        return true;
    }

    public void ShowHighlight(int width, int height, float cellSize, float spacing)
    {
        highlightRect.gameObject.SetActive(true);

        float w = (width * cellSize) + ((width - 1) * spacing);
        float h = (height * cellSize) + ((height - 1) * spacing);
        highlightRect.sizeDelta = new Vector2(w, h);

        highlightRect.SetAsFirstSibling();
    }

    public void MoveHighlight(int gridX, int gridY, bool isValid, float cellSize, float spacing)
    {
        float step = cellSize + spacing;

        highlightRect.anchoredPosition = new Vector2(gridX * step, -gridY * step);
        highlightImage.color = isValid ? new Color(0, 1, 0, 0.6f) : new Color(1, 0, 0, 0.6f);
    }
    public void HideHighlight()
    {
        highlightRect.gameObject.SetActive(false);
    }

    public void SetHighlightActive(bool isActive)
    {
        if (highlightRect.gameObject.activeSelf != isActive)
        {
            highlightRect.gameObject.SetActive(isActive);
        }
    }

    public void PrintLog(string message)
    {
        if (logText != null)
        {
            logText.text = message;
        }
    }

    public void RefreshGoldText(int gold)
    {
        goldText.text = gold.ToString();
    }
}