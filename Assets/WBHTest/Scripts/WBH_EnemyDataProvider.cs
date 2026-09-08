using DataSystem;
using EnemySystem;
using System;
using UnityEngine;

[DisallowMultipleComponent] // 중복금지 속성
public class WBH_EnemyDataProvider : MonoBehaviour
{
    private enum EnemyInfoSource
    {
        GeneratedData,
        TestOverride
    }

    [Serializable]
    private sealed class TestEnemyInfoEntry
    {
        [SerializeField] private EnemyDefinitionSO testDef;
        [SerializeField] private WBH_EnemyInfo info = new WBH_EnemyInfo();

        public string EnemyId => testDef != null ? testDef.enemyId : string.Empty;

        public EnemyDefinitionSO TestDef => testDef;
        public WBH_EnemyInfo Info => info;
    }

    [Header("Data Source")]
    [SerializeField] private EnemyInfoSource infoSource = EnemyInfoSource.GeneratedData;

    [Header("Generated Enemy Data")]
    [SerializeField] private EnemyDatabaseSO enemyDatabase;

    [Header("Test Override")]
    [SerializeField] private TestEnemyInfoEntry[] testInfos;

    public bool TryCreateEnemyInfo(string enemyId, WBH_EnemyStatContext context, out WBH_EnemyInfo result)
    {
        result = null;
        if(string.IsNullOrWhiteSpace(enemyId))
        {
            Log.Error("enemyId 가 비어 있습니다");
            return false;
        }

        if (infoSource == EnemyInfoSource.TestOverride)
        {
            return TryCreateTestInfo(enemyId, out result);
        }
            return TryCreateGeneratedInfo(enemyId, context, out result);
    }

    // 정식 경로로 생성되는 EnemyInfo
    private bool TryCreateGeneratedInfo(string enemyId, WBH_EnemyStatContext context, out WBH_EnemyInfo result)
    {
        result = null;

        // 예외 처리 및 오류메세지
        if(enemyDatabase == null)
        {
            Log.Error("EnemyDatabaseSO 가 지정되지 않았습니다.");
            return false;
        }

        if(!context.IsValid)
        {
            Log.Error($"잘못된 적 능력치 컨텍스트입니다. 층 = {context.floor}, 난이도 = {context.difficultyName}, 플레이어 수 = {context.playerCount}");
            return false;
        }

        if(!FloorStatScaleTable.IsLoaded || !DifficultyStatScaleTable.IsLoaded || !PlayerCountStatScaleTable.IsLoaded)
        {
            Log.Error("적 능력치 배율 Json을 불러오지 못했습니다.");
            return false;
        }

        // SO 의 base 능력치를 불러오고 층, 난이도, 플레이어 수에 따라 적 능력치 배율 계산
        EnemyDefinitionSO def = enemyDatabase.GetById(enemyId);

        if (def == null)
            return false;

        FloorStatScaleRow floorScale = FloorStatScaleTable.GetByFloor(context.floor);
        DifficultyStatScaleRow difficultyfScale = DifficultyStatScaleTable.GetByDifficulty(context.difficultyName);
        PlayerCountStatScaleRow playerCountScale = PlayerCountStatScaleTable.GetByPlayerCount(context.playerCount);

        float hpMultiplier = floorScale.HpMultiplier * difficultyfScale.HpMultiplier * playerCountScale.HpMultiplier;
        float atkMultiplier = floorScale.AtkMultiplier * difficultyfScale.AtkMultiplier;
        float defMultiplier = floorScale.DefMultiplier * difficultyfScale.DefMultiplier;
        float expMultiplier = floorScale.ExpMultiplier * difficultyfScale.ExpMultiplier;
        float creditMultiplier = floorScale.CreditMultiplier * difficultyfScale.CreditMultiplier;

        // 스폰 시, 산정되는 최종능력치
        result = new WBH_EnemyInfo
        {
            enemyId = def.enemyId,
            enemyName = def.enemyName,
            enemyGrade = def.enemyGrade,
            enemyAttackType = def.attackType,

            maxHP = def.baseHealth * hpMultiplier,
            attack = def.baseAttackPower * atkMultiplier,
            defense = def.baseDefensePower * defMultiplier,

            moveSpeed = def.baseMoveSpeed,
            attackSpeed = def.baseAttackSpeed,
            attackRange = def.attackRange,
            attackCoolTime = def.attackCooldown,
            pen = def.penetration,
            projectileSpeed = def.projectileSpeed,

            exp = Mathf.RoundToInt(def.expReward * expMultiplier),
            credit = Mathf.RoundToInt(def.creditReward * creditMultiplier),

            patternID = def.patternId
        };
        return true;

    }

    // 테스트용도로 생성되는 EnemyInfo
    private bool TryCreateTestInfo(string enemyId, out WBH_EnemyInfo result)
    {
        result = null;

        if(testInfos == null || testInfos.Length == 0)
        {
            Log.Error("테스트 기능이 활성화 되었지만 testInfos 가 없습니다.");
            return false;
        }

        // enemyId 가 달라지는 것 방지
        foreach(TestEnemyInfoEntry entry in testInfos)
        {
            if (entry == null || entry.TestDef == null)
                continue;

            if (!string.Equals(entry.EnemyId, enemyId, StringComparison.Ordinal))
                continue;

            if (entry.Info == null)
                return false;

            result = entry.Info.Clone();

            result.enemyId = enemyId;
            return true;
        }

        Log.Error($"테스트 기능에서 {enemyId} 를 찾지 못했습니다");
        return false;
    }
}
