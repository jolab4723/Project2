using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 장착(또는 보유) 중인 동안, 소유자를 중심으로 한 반경 안의 대상에게 버프를 거는 고유 효과.
    /// targetEnemies가 꺼져 있으면 플레이어(예: "영역: 가속화" - 이동속도 증가), 켜져 있으면
    /// 적(EnemyBuffManager)에게 적용된다(예: "중력장 발생" - 적 이동속도 감소).
    ///
    /// 실제 영역 판정/적용/해제는 BuffFieldZone을 런타임에 생성해서 그대로 위임한다(로직 중복 없음).
    /// 존은 소유자의 자식으로 붙이지 않고 FollowTransform으로 위치만 매 프레임 따라가게 한다
    /// (자식으로 붙이면 소유자의 Rigidbody와 같은 복합 콜라이더로 묶여서 소유자 본인에게는
    /// 트리거가 발생하지 않기 때문 - 오라가 소유자 자신은 빼고 남만 buff하게 되는 문제 방지).
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·반경·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/FieldAura")]
    public class FieldAuraUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        private const int IgnoreRaycastLayer = 2;

        [Header("영역")]
        [Tooltip("오라 반경(월드 유닛). 소유자를 중심으로 이 반경 안의 대상에게 버프가 적용된다.")]
        public float radius = 10f;

        [Tooltip("켜면 반경 안의 적(EnemyBuffManager)에게 적용된다(디버프용). 끄면 기존처럼 플레이어에게 적용된다. " +
                 "엑셀 파이프라인 컬럼이 아직 없어서 이 필드는 에셋에서 직접 설정해야 한다.")]
        public bool targetEnemies = false;

        [Header("적용할 버프")]
        [Tooltip("영역 안에 있는 동안 적용될 효과. 영역을 나가면 해제되므로 duration은 0(영구)으로 둬야 한다.")]
        public BuffSpec buffSpec = new BuffSpec { duration = 0f, stackBehavior = BuffStackBehavior.Ignore };

        [Header("시각 표시")]
        [Tooltip("끄면 링을 그리지 않는다(판정만 남기고 싶을 때).")]
        public bool showAreaVisual = true;

        [Tooltip("바닥에 그릴 링 색상.")]
        public Color areaVisualColor = new Color(0.4f, 0.8f, 1f, 0.8f);

        private GameObject zoneObject;

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (zoneObject != null)
                return; // 이미 만들어져 있으면 중복 생성 방지

            Transform owner = ResolveOwnerTransform();
            if (owner == null)
            {
                Debug.LogWarning("[FieldAuraUniqueEffectSO] 소유자 트랜스폼을 찾지 못해 오라를 만들지 못했습니다.");
                return;
            }

            zoneObject = new GameObject("[FieldAura] " + effectName);
            zoneObject.layer = IgnoreRaycastLayer;
            zoneObject.transform.position = owner.position;

            var follow = zoneObject.AddComponent<FollowTransform>();
            follow.SetTarget(owner);

            var collider = zoneObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = radius;

            // 트리거 이벤트는 둘 중 하나에 Rigidbody가 있어야 발생한다. 플레이어는 Rigidbody가 있어서
            // 기존(플레이어 대상) 오라는 우연히 동작했지만, 적(Droid 등)은 Rigidbody가 없어서
            // targetEnemies=true인 오라는 존 쪽에 직접 kinematic Rigidbody를 둬야 트리거가 감지된다.
            var rigidbody = zoneObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var zone = zoneObject.AddComponent<BuffFieldZone>();
            zone.ConfigureRuntime(this, targetEnemies: targetEnemies, removeOnExit: true, removeWhenZoneDisabled: true);

            if (showAreaVisual)
            {
                var ring = zoneObject.AddComponent<AreaRingVisual>();
                ring.SetColor(areaVisualColor);
                ring.SetRadius(radius);
            }
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            if (zoneObject == null)
                return;

            Object.Destroy(zoneObject); // BuffFieldZone.OnDisable에서 안에 있던 대상들의 버프 정리까지 처리됨
            zoneObject = null;
        }

        /// <summary>
        /// 오라가 따라다닐 소유자의 트랜스폼. 지금은 로컬 플레이어 전용이라 PlayerStatManager.Instance로 찾는다.
        /// !! 멀티플레이 대비: 나중에 "이 아이템을 실제로 소유한 캐릭터"를 알 수 있게 되면 그 트랜스폼으로 바꿔야 한다.
        /// </summary>
        private static Transform ResolveOwnerTransform()
        {
            return PlayerStatManager.Instance != null ? PlayerStatManager.Instance.transform : null;
        }

        // ----- IBuffSource -----
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => buffSpec?.statEffects;
        public float Duration => buffSpec != null ? buffSpec.duration : 0f;
        public BuffStackBehavior StackBehavior => buffSpec != null ? buffSpec.stackBehavior : BuffStackBehavior.Ignore;
        public int MaxStack => buffSpec != null ? buffSpec.maxStack : 0;
        public bool IsPermanent => buffSpec == null || buffSpec.IsPermanent;
    }
}
