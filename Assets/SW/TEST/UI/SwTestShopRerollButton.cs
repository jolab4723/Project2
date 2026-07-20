using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class SwTestShopRerollButton : MonoBehaviour
{
    [SerializeField] private ShopStockInitializer stockInitializer;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (stockInitializer == null)
            stockInitializer = GetComponentInParent<ShopStockInitializer>();

        // 복제한 테스트 버튼에 남아 있을 수 있는 기존 호출을 제거한다.
        button.onClick.RemoveAllListeners();
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(HandleRerollClicked);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleRerollClicked);
    }

    private void HandleRerollClicked()
    {
        if (stockInitializer == null)
        {
            Debug.LogWarning(
                "[SwTestShopRerollButton] ShopStockInitializer가 연결되지 않았습니다.",
                this);
            return;
        }

        stockInitializer.RerollStock();
    }
}
