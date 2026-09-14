using UnityEngine;

/// <summary>씬 전환 전후에 공유하는 결과. 런타임 값은 에셋 파일에 기록하지 않는다.</summary>
[CreateAssetMenu(menuName = "KY/UI/Result Payload")]
public sealed class KY_ResultPayload : ScriptableObject
{
    [System.NonSerialized] private KY_ResultData data;
    [System.NonSerialized] private bool available;
    [System.NonSerialized] private int session;
    private static int playSession;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void BeginSession() { unchecked { playSession++; } }
    public void SetResult(KY_ResultData result) { data = result; available = true; session = playSession; }
    public bool TryRead(out KY_ResultData result)
    {
        result = data;
        return available && session == playSession;
    }
    public void Clear() { available = false; data = default; }
}
