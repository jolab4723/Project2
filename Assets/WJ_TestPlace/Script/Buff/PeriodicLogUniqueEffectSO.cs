using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 장착(보유) 중인 동안 일정 간격마다 콘솔에 로그를 출력하는 상시 효과.
    /// 스탯 변화가 아니라 순수 로그 출력용 데모/테스트 효과다.
    /// PassiveBuffUniqueEffectSO와 같은 OnEquip/OnUnequip 상시 효과 패턴을 쓰되,
    /// PlayerBuffManager 대신 임시 GameObject(PeriodicLogRunner)의 InvokeRepeating으로 반복시킨다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/PeriodicLog")]
    public class PeriodicLogUniqueEffectSO : UniqueEffectSO
    {
        [Tooltip("로그를 출력할 간격(초)")]
        public float intervalSeconds = 10f;

        [Tooltip("출력할 로그 메시지")]
        public string message = "HELLO WORLD!";

        private GameObject runnerObject;

        public override void OnEquip(ItemInstance ownerItem)
        {
            if (runnerObject != null)
                return; // 이미 실행 중이면 중복 생성 방지

            runnerObject = new GameObject("[PeriodicLogEffect] " + effectName);
            runnerObject.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(runnerObject);

            PeriodicLogRunner runner = runnerObject.AddComponent<PeriodicLogRunner>();
            runner.Begin(intervalSeconds, message);
        }

        public override void OnUnequip(ItemInstance ownerItem)
        {
            if (runnerObject == null)
                return;

            Object.Destroy(runnerObject);
            runnerObject = null;
        }
    }
}
