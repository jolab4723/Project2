using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>캐릭터의 공격력, 체력, 난이도를 애니메이션 삼각형 차트로 표시한다.</summary>
public class KY_CharacterStatRadarChart : MaskableGraphic
{
    [SerializeField, Min(1f)] private float maxValue = 5f;
    [SerializeField, Min(0f)] private float animationDuration = 0.3f;

    private float power;
    private float health;
    private float difficulty;
    private Coroutine animationCoroutine;

    /// <summary>새 능력치 목표로 차트 채움 애니메이션을 시작한다.</summary>
    public void SetValues(float powerValue, float healthValue, float difficultyValue)
    {
        // 닫힌 상세 창에서도 최신 값을 보관하고 다시 열면 바로 표시한다.
        if (!isActiveAndEnabled)
        {
            power = powerValue;
            health = healthValue;
            difficulty = difficultyValue;
            animationCoroutine = null;
            SetVerticesDirty();
            return;
        }
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        power = 0f;
        health = 0f;
        difficulty = 0f;
        SetVerticesDirty();

        animationCoroutine = StartCoroutine(AnimateValues(powerValue, healthValue, difficultyValue));
    }

    /// <summary>0에서 목표 능력치까지 차트 값을 보간한다.</summary>
    private IEnumerator AnimateValues(float targetPower, float targetHealth, float targetDifficulty)
    {
        if (animationDuration <= 0f)
        {
            power = targetPower;
            health = targetHealth;
            difficulty = targetDifficulty;
            SetVerticesDirty();
            animationCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            float progress = elapsed / animationDuration;
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

            power = Mathf.Lerp(0f, targetPower, easedProgress);
            health = Mathf.Lerp(0f, targetHealth, easedProgress);
            difficulty = Mathf.Lerp(0f, targetDifficulty, easedProgress);
            SetVerticesDirty();

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        power = targetPower;
        health = targetHealth;
        difficulty = targetDifficulty;
        SetVerticesDirty();
        animationCoroutine = null;
    }

    /// <summary>현재 능력치에 맞는 삼각형 메쉬를 다시 그린다.</summary>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        if (radius <= 0f)
            return;

        Vector2 center = rect.center;
        Vector2 powerPoint = GetPoint(center, radius, 90f, power);
        Vector2 healthPoint = GetPoint(center, radius, -30f, health);
        Vector2 difficultyPoint = GetPoint(center, radius, 210f, difficulty);

        vertexHelper.AddVert(powerPoint, color, Vector2.zero);
        vertexHelper.AddVert(healthPoint, color, Vector2.zero);
        vertexHelper.AddVert(difficultyPoint, color, Vector2.zero);

        vertexHelper.AddTriangle(0, 1, 2);
    }

    /// <summary>차트 축의 각도와 값을 UI 좌표로 변환한다.</summary>
    private Vector2 GetPoint(Vector2 center, float radius, float angle, float value)
    {
        float normalizedValue = Mathf.Clamp01(value / maxValue);
        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        return center + direction * radius * normalizedValue;
    }
}
