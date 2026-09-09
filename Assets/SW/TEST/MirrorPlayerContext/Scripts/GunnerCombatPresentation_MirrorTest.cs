using UnityEngine;

/// <summary>이미 로드된 동일 무기의 VFX만 재사용한다. 피해나 Addressables 로딩을 소유하지 않는다.</summary>
public static class GunnerCombatPresentation_MirrorTest
{
    public static GunnerWeaponVfxBinding FindBinding(GameObject owner, string itemId, GunnerWeaponType weaponType)
    {
        if (owner == null) return null;
        PlayerWeaponVisualPresenter presenter = owner.GetComponent<PlayerWeaponVisualPresenter>();
        if (presenter == null || !string.Equals(presenter.CurrentVisualItemId ?? string.Empty,
            itemId ?? string.Empty, System.StringComparison.Ordinal)) return null;
        GunnerWeaponVfxBinding binding = owner.GetComponentInChildren<GunnerWeaponVfxBinding>();
        return binding != null && binding.WeaponType == weaponType ? binding : null;
    }

    public static void PlayShot(GameObject owner, string itemId, GunnerWeaponType weaponType, Vector3 origin, Vector3 direction, float range)
    {
        GunnerWeaponVfxBinding binding = FindBinding(owner, itemId, weaponType);
        if (binding != null) binding.PlayMuzzle(origin, direction, range);
        else if (weaponType == GunnerWeaponType.Shotgun)
            SkillRangeVisual.ShowSector(origin, direction, range, 90f, new Color(1f, 0.7f, 0.2f, 0.3f), 0.15f);
    }

    public static void PlayImpact(GameObject owner, string itemId, GunnerWeaponType weaponType, Vector3 position, Vector3 direction)
    {
        GunnerWeaponVfxBinding binding = FindBinding(owner, itemId, weaponType);
        if (binding != null) GunnerVfxPlayback.SpawnTransient(binding.ImpactVisualPrefab, position, direction);
    }
}
