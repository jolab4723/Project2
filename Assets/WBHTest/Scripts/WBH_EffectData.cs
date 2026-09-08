using UnityEngine;
using UnityEngine.Serialization;

public enum EffectAttachType
{
    World,
    AttachOnce,
    Follow,
    Follow_Billboard
}

[CreateAssetMenu(fileName = "EffectData", menuName = "WBH/Effect Data")]
public class WBH_EffectData : ScriptableObject
{
    [Header("Effect")]
    // SW 추가:
    // 기존 EffectData 자산에는 이 값이 effectPrefab이라는 이름으로 저장되어 있습니다.
    // 필드 이름이 attackEffectPrefab으로 바뀐 뒤에도 기존 프리팹 연결을 그대로 읽도록 이전 이름을 알려 줍니다.
    [FormerlySerializedAs("effectPrefab")]
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
    public bool applyAttackSpeed = true;
}
