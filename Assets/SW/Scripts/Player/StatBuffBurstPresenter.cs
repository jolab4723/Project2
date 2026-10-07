using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// 아이템 시한·조건부 능력치 버프가 켜지거나 스택이 오르는 순간 스탯 카테고리별 발동 연출(UEVFX_StatBuff_{카테고리})을 한 번 재생한다.
/// 버프가 유지되는 동안의 표시는 HUD 아이콘에 맡기고 월드에는 아무것도 남기지 않는다.
/// 버프 목록은 싱글 로컬 판정·멀티 서버 스냅샷이 채운 PlayerBuffManager가 원본이며, 표시 쪽에서 발동 조건을 다시 판정하지 않는다.
/// </summary>
internal sealed class StatBuffBurstPresenter
{
    private const string PrefabPrefix = "UEVFX_StatBuff_";
    private const float BurstLifetime = 1.2f;          // 아이콘이 떠올라 사라지는 시간보다 조금 길게
    private const float BurstInterval = 0.8f;          // 같은 카테고리의 발동 연출 최소 간격
    private const float SameFrameStagger = 0.12f;      // 한 번에 여러 버프가 켜지면 순서대로 띄우고
    private const float SameFrameSpacing = 0.55f;      // 아이콘을 좌우로 벌려 같은 자리에 겹치지 않게 한다
    private const float SuppressBurstsSeconds = 1f;    // 접속·씬 진입 직후 이미 걸려 있던 버프는 연출을 생략
    private const float RemoteIntensity = 0.55f;       // 다른 플레이어 연출은 본인보다 옅게
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private Dictionary<IBuffSource, int> previousStacks = new();
    private Dictionary<IBuffSource, int> currentStacks = new();
    private readonly Dictionary<string, float> nextBurstAt = new();
    private readonly List<(string category, float playAt, float offset)> pending = new();
    private readonly List<(GameObject instance, float endsAt)> playing = new();
    private float suppressBurstsUntil = float.NegativeInfinity;
    private bool started;

    /// <summary>재생 대기 중이거나 재생 중인 연출 수(검증·디버그용).</summary>
    public int ActiveBurstCount => pending.Count + playing.Count;

    /// <summary>
    /// 버프 목록을 프레임마다 이전 상태와 비교한다. 같은 버프의 스택 증가는 OnBuffsChanged가 오지 않으므로 이벤트 대신 폴링하며,
    /// 멀티 클라이언트가 스냅샷마다 목록을 다시 채워도 같은 SO 참조라 새 버프로 오인하지 않는다.
    /// </summary>
    public void Refresh(Transform owner, IReadOnlyList<BuffInstance> buffs, bool visible, bool remote)
    {
        float now = Time.time;
        if (!started)
        {
            started = true;
            suppressBurstsUntil = now + SuppressBurstsSeconds;
        }

        currentStacks.Clear();
        int queuedThisFrame = 0;
        if (buffs != null)
        {
            foreach (BuffInstance buff in buffs)
            {
                if (buff == null || !TryGetCategory(buff.source, out string category)) continue;
                int stack = Mathf.Max(1, buff.stackCount);
                currentStacks[buff.source] = stack;
                bool fresh = !previousStacks.TryGetValue(buff.source, out int previous) || stack > previous;
                if (!fresh || !visible || now < suppressBurstsUntil) continue;
                if (nextBurstAt.TryGetValue(category, out float readyAt) && now < readyAt) continue;
                nextBurstAt[category] = now + BurstInterval;
                // 0, 왼쪽, 오른쪽, 더 왼쪽... 순서로 화면 가로(월드 X) 방향에 자리를 나눈다.
                float offset = (queuedThisFrame + 1) / 2 * SameFrameSpacing * (queuedThisFrame % 2 == 1 ? -1f : 1f);
                pending.Add((category, now + queuedThisFrame * SameFrameStagger, offset));
                queuedThisFrame++;
            }
        }
        (previousStacks, currentStacks) = (currentStacks, previousStacks);

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (now < pending[i].playAt) continue;
            var (category, _, offset) = pending[i];
            pending.RemoveAt(i);
            if (visible && UniqueEffectPresentation.TryGetVfx(PrefabPrefix + category, out GameObject prefab))
                playing.Add((Spawn(owner, prefab, remote, offset), now + BurstLifetime));
        }
        for (int i = playing.Count - 1; i >= 0; i--)
        {
            if (playing[i].instance != null && now < playing[i].endsAt) continue;
            DestroyInstance(playing[i].instance);
            playing.RemoveAt(i);
        }
    }

    /// <summary>재생 중인 연출을 제거하고 다음 표시를 처음 상태(이미 걸린 버프는 연출 생략)로 되돌린다.</summary>
    public void Clear()
    {
        foreach (var entry in playing)
            DestroyInstance(entry.instance);
        playing.Clear();
        pending.Clear();
        previousStacks.Clear();
        currentStacks.Clear();
        nextBurstAt.Clear();
        started = false;
    }

    /// <summary>
    /// 아이템 시한·조건부 버프만 대상으로 하고, 첫 번째 상승 스탯을 대표 카테고리로 쓴다.
    /// 처치마다 영구로 쌓이는 누적 버프(고철 압축기 등)와 범위 오라·장비 상시 버프·포션·디버프·스냅샷 대체 소스는 제외한다.
    /// </summary>
    internal static bool TryGetCategory(IBuffSource source, out string category)
    {
        category = null;
        bool timed = source is TriggeredBuffUniqueEffectSO && !source.IsPermanent;
        if (!timed && source is not StatThresholdBuffUniqueEffectSO) return false;
        if (source.DisplayKind == BuffDisplayKind.Debuff) return false;
        FixedStatValue[] effects = source.StatEffects;
        if (effects == null) return false;
        foreach (FixedStatValue effect in effects)
        {
            if (effect == null || effect.value <= 0f) continue;
            category = effect.statType switch
            {
                StatType.attackPowerFlat or StatType.attackPowerPercent or StatType.penetrationFlat or
                    StatType.normalDamagePercent or StatType.skillDamagePercent => "Attack",
                StatType.attackSpeedFlat or StatType.attackSpeedPercent => "AttackSpeed",
                StatType.defensePowerFlat or StatType.defensePowerPercent or StatType.healthFlat or StatType.healthPercent => "Defense",
                StatType.moveSpeedFlat or StatType.moveSpeedPercent => "MoveSpeed",
                StatType.critRateFlat or StatType.critMultFlat => "Crit",
                StatType.cdrFlat => "Cooldown",
                StatType.mpRegenFlat or StatType.mpRegenPercent or StatType.mpMaxFlat => "ManaRegen",
                StatType.fireBonusFlat => "Fire",
                StatType.iceBonusFlat => "Ice",
                StatType.electricBonusFlat => "Electric",
                _ => null,
            };
            if (category != null) return true;
        }
        return false;
    }

    private static GameObject Spawn(Transform owner, GameObject prefab, bool remote, float iconOffset)
    {
        GameObject instance = Object.Instantiate(prefab, owner, false);
        instance.name = prefab.name;
        instance.hideFlags = HideFlags.DontSave;
        Transform icon = instance.transform.Find("IconPop");
        if (icon != null && iconOffset != 0f) icon.position += Vector3.right * iconOffset;
        if (!remote) return instance;
        var block = new MaterialPropertyBlock();
        foreach (var renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null || !material.HasProperty(TintColorId)) continue;
            renderer.GetPropertyBlock(block);
            block.SetColor(TintColorId, material.GetColor(TintColorId) * RemoteIntensity);
            renderer.SetPropertyBlock(block);
        }
        return instance;
    }

    private static void DestroyInstance(GameObject instance)
    {
        if (instance == null) return;
        if (Application.isPlaying) Object.Destroy(instance);
        else Object.DestroyImmediate(instance);
    }
}
