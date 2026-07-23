using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;

// 이 스크립트는 "Assets/Scenes/Maps/Unknown_Maps/Maps/"폴더 내의 씬 중 랜덤하게 1개의 씬을 불러오는 스크립트입니다.
// 중요) File > Build Profiles > Scene List에 불러오려는 씬을 등록해야만 정상 동작합니다.

public class YJ_UnknownStageManager : MonoBehaviour
{
    private const string UnknownSceneFolder = "Assets/Scenes/Maps/Unknown_Maps/Maps/";
    private List<string> scenePaths = new();

    private IEnumerator Start()
    {
        // GameManager.Start에서 SceneLoader를 활성화한 다음 프레임에 전환을 요청합니다.
        yield return null;

        RefreshSceneList();
        LoadRandomScene();
    }

    private void RefreshSceneList()
    {
        scenePaths.Clear();

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i).Replace('\\', '/');

            if (path.StartsWith(UnknownSceneFolder, StringComparison.OrdinalIgnoreCase))
            {
                scenePaths.Add(path);
            }
        }
    }

    public void LoadRandomScene()
    {
        if (scenePaths.Count == 0)
        {
            Log.Warning("등록된 Unknown씬이 없습니다.");
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, scenePaths.Count);
        string scenePath = scenePaths[randomIndex];
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);

        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
        {
            Log.Error("SceneLoader를 찾을 수 없습니다.");
            return;
        }

        sceneLoader.LoadScene(sceneName);
    }
}
