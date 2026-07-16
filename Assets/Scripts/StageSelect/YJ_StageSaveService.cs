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
            Debug.LogError("YJ_StageSelectManager was not found.", this);
            return false;
        }

        StageMapSaveData saveData = stageSelectManager.CaptureSaveData();
        if (saveData == null || saveData.nodes == null || saveData.nodes.Count == 0)
        {
            Debug.LogError("There is no generated stage map to save.", this);
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

            Debug.Log($"Stage map saved: {path}", this);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save stage map.\n{exception}", this);
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
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
            Debug.LogError("YJ_StageSelectManager was not found.", this);
            return false;
        }

        if (!TryLoadSaveData(out StageMapSaveData saveData))
            return false;

        bool restored = stageSelectManager.RestoreMap(saveData);
        if (restored)
            Debug.Log($"Stage map loaded: {SavePath}", this);

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
            Debug.LogWarning($"Stage map save file does not exist: {path}", this);
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
                Debug.LogError("The stage map save file contains no node data.", this);
                saveData = null;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to load stage map.\n{exception}", this);
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
            Debug.Log($"No stage map save file to delete: {path}", this);
            return true;
        }

        try
        {
            File.Delete(path);
            Debug.Log($"Stage map save deleted: {path}", this);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to delete stage map save.\n{exception}", this);
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
