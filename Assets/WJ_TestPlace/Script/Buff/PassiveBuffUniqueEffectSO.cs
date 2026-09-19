using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 장착하면 상시 적용되는 고유 효과. 장착 시 ApplyBuff, 해제 시 RemoveBuff 한다.
    /// buffSpec.duration을 0 이하로 두면 영구 지속(해제 전까지 유지)로 동작한다.
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/PassiveBuff")]
    public class PassiveBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("적용할 버프")]
        [Tooltip("장착 중 상시 적용될 효과. duration을 0 이하로 두면 영구 지속.")]
        public BuffSpec buffSpec = new BuffSpec { duration = 0f, stackBehavior = BuffStackBehavior.Ignore };

        /// <summary>
        /// 지금 이 효과를 켜두고 있는 아이템들.
        ///
        /// !! 버프 식별 키가 이 에셋 자신이라 같은 유물을 2개 보유해도 버프는 1개만 걸린다
        ///    (stackBehavior가 Ignore라 두 번째 ApplyBuff는 무시된다). 그런데 예전엔 그중 하나만
        ///    잃어도 OnUnequip이 그 하나뿐인 버프를 지워버려서, 인벤토리에 남아 있는 사본의 효과까지
        ///    같이 사라졌다. 소유자를 세어두고 마지막 하나가 빠질 때만 버프를 제거한다.
        ///    (SW의 DropRarityModifierUniqueEffectSO도 같은 방식으로 중복 보유를 처리한다.)
        ///
        ///    instanceId가 비어 있는 아이템도 있어서 ID 대신 ItemInstance 참조로 센다
        ///    (ItemInstance는 Equals를 재정의하지 않은 일반 클래스라 참조 비교가 된다).
        /// </summary>
        [System.NonSerialized] private readonly HashSet<ItemInstance> owners = new HashSet<ItemInstance>();

        /// <summary>
        /// owners가 어느 캐릭터의 버프 매니저를 기준으로 쌓인 기록인지.
        /// PlayerBuffManager는 캐릭터에 붙어 있어서 씬 이동·캐릭터 교체 때 새로 생기는데, 새 매니저엔
        /// 버프가 없으므로 이전 소유 기록을 그대로 두면 "이미 켜져 있다"고 오판해 다시 적용하지 않는다.
        /// 매니저가 바뀌면 기록을 비우고 처음부터 다시 센다.
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

            // 소유자를 특정할 수 없으면 세지 못하므로 예전처럼 바로 적용만 한다.
            if (ownerItem == null)
            {
                manager.ApplyBuff(this);
                return;
            }

            bool alreadyActive = owners.Count > 0;
            bool added = owners.Add(ownerItem);

            if (!added || alreadyActive)
                return; // 이미 센 아이템이거나, 다른 사본이 이미 효과를 켜둔 상태

            manager.ApplyBuff(this);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            PlayerBuffManager manager = PlayerBuffManager.Instance;
            if (manager == null || appliedManager != manager)
            {
                // 적용 대상이 사라졌거나 다른 캐릭터로 바뀐 뒤라 이 매니저에서 지울 버프가 없다.
                owners.Clear();
                appliedManager = manager;
                return;
            }

            if (ownerItem == null)
            {
                manager.RemoveBuff(this);
                return;
            }

            if (!owners.Remove(ownerItem))
                return; // 이 효과를 켠 적 없는 아이템

            if (owners.Count == 0)
                manager.RemoveBuff(this); // 마지막 사본까지 빠졌을 때만 해제
        }

        private void SyncManager(PlayerBuffManager manager)
        {
            if (appliedManager == manager)
                return;

            owners.Clear();
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
