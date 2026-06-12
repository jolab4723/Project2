using System.Diagnostics;
using UnityEngine;

// 에디터 콘솔에 로그를 기록하는 스크립트 입니다.
// [Conditional("UNITY_EDITOR")] 메서드는 에디터 상태에서만 동작합니다.

public static class Log
{
    [Conditional("UNITY_EDITOR")]
    public static void Print(object message)
    {
        UnityEngine.Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void PrintFormat(string format, params object[] args)
    {
        UnityEngine.Debug.LogFormat(format, args);
    }

    [Conditional("UNITY_EDITOR")]
    public static void Warning(object message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    public static void Error(object message)
    {
        UnityEngine.Debug.LogError(message);
    }
}