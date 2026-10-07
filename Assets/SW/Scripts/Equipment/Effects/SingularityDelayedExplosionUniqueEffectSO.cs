using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// SW 수정: 특이점 박격포 유탄이 충돌한 자리에 남기는 지연 폭발 설정입니다.
    /// 서버가 지연 시간과 피해를 판정하고 클라이언트는 같은 위치에 경고 링을 표시합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Singularity Delayed Explosion")]
    public sealed class SingularityDelayedExplosionUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float delaySeconds = 2f;
        [Min(0.1f)] public float explosionRadius = 4f;
        [Min(0.01f)] public float damageMultiplier = 1f;
        [Range(1, 8)] public int maxPendingExplosions = 3;
        public Color warningColor = new(0.85f, 0.15f, 1f, 0.8f);
    }
}
