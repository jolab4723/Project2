using System.Collections.Generic;
using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 같은 고유 효과를 여러 개 보유했을 때 쿨타임을 어떻게 셀지.
    /// </summary>
    public enum DuplicateTriggerPolicy
    {
        /// <summary>
        /// 보유 개수와 무관하게 효과 하나로 취급한다. 한 번 발동하면 나머지 사본도 같이 쿨타임에 들어간다.
        /// 쿨타임 중에 같은 유물을 하나 더 주워도 추가로 발동하지 않는다. (기본값)
        /// </summary>
        ShareCooldown,

        /// <summary>
        /// 아이템 사본마다 쿨타임을 따로 센다. 보유 개수만큼 발동한다.
        /// 실제로 효과가 겹쳐 쌓이려면 buffSpec의 stackBehavior도 Stack이어야 한다
        /// (RefreshDuration이면 두 번째 발동은 지속시간만 갱신한다).
        /// </summary>
        PerItem,
    }

    /// <summary>
    /// 특정 조건을 만족했을 때(OnTrigger 호출 시)만 적용되는 고유 효과.
    /// 보통 지속시간이 있는 임시 버프로 설정해서 쓴다.
    /// 언제 OnTrigger를 부를지(치명타 시, 피격 시 등)는 전투 시스템 쪽 책임.
    ///
    /// 별도 버프 에셋을 참조하지 않고 buffSpec을 직접 들고 있어서, 효과 이름·설명·발동 조건·수치를
    /// 이 에셋 하나에서 전부 설정한다. 버프 식별 키는 이 에셋 자신이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/UniqueEffect/TriggeredBuff")]
    public class TriggeredBuffUniqueEffectSO : UniqueEffectSO, IBuffSource
    {
        [Header("발동 조건")]
        [Tooltip("이 효과가 반응하는 발동 조건. ItemTriggerManager가 이 값과 일치하는 실제 게임 이벤트가 발생했을 때 OnTrigger를 불러준다.")]
        public TriggerCondition triggerCondition = TriggerCondition.None;

        [Tooltip("발동 후 다시 발동할 수 있게 되기까지의 시간(초). 0이면 쿨타임 없음.")]
        public float cooldownSeconds = 0f;

        [Tooltip("같은 고유 효과를 여러 개 보유했을 때의 처리.\n" +
                 "ShareCooldown: 개수와 무관하게 쿨타임을 공유해 한 번만 발동한다.\n" +
                 "PerItem: 아이템마다 쿨타임을 따로 세서 보유 개수만큼 발동한다.")]
        public DuplicateTriggerPolicy duplicatePolicy = DuplicateTriggerPolicy.ShareCooldown;

        [Header("적용할 버프")]
        [Tooltip("조건 만족 시 적용될 효과. 보통 duration을 양수로 둬서 일정 시간만 유지되게 한다.")]
        public BuffSpec buffSpec = new BuffSpec();

        /// <summary>
        /// ShareCooldown일 때의 마지막 발동 시각. 소유 아이템을 알 수 없는 호출(테스트 등)도 여기로 처리한다.
        /// !! 에셋(SO)에 들고 있으므로 이 효과를 여러 캐릭터가 동시에 쓰면 쿨타임을 공유하게 된다.
        ///    지금은 로컬 플레이어 전용이라 문제없지만, 멀티플레이에서 각자 쿨타임을 가져야 하면
        ///    캐릭터별 상태로 옮겨야 한다.
        /// </summary>
        private float sharedLastTriggerTime = float.NegativeInfinity;

        /// <summary>PerItem일 때 아이템 인스턴스별 마지막 발동 시각. 키는 ItemInstance.instanceId.</summary>
        private Dictionary<string, float> lastTriggerTimeByItem;

        /// <summary>이 소유 아이템 기준으로 지금 발동 가능한지(쿨타임이 끝났는지).</summary>
        public bool IsReadyFor(ItemInstance ownerItem) =>
            cooldownSeconds <= 0f || Time.time >= GetLastTriggerTime(ownerItem) + cooldownSeconds;

        /// <summary>이 소유 아이템 기준 남은 쿨타임(초). 발동 가능하면 0.</summary>
        public float GetRemainingCooldown(ItemInstance ownerItem) =>
            IsReadyFor(ownerItem) ? 0f : GetLastTriggerTime(ownerItem) + cooldownSeconds - Time.time;

        public override void OnTrigger(ItemInstance ownerItem)
        {
            if (PlayerBuffManager.Instance == null)
            {
                Debug.LogWarning("[TriggeredBuffUniqueEffectSO] PlayerBuffManager.Instance가 없습니다.");
                return;
            }

            if (!IsReadyFor(ownerItem))
                return; // 쿨타임 중이면 조용히 무시 (매 발동 시도마다 로그가 쌓이지 않게)

            SetLastTriggerTime(ownerItem, Time.time);
            PlayerBuffManager.Instance.ApplyBuff(this);
        }

        /// <summary>
        /// 쿨타임을 아이템별로 나눌 키. PerItem이고 소유 아이템을 알 수 있을 때만 아이템별로 나눈다.
        /// null이면 공유 쿨타임(sharedLastTriggerTime)을 쓴다.
        /// </summary>
        private string GetCooldownKey(ItemInstance ownerItem) =>
            duplicatePolicy == DuplicateTriggerPolicy.PerItem && !string.IsNullOrEmpty(ownerItem?.instanceId)
                ? ownerItem.instanceId
                : null;

        private float GetLastTriggerTime(ItemInstance ownerItem)
        {
            string key = GetCooldownKey(ownerItem);
            float stored = key == null
                ? sharedLastTriggerTime
                : (lastTriggerTimeByItem != null && lastTriggerTimeByItem.TryGetValue(key, out float time) ? time : float.NegativeInfinity);

            // Time.time은 Play 모드를 새로 시작할 때마다 0부터 다시 흐른다. 도메인 리로드 없이 Play를
            // 반복하면(예: Enter Play Mode Options에서 Reload Domain을 끈 경우) OnEnable이 다시 호출되지
            // 않아 이전 Play 세션에서 남은 기록이 그대로 남는다 - 그러면 기록된 시각이 지금 Time.time보다
            // 커지는 모순이 생기므로, 이 경우를 새 세션으로 간주해 기록 없음으로 취급한다.
            return stored <= Time.time ? stored : float.NegativeInfinity;
        }

        private void SetLastTriggerTime(ItemInstance ownerItem, float time)
        {
            string key = GetCooldownKey(ownerItem);
            if (key == null)
            {
                sharedLastTriggerTime = time;
                return;
            }

            lastTriggerTimeByItem ??= new Dictionary<string, float>();
            PruneExpiredCooldowns(time);
            lastTriggerTimeByItem[key] = time;
        }

        /// <summary>
        /// 버려진 아이템의 기록이 계속 쌓이지 않도록, 쿨타임이 이미 끝난 항목을 정리한다.
        /// 끝난 항목은 지워도 판정 결과가 같다(없으면 발동 가능으로 취급).
        /// 항목 수가 보유 개수 수준이라 발동 시점에 훑어도 부담이 없다.
        /// </summary>
        private void PruneExpiredCooldowns(float now)
        {
            List<string> expired = null;
            foreach (KeyValuePair<string, float> pair in lastTriggerTimeByItem)
            {
                if (now >= pair.Value + cooldownSeconds)
                    (expired ??= new List<string>()).Add(pair.Key);
            }

            if (expired == null)
                return;

            foreach (string key in expired)
                lastTriggerTimeByItem.Remove(key);
        }

        /// <summary>
        /// Play 모드를 나가도 SO의 발동 기록이 남아 다음 실행에 영향을 주지 않도록 초기화한다.
        /// (Time.time은 Play 시작마다 0부터라, 초기화하지 않으면 첫 발동이 막힐 수 있다)
        /// </summary>
        private void OnEnable()
        {
            sharedLastTriggerTime = float.NegativeInfinity;
            lastTriggerTimeByItem?.Clear();
        }

        // ----- IBuffSource -----
        public string BuffDisplayName => string.IsNullOrEmpty(effectName) ? name : effectName;
        public Sprite BuffIcon => icon;
        public FixedStatValue[] StatEffects => buffSpec?.statEffects;
        public float Duration => buffSpec != null ? buffSpec.duration : 0f;
        public BuffStackBehavior StackBehavior => buffSpec != null ? buffSpec.stackBehavior : BuffStackBehavior.RefreshDuration;
        public int MaxStack => buffSpec != null ? buffSpec.maxStack : 0;
        public bool IsPermanent => buffSpec == null || buffSpec.IsPermanent;
    }
}
