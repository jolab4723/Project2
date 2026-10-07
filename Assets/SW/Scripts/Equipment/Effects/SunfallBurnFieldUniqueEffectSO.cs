using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// SW 수정 : 일식 기관 기본 유탄이 충돌한 자리에 남기는 화상 장판 설정이다.
    /// 싱글·서버가 기존 Burn1을 갱신하고 클라이언트는 같은 위치와 반경의 장판만 표시한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/Unique Effect/Sunfall Burn Field")]
    public sealed class SunfallBurnFieldUniqueEffectSO : UniqueEffectSO
    {
        [Min(0.1f)] public float radius = 3f;
        [Min(0.1f)] public float durationSeconds = 4f;
        [Min(0.05f)] public float burnRefreshSeconds = 0.5f;
        [Range(1, 8)] public int maxConcurrentFields = 2;
        public Color fieldColor = new(0.85f, 0.25f, 0.04f, 0.55f);
    }
}
