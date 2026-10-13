using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 6000.3 내장 Multiplayer Play Mode의 초기화 누락을 보정한다.
/// 도메인 리로드 뒤 VirtualProjectWorkflow가 EditorContexts 초기화 이벤트를 이미 지나간 뒤에 구독해
/// 워크플로가 초기화되지 않고, Play 진입마다 ScenarioConfig.SendEnterPlayModeOnTagsAppliedEvent가
/// null인 MultiplayerPlaymode.Players를 읽어 NullReferenceException을 낸다.
/// 컨텍스트는 준비됐는데 워크플로만 미초기화인 경우에만 모듈의 초기화 함수를 한 번 다시 호출한다.
/// ponytail: 내부 API를 리플렉션으로 호출한다. Unity가 이 초기화 순서를 고치면 이 파일을 삭제한다.
/// </summary>
[InitializeOnLoad]
internal static class MppmWorkflowInitializationFix
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    // 리로드 직후에는 EditorContexts도 아직 초기화 전일 수 있어 준비될 때까지 몇 프레임 재시도한다.
    private const int MaxAttemptFrames = 600;
    private static int remainingFrames;

    static MppmWorkflowInitializationFix()
    {
        remainingFrames = MaxAttemptFrames;
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        if (TryInitialize() || --remainingFrames <= 0)
            EditorApplication.update -= Update;
    }

    /// <summary>보정이 끝났거나 더 할 일이 없으면 true, 컨텍스트 준비를 더 기다려야 하면 false.</summary>
    private static bool TryInitialize()
    {
        try
        {
            Assembly module = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "UnityEditor.MultiplayerModule")
                {
                    module = assembly;
                    break;
                }
            }

            Type workflow = module?.GetType("Unity.Multiplayer.PlayMode.Editor.VirtualProjectWorkflow");
            Type contexts = module?.GetType("Unity.Multiplayer.PlayMode.Editor.EditorContexts");
            Type migration = module?.GetType("Unity.Multiplayer.PlayMode.Editor.MigrationUtility");
            if (workflow == null || contexts == null || migration == null ||
                migration.GetMethod("ShouldDisableMultiplayerPlayMode", StaticMembers)?.Invoke(null, null) is true ||
                workflow.GetProperty("IsInitialized", StaticMembers)?.GetValue(null) is not false)
            {
                return true;
            }

            if (contexts.GetProperty("IsInitialized", StaticMembers)?.GetValue(null) is not true)
                return false;

            workflow.GetMethod("InitializeMPPMContexts", StaticMembers)?.Invoke(null, null);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[MppmWorkflowInitializationFix] Multiplayer Play Mode 초기화 보정에 실패했습니다: {(exception.InnerException ?? exception).Message}");
        }

        return true;
    }
}
