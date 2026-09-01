using UnityEngine;

public enum EffectAttachType
{
    World,
    AttachOnce,
    Follow
}

[CreateAssetMenu(fileName = "EffectData", menuName = "WBH/Effect Data")]
public class WBH_EffectData : ScriptableObject
{
    [Header("Effect")]
    public WBH_Effect attackEffectPrefab;
    public ParticleSystem hitEffectPrefab;

    [Header("Pool")]
    [Min(1)]
    public int poolSize = 3;

    [Header("Play Option")]
    public float autoReturnTime = 1f;
    public EffectAttachType attachType;
    public Vector3 localPos;
    public Vector3 localRot;
    public bool applyEnhancementScale = true;
}
