using UnityEngine;
using Core;

public class YJ_StageManager : MonoBehaviour
{
    private DataManager dataManager;
    [SerializeField] private bool bootScene = false;
    public bool isBossStage = false;

    private void Start()
    {
        if (bootScene)
        {
            Initialize();
            return;
        }

        StartScene();
    }

    public void Initialize()
    {
        if ( ! TryGetDataManager())
            return;

        dataManager.ResetGameplayData();
        Log.Print("ResetGameplayData");
    }

    public void StartScene()
    {
        if ( ! TryGetDataManager())
            return;

        dataManager.LoadGameplayData();
    }

    public void EndScene()
    {
        if ( ! TryGetDataManager())
            return;

        dataManager.SaveGameplayData();
    }

    private bool TryGetDataManager()
    {
        if (dataManager == null)
            dataManager = DataManager.Instance;

        if (dataManager != null)
            return true;

        Log.Error("DataManager를 찾을 수 없습니다.");
        return false;
    }
}