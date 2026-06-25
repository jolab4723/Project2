using UnityEngine;

public class Player_ClickToAction : MonoBehaviour
{
    private Animator animator;

    protected RaycastHit hit;
    protected GameObject targetObject = null;
    public LayerMask clickableLayer;

    protected bool isAttacking = false;

    protected virtual void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PerformRaycast(0);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            PerformRaycast(1);
        }
    }

    private void PerformRaycast(int buttonType)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickableLayer))
        {
            targetObject = hit.collider.gameObject;

            if (buttonType == 0)
            {
                OnLeftClickTarget(targetObject, hit.point);
            }
            else if (buttonType == 1 && isAttacking == false)
            {
                OnRightClickTarget(targetObject, hit.point);
            }
        }
    }

    // 자식 스크립트들이 덮어쓸 수 있도록 비워둔 가상 함수 2개
    protected virtual void OnLeftClickTarget(GameObject target, Vector3 clickPosition) { }
    protected virtual void OnRightClickTarget(GameObject target, Vector3 clickPosition) { }
}