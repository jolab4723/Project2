using UnityEngine;

/// <summary>
/// 폭탄 투척 진화2 "에너지 폭발" 전용 - 폭발 지점에 일정 시간(zoneDuration) 동안 남아서, 그 안에 있는
/// 적에게 계속 Slow 상태이상을 걸어준다. WBH_StatusEffectController.AddStatusEffect가 같은 타입을
/// 재적용하면 지속시간을 갱신(누적 아님)하는 구조라, 안에 머무는 동안 매 프레임 재적용하면 "영역 안에
/// 있는 동안 슬로우 유지, 나가면 곧 풀림"이 그대로 구현된다(118번).
/// </summary>
public class GunnerSlowZone : MonoBehaviour
{
    // AddStatusEffect를 매 프레임 재호출해서 지속시간을 계속 갱신하므로, 실제로는 이 값보다 훨씬 짧게만
    // 유지되면 충분하다 - 프레임이 끊겨도(렉 등) 잠깐은 슬로우가 남아있도록 약간의 여유를 둔다.
    private const float SlowRefreshDuration = 0.5f;

    private float slowSpeedMultiplier;
    private LayerMask targetLayer;

    public void Initialize(float radius, float zoneDuration, float slowSpeedMultiplier, LayerMask targetLayer)
    {
        this.slowSpeedMultiplier = slowSpeedMultiplier;
        this.targetLayer = targetLayer;

        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;

        // SW 수정: 정식 적 루트에는 Rigidbody가 없어 구역 쪽에도 없으면 OnTriggerStay가 오지 않는다.
        // FieldAura 구역과 같이 판정용 kinematic Rigidbody를 둔다.
        var body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        Destroy(gameObject, zoneDuration);
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
            return;

        if (other.TryGetComponent<WBH_ICombat>(out var combatTarget))
        {
            var slow = new WBH_StatusEffectData(WBH_StatusEffectType.Slow, duration: SlowRefreshDuration, value: slowSpeedMultiplier);
            combatTarget.AddStatusEffect(slow);
        }
    }
}
