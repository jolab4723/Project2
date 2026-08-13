using System;
using System.Collections.Generic;

namespace ItemSystem
{
    /// <summary>
    /// PlayerBuffManager가 원래 직접 들고 있던 "버프 목록 적용/제거/틱/스탯 합산" 로직을 재사용
    /// 가능한 순수 C# 클래스로 뽑아낸 것. PlayerBuffManager는 이 클래스를 내부에 들고 쓰면서
    /// 버프가 바뀔 때마다 PlayerStatManager.Recalculate()를 추가로 호출하고, 허수아비처럼
    /// PlayerStatManager가 없는 대상은 DummyBuffManager처럼 이 클래스를 직접 들고 쓰면 된다.
    ///
    /// MonoBehaviour가 아니라서 Update()를 직접 안 받는다 - 소유자(MonoBehaviour)가 매 프레임
    /// Tick(Time.deltaTime)을 불러줘야 한다.
    /// </summary>
    public class BuffTracker
    {
        private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>();

        public IReadOnlyList<BuffInstance> ActiveBuffs => activeBuffs;

        /// <summary>버프가 추가/제거돼서 목록 구성 자체가 바뀔 때 발행(스택/지속시간만 갱신되는 경우는 발행 안 함).</summary>
        public event Action OnBuffsChanged;

        /// <summary>매 프레임 소유자가 호출. 만료된 버프가 있어서 재계산이 필요했으면 true.</summary>
        public bool Tick(float deltaTime)
        {
            if (activeBuffs.Count == 0)
                return false;

            bool anyExpired = false;

            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = activeBuffs[i];
                if (buff.source == null || buff.source.IsPermanent)
                    continue;

                buff.remainingTime -= deltaTime;
                if (buff.remainingTime <= 0f)
                {
                    activeBuffs.RemoveAt(i);
                    anyExpired = true;
                }
            }

            if (anyExpired)
                OnBuffsChanged?.Invoke();

            return anyExpired;
        }

        /// <summary>
        /// 버프를 적용한다. 이미 같은 버프가 있으면 source.StackBehavior에 따라
        /// 지속시간 갱신(RefreshDuration) / 스택 증가(Stack) / 무시(Ignore) 중 하나로 처리한다.
        /// 반환값은 소유자가 재계산(예: PlayerStatManager.Recalculate)이 필요한지 여부 -
        /// Ignore로 아무 것도 안 바뀐 경우는 false.
        /// </summary>
        public bool ApplyBuff(IBuffSource source)
        {
            if (source == null)
            {
                UnityEngine.Debug.LogWarning("[BuffTracker] source가 null입니다.");
                return false;
            }

            var existing = activeBuffs.Find(b => ReferenceEquals(b.source, source));

            if (existing != null)
            {
                switch (source.StackBehavior)
                {
                    case BuffStackBehavior.RefreshDuration:
                        existing.remainingTime = source.Duration;
                        break;

                    case BuffStackBehavior.Stack:
                        if (source.MaxStack <= 0 || existing.stackCount < source.MaxStack)
                            existing.stackCount++;
                        existing.remainingTime = source.Duration;
                        break;

                    case BuffStackBehavior.Ignore:
                        return false;
                }
            }
            else
            {
                activeBuffs.Add(new BuffInstance(source));
                OnBuffsChanged?.Invoke();
            }

            return true;
        }

        /// <summary>해당 버프를 스택 상관없이 완전히 제거한다. 반환값은 실제로 제거된 게 있었는지.</summary>
        public bool RemoveBuff(IBuffSource source)
        {
            if (source == null)
                return false;

            int removed = activeBuffs.RemoveAll(b => ReferenceEquals(b.source, source));

            if (removed > 0)
                OnBuffsChanged?.Invoke();

            return removed > 0;
        }

        /// <summary>모든 버프를 제거한다. 반환값은 실제로 지울 게 있었는지.</summary>
        public bool ClearAllBuffs()
        {
            if (activeBuffs.Count == 0)
                return false;

            activeBuffs.Clear();
            OnBuffsChanged?.Invoke();
            return true;
        }

        public StatSet GetStatSet()
        {
            StatSet total = StatSet.Zero;

            foreach (var buff in activeBuffs)
            {
                if (buff?.source?.StatEffects == null)
                    continue;

                foreach (var effect in buff.source.StatEffects)
                    StatSetMapper.AddStat(ref total, effect.statType, effect.value * buff.stackCount);
            }

            return total;
        }
    }
}
