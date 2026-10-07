using System.Collections.Generic;
using UnityEngine;

/// <summary>로비에 참여한 플레이어의 선택 캐릭터 3D 모델을 지정된 위치에 나란히 표시한다.</summary>
public class KY_LobbyLineupView : MonoBehaviour
{
    [Header("캐릭터 프리팹")]
    [SerializeField] private GameObject fighterPrefab;
    [SerializeField] private GameObject gunnerPrefab;
    [Header("플레이어 배치 위치")]
    [SerializeField] private Transform[] playerAnchors;
    private readonly List<GameObject> spawnedModels = new List<GameObject>();

    /// <summary>현재 방에 있는 플레이어의 선택 캐릭터를 3D 라인업으로 다시 만든다.</summary>
    public void ShowPlayers(IReadOnlyList<KY_LobbyPlayerData> players)
    {
        Clear();
        if (players == null || playerAnchors == null) return;
        int anchorIndex = 0;
        foreach (KY_LobbyPlayerData player in players)
        {
            if (player == null || player.readyState == KY_LobbyReadyState.Selecting) continue;
            if (anchorIndex >= playerAnchors.Length)
            {
                Debug.LogWarning("[KY_LobbyLineupView] 플레이어 배치 위치가 부족합니다.", this);
                break;
            }
            Transform anchor = playerAnchors[anchorIndex++];
            GameObject prefab = player.selectedCharacterId == KY_CharacterId.Fighter ? fighterPrefab : gunnerPrefab;
            if (anchor == null || prefab == null) continue;
            GameObject model = Instantiate(prefab, anchor);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            spawnedModels.Add(model);
        }
    }

    /// <summary>로비에서 생성한 3D 모델만 제거한다.</summary>
    public void Clear()
    {
        foreach (GameObject model in spawnedModels) if (model != null) Destroy(model);
        spawnedModels.Clear();
    }

    /// <summary>오브젝트가 파괴될 때 생성한 라인업 모델을 정리한다.</summary>
    private void OnDestroy() => Clear();
}
