using Core;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class YJ_LoadingSceneProgress : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text progressText;

    [Header("Presentation")]
    [SerializeField] private string progressLabel = "Progress";

    private SceneLoader sceneLoader;

    private void OnEnable()
    {
        ResolveProgressText();
        SetProgress(0f);
        SubscribeToSceneLoader();
    }

    private void Start()
    {
        // 실행 순서상 SceneLoader가 OnEnable 이후 준비되는 경우를 보완합니다.
        SubscribeToSceneLoader();
    }

    private void OnDisable()
    {
        UnsubscribeFromSceneLoader();
    }

    private void SubscribeToSceneLoader()
    {
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || sceneLoader == loader)
            return;

        UnsubscribeFromSceneLoader();
        sceneLoader = loader;
        sceneLoader.OnLoadProgress += SetProgress;
    }

    private void UnsubscribeFromSceneLoader()
    {
        if (sceneLoader == null)
            return;

        sceneLoader.OnLoadProgress -= SetProgress;
        sceneLoader = null;
    }

    private void SetProgress(float progress)
    {
        if (progressText == null)
            return;

        int percentage = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
        progressText.text = string.IsNullOrWhiteSpace(progressLabel)
            ? $"{percentage}%"
            : $"{progressLabel} {percentage}%";
    }

    private void ResolveProgressText()
    {
        if (progressText != null)
            return;

        GameObject loadingBar = GameObject.Find("LoadingBar");
        if (loadingBar != null)
            progressText = loadingBar.GetComponentInChildren<TMP_Text>(true);

        if (progressText == null)
            Debug.LogError("[YJ_LoadingSceneProgress] 진행률을 표시할 TMP_Text를 찾을 수 없습니다.", this);
    }
}
