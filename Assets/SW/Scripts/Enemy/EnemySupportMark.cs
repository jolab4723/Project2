using ItemSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>SW 수정 : 한 적의 지원 표식 소비권을 서버/싱글에서 원자적으로 소유하며 풀 생애가 끝나면 비운다.</summary>
[DisallowMultipleComponent]
public sealed class EnemySupportMark : MonoBehaviour
{
    private PlayerContext owner;
    private string itemInstanceId;
    private SmileSignalMarkUniqueEffectSO effect;
    private uint sourceAttackId;
    private double expiresAt, readyAt;
    private int sceneHandle;

    public bool IsMarked => owner != null;

    internal bool TryMark(PlayerContext source, ItemInstance item, SmileSignalMarkUniqueEffectSO settings, uint attackId)
    {
        ValidateOwner();
        if (owner != null || source.Effects.Now < readyAt)
            return false;

        owner = source;
        itemInstanceId = item.instanceId;
        effect = settings;
        sourceAttackId = attackId;
        expiresAt = source.Effects.Now + settings.durationSeconds;
        sceneHandle = SceneManager.GetActiveScene().handle;
        SetPresentation(true, settings.durationSeconds);
        return true;
    }

    internal bool TryConsume(PlayerContext consumer, uint attackId, out float damageMultiplier)
    {
        damageMultiplier = 0f;
        ValidateOwner();
        if (owner == null || consumer == null || !consumer.Effects.CanExecute ||
            consumer.Health == null || consumer.Health.CurrentHealth <= 0f || attackId == 0)
            return false;

        if (consumer == owner)
        {
            if (attackId == sourceAttackId)
                return false;

            var session = Mirror.NetworkManager.singleton as MirrorNetworkManager;
            if (session != null)
            {
                foreach (var player in session.ServerPlayerContexts)
                {
                    if (player != null && player != owner && player.isActiveAndEnabled && player.Health != null && player.Health.CurrentHealth > 0f)
                        return false;
                }
            }
        }

        damageMultiplier = effect.damageMultiplier;
        readyAt = consumer.Effects.Now + effect.targetRecoverySeconds;
        Clear(); // 추가 피해보다 먼저 소비권을 비워 동시 도착/재진입을 막는다.
        return true;
    }

    private void Update() => ValidateOwner();

    private void ValidateOwner()
    {
        if (owner == null)
            return;

        if (!owner.isActiveAndEnabled || owner.Health == null || owner.Health.CurrentHealth <= 0f ||
            !owner.Effects.CanExecute || owner.Effects.Now >= expiresAt || SceneManager.GetActiveScene().handle != sceneHandle ||
            owner.Equipment == null || !owner.Equipment.TryGetEquippedItemInstance(EquipSlotType.Weapon, out var item) ||
            item == null || item.instanceId != itemInstanceId || item.definition.uniqueEffect != effect ||
            GetComponent<WBH_EnemyController>()?.Status?.IsDead != false)
            Clear();
    }

    internal void Clear()
    {
        var previous = owner;
        owner = null;
        effect = null;
        itemInstanceId = null;
        previous?.Effects.ForgetSupportMark(this);
        SetPresentation(false, 0f);
    }

    private void SetPresentation(bool active, float seconds)
    {
        var network = GetComponent<NetworkEnemyAuthority>();
        if (network != null && network.IsServerDamageHandlingActive)
            network.ServerSetSupportMark(active, seconds);
        else if (!Mirror.NetworkServer.active && !Mirror.NetworkClient.active)
            (GetComponent<EnemyEffectIndicator>() ?? gameObject.AddComponent<EnemyEffectIndicator>()).SetSupportMark(active, seconds);
    }

    private void OnDisable()
    {
        Clear();
        readyAt = 0d;
    }
}
