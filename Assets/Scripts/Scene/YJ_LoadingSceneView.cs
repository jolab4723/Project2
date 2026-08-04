using Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YJ_LoadingSceneView : MonoBehaviour
{
    private const string LoadingSceneName = "LoadingScene";

    [Header("References")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;

    [Header("Presentation")]
    [SerializeField] private string progressLabel = "NOW LOADING...";

    private SceneLoader sceneLoader;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name != LoadingSceneName)
            return;

        if (FindFirstObjectByType<YJ_LoadingSceneView>() != null)
            return;

        GameObject canvasObject = GameObject.Find("Canvas_Loading");
        if (canvasObject == null)
        {
            Log.Error("[LoadingScene] Canvas_Loading 오브젝트를 찾을 수 없습니다.");
            return;
        }

        canvasObject.AddComponent<YJ_LoadingSceneView>();
    }

    private void OnEnable()
    {
        ResolveProgressText();
        SetProgress(0f);

        sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
            return;

        sceneLoader.OnLoadProgress += SetProgress;
    }

    private void OnDisable()
    {
        if (sceneLoader != null)
            sceneLoader.OnLoadProgress -= SetProgress;

        sceneLoader = null;
    }

    private void SetProgress(float progress)
    {
        float normalizedProgress = Mathf.Clamp01(progress);

        if (progressFill != null)
            progressFill.fillAmount = normalizedProgress;

        if (progressText != null)
            progressText.text = $"{progressLabel} {Mathf.RoundToInt(normalizedProgress * 100f)}%";
    }

    private void ResolveProgressText()
    {
        if (progressText != null)
            return;

        Transform progressTextTransform = transform.Find("LoadingBar/Text (TMP)");
        if (progressTextTransform == null)
        {
            Log.Error("[LoadingScene] LoadingBar/Text (TMP) 오브젝트를 찾을 수 없습니다.");
            return;
        }

        progressText = progressTextTransform.GetComponent<TMP_Text>();
        if (progressText == null)
            Log.Error("[LoadingScene] LoadingBar/Text (TMP)에 TMP_Text 컴포넌트가 없습니다.");
    }
}
