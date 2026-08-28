using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public sealed class MirrorTestDisplaySettings : MonoBehaviour
{
    private const string MirrorCombatScenePath =
        "Assets/SW/TEST/MirrorCombat/Scenes/Act1_Stage1_MirrorCombatTest.unity";

    private static readonly int[] QualityLevelMap = { 1, 3, 5 };

    private int lastUiQuality = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeForMirrorCombatTest()
    {
        if (SceneManager.GetActiveScene().path != MirrorCombatScenePath)
            return;

        Debug.Assert(
            GetResolutionIndex(2560, 1440) == 2 && GetResolutionIndex(3440, 1440) == -1,
            "[MirrorTestDisplaySettings] 해상도 인덱스 매핑이 변경되었습니다.");

        var gameObject = new GameObject(nameof(MirrorTestDisplaySettings));
        gameObject.AddComponent<MirrorTestDisplaySettings>();
    }

    private IEnumerator Start()
    {
        ApplyNativeFullscreenResolution();
        ApplyMappedQuality(force: true);

        yield return null;

        Debug.Log(
            $"[MirrorTestDisplaySettings] 시작 화면 설정 적용: " +
            $"{Screen.width}x{Screen.height}, Quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
    }

    private void LateUpdate()
    {
        ApplyMappedQuality(force: false);
    }

    private static void ApplyNativeFullscreenResolution()
    {
#if UNITY_EDITOR
        return;
#else
        var settingsManager = SettingManager.Instance;
        if (settingsManager == null)
        {
            var current = Screen.currentResolution;
            Screen.SetResolution(current.width, current.height, FullScreenMode.FullScreenWindow, current.refreshRateRatio);
            return;
        }

        var settings = settingsManager.GetData();
        if (!settings.isFullscreen)
            return;

        var native = Screen.currentResolution;
        int nativeIndex = GetResolutionIndex(native.width, native.height);
        if (nativeIndex >= 0)
        {
            settings.resolutionIndex = nativeIndex;
            settingsManager.Apply(settings);
            return;
        }

        Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow, native.refreshRateRatio);
#endif
    }

    private static int GetResolutionIndex(int width, int height)
    {
        if (width == 1280 && height == 720) return 0;
        if (width == 1920 && height == 1080) return 1;
        if (width == 2560 && height == 1440) return 2;
        if (width == 3840 && height == 2160) return 3;
        return -1;
    }

    private void ApplyMappedQuality(bool force)
    {
        var settingsManager = SettingManager.Instance;
        if (settingsManager == null)
            return;

        int uiQuality = Mathf.Clamp(
            settingsManager.GetData().graphicsQuality,
            0,
            QualityLevelMap.Length - 1);
        int qualityLevel = Mathf.Min(QualityLevelMap[uiQuality], QualitySettings.names.Length - 1);

        if (!force && lastUiQuality == uiQuality && QualitySettings.GetQualityLevel() == qualityLevel)
            return;

        lastUiQuality = uiQuality;
        QualitySettings.SetQualityLevel(qualityLevel, true);
    }
}
