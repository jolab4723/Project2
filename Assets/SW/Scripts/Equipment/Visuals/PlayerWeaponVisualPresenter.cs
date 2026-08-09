using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public sealed class PlayerWeaponVisualPresenter : MonoBehaviour
{
    private const string LeftHandGripName = "LeftHandGrip";

    // Fighter 손가락이 만드는 실제 파지 고리의 중심입니다.
    // 기존 PalmContact 마커는 손바닥 표면 쪽이라 손잡이 중심축을 맞추는 기준으로는 부족합니다.
    [SerializeField] private Vector3 rightHandGripCenterLocalPosition =
        new(0.010f, 0.080f, -0.040f);
    [SerializeField] private Vector3 leftHandGripCenterLocalPosition =
        new(-0.010f, 0.080f, -0.040f);

    [SerializeField] private EquipmentSystem equipmentSystem;
    [SerializeField] private Transform weaponMount;
    [SerializeField] private GameObject defaultVisual;
    [SerializeField] private WeaponVisualCatalogSO visualCatalog;
    [SerializeField] private Transform leftHandContact;
    [SerializeField] private Transform leftHandIkTarget;
    [SerializeField] private TwoBoneIKConstraint leftHandIkConstraint;

    private string currentItemId;
    private GameObject currentVisual;
    private Transform currentLeftHandGrip;
    private readonly Dictionary<string, GameObject> visualCache = new();

    private void Update()
    {
        if (currentLeftHandGrip != null &&
            currentLeftHandGrip.gameObject.activeInHierarchy)
        {
            ApplyLeftHandIk(currentLeftHandGrip);
        }
    }

    private void OnEnable()
    {
        if (equipmentSystem == null)
        {
            ShowDefaultVisual();
            return;
        }

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
            ShowDefaultVisual();
            return;
        }

        if (currentVisual != null &&
            currentVisual.activeSelf &&
            currentItemId == itemId)
            return;

        HideCurrentVisual();

        if (weaponMount == null ||
            visualCatalog == null ||
            !visualCatalog.TryGetVisualPrefab(itemId, out GameObject visualPrefab))
        {
            Debug.LogWarning(
                $"[{nameof(PlayerWeaponVisualPresenter)}] '{itemId}'에 연결된 무기 외형을 찾지 못했습니다.",
                this);
            return;
        }

        if (!visualCache.TryGetValue(itemId, out currentVisual) || currentVisual == null)
        {
            currentVisual = Instantiate(visualPrefab, weaponMount, false);
            visualCache[itemId] = currentVisual;
        }

        currentVisual.SetActive(true);
        currentItemId = itemId;

        currentLeftHandGrip = currentVisual.transform.Find(LeftHandGripName);
        ApplyLeftHandIk(currentLeftHandGrip);
    }

    private void ShowDefaultVisual()
    {
        HideCurrentVisual();

        if (defaultVisual == null)
            return;

        CalibrateDefaultVisualToRightHand();
        defaultVisual.SetActive(true);
        currentLeftHandGrip = defaultVisual.transform.Find(LeftHandGripName);
        ApplyLeftHandIk(currentLeftHandGrip);
    }

    private void CalibrateDefaultVisualToRightHand()
    {
        if (weaponMount == null || defaultVisual == null)
            return;

        Transform handBone = FindDescendant(transform, "hand_R");
        if (handBone == null)
            return;

        Transform visual = defaultVisual.transform;
        Vector3 currentShaftDirection = visual.TransformDirection(Vector3.up).normalized;
        Vector3 handAxis = handBone.TransformDirection(Vector3.right).normalized;
        Vector3 targetShaftDirection =
            Vector3.Dot(currentShaftDirection, handAxis) >= 0f
                ? handAxis
                : -handAxis;

        visual.rotation =
            Quaternion.FromToRotation(currentShaftDirection, targetShaftDirection) *
            visual.rotation;

        Vector3 gripCenter = handBone.TransformPoint(rightHandGripCenterLocalPosition);
        Vector3 shaftOffset = visual.position - gripCenter;
        Vector3 radialOffset = shaftOffset -
                               Vector3.Dot(shaftOffset, targetShaftDirection) *
                               targetShaftDirection;
        visual.position -= radialOffset;

        Transform leftGrip = visual.Find(LeftHandGripName);
        if (leftGrip == null)
            return;

        float alongShaft = Vector3.Dot(leftGrip.localPosition, Vector3.up);
        leftGrip.localPosition = Vector3.up * alongShaft;
        Vector3 currentHandAxis = leftGrip.localRotation * Vector3.right;
        Vector3 targetHandAxis = Vector3.Dot(currentHandAxis, Vector3.up) >= 0f
            ? Vector3.up
            : Vector3.down;
        leftGrip.localRotation =
            Quaternion.FromToRotation(currentHandAxis, targetHandAxis) *
            leftGrip.localRotation;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    private void HideCurrentVisual()
    {
        if (leftHandIkConstraint != null)
            leftHandIkConstraint.weight = 0f;

        if (defaultVisual != null)
            defaultVisual.SetActive(false);

        if (currentVisual != null)
            currentVisual.SetActive(false);

        currentVisual = null;
        currentItemId = null;
        currentLeftHandGrip = null;
    }

    private void ApplyLeftHandIk(Transform grip)
    {
        if (grip == null ||
            leftHandIkTarget == null ||
            leftHandIkConstraint == null)
        {
            return;
        }

        Vector3 targetPosition = grip.position;
        Quaternion targetRotation = grip.rotation;
        Transform handBone = leftHandIkConstraint.data.tip;
        if (handBone != null)
        {
            // TwoBoneIK의 Target은 손바닥이 아니라 손목 뼈 피벗을 움직인다.
            // 손잡이 중심축이 손가락 고리의 중심을 지나도록 손뼈 기준점을 역산한다.
            Vector3 localContactPosition = leftHandGripCenterLocalPosition;
            Quaternion localContactRotation = Quaternion.identity;
            if (leftHandContact != null && leftHandContact.parent != handBone)
            {
                localContactPosition =
                    handBone.InverseTransformPoint(leftHandContact.position);
                localContactRotation =
                    Quaternion.Inverse(handBone.rotation) * leftHandContact.rotation;
            }

            targetRotation = grip.rotation * Quaternion.Inverse(localContactRotation);
            Vector3 scaledContactPosition = Vector3.Scale(
                localContactPosition,
                handBone.lossyScale);
            targetPosition -= targetRotation * scaledContactPosition;
        }

        leftHandIkTarget.SetPositionAndRotation(targetPosition, targetRotation);
        leftHandIkConstraint.weight = 1f;
    }
}
