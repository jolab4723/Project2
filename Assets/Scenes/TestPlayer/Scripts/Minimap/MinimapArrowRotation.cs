using UnityEngine;

public class MinimapArrowRotation : MonoBehaviour
{
    [Tooltip("Hierarchy에 있는 플레이어(Fighter) 오브젝트를 넣으세요")]
    public Transform playerTransform;

    [Tooltip("기준이 될 카메라 (비워두면 Main Camera를 자동으로 찾습니다)")]
    public Transform mainCameraTransform;

    private RectTransform arrowRectTransform;

    void Start()
    {
        arrowRectTransform = GetComponent<RectTransform>();

        // 카메라 빈칸을 비워뒀다면, 태그가 MainCamera인 카메라를 자동으로 찾습니다.
        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        // 플레이어와 카메라가 모두 존재할 때만 실행
        if (playerTransform != null && mainCameraTransform != null)
        {
            // 1. 플레이어와 카메라의 Y축(좌우) 회전값을 각각 가져옵니다.
            float playerRotationY = playerTransform.eulerAngles.y;
            float cameraRotationY = mainCameraTransform.eulerAngles.y;

            // ★ 2. 핵심 보정 공식: 카메라가 바라보는 방향을 기준으로 플레이어가 얼마나 더 돌았는지 뺍니다.
            // (카메라 방향 = 미니맵의 12시 방향이 됨)
            float relativeRotation = playerRotationY - cameraRotationY;

            // 3. 미니맵 화살표(2D UI)에 적용 (3D와 방향이 반대이므로 마이너스(-)를 붙여줍니다)
            arrowRectTransform.localRotation = Quaternion.Euler(0f, 0f, -relativeRotation);
        }
    }
}