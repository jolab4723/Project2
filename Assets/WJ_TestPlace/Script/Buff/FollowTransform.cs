using UnityEngine;

/// <summary>
/// 지정한 대상의 위치를 매 프레임 따라간다.
/// 부모-자식 관계로 붙이면 대상에 Rigidbody가 있을 때 같은 복합 콜라이더로 묶여서
/// 트리거 이벤트가 자기 자신과는 발생하지 않는 문제가 생긴다(예: 소유자를 따라다니는 오라 존이
/// 소유자 본인에게는 적용되지 않음). 부모로 붙이지 않고 위치만 매 프레임 복사해서 이를 피한다.
/// </summary>
public class FollowTransform : MonoBehaviour
{
    private Transform target;
    private Vector3 offset;

    public void SetTarget(Transform target, Vector3 offset = default)
    {
        this.target = target;
        this.offset = offset;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
    }
}
