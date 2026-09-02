using System.Collections;
using ItemSystem;
using UnityEngine;

public enum WorldItemVisualCategory
{
    FighterGreatsword,
    FighterBlunt,
    FighterAxe,
    GunnerGrenadeLauncher,
    GunnerShotgun,
    GunnerRifle,
    ArmorHelmet,
    Armor,
    ArmorBoots,
    Potion,
    Relic
}

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class WorldItemCategoryVisualView : MonoBehaviour
{
    [Header("Fallback")]
    [SerializeField] private MeshRenderer fallbackRenderer;

    [Header("Fighter")]
    [SerializeField] private GameObject fighterGreatswordVisual;
    [SerializeField] private GameObject fighterBluntVisual;
    [SerializeField] private GameObject fighterAxeVisual;

    [Header("Gunner")]
    [SerializeField] private GameObject gunnerGrenadeLauncherVisual;
    [SerializeField] private GameObject gunnerShotgunVisual;
    [SerializeField] private GameObject gunnerRifleVisual;

    [Header("Armor")]
    [SerializeField] private GameObject armorHelmetVisual;
    [SerializeField] private GameObject armorVisual;
    [SerializeField] private GameObject armorBootsVisual;

    [Header("Shared")]
    [SerializeField] private GameObject potionVisual;
    [SerializeField] private GameObject relicVisual;

    [Header("Hover")]
    [SerializeField, Min(1f)] private float hoveredScale = 1.06f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.08f;

    private Transform activeVisual;
    private Vector3 activeVisualBaseScale = Vector3.one;
    private Coroutine hoverRoutine;
    private bool isHovered;

    private void Awake()
    {
        if (fallbackRenderer == null)
            fallbackRenderer = GetComponent<MeshRenderer>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fallbackRenderer == null)
            fallbackRenderer = GetComponent<MeshRenderer>();
    }
#endif

    public void Apply(ItemDefinitionSO definition)
    {
        StopHoverRoutine();
        ResetActiveVisualScale();
        SetAllVisualsActive(false);

        activeVisual = null;
        if (fallbackRenderer == null)
            fallbackRenderer = GetComponent<MeshRenderer>();

        if (fallbackRenderer != null)
            fallbackRenderer.enabled = true;

        if (!TryResolveCategory(definition, out WorldItemVisualCategory category))
            return;

        GameObject resolvedVisual = GetVisual(category);
        if (resolvedVisual == null)
            return;

        resolvedVisual.SetActive(true);
        activeVisual = resolvedVisual.transform;
        AlignVisualToMainCamera(activeVisual, category);
        activeVisualBaseScale = activeVisual.localScale;
        activeVisual.localScale = activeVisualBaseScale * (isHovered ? hoveredScale : 1f);

        if (fallbackRenderer != null)
            fallbackRenderer.enabled = false;
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        StopHoverRoutine();

        if (activeVisual == null)
            return;

        Vector3 targetScale = activeVisualBaseScale * (hovered ? hoveredScale : 1f);
        if (hoverDuration <= 0f || !isActiveAndEnabled)
        {
            activeVisual.localScale = targetScale;
            return;
        }

        hoverRoutine = StartCoroutine(AnimateScale(targetScale));
    }

    public bool TryResolveCategory(
        ItemDefinitionSO definition,
        out WorldItemVisualCategory category)
    {
        category = default;
        if (definition == null)
            return false;

        switch (definition.category)
        {
            case ItemCategory.Weapon:
                return TryResolveWeapon(
                    definition.characterClass,
                    definition.weaponType,
                    out category);

            case ItemCategory.Armor:
                switch (definition.armorType)
                {
                    case ArmorType.Helmet:
                        category = WorldItemVisualCategory.ArmorHelmet;
                        return true;
                    case ArmorType.Armor:
                        category = WorldItemVisualCategory.Armor;
                        return true;
                    case ArmorType.Boots:
                        category = WorldItemVisualCategory.ArmorBoots;
                        return true;
                    default:
                        return false;
                }

            case ItemCategory.Potion:
                category = WorldItemVisualCategory.Potion;
                return true;

            case ItemCategory.Relic:
                category = WorldItemVisualCategory.Relic;
                return true;

            default:
                return false;
        }
    }

    private static bool TryResolveWeapon(
        CharacterClass characterClass,
        WeaponType weaponType,
        out WorldItemVisualCategory category)
    {
        category = default;

        if (characterClass == CharacterClass.Fighter)
        {
            switch (weaponType)
            {
                case WeaponType.Greatsword:
                    category = WorldItemVisualCategory.FighterGreatsword;
                    return true;
                case WeaponType.Blunt:
                    category = WorldItemVisualCategory.FighterBlunt;
                    return true;
                case WeaponType.Axe:
                    category = WorldItemVisualCategory.FighterAxe;
                    return true;
                default:
                    return false;
            }
        }

        if (characterClass == CharacterClass.Gunner)
        {
            switch (weaponType)
            {
                case WeaponType.GrenadeLauncher:
                    category = WorldItemVisualCategory.GunnerGrenadeLauncher;
                    return true;
                case WeaponType.Shotgun:
                    category = WorldItemVisualCategory.GunnerShotgun;
                    return true;
                case WeaponType.Rifle:
                    category = WorldItemVisualCategory.GunnerRifle;
                    return true;
                default:
                    return false;
            }
        }

        return false;
    }

    private GameObject GetVisual(WorldItemVisualCategory category)
    {
        switch (category)
        {
            case WorldItemVisualCategory.FighterGreatsword:
                return fighterGreatswordVisual;
            case WorldItemVisualCategory.FighterBlunt:
                return fighterBluntVisual;
            case WorldItemVisualCategory.FighterAxe:
                return fighterAxeVisual;
            case WorldItemVisualCategory.GunnerGrenadeLauncher:
                return gunnerGrenadeLauncherVisual;
            case WorldItemVisualCategory.GunnerShotgun:
                return gunnerShotgunVisual;
            case WorldItemVisualCategory.GunnerRifle:
                return gunnerRifleVisual;
            case WorldItemVisualCategory.ArmorHelmet:
                return armorHelmetVisual;
            case WorldItemVisualCategory.Armor:
                return armorVisual;
            case WorldItemVisualCategory.ArmorBoots:
                return armorBootsVisual;
            case WorldItemVisualCategory.Potion:
                return potionVisual;
            case WorldItemVisualCategory.Relic:
                return relicVisual;
            default:
                return null;
        }
    }

    private void SetAllVisualsActive(bool active)
    {
        SetVisualActive(fighterGreatswordVisual, active);
        SetVisualActive(fighterBluntVisual, active);
        SetVisualActive(fighterAxeVisual, active);
        SetVisualActive(gunnerGrenadeLauncherVisual, active);
        SetVisualActive(gunnerShotgunVisual, active);
        SetVisualActive(gunnerRifleVisual, active);
        SetVisualActive(armorHelmetVisual, active);
        SetVisualActive(armorVisual, active);
        SetVisualActive(armorBootsVisual, active);
        SetVisualActive(potionVisual, active);
        SetVisualActive(relicVisual, active);
    }

    private static void SetVisualActive(GameObject visual, bool active)
    {
        if (visual != null)
            visual.SetActive(active);
    }

    private static void AlignVisualToMainCamera(
        Transform visual,
        WorldItemVisualCategory category)
    {
        Camera mainCamera = Camera.main;
        if (visual == null || mainCamera == null)
            return;

        Transform cameraTransform = mainCamera.transform;
        switch (category)
        {
            case WorldItemVisualCategory.FighterGreatsword:
            case WorldItemVisualCategory.FighterAxe:
            case WorldItemVisualCategory.Relic:
                visual.rotation = Quaternion.LookRotation(
                    cameraTransform.forward,
                    cameraTransform.up);
                break;

            case WorldItemVisualCategory.GunnerGrenadeLauncher:
                visual.rotation = Quaternion.LookRotation(
                    cameraTransform.up,
                    cameraTransform.right);
                break;

            case WorldItemVisualCategory.ArmorHelmet:
                visual.rotation = Quaternion.LookRotation(
                    cameraTransform.up,
                    cameraTransform.forward);
                break;

            default:
                visual.rotation = Quaternion.LookRotation(
                    cameraTransform.up,
                    -cameraTransform.forward);
                break;
        }
    }

    private IEnumerator AnimateScale(Vector3 targetScale)
    {
        Transform animatedVisual = activeVisual;
        Vector3 startScale = animatedVisual.localScale;
        float elapsed = 0f;

        while (elapsed < hoverDuration && animatedVisual == activeVisual)
        {
            elapsed += Time.unscaledDeltaTime;
            animatedVisual.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                Mathf.Clamp01(elapsed / hoverDuration));
            yield return null;
        }

        if (animatedVisual == activeVisual)
            animatedVisual.localScale = targetScale;

        hoverRoutine = null;
    }

    private void StopHoverRoutine()
    {
        if (hoverRoutine == null)
            return;

        StopCoroutine(hoverRoutine);
        hoverRoutine = null;
    }

    private void ResetActiveVisualScale()
    {
        if (activeVisual != null)
            activeVisual.localScale = activeVisualBaseScale;
    }

    private void OnDisable()
    {
        StopHoverRoutine();
        ResetActiveVisualScale();
        isHovered = false;
    }
}
