using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class YJ_WorldCursorDetector : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private YJ_CursorManager cursorManager;
    [SerializeField] private LayerMask targetLayers;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveTargetCamera();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        targetCamera = null;
        ResolveTargetCamera();
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            ResolveTargetCamera();

            if (targetCamera == null)
                return;
        }

        // UI 위에 있으면 월드 오브젝트를 검사하지 않음
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            cursorManager?.ResetCursor();
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if ( ! Physics.Raycast(ray, out RaycastHit hit, 1000f, targetLayers))
        {
            cursorManager.ResetCursor();
            return;
        }

        if (hit.collider.TryGetComponent(out WBH_EnemyController enemy))
        {
            cursorManager.SetCursor(CursorType.Enemy);
            return;
        }

        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("WorldItem"))
        {
            cursorManager.SetCursor(CursorType.WorldItem);
            return;
        }

        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("NPC"))
        {
            cursorManager.SetCursor(CursorType.NPC);
            return;
        }

        cursorManager.ResetCursor();
    }

    private void ResolveTargetCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }
}
