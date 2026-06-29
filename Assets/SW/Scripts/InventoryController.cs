using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryController : MonoBehaviour
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
        Instance=this;
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

    public bool AddItem(ItemData itemData)
    {
          if (playerGrid.FindEmptySpace(itemData.width, itemData.height, out int x, out int y))
          {
                    InventoryItem item = new InventoryItem(itemData);
                    playerGrid.PlaceItem(item, x, y);
                    SpawnItemUI(item);
                    logText.text = $"{itemData.itemName} 아이템을 획득했습니다. 위치 : {x}, {y}";
                    return true;
          }
        logText.text = "인벤토리가 꽉 찼습니다!";
        return false;
    }
    public void SpawnItemUI(InventoryItem itemData)
    {
        // 1. 바탕화면(itemsContainer)의 자식으로 프리팹(그림)을 생성합니다.
        GameObject newObj = Instantiate(itemUIPrefab, playerGrid.ItemsContainer);

        // 2. 방금 만든 그림의 ItemUI 스크립트를 가져옵니다.
        ItemUI ui = newObj.GetComponent<ItemUI>();

        // 3. 데이터를 넘겨주어 스스로 크기와 위치를 맞추게 합니다.
        ui.Setup(itemData,  playerGrid);
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
