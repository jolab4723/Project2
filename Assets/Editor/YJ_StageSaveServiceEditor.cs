using UnityEditor;
using UnityEngine;

/// <summary>
/// YJ_StageSaveService의 JSON 저장과 불러오기를 Play Mode에서 바로 시험할 버튼을 제공합니다.
/// </summary>
[CustomEditor(typeof(YJ_StageSaveService))]
public class YJ_StageSaveServiceEditor : Editor
{
    /// <summary>
    /// 기본 Inspector 아래에 저장, 불러오기, 삭제 테스트 UI를 표시합니다.
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        YJ_StageSaveService saveService = (YJ_StageSaveService)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("JSON Test", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(
            saveService.SavePath,
            EditorStyles.textField,
            GUILayout.Height(EditorGUIUtility.singleLineHeight));

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Save와 Load 테스트는 Play Mode에서 실행하세요.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save JSON"))
                saveService.SaveCurrentMap();

            if (GUILayout.Button("Load JSON"))
                saveService.LoadCurrentMap();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Show Save File"))
        {
            string revealPath = saveService.HasSaveFile
                ? saveService.SavePath
                : Application.persistentDataPath;
            EditorUtility.RevealInFinder(revealPath);
        }

        using (new EditorGUI.DisabledScope(!saveService.HasSaveFile))
        {
            if (GUILayout.Button("Delete JSON") &&
                EditorUtility.DisplayDialog(
                    "Delete Stage Map Save",
                    "저장된 Stage Map JSON 파일을 삭제할까요?",
                    "Delete",
                    "Cancel"))
            {
                saveService.DeleteSaveFile();
            }
        }

        EditorGUILayout.EndHorizontal();
    }
}
