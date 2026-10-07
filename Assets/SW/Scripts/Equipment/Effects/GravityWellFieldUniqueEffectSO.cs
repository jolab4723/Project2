using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// SW 수정: 중력 우물 유탄이 충돌한 자리에 남기는 둔화 장판 설정입니다.
    /// 서버가 범위와 지속시간을 판정하고, 클라이언트는 같은 위치의 링을 표시합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Gravity Well Field")]
    public sealed class GravityWellFieldUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float radius = 3.5f;
        [Min(0.1f)] public float durationSeconds = 5f;
        [Range(0.01f, 1f)] public float slowMultiplier = 0.55f;
        [Min(0.05f)] public float slowRefreshSeconds = 0.5f;
        [Range(1, 8)] public int maxConcurrentFields = 3;
        public Color fieldColor = new(0.45f, 0.2f, 1f, 0.75f);
    }
}
