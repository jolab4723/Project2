using System;
using UnityEngine;

/// <summary>
/// 적이 죽으면 WBH_EnemyInfo.exp만큼 플레이어에게 경험치를 지급한다.
/// WBHEnemyItemDropAdapter(Assets/SW/Scripts/Enemy/Drop)와 같은 방식으로 적의 기존 컴포넌트는
/// 전혀 수정하지 않고 WBH_EnemyStatus.OnDead(공개 이벤트)만 구독한다.
/// 풀에서 재사용되는 적이라도 OnEnable/OnDisable에서 매번 구독/해제하므로 중복 지급 걱정은 없다.
/// 8/21 WBH 수정. OnDead 이벤트 대신 OnDamaged 구독을 통해 피해를 받았을 때 hp 가 0이 되는지 검사. (사망이벤트 구독 시, 풀 반환으로 크레딧 텍스트 비활성화 우려.)
/// 로컬 환경에서는 SpawnManager 부터 Initialize()를 통해 주입받은 단일 PlayerWallet 에 크레딧 지급.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WBH_EnemyStatus))]
[RequireComponent(typeof(WBH_EnemyController))]
public sealed class EnemyKillReward : MonoBehaviour
{
    private WBH_EnemyStatus status;
    private WBH_EnemyController controller;
    private PlayerWallet wallet;

    private bool hasGrantedReward;

    public event Action<int> OnCreditGranted; // WBH_EnemyView 에서 크레딧 텍스트 표시에 사용.

    private void Awake()
    {
        status = GetComponent<WBH_EnemyStatus>();
        controller = GetComponent<WBH_EnemyController>();
    }

    private void OnEnable()
    {
        hasGrantedReward = false;
        status.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (status != null)
            status.OnDamaged -= HandleDamaged;
    }

    public void Initialize(PlayerWallet wallet)
    {
        this.wallet = wallet;
    }

    private void HandleDamaged(WBH_DamageResult result)
    {
        if (hasGrantedReward || status.CurrentHp > 0f)
            return;

        WBH_EnemyInfo enemyInfo = controller.Info;

        if(enemyInfo == null)
        {
            Log.Warning($"[EnemyKillExpReward] 적 정보가 없어 보상 지급에 실패하였습니다. {this}");
            return;
        }

        hasGrantedReward = true;
        KY_RunStatsTracker.Instance?.RecordEnemyDefeated(); // 결과창 데이터 집계용으로 추가

        GrantExp(enemyInfo.exp);
        GrantCredit(enemyInfo.credit);
    }

    private void GrantExp(int amount)
    {
        if (amount <= 0)
            return;

        PlayerStatManager.Instance?.GainExp(controller.Info.exp);
    }

    /// <summary>
    /// !! 지갑은 스포너가 Initialize로 주입한다(WBH_EnemySpawner). 주입이 빠진 적이 죽으면 예전엔 여기서
    ///    NullReferenceException이 났는데, 이 호출이 WBH_EnemyStatus.OnDamaged 구독 체인 한가운데라
    ///    예외가 나면 뒤에 등록된 구독자들의 사망 처리까지 통째로 끊겼다(실제로 수동 생성한 적에서 발생).
    ///    보상만 건너뛰고 나머지 흐름은 살리도록 경고만 남기고 빠진다.
    /// </summary>
    private void GrantCredit(int amount)
    {
        if (amount <= 0)
            return;

        if (wallet == null)
        {
            Log.Warning($"[EnemyKillReward] 지갑이 연결되지 않아 크레딧 {amount} 지급을 건너뜁니다. {name}");
            return;
        }

        wallet.AddGold(amount);
        OnCreditGranted?.Invoke(amount);
    }
}
