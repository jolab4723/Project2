using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the selected lobby character's three stat grades as a filled radar chart.
/// </summary>
public class KY_LobbyRadarChart : MaskableGraphic
{
    [SerializeField, Min(1f)] private float maxValue = 5f;
    [SerializeField, Min(0f)] private float animationDuration = 0.3f;

    private float power;
    private float health;
    private float difficulty;
    private Coroutine animationCoroutine;

    public void SetValues(float powerValue, float healthValue, float difficultyValue)
    {
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        power = 0f;
        health = 0f;
        difficulty = 0f;
        SetVerticesDirty();

        animationCoroutine = StartCoroutine(AnimateValues(powerValue, healthValue, difficultyValue));
    }

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

    private Vector2 GetPoint(Vector2 center, float radius, float angle, float value)
    {
        float normalizedValue = Mathf.Clamp01(value / maxValue);
        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        return center + direction * radius * normalizedValue;
    }
}
