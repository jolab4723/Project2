using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 장착하면 상시 적용되는 고유 효과. 장착 시 ApplyBuff, 해제 시 RemoveBuff 한다.
    /// buffSpec.duration을 0 이하로 두면 영구 지속(해제 전까지 유지)로 동작한다.
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    ///
    /// !! 같은 유물을 2개 보유해도 버프는 1개만 걸린다(stackBehavior가 Ignore). 그 1개를
    ///    누가 켰는지는 UniqueEffectOwners가 세고, **마지막 소유자가 빠질 때만** 해제한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/PassiveBuff")]
    public class PassiveBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("적용할 버프")]
        [Tooltip("장착 중 상시 적용될 효과. duration을 0 이하로 두면 영구 지속.")]
        public BuffSpec buffSpec = new BuffSpec { duration = 0f, stackBehavior = BuffStackBehavior.Ignore };

        /// <summary>
        /// 지금 장부가 어느 캐릭터의 버프 매니저를 기준으로 쌓인 것인지.
        ///
        /// PlayerBuffManager는 캐릭터에 붙어 있어서 씬 이동·캐릭터 교체 때 새로 생긴다. 새 매니저엔
        /// 버프가 없으므로 이전 소유 기록을 그대로 두면 "이미 켜져 있다"고 오판해 다시 적용하지 않는다.
        /// </summary>
        [System.NonSerialized] private PlayerBuffManager appliedManager;

        public override void OnEquip(ItemInstance ownerItem)
        {
            PlayerBuffManager manager = PlayerBuffManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning(
                    $"[PassiveBuffUniqueEffectSO] PlayerBuffManager.Instance가 없어 '{BuffDisplayName}' 효과를 적용하지 못했습니다.",
                    this);
                return;
            }

            SyncManager(manager);

            if (UniqueEffectOwners.AddOwner(this, ownerItem))
                manager.ApplyBuff(this);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            PlayerBuffManager manager = PlayerBuffManager.Instance;
            if (manager == null || appliedManager != manager)
            {
                // 적용 대상이 사라졌거나 다른 캐릭터로 바뀐 뒤라 이 매니저에서 지울 버프가 없다.
                UniqueEffectOwners.Clear(this);
                appliedManager = manager;
                return;
            }

            if (UniqueEffectOwners.RemoveOwner(this, ownerItem))
                manager.RemoveBuff(this); // 마지막 사본까지 빠졌을 때만 해제
        }

        private void SyncManager(PlayerBuffManager manager)
        {
            if (appliedManager == manager)
                return;

            UniqueEffectOwners.Clear(this);
            appliedManager = manager;
        }

        // ----- IBuffSource -----
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => buffSpec?.statEffects;
        public float Duration => buffSpec != null ? buffSpec.duration : 0f;
        public BuffStackBehavior StackBehavior => buffSpec != null ? buffSpec.stackBehavior : BuffStackBehavior.Ignore;
        public int MaxStack => buffSpec != null ? buffSpec.maxStack : 0;
        public bool IsPermanent => buffSpec == null || buffSpec.IsPermanent;
        public BuffDisplayKind DisplayKind => buffSpec != null ? buffSpec.displayKind : BuffDisplayKind.Auto;
    }
}
