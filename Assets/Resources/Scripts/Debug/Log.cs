using System.Diagnostics;
using UnityEngine;

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