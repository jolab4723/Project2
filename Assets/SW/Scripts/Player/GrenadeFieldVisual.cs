using UnityEngine;

/// <summary>SW 수정: 장판 링의 생성 재질을 정리하고 일식 장판 중심에 표시용 불티를 유지한다.</summary>
public sealed class GrenadeFieldVisual : MonoBehaviour
{
    private Material ownedMaterial;

    public void Initialize(bool sunfall, Color color)
    {
        ownedMaterial = GetComponent<LineRenderer>().sharedMaterial;
        if (!sunfall) return;
        var ember = new GameObject("Sunfall Embers");
        ember.transform.SetParent(transform, false);
        ember.transform.localPosition = Vector3.up * 0.1f;
        var particles = ember.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startColor = color;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.rateOverTime = 20f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.55f;
        shape.angle = 12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = ownedMaterial;
    }

    private void OnDestroy()
    {
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
