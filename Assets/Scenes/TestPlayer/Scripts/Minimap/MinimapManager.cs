using System.Collections.Generic;
using UnityEngine;

public class MinimapManager : MonoBehaviour
{
    public static MinimapManager Instance;

    [Header("필수 연결 요소")]
    [Tooltip("기준이 될 플레이어의 Transform")]
    public Transform playerTransform;
    [Tooltip("미니맵 위에 생성될 빨간 점 아이콘 프리팹 (가진 이미지를 이용해 만든 프리팹)")]
    public GameObject enemyIconPrefab;
    [Tooltip("아이콘들이 생성될 부모 UI (Mask가 달린 미니맵 배경 이미지)")]
    public RectTransform minimapContent;

    [Header("미니맵 설정")]
    [Tooltip("실제 월드와 미니맵 UI 크기의 비율 (숫자가 클수록 미니맵에 작게/멀리 표시됨)")]
    public float mapScale = 2.0f;
    [Tooltip("이 거리보다 멀리 있는 적은 미니맵에서 지웁니다 (최적화용)")]
    public float maxDistance = 50f;

    // 현재 추적 중인 적들과 그들의 아이콘 UI를 연결해두는 사전(Dictionary)
    private Dictionary<MinimapTarget, RectTransform> targetIcons = new Dictionary<MinimapTarget, RectTransform>();

    private void Awake()
    {
        // 어디서든 쉽게 접근할 수 있도록 싱글톤 설정
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // 등록된 모든 적들의 위치를 매 프레임 업데이트합니다.
        foreach (var kvp in targetIcons)
        {
            MinimapTarget target = kvp.Key;
            RectTransform iconRect = kvp.Value;

            if (target == null || target.gameObject.activeInHierarchy == false)
                continue;

            // 1. 플레이어와 적 사이의 거리를 구합니다.
            Vector3 offset = target.transform.position - playerTransform.position;
            float distance = offset.magnitude;

            // 2. 지정된 거리(maxDistance) 밖이라면 아이콘을 끕니다. (마스크 밖으로 나가도 꺼지게 하여 성능 확보)
            if (distance > maxDistance)
            {
                if (iconRect.gameObject.activeSelf)
                    iconRect.gameObject.SetActive(false);
            }
            else
            {
                if (!iconRect.gameObject.activeSelf)
                    iconRect.gameObject.SetActive(true);

                // 3. 거리가 범위 안이라면, 3D 좌표(X, Z)를 2D UI 좌표(X, Y)로 변환하여 적용합니다.
                // 쿼터뷰 카메라 회전에 대한 미니맵 UI 자체 회전은 따로 되어있다고 가정하고, 여긴 상대 위치만 잡습니다.
                iconRect.anchoredPosition = new Vector2(offset.x * mapScale, offset.z * mapScale);
            }
        }
    }

    // 적(MinimapTarget)이 스폰될 때 자신을 등록하는 함수
    public void RegisterTarget(MinimapTarget target)
    {
        if (!targetIcons.ContainsKey(target))
        {
            // 빨간 점 프리팹을 미니맵 배경 안에 동적으로 생성합니다.
            GameObject newIcon = Instantiate(enemyIconPrefab, minimapContent);
            targetIcons.Add(target, newIcon.GetComponent<RectTransform>());
        }
    }

    // 적이 죽거나 비활성화될 때 자신을 지우는 함수
    public void UnregisterTarget(MinimapTarget target)
    {
        if (targetIcons.ContainsKey(target))
        {
            // 아이콘 UI를 삭제하고 리스트에서 뺍니다.
            if (targetIcons[target] != null)
                Destroy(targetIcons[target].gameObject);
                
            targetIcons.Remove(target);
        }
    }
}
