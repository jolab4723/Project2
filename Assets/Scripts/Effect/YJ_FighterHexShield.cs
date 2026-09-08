using System.Collections;
using UnityEngine;

public class YJ_FighterHexShield : MonoBehaviour
{
    private const int DashSkillIndex = 2;

    private static readonly int ShieldStepId = Shader.PropertyToID("_Shield_step");

    [Header("Shield Fade")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float fallbackInvincibleDuration = 0.5f;

    private MeshRenderer[] targetRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine shieldRoutine;
    [SerializeField] private GameObject circle;

    private void Awake()
    {
        targetRenderers = GetComponentsInChildren<MeshRenderer>(true);
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (shieldRoutine != null)
            StopCoroutine(shieldRoutine);

        SetShieldStep(1f);
        shieldRoutine = StartCoroutine(FadeAfterInvincibility());
    }

    private void OnDisable()
    {
        if (shieldRoutine == null)
            return;

        StopCoroutine(shieldRoutine);
        shieldRoutine = null;
    }

    private IEnumerator FadeAfterInvincibility()
    {
        float enabledTime = Time.time;

        // 풀에서 활성화된 뒤 플레이어 하위로 재배치되므로 한 프레임 대기한다.
        yield return null;

        // 외부 이펙트 컴포넌트의 Awake가 덮어쓴 값도 다시 복구한다.
        SetShieldStep(1f);
        circle.gameObject.SetActive(false);

        float invincibleDuration = ResolveInvincibleDuration();
        float remainingInvincibleTime = invincibleDuration - (Time.time - enabledTime);

        if (remainingInvincibleTime > 0f)
            yield return new WaitForSeconds(remainingInvincibleTime);

        if (fadeDuration <= 0f)
        {
            SetShieldStep(0f);
            shieldRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float fadeRatio = Mathf.Clamp01(elapsed / fadeDuration);
            SetShieldStep(Mathf.Lerp(1f, 0f, fadeRatio));
            yield return null;
        }

        SetShieldStep(0f);
        shieldRoutine = null;
    }

    private float ResolveInvincibleDuration()
    {
        FighterSkillController skillController = GetComponentInParent<FighterSkillController>();
        SkillDefinitionSO skillDefinition = skillController != null
            ? skillController.GetSkillDefinition(DashSkillIndex)
            : null;

        return skillDefinition != null
            ? Mathf.Max(0f, skillDefinition.evoInvincibleDuration)
            : fallbackInvincibleDuration;
    }

    private void SetShieldStep(float value)
    {
        foreach (MeshRenderer targetRenderer in targetRenderers)
        {
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ShieldStepId, value);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
