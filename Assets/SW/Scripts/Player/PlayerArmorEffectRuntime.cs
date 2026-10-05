using ItemSystem;
using UnityEngine;

/// <summary>
/// 싱글·서버에서 장착된 투구와 상의의 동일한 고유효과를 관리합니다.
/// 마나 조건과 버프 적용은 StatThresholdRunner에 맡깁니다.
/// 장비 해제, 컴포넌트 비활성화, 서버 종료 때 기존 실행 객체를 정리합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerContext))]
public sealed class PlayerArmorEffectRuntime : MonoBehaviour
{
    [SerializeField] private EquipmentSystem equipment;
    [SerializeField] private PlayerStatManager stats;
    [SerializeField] private PlayerHealthManager health;
    [SerializeField] private PlayerManaManager mana;
    [SerializeField] private PlayerBuffManager buffs;
    private PlayerContext owner;
    private bool started;
    private ItemInstance activeItem;
    private StatThresholdBuffUniqueEffectSO activeEffect;
    private GameObject runtimeObject;
    private ItemInstance shieldItem;
    private SolarGraceShieldUniqueEffectSO shieldEffect;
    private float nextShieldChargeAt;
    private float shieldExpiresAt;
    private bool waitingForRevive;
    private float shieldAmount;
    private GameObject shieldVisual;
    private Material shieldMaterial;
    private MirrorSpawnedPlayerBinder binder;
    private string activeShieldSource;
    private float activeShieldCapFraction;
    private ItemInstance guardianItem;
    private GuardiansJusticeShieldUniqueEffectSO guardianEffect;
    private float guardianEnergy;
    private double guardianEnergyExpiresAt;
    private int guardianScene;

    public float ShieldAmount => shieldAmount;
    private bool CanExecute => owner != null && owner.Effects.CanExecute;
    private bool CanPresent => GetComponent<Mirror.NetworkIdentity>() == null || Mirror.NetworkClient.active;

    public void ApplyVisualAmount(float amount)
    {
        if (CanExecute) return;
        float old = shieldAmount;
        shieldAmount = amount;
        OnShieldAmountChanged(old, amount);
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        owner ??= GetComponent<PlayerContext>();
        equipment ??= owner?.Equipment;
        stats ??= GetComponent<PlayerStatManager>();
        health ??= GetComponent<PlayerHealthManager>();
        mana ??= GetComponent<PlayerManaManager>();
        buffs ??= GetComponent<PlayerBuffManager>();
        binder ??= GetComponent<MirrorSpawnedPlayerBinder>();
    }

    private void OnEnable()
    {
        EnsureReferences();
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }
        if (health != null)
        {
            health.OnBeforeDamageApplied -= ReduceDamageWithShield;
            health.OnBeforeDamageApplied += ReduceDamageWithShield;
            health.OnDeath -= HandleShieldOwnerDeath;
            health.OnDeath += HandleShieldOwnerDeath;
            health.OnCombatHealthLost -= RecordGuardianHealthLoss;
            health.OnCombatHealthLost += RecordGuardianHealthLoss;
        }
        ReconcileEquipment();
    }

    private void Start()
    {
        started = true;
        OnEnable();
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] _)
        => ReconcileEquipment();

    private void OnDisable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        if (health != null)
        {
            health.OnBeforeDamageApplied -= ReduceDamageWithShield;
            health.OnDeath -= HandleShieldOwnerDeath;
            health.OnCombatHealthLost -= RecordGuardianHealthLoss;
        }
        ClearGuardian();
        StopRuntime();
        StopShield();
        DestroyShieldVisual();
    }

    public void ReconcileEquipment()
    {
        EnsureReferences();
        if (!started || !isActiveAndEnabled || !CanExecute) return;
        if (owner == null || equipment == null || stats == null ||
            mana == null || buffs == null || health == null ||
            owner.Equipment != equipment || owner.Stats != stats ||
            owner.Mana != mana || owner.Buffs != buffs || owner.Health != health)
        {
            StopRuntime();
            StopShield();
            Debug.LogError("[PlayerArmorEffectProvider] 같은 PlayerContext의 참조가 필요합니다.", this);
            return;
        }

        ReconcileHelmet();
        ReconcileShield();
        ReconcileGuardian();
    }

    private void ReconcileHelmet()
    {
        equipment.TryGetEquippedItemInstance(EquipSlotType.Helmet, out ItemInstance next);
        if (!equipment.UsesLowManaHelmetEffect(next)) next = null;
        var nextEffect = next?.definition?.uniqueEffect as StatThresholdBuffUniqueEffectSO;
        if (ReferenceEquals(activeItem, next) && activeEffect == nextEffect) return;

        StopRuntime(); // 이전 구독·효과를 먼저 종료한다.
        if (next == null || nextEffect == null) return;
        stats.EnsureInitialized();
        activeItem = next;
        activeEffect = nextEffect;
        runtimeObject = new GameObject("LowManaHelmetEffect");
        runtimeObject.transform.SetParent(transform, false);
        runtimeObject.AddComponent<StatThresholdRunner>()
            .Bind(stats, health, mana, buffs, nextEffect);
    }

    private void ReconcileShield()
    {
        equipment.TryGetEquippedItemInstance(EquipSlotType.Chest, out ItemInstance next);
        var nextEffect = next?.definition?.uniqueEffect as SolarGraceShieldUniqueEffectSO;
        if (ReferenceEquals(shieldItem, next) && shieldEffect == nextEffect) return;

        ClearShieldFrom(shieldItem?.instanceId);
        shieldItem = null;
        shieldEffect = null;
        if (next == null || nextEffect == null) return;
        shieldItem = next;
        shieldEffect = nextEffect;
        nextShieldChargeAt = Time.time + nextEffect.undamagedSeconds;
        waitingForRevive = health.CurrentHealth <= 0f;
    }

    private void ReconcileGuardian()
    {
        equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance next);
        var effect = next?.definition?.characterClass == CharacterClass.Fighter
            ? next.definition.uniqueEffect as GuardiansJusticeShieldUniqueEffectSO : null;
        if (ReferenceEquals(guardianItem, next) && guardianEffect == effect) return;
        ClearGuardian();
        if (effect == null) return;
        guardianItem = next;
        guardianEffect = effect;
        guardianScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
    }

    private void ClearGuardian()
    {
        ClearShieldFrom(guardianItem?.instanceId);
        guardianItem = null;
        guardianEffect = null;
        guardianEnergy = 0f;
        guardianEnergyExpiresAt = 0d;
        // 장착을 바꿔도 마지막 발동의 대기 시간은 유지한다.
    }

    private void RecordGuardianHealthLoss(WBH_ICombat attacker, float loss)
    {
        if (!CanExecute || guardianEffect == null || attacker == null || attacker == owner.Controller ||
            attacker is T_PlayerController || health.CurrentHealth <= 0f || loss <= 0f || owner.Effects.GetRemainingCooldown(guardianItem) > 0f)
            return;
        double now = owner.Effects.Now;
        if (now >= guardianEnergyExpiresAt) guardianEnergy = 0f;
        if (guardianEnergy <= 0f) guardianEnergyExpiresAt = now + guardianEffect.energyDurationSeconds;
        guardianEnergy = Mathf.Min(health.MaxHealth * guardianEffect.maximumHealthFraction,
            guardianEnergy + loss * guardianEffect.lossToEnergyFraction);
    }

    /// <summary>유효한 기본 직접 적중에서 저장 에너지와 발동 기회를 먼저 소비한다.</summary>
    public void ResolveGuardianHit(in WBH_DamageResult result)
    {
        if (!CanExecute || result.DamageCause != DamageCause.Direct || result.FinalDamage <= 0f ||
            guardianEffect == null || health.CurrentHealth <= 0f || guardianEnergy <= 0f ||
            owner.Effects.Now >= guardianEnergyExpiresAt || owner.Effects.GetRemainingCooldown(guardianItem) > 0f)
            return;
        float amount = Mathf.Min(guardianEnergy, health.MaxHealth * guardianEffect.maximumHealthFraction);
        guardianEnergy = 0f;
        owner.Effects.BeginGuardianCooldown(guardianEffect);
        TryGrantShield(guardianItem, amount, guardianEffect.shieldDurationSeconds, guardianEffect.maximumHealthFraction);
    }

    private bool TryGrantShield(ItemInstance source, float amount, float duration, float capFraction)
    {
        if (source == null || !float.IsFinite(amount) || amount <= shieldAmount || amount <= 0f || duration <= 0f)
            return false;
        activeShieldSource = source.instanceId;
        activeShieldCapFraction = capFraction;
        shieldAmount = amount;
        shieldExpiresAt = Time.time + duration;
        UpdateShieldVisual();
        return true;
    }

    private void ClearShieldFrom(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || activeShieldSource != instanceId) return;
        shieldAmount = 0f;
        activeShieldSource = null;
        UpdateShieldVisual();
    }

    private void Update()
    {
        if (!CanExecute || health == null) return;
        if (guardianEffect != null && (owner.Effects.Now >= guardianEnergyExpiresAt ||
            guardianScene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle)) guardianEnergy = 0f;
        if (guardianEffect != null) guardianEnergy = Mathf.Min(guardianEnergy, health.MaxHealth * guardianEffect.maximumHealthFraction);
        if (guardianScene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle)
        {
            ClearShieldFrom(guardianItem?.instanceId);
            guardianScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        }
        if (health.CurrentHealth <= 0f)
        {
            waitingForRevive = true;
            if (shieldAmount > 0f) shieldAmount = 0f;
            return;
        }
        if (waitingForRevive)
        {
            waitingForRevive = false;
            if (shieldEffect != null) nextShieldChargeAt = Time.time + shieldEffect.undamagedSeconds;
        }
        float cap = Mathf.Max(0f, health.MaxHealth * activeShieldCapFraction);
        if (shieldAmount > cap) shieldAmount = cap;
        if (shieldAmount > 0f && Time.time >= shieldExpiresAt)
        {
            shieldAmount = 0f;
            activeShieldSource = null;
            if (shieldEffect != null) nextShieldChargeAt = Time.time + shieldEffect.undamagedSeconds;
        }
        if (shieldEffect == null) return;
        if (Time.time < nextShieldChargeAt ||
            health.MaxHealth <= 0f ||
            health.CurrentHealth / health.MaxHealth < shieldEffect.minimumHealthFraction)
            return;

        TryGrantShield(shieldItem, health.MaxHealth * shieldEffect.shieldFraction,
            shieldEffect.shieldDurationSeconds, shieldEffect.shieldFraction);
        nextShieldChargeAt = float.PositiveInfinity;
        UpdateShieldVisual();
    }

    private void LateUpdate()
    {
        UpdateShieldVisual();
        if (shieldVisual == null || shieldAmount <= 0f) return;
        bool visible = binder == null || !binder.IsTemporarilyAbsent;
        if (shieldVisual.activeSelf != visible) shieldVisual.SetActive(visible);
    }

    /// <summary>
    /// SW 수정: 보호막으로 막은 피해를 빼고 체력에 적용할 남은 피해를 반환합니다.
    /// 피격을 받으면 보호막 재생성 대기 시간도 다시 시작합니다.
    /// </summary>
    private float ReduceDamageWithShield(float damage)
    {
        if (!CanExecute || damage <= 0f) return damage;
        if (shieldEffect != null) nextShieldChargeAt = Time.time + shieldEffect.undamagedSeconds;
        if (shieldAmount <= 0f) return damage;
        float blockedDamage = Mathf.Min(shieldAmount, damage);
        shieldAmount -= blockedDamage;
        if (shieldMaterial != null) shieldMaterial.SetFloat("_HitAt", Time.time);
        UpdateShieldVisual();
        return damage - blockedDamage;
    }

    private void HandleShieldOwnerDeath()
    {
        shieldAmount = 0f;
        guardianEnergy = 0f;
        waitingForRevive = true;
        UpdateShieldVisual();
    }

    private void StopShield()
    {
        shieldItem = null;
        shieldEffect = null;
        shieldAmount = 0f;
        activeShieldSource = null;
        waitingForRevive = false;
        UpdateShieldVisual();
    }

    private void OnShieldAmountChanged(float oldValue, float newValue)
    {
        UpdateShieldVisual();
        if (shieldMaterial != null && newValue < oldValue)
            shieldMaterial.SetFloat("_HitAt", Time.time);
    }

    private void UpdateShieldVisual()
    {
        if (!CanPresent) return;
        if (shieldAmount <= 0f)
        {
            if (shieldVisual != null) shieldVisual.SetActive(false);
            return;
        }
        if (shieldVisual == null)
        {
            Shader shader = Resources.Load<Shader>("Shaders/SolarGraceBarrier");
            if (shader == null)
            {
                Debug.LogError("[PlayerArmorEffectProvider] SolarGraceBarrier shader를 찾지 못했습니다.", this);
                return;
            }
            shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shieldVisual.name = "SolarGraceBarrier";
            shieldVisual.transform.SetParent(transform, false);
            shieldVisual.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            shieldVisual.transform.localScale = new Vector3(2.5f, 2.65f, 2.5f);
            Collider visualCollider = shieldVisual.GetComponent<Collider>();
            visualCollider.enabled = false;
            Destroy(visualCollider);
            shieldMaterial = new Material(shader);
            shieldVisual.GetComponent<MeshRenderer>().sharedMaterial = shieldMaterial;
        }
        shieldVisual.SetActive(binder == null || !binder.IsTemporarilyAbsent);
    }

    private void DestroyShieldVisual()
    {
        if (shieldVisual != null) Destroy(shieldVisual);
        if (shieldMaterial != null) Destroy(shieldMaterial);
        shieldVisual = null;
        shieldMaterial = null;
    }

    /// <summary>
    /// 이전 효과의 구독과 버프를 먼저 해제한 뒤 실행 객체를 제거합니다.
    /// 여러 번 호출해도 이미 정리한 객체를 다시 처리하지 않습니다.
    /// </summary>
    private void StopRuntime()
    {
        GameObject previous = runtimeObject;
        // 버프 제거가 다른 이벤트를 호출해도 이전 객체를 다시 사용하지 않게 합니다.
        runtimeObject = null;
        activeItem = null;
        activeEffect = null;
        if (previous == null)
            return;

        // 객체가 실제로 파괴되기 전에 이벤트 구독과 버프를 먼저 정리합니다.
        previous.GetComponent<StatThresholdRunner>()?.Unbind();
        previous.SetActive(false);
        if (Application.isPlaying)
            Destroy(previous);
        else
            DestroyImmediate(previous);
    }
}
