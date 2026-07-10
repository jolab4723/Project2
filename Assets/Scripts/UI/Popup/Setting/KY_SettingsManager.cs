using UnityEngine;
using System.IO;

public class KY_SettingsManager : MonoBehaviour
{
    public static KY_SettingsManager Instance;

    private KY_SettingsData currentData = new KY_SettingsData();
    private string savePath;

    private readonly int[] frameRates = { 30, 60, -1 }; // -1은 무제한

    private readonly (int width, int height)[] resolutions =
    {
        (1280, 720),
        (1920, 1080),
        (2560, 1440),
        (3840, 2160)
    };

    void Awake()
    {
        Instance = this;
        savePath = Application.persistentDataPath + "/settings.json";
        Load();
    }

    // 데이터 반환
    public KY_SettingsData GetData() => currentData;

    // 적용
    public void Apply(KY_SettingsData data)
    {
        currentData = data;
        ApplyDisplay();
        ApplyAudio();
        ApplyGameplay();
        Save();
    }

    void ApplyDisplay()
    {
        var res = resolutions[currentData.resolutionIndex];
        Screen.SetResolution(res.width, res.height, currentData.isFullscreen);
        QualitySettings.SetQualityLevel(currentData.graphicsQuality);
        Application.targetFrameRate = frameRates[currentData.targetFrameRate];
    }

    void ApplyAudio()
    {
        AudioListener.volume = currentData.isMuted ? 0f : currentData.masterVolume;
    }

    void ApplyGameplay()
    {
        // 나중에 연결
    }

    void Save()
    {
        string json = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(savePath, json);
    }

    void Load()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currentData = JsonUtility.FromJson<KY_SettingsData>(json);
        }
        Apply(currentData);
    }
}