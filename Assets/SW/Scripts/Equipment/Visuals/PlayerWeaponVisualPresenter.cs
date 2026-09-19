using ItemSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations.Rigging;
using UnityEngine.ResourceManagement.AsyncOperations;

[DisallowMultipleComponent]
public sealed class PlayerWeaponVisualPresenter : MonoBehaviour
{
    private const string LeftHandGripName = "LeftHandGrip";
    private static readonly int LocomotionStateHash =
        Animator.StringToHash("Locomotion");
    private static readonly int AttackStateHash =
        Animator.StringToHash("Attack");

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
    [SerializeField] private CharacterClass characterClass = CharacterClass.Fighter;
    [SerializeField] private Animator characterAnimator;
    [SerializeField, Min(0f)] private float leftHandIkBlendSpeed = 12f;

    private string currentItemId;
    public string CurrentVisualItemId => currentItemId;
    private string pendingItemId;
    private string pooledItemId;
    private GameObject currentVisual;
    private GameObject pooledVisual;
    private Transform currentLeftHandGrip;
    private int visualRequestVersion;

    private void Update()
    {
        bool hasActiveLeftHandGrip =
            currentLeftHandGrip != null &&
            currentLeftHandGrip.gameObject.activeInHierarchy;

        if (hasActiveLeftHandGrip)
        {
            ApplyLeftHandIk(currentLeftHandGrip);
        }

        if (characterClass == CharacterClass.Gunner)
            UpdateGunnerLeftHandIkWeight(hasActiveLeftHandGrip);
    }

    private void OnEnable()
    {
#if UNITY_SERVER && !UNITY_EDITOR
        // 전용 서버는 장비 상태만 처리하며 렌더링용 Addressables 무기 외형을 생성하지 않습니다.
        enabled = false;
#else
        if (equipmentSystem == null)
            equipmentSystem = InventoryController.GetLocalEquipmentSystem(this);

        if (equipmentSystem == null)
        {
            ShowDefaultVisual();
            return;
        }

        // 이 캐릭터가 활성화될 때마다 공용 EquipmentSystem에 "지금은 나(Fighter/Gunner)다"를 알려준다 -
        // 장비 장착 검증(캐릭터 전용 무기 체크)이 이 값을 기준으로 동작한다.
        equipmentSystem.SetActiveCharacterClass(characterClass);

        equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;
        RefreshFromEquipment();
#endif
    }

    private void OnDisable()
    {
        if (equipmentSystem != null)
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;

        CancelPendingVisualRequest();
        ReleaseAllVisuals();
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] _)
    {
        RefreshFromEquipment();
    }

    /// <summary>
    /// Mirror 테스트의 서버 확정 무기 itemId를 이 플레이어 복제본 외형에 적용한다.
    /// 로컬 장비 모델이 없는 원격 플레이어도 같은 Presenter와 Addressables 풀을 재사용한다.
    /// 전용 서버에서는 렌더링 자산을 생성하지 않는다.
    /// </summary>
    public void ApplyAuthoritativeWeaponItemId(string itemId)
    {
#if !UNITY_SERVER || UNITY_EDITOR
        ApplyVisual(itemId);
#endif
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
            CancelPendingVisualRequest();
            ShowDefaultVisual();
            return;
        }

        if (pendingItemId == itemId)
            return;

        if (currentVisual != null &&
            currentVisual.activeSelf &&
            currentItemId == itemId)
        {
            CancelPendingVisualRequest();
            return;
        }

        if (TryActivatePooledVisual(itemId))
            return;

        int requestVersion = ++visualRequestVersion;
        pendingItemId = itemId;

        if (weaponMount == null ||
            visualCatalog == null ||
            !visualCatalog.TryGetVisualReference(
                itemId,
                out AssetReferenceGameObject visualReference))
        {
            Debug.LogWarning(
                $"[{nameof(PlayerWeaponVisualPresenter)}] '{itemId}'에 연결된 무기 외형을 찾지 못했습니다.",
                this);
            pendingItemId = null;
            if (currentVisual == null)
                ShowDefaultVisual();
            return;
        }

        AsyncOperationHandle<GameObject> operation =
            visualReference.InstantiateAsync(weaponMount, false);
        operation.Completed += completed =>
            HandleVisualLoaded(itemId, requestVersion, completed);
    }

    private void HandleVisualLoaded(
        string itemId,
        int requestVersion,
        AsyncOperationHandle<GameObject> operation)
    {
        if (operation.Status != AsyncOperationStatus.Succeeded ||
            operation.Result == null)
        {
            if (operation.IsValid())
                Addressables.Release(operation);

            if (this != null &&
                requestVersion == visualRequestVersion &&
                isActiveAndEnabled)
            {
                pendingItemId = null;
                Debug.LogWarning(
                    $"[{nameof(PlayerWeaponVisualPresenter)}] '{itemId}' 무기 외형 로드에 실패했습니다.",
                    this);
                if (currentVisual == null)
                    ShowDefaultVisual();
            }

            return;
        }

        if (this == null ||
            requestVersion != visualRequestVersion ||
            !isActiveAndEnabled)
        {
            Addressables.ReleaseInstance(operation.Result);
            return;
        }

        pendingItemId = null;
        GameObject previousVisual = currentVisual;
        string previousItemId = currentItemId;

        ActivateVisual(itemId, operation.Result);
        PoolOrReleaseVisual(previousItemId, previousVisual);
    }

    private void ShowDefaultVisual()
    {
        PoolCurrentVisual();

        if (defaultVisual == null)
            return;

        if (characterClass == CharacterClass.Fighter)
            CalibrateDefaultVisualToRightHand();
        defaultVisual.SetActive(true);
        currentLeftHandGrip = FindDescendant(defaultVisual.transform, LeftHandGripName);
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
        Transform directChild = root.Find(objectName);
        if (directChild != null)
            return directChild;

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    private void CancelPendingVisualRequest()
    {
        visualRequestVersion++;
        pendingItemId = null;
    }

    private bool TryActivatePooledVisual(string itemId)
    {
        if (pooledVisual == null || pooledItemId != itemId)
            return false;

        CancelPendingVisualRequest();

        GameObject nextVisual = pooledVisual;
        GameObject previousVisual = currentVisual;
        string previousItemId = currentItemId;
        pooledVisual = null;
        pooledItemId = null;

        ActivateVisual(itemId, nextVisual);
        PoolOrReleaseVisual(previousItemId, previousVisual);
        return true;
    }

    private void ActivateVisual(string itemId, GameObject visual)
    {
        currentVisual = visual;
        currentVisual.SetActive(true);
        currentItemId = itemId;

        if (defaultVisual != null)
            defaultVisual.SetActive(false);

        currentLeftHandGrip = FindDescendant(currentVisual.transform, LeftHandGripName);
        ApplyLeftHandIk(currentLeftHandGrip);
    }

    private void PoolCurrentVisual()
    {
        if (leftHandIkConstraint != null)
            leftHandIkConstraint.weight = 0f;

        PoolOrReleaseVisual(currentItemId, currentVisual);

        currentVisual = null;
        currentItemId = null;
        currentLeftHandGrip = null;
    }

    private void PoolOrReleaseVisual(string itemId, GameObject visual)
    {
        if (visual == null)
            return;

        visual.SetActive(false);

        // 직전에 사용한 외형 하나만 보관하여 A↔B 반복 장착의 재로드와 재생성을 막습니다.
        // 다른 외형이 보관되면 기존 인스턴스를 해제해 메모리 사용이 계속 늘어나지 않게 합니다.
        ReleaseVisualInstance(pooledVisual);
        pooledItemId = itemId;
        pooledVisual = visual;
    }

    private void ReleaseAllVisuals()
    {
        if (leftHandIkConstraint != null)
            leftHandIkConstraint.weight = 0f;

        if (defaultVisual != null)
            defaultVisual.SetActive(false);

        ReleaseVisualInstance(currentVisual);
        ReleaseVisualInstance(pooledVisual);

        currentVisual = null;
        currentItemId = null;
        pooledVisual = null;
        pooledItemId = null;
        currentLeftHandGrip = null;
    }

    private static void ReleaseVisualInstance(GameObject visual)
    {
        if (visual != null)
            Addressables.ReleaseInstance(visual);
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
            if (leftHandContact != null &&
                (characterClass == CharacterClass.Gunner ||
                 leftHandContact.parent != handBone))
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
        if (characterClass == CharacterClass.Fighter)
            leftHandIkConstraint.weight = 1f;
    }

    private void UpdateGunnerLeftHandIkWeight(bool hasActiveLeftHandGrip)
    {
        if (leftHandIkConstraint == null)
            return;

        float targetWeight =
            hasActiveLeftHandGrip && IsGunnerHoldingWeapon() ? 1f : 0f;

        // The shot is authored from the first attack event, so do not leave the
        // support hand in the blend-in window while the weapon fires.
        if (targetWeight > 0f && IsGunnerAttackState())
        {
            leftHandIkConstraint.weight = 1f;
            return;
        }

        leftHandIkConstraint.weight = Mathf.MoveTowards(
            leftHandIkConstraint.weight,
            targetWeight,
            leftHandIkBlendSpeed * Time.deltaTime);
    }

    private bool IsGunnerAttackState()
    {
        if (characterAnimator == null || !characterAnimator.isActiveAndEnabled)
            return false;

        AnimatorStateInfo state = characterAnimator.IsInTransition(0)
            ? characterAnimator.GetNextAnimatorStateInfo(0)
            : characterAnimator.GetCurrentAnimatorStateInfo(0);
        return state.shortNameHash == AttackStateHash;
    }

    private bool IsGunnerHoldingWeapon()
    {
        if (characterAnimator == null || !characterAnimator.isActiveAndEnabled)
            return true;

        AnimatorStateInfo state = characterAnimator.IsInTransition(0)
            ? characterAnimator.GetNextAnimatorStateInfo(0)
            : characterAnimator.GetCurrentAnimatorStateInfo(0);
        int stateHash = state.shortNameHash;
        return stateHash == 0 ||
               stateHash == LocomotionStateHash ||
               stateHash == AttackStateHash;
    }
}
