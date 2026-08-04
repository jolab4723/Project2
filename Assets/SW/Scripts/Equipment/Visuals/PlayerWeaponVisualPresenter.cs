using ItemSystem;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public sealed class PlayerWeaponVisualPresenter : MonoBehaviour
{
    private const string LeftHandGripName = "LeftHandGrip";

    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private Transform weaponMount;
    [SerializeField] private WeaponVisualCatalogSO visualCatalog;
    [SerializeField] private Transform leftHandIkTarget;
    [SerializeField] private TwoBoneIKConstraint leftHandIkConstraint;

    private string currentItemId;
    private GameObject currentVisual;

    private void OnEnable()
    {
        if (equipmentSystem == null)
            return;

        equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;
        RefreshFromEquipment();
    }

    private void OnDisable()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] _)
    {
        RefreshFromEquipment();
    }

    private void RefreshFromEquipment()
    {
        if (equipmentSystem.TryGetEquippedItemInstance(
                EquipSlotType.Weapon,
                out ItemInstance weapon))
        {
            ApplyVisual(weapon.definition.itemId);
            return;
        }

        ApplyVisual(null);
    }

    private void ApplyVisual(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            ClearVisual();
            return;
        }

        if (currentVisual != null && currentItemId == itemId)
            return;

        ClearVisual();

        if (weaponMount == null ||
            visualCatalog == null ||
            !visualCatalog.TryGetVisualPrefab(itemId, out GameObject visualPrefab))
        {
            Debug.LogWarning(
                $"[{nameof(PlayerWeaponVisualPresenter)}] '{itemId}'에 연결된 무기 외형을 찾지 못했습니다.",
                this);
            return;
        }

        currentVisual = Instantiate(visualPrefab, weaponMount);
        currentVisual.transform.SetLocalPositionAndRotation(
            Vector3.zero,
            Quaternion.identity);
        currentVisual.transform.localScale = Vector3.one;
        currentItemId = itemId;

        ApplyLeftHandIk(currentVisual.transform.Find(LeftHandGripName));
    }

    private void ClearVisual()
    {
        if (leftHandIkConstraint != null)
            leftHandIkConstraint.weight = 0f;

        if (currentVisual != null)
        {
            currentVisual.SetActive(false);
            Destroy(currentVisual);
        }

        currentVisual = null;
        currentItemId = null;
    }

    private void ApplyLeftHandIk(Transform grip)
    {
        if (grip == null ||
            leftHandIkTarget == null ||
            leftHandIkConstraint == null)
        {
            return;
        }

        leftHandIkTarget.SetPositionAndRotation(
            grip.position,
            grip.rotation);
        leftHandIkConstraint.weight = 1f;
    }
}
