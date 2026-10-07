using System;
using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

public sealed partial class PlayerItemEffectState
{
    private readonly List<EnemySupportMark> supportMarks = new();
    public event Action<Vector3, Vector3> SupportMarkConsumed;

    internal void ForgetSupportMark(EnemySupportMark mark) => supportMarks.Remove(mark);
    private void ClearSupportMarks()
    {
        while (supportMarks.Count > 0)
        {
            var mark = supportMarks[0]; supportMarks.RemoveAt(0);
            if (mark != null) mark.Clear();
        }
    }

    /// <summary>SW 수정: 살아남은 라이플 직접 표적에만 표시하고 기존 표식의 시전자·시간을 빼앗지 않는다.</summary>
    internal void TryCreateSupportMark(in WBH_DamageRequest request)
    {
        if (request.AttackType != WBH_AttackType.Normal || request.DamageCause != DamageCause.Direct ||
            request.Target is not Component component || component == null || !component.gameObject.activeInHierarchy ||
            request.Target.Status.IsDead || !TryGetGunnerHitSource(request.AttackId, out var source, out var weaponType) ||
            weaponType != GunnerWeaponType.Rifle || source is not SmileSignalMarkUniqueEffectSO effect ||
            !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out var item) ||
            item?.definition?.uniqueEffect != effect) return;
        var target = component.GetComponentInParent<WBH_EnemyController>();
        if (target == null) return;
        var mark = target.GetComponent<EnemySupportMark>() ?? target.gameObject.AddComponent<EnemySupportMark>();
        if (!mark.TryMark(context, item, effect, request.AttackId)) return;
        while (supportMarks.Count >= Mathf.Clamp(effect.maxTargets, 1, 16))
        {
            var oldest = supportMarks[0]; supportMarks.RemoveAt(0);
            if (oldest != null) oldest.Clear();
        }
        supportMarks.Add(mark);
    }

    internal void PresentSupportConsumption(Vector3 targetPosition)
        => SupportMarkConsumed?.Invoke(context.transform.position + Vector3.up, targetPosition + Vector3.up);
}
