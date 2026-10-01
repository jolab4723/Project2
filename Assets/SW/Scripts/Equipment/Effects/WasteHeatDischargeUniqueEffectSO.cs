using System;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>SW 수정: Fighter의 유효 기본 공격으로 열을 충전하고 준비 다음 공격에서 싱글·서버가 확정하는 비치명 화염 방출 설정이다.</summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Waste Heat Discharge")]
    public sealed class WasteHeatDischargeUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Min(1)] public int requiredHeat = 4;
        [Min(0.1f)] public float length = 4f;
        [Range(0.1f, 180f)] public float angleDegrees = 70f;
        [Min(0.001f)] public float damageMultiplier = 0.5f;
        [Range(1, 16)] public int maxTargets = 4;
        [Min(0.1f)] public float idleResetSeconds = 5f;

        // SW 수정: 열은 플레이어 BuffInstance의 0~필요 열 스택이며 공유 SO에는 상태나 실제 스탯 버프를 저장하지 않는다.
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => Array.Empty<FixedStatValue>();
        public float Duration => 0f;
        public BuffStackBehavior StackBehavior => BuffStackBehavior.Stack;
        public int MaxStack => requiredHeat;
        public bool IsPermanent => true;
        public BuffDisplayKind DisplayKind => BuffDisplayKind.Buff;
    }
}
