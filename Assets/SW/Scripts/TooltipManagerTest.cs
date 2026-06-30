using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TooltipManagerTest : MonoBehaviour
{
    public static TooltipManagerTest Instance;
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image overlayImage;
    [SerializeField] private Image itemImage;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI itemType;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private TextMeshProUGUI statText;
    [SerializeField] private TextMeshProUGUI itemSize;
    [SerializeField] private Vector3 offset = new Vector3(15, 15, 0);

    private readonly string[] gradeHexColors = {
        "#FFFFFF", // Normal (일반 - 흰색)
        "#B7E1CD", // Magic (고급 - 초록)
        "#9FC5E8", // Rare (희귀 - 파랑)
        "#C27BA0", // Unique (유일 - 보라)
        "#FFE599"  // Legendary (전설 - 주황)
    };

    private readonly string[] typeHexColors = {
        "#A8A8A8", // 무속성
        "#E06666", // 불
        "#6D9EEB", // 얼음
        
        "#FFD966", // 번개

    };
    private readonly string[] gradeNames_KR = {
        "일반", // 0: Normal
        "고급", // 1: Magic
        "희귀", // 2: Rare
        "유일", // 3: Unique
        "전설"  // 4: Legendary
    };


    private readonly string[] itemTypeNames_KR = {
        "무기",
        "방어구",
        "유물",
        "포션"
    };
    private readonly string[] typeNames_KR = {
        "무속성",
        "불",
        "얼음",
        "번개"
    };

    private readonly string[] classNames_KR = {
        "공용",
        "파이터",
        "거너"
    };

    private readonly string[] weaponTypeNames_KR = {
        "없음",
        "대검",
        "도끼",
        "둔기",
        "소총",
        "샷건",
        "유탄발사기"
    };

    private readonly string[] statTypeNames_KR = {
        "공격력",
        "방어력",
        "체력 회복",
    };
    private void Awake()
    {
        Instance = this;
        HideTooltip(); // 게임 시작 시 무조건 숨김
    }
    private void Update()
    {
        if (tooltipPanel.activeSelf && Mouse.current != null)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            tooltipPanel.transform.position = mousePos + offset;
        }
    }

    public void ShowTooltip(ItemData itemData)
    {
        tooltipPanel.SetActive(true);

        if (Mouse.current != null)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            tooltipPanel.transform.position = mousePos + offset;
        }

        ApplyColor(itemData);

        itemImage.sprite = itemData.itemIcon;
        itemName.text = itemData.itemName;

        if (itemData.itemType == ItemType.Weapon)
            itemType.text = $"{classNames_KR[(int)itemData.characterClass]} | {weaponTypeNames_KR[(int)itemData.weaponType]}";
        else
            itemType.text = $"{itemTypeNames_KR[(int)itemData.itemType]}";


        if(itemData.statType == StatType.Heal)
            statText.text = $"{statTypeNames_KR[(int)itemData.statType]} + {itemData.statValue}%";
        else
            statText.text = $"{statTypeNames_KR[(int)itemData.statType]} + {itemData.statValue}";

        itemSize.text = $"{itemData.width}x{itemData.height}";
    }

    public void HideTooltip()
    {
        tooltipPanel.SetActive(false);
    }

    public void ApplyColor(ItemData itemData)
    {
        string gradeColorCode = gradeHexColors[(int)itemData.itemGrade];
        string typeColorCode = typeHexColors[(int)itemData.type];

        bool hasGradeColor = ColorUtility.TryParseHtmlString(gradeColorCode, out Color gradeColor);
        bool hasTypeColor = ColorUtility.TryParseHtmlString(typeColorCode, out Color typeColor);

        if (!hasGradeColor || !hasTypeColor)
            return;

        if (borderImage != null)
            borderImage.color = gradeColor;

        if (gradeText != null)
        {
            gradeText.color = gradeColor;

            gradeText.text =
                gradeNames_KR[(int)itemData.itemGrade] +
                $"<color=#A8A8A8> | </color>" +
                $"<color={typeColorCode}>{typeNames_KR[(int)itemData.type]}</color>";
        }

        if (overlayImage != null)
        {
            Color overlayColor = gradeColor;
            overlayColor.a = 0.26f;
            overlayImage.color = overlayColor;
        }
    }
}
