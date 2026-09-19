using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_ElementRow : MonoBehaviour
{
    public TextMeshProUGUI totalValueText;

    [SerializeField] private Image backgroundImage;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.3f;

    [Header("인챈트 표시")]
    [Tooltip("이 칸의 속성 아이콘. 인챈트 중에는 같은 이름의 _Active 스프라이트로 바뀐다.")]
    [SerializeField] private Image elementIcon;

    [Tooltip("인챈트 중에만 켜지는 'Enchanted' 배지.")]
    [SerializeField] private GameObject enchantedBadge;

    [Tooltip("인챈트 중 배경색을 얼마나 진하게 만들지. 0.5면 원색의 절반 밝기.")]
    [SerializeField, Range(0.1f, 1f)] private float enchantedDarken = 0.5f;

    [Tooltip("인챈트 중 아이콘을 얼마나 키울지. 1.1이면 10% 확대.")]
    [SerializeField, Range(1f, 2f)] private float enchantedIconScale = 1.1f;

    private const string ElementSpriteFolder = "Images/Element/";
    private const string ActiveSuffix = "_Active";

    /// <summary>인챈트 해제 시 되돌릴 원본. 첫 호출 때 한 번만 잡아둔다.</summary>
    private Sprite baseIcon;
    private Vector3 baseIconScale = Vector3.one;
    private Color baseBackgroundColor;
    private bool cached;

    private bool enchanted;

    /// <summary>첫 호출은 현재 상태와 같아 보여도 반드시 적용한다(에디터에 배지를 켜둔 채 저장한 경우 대비).</summary>
    private bool enchantApplied;

    private void CacheBaseVisuals()
    {
        if (cached)
            return;

        if (elementIcon != null)
        {
            baseIcon = elementIcon.sprite;
            baseIconScale = elementIcon.transform.localScale;
        }

        if (backgroundImage != null)
            baseBackgroundColor = backgroundImage.color;

        cached = true;
    }

    public void SetData(KY_StatTypeData data)
    {
        totalValueText.text = data.Total.ToString("0.##");
    }

    public void SetElementActive(bool isActive)
    {
        CacheBaseVisuals();

        Color c = backgroundImage.color;
        c.a = isActive ? 1f : dimAlpha;
        backgroundImage.color = c;
    }

    /// <summary>
    /// 이 속성으로 인챈트된 상태인지 표시한다. 아이콘을 _Active 스프라이트로 바꾸고,
    /// 배경을 원색보다 진하게 만들고, Enchanted 배지를 켠다.
    ///
    /// !! 알파는 건드리지 않는다. 알파는 SetElementActive가 보너스 수치 유무에 따라 따로 관리한다.
    /// </summary>
    public void SetEnchanted(bool isEnchanted)
    {
        CacheBaseVisuals();

        if (enchantApplied && enchanted == isEnchanted)
            return;

        enchanted = isEnchanted;
        enchantApplied = true;

        if (enchantedBadge != null)
            enchantedBadge.SetActive(isEnchanted);

        if (elementIcon != null)
        {
            Sprite target = isEnchanted ? ResolveActiveIcon() : baseIcon;
            if (target != null)
                elementIcon.sprite = target;

            // 크기는 sizeDelta 대신 localScale로 키운다. 레이아웃이 계산한 칸 크기를 건드리지 않아
            // 다른 칸의 위치가 밀리지 않는다.
            elementIcon.transform.localScale = isEnchanted
                ? baseIconScale * enchantedIconScale
                : baseIconScale;
        }

        if (backgroundImage != null)
        {
            // 알파는 SetElementActive의 dim 처리를 덮지 않도록 현재 값을 유지한다.
            Color c = isEnchanted
                ? new Color(baseBackgroundColor.r * enchantedDarken,
                            baseBackgroundColor.g * enchantedDarken,
                            baseBackgroundColor.b * enchantedDarken)
                : baseBackgroundColor;

            c.a = backgroundImage.color.a;
            backgroundImage.color = c;
        }
    }

    /// <summary>
    /// 원본 아이콘과 같은 이름 + "_Active" 스프라이트를 Resources에서 찾는다.
    /// (예: Fire_Flame → Images/Element/Fire_Flame_Active)
    /// </summary>
    private Sprite ResolveActiveIcon()
    {
        if (baseIcon == null)
        {
            Debug.LogWarning($"[KY_ElementRow] '{name}'에 원본 아이콘이 없어 인챈트 아이콘을 찾을 수 없습니다.", this);
            return null;
        }

        string path = ElementSpriteFolder + baseIcon.name + ActiveSuffix;
        Sprite active = Resources.Load<Sprite>(path);

        if (active == null)
            Debug.LogWarning($"[KY_ElementRow] 인챈트 아이콘을 찾지 못했습니다: Resources/{path}", this);

        return active;
    }
}
