using UnityEngine;

public class WBH_DrawGizmo : MonoBehaviour
{
    [SerializeField] private bool isRay;

    private void OnDrawGizmosSelected()
    {
        if (isRay)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.up.normalized * 10f);
        }
    }
}
