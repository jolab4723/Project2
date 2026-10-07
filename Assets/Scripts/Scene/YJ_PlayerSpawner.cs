using Core;
using ItemSystem;
using UnityEngine;

// 씬의 Awake/OnEnable(NavMesh 등록 등) 이후, 카메라 등의 Start보다 먼저 생성합니다.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class YJ_PlayerSpawner : MonoBehaviour
{
    [Tooltip("Hierarchy의 인스턴스가 아닌 Project 창의 Fighter 프리팹을 연결합니다.")]
    [SerializeField] private T_PlayerController fighterPrefab;
    [Tooltip("Hierarchy의 인스턴스가 아닌 Project 창의 Gunner 프리팹을 연결합니다.")]
    [SerializeField] private T_PlayerController gunnerPrefab;
    [SerializeField] private WorldItemTooltipScanner worldItemScanner;
    [SerializeField] private YJ_MinimapPing minimapPing;
    public T_PlayerController SpawnedPlayer { get; private set; }
    private PlayerActionInputHandler actionInput;
    private bool actionInputWasEnabled;

    /// <summary>SW 수정: 상태·외형 준비가 끝날 때까지 이동과 포션/스킬 입력을 함께 보류합니다.</summary>
    public void SetGameplayInput(bool ready)
    {
        SpawnedPlayer?.SetControlEnable(ready);
        if (actionInput != null) actionInput.enabled = ready && actionInputWasEnabled;
    }

    /// <summary>
    /// 선택한 플레이어를 생성하고 HUD를 연결합니다.
    /// SW 수정: 생성 직후 공통 PlayerContext에 기존 씬 인벤토리를 연결합니다.
    /// 멀티 세션에서는 기존 SessionLifecycle이 참가자를 생성합니다.
    /// </summary>
    private void Start()
    {
        if (MirrorNetworkManager.OwnsGameplay) return;
        DataManager dataManager = DataManager.Instance;
        if (dataManager == null)
        {
            Log.Error("플레이어 생성 실패: DataManager가 없습니다. 부트 씬의 초기화를 확인하세요.");
            return;
        }

        if (!dataManager.TryGetSavedCharacter(out CharacterClass character))
            return;

        T_PlayerController prefab = character == CharacterClass.Fighter ? fighterPrefab : gunnerPrefab;
        if (prefab == null || prefab.gameObject.scene.IsValid() ||
            !prefab.gameObject.activeSelf || !prefab.enabled || prefab.transform.parent != null)
        {
            Log.Error($"플레이어 생성 실패: {character}의 활성 루트 프리팹을 Project 창에서 연결하세요.");
            return;
        }

        if (FindFirstObjectByType<T_PlayerController>() != null)
        {
            Log.Error("플레이어 생성 실패: 씬의 기존 플레이어를 비활성화하세요. 중복 생성은 허용하지 않습니다.");
            return;
        }

        // SpawnPoint의 자식으로 두지 않아 부모 스케일/회전의 영향을 받지 않습니다.
        SpawnedPlayer = Instantiate(prefab, transform.position, transform.rotation);
        actionInput = SpawnedPlayer.GetComponent<PlayerActionInputHandler>();
        actionInputWasEnabled = actionInput != null && actionInput.enabled;
        SetGameplayInput(false);
        // SW 수정: 기존 Awake/OnEnable 초기화를 마친 뒤 싱글의 상태 참조만 연결합니다.
        PlayerContext context = SpawnedPlayer.GetComponent<PlayerContext>();
        if (context == null) context = SpawnedPlayer.gameObject.AddComponent<PlayerContext>();
        if (!context.BindSinglePlayerInventory(InventoryController.Instance))
            Log.Error("플레이어 상태 연결 실패: 같은 씬의 인벤토리와 필수 상태 컴포넌트를 확인하세요.");

        // Bind inactive HUDs too; they subscribe when enabled.
        if (minimapPing == null)
            minimapPing = FindFirstObjectByType<YJ_MinimapPing>(FindObjectsInactive.Include);
        if (minimapPing != null)
        {
            WBH_PlayerInputHandler input = SpawnedPlayer.GetComponent<WBH_PlayerInputHandler>();
            minimapPing.BindPlayer(SpawnedPlayer.transform, input);
            if (input == null)
                Debug.LogWarning("Minimap ping requires WBH_PlayerInputHandler on the spawned player.", SpawnedPlayer);
        }
        Log.Print($"선택 캐릭터 생성: {character}");

        if (worldItemScanner == null)
            worldItemScanner = FindFirstObjectByType<WorldItemTooltipScanner>();

        if (worldItemScanner != null)
            worldItemScanner.BindPlayer(SpawnedPlayer.transform);
        else
            Log.Warning("WorldItemTooltipScanner가 없어 아이템 획득 연결을 생략합니다.");
    }
}
