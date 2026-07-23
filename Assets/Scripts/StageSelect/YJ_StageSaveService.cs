using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 스테이지 선택 맵 상태를 JSON 파일로 저장하고 다시 불러옵니다.
/// </summary>
[DisallowMultipleComponent]
public class YJ_StageSaveService : MonoBehaviour
{
    private const string DefaultFileName = "stage_map_save.json";

    // 저장 파일에 CLR 타입 정보가 기록되지 않도록 제한하고 컬렉션을 JSON 값으로 교체합니다.
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    [Header("Reference")]
    // 저장하거나 복원할 현재 스테이지 선택 매니저입니다.
    [SerializeField] private YJ_StageSelectManager stageSelectManager;

    [Header("File")]
    // Application.persistentDataPath 아래에 생성할 JSON 파일 이름입니다.
    [SerializeField] private string fileName = DefaultFileName;
    // 사람이 직접 내용을 확인하기 쉽도록 JSON 들여쓰기를 적용할지 결정합니다.
    [SerializeField] private bool prettyPrint = true;

    /// <summary>
    /// json 파일 저장 경로
    /// </summary>
    public string SavePath => Path.Combine(
        Application.persistentDataPath,
        GetSafeFileName());

    /// <summary>
    /// 현재 저장 경로에 JSON 파일이 존재하는지 반환합니다.
    /// </summary>
    public bool HasSaveFile => File.Exists(SavePath);

    /// <summary>
    /// 런타임 시작 시 Inspector 참조가 비어 있으면 같은 씬의 매니저를 찾습니다.
    /// </summary>
    private void Awake()
    {
        FindReferences();
    }

    /// <summary>
    /// Inspector에 잘못된 파일 이름이 입력되지 않도록 기본값과 확장자를 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = DefaultFileName;
        else if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName += ".json";
    }

    /// <summary>
    /// 현재 생성된 맵과 진행 상태를 JSON 파일로 저장합니다.
    /// </summary>
    public bool SaveCurrentMap()
    {
        FindReferences();
        if (stageSelectManager == null)
        {
            Log.Error("YJ_StageSelectManager를 찾을 수 없습니다.");
            return false;
        }

        StageMapSaveData saveData = stageSelectManager.CaptureSaveData();
        if (saveData == null || saveData.nodes == null || saveData.nodes.Count == 0)
        {
            Log.Error("저장할 스테이지 맵이 생성되지 않았습니다.");
            return false;
        }

        return WriteSaveData(saveData);
    }

    /// <summary>
    /// 스테이지 Portal에 도달한 시점에 저장된 pending 노드를 클리어 상태로 변경합니다.
    /// </summary>
    public bool CompletePendingNode()
    {
        if (!TryLoadSaveData(out StageMapSaveData saveData))
            return false;

        if (string.IsNullOrWhiteSpace(saveData.pendingNodeId))
        {
            Log.Warning("완료 처리할 pending 스테이지 노드가 없습니다.");
            return true;
        }

        StageNodeSaveData completedNode = saveData.nodes.Find(
            node => node != null && node.id == saveData.pendingNodeId);
        if (completedNode == null)
        {
            Log.Error(
                $"저장 데이터에서 pending 노드를 찾지 못했습니다: {saveData.pendingNodeId}");
            return false;
        }

        saveData.clearedNodeIds ??= new System.Collections.Generic.List<string>();
        saveData.visitedNodeIds ??= new System.Collections.Generic.List<string>();

        AddUnique(saveData.clearedNodeIds, completedNode.id);
        AddUnique(saveData.visitedNodeIds, completedNode.id);

        saveData.clearedFloor = Math.Max(
            saveData.clearedFloor,
            completedNode.floor);
        saveData.lastClearedNodeId = completedNode.id;
        saveData.pendingNodeId = string.Empty;

        return WriteSaveData(saveData);
    }

    /// <summary>
    /// JSON 저장 데이터 전체를 임시 파일에 먼저 기록한 뒤 실제 저장 파일로 교체합니다.
    /// </summary>
    private bool WriteSaveData(StageMapSaveData saveData)
    {
        if (saveData == null)
        {
            Log.Error("저장할 스테이지 맵 데이터가 없습니다.");
            return false;
        }

        string path = SavePath;
        string temporaryPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Formatting formatting = prettyPrint
                ? Formatting.Indented
                : Formatting.None;
            string json = JsonConvert.SerializeObject(
                saveData,
                formatting,
                SerializerSettings);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Copy(temporaryPath, path, true);
            File.Delete(temporaryPath);

            Log.Print($"스테이지 맵 저장 완료: {path}");
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"스테이지 맵 저장 실패\n{exception}");
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// 문자열 목록에 동일한 ID가 없을 때만 새 ID를 추가합니다.
    /// </summary>
    private static void AddUnique(
        System.Collections.Generic.ICollection<string> ids,
        string id)
    {
        if (!ids.Contains(id))
            ids.Add(id);
    }

    /// <summary>
    /// uGUI Button의 On Click 이벤트에서 현재 맵 저장을 호출합니다.
    /// </summary>
    public void SaveFromButton()
    {
        SaveCurrentMap();
    }

    /// <summary>
    /// JSON 파일을 읽어 현재 씬의 맵 배치와 진행 상태를 교체합니다.
    /// </summary>
    public bool LoadCurrentMap()
    {
        FindReferences();
        if (stageSelectManager == null)
        {
            Log.Error("YJ_StageSelectManager를 찾을 수 없습니다.");
            return false;
        }

        if (!TryLoadSaveData(out StageMapSaveData saveData))
            return false;

        bool restored = stageSelectManager.RestoreMap(saveData);
        if (restored)
            Log.Print($"스테이지 맵 불러오기 완료: {SavePath}");

        return restored;
    }

    /// <summary>
    /// uGUI Button의 On Click 이벤트에서 저장된 맵 불러오기를 호출합니다.
    /// </summary>
    public void LoadFromButton()
    {
        LoadCurrentMap();
    }

    /// <summary>
    /// 저장 파일을 역직렬화하고 유효한 저장 데이터가 있으면 true를 반환합니다.
    /// 자동 불러오기 기능을 구현할 때도 이 메서드를 사용할 수 있습니다.
    /// </summary>
    public bool TryLoadSaveData(out StageMapSaveData saveData)
    {
        saveData = null;
        string path = SavePath;

        if (!File.Exists(path))
        {
            Log.Warning($"스테이지 맵 저장 파일이 없습니다: {path}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            saveData = JsonConvert.DeserializeObject<StageMapSaveData>(
                json,
                SerializerSettings);

            if (saveData == null || saveData.nodes == null || saveData.nodes.Count == 0)
            {
                Log.Error("스테이지 맵 저장 파일에 노드 데이터가 없습니다.");
                saveData = null;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"스테이지 맵 불러오기 실패\n{exception}");
            saveData = null;
            return false;
        }
    }

    /// <summary>
    /// 테스트 또는 새 게임 시작 시 기존 JSON 저장 파일을 삭제합니다.
    /// </summary>
    public bool DeleteSaveFile()
    {
        string path = SavePath;
        if (!File.Exists(path))
        {
            Log.Print($"삭제할 스테이지 맵 저장 파일이 없습니다: {path}");
            return true;
        }

        try
        {
            File.Delete(path);
            Log.Print($"스테이지 맵 저장 파일 삭제 완료: {path}");
            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"스테이지 맵 저장 파일 삭제 실패\n{exception}");
            return false;
        }
    }

    /// <summary>
    /// uGUI Button의 On Click 이벤트에서 현재 JSON 저장 파일을 즉시 삭제합니다.
    /// </summary>
    public void DeleteFromButton()
    {
        DeleteSaveFile();
    }

    /// <summary>
    /// 저장 파일 이름에서 디렉터리 문자를 제거해 persistentDataPath 밖으로 나가지 않게 합니다.
    /// </summary>
    private string GetSafeFileName()
    {
        string safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = DefaultFileName;

        if (!safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            safeName += ".json";

        return safeName;
    }

    /// <summary>
    /// Inspector 참조가 없으면 같은 오브젝트 또는 현재 씬에서 매니저를 찾습니다.
    /// </summary>
    private void FindReferences()
    {
        if (stageSelectManager == null)
            stageSelectManager = GetComponent<YJ_StageSelectManager>();

        if (stageSelectManager == null)
            stageSelectManager = FindFirstObjectByType<YJ_StageSelectManager>();
    }
}
