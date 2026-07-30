using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// PeriodicLogUniqueEffectSO 전용 실행기. ScriptableObject는 InvokeRepeating/코루틴을 직접 못 돌리므로,
    /// OnEquip 시점에 이 컴포넌트를 담은 임시 GameObject를 하나 만들어서 대신 반복시킨다.
    /// </summary>
    internal class PeriodicLogRunner : MonoBehaviour
    {
        private string message;

        public void Begin(float intervalSeconds, string logMessage)
        {
            message = logMessage;
            InvokeRepeating(nameof(LogMessage), intervalSeconds, intervalSeconds);
        }

        private void LogMessage()
        {
            Debug.Log(message);
        }
    }
}
