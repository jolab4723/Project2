using UnityEngine;

public enum EffectAttachType
{
    World,
    Local,
    Follow
}

[CreateAssetMenu(fileName = "EffectData",
                 menuName = "WBH/Effect Data")]
public class WBH_EffectData : MonoBehaviour
{
    [Header("Effect")]
    public WBH_Effect effectPrefab;

    [Header("Pool")]
    [Min(1)]
    public int poolSize = 3;

    [Header("Play Option")]
    public EffectAttachType attachType;

    public float autoReturnTime = 1f;

    public Vector3 localPos;

    public Vector3 localRot;
}
