using System.Collections.Generic;
using System.Text;
using ItemSystem;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SW.Test.RandomDrop
{
    public class SwTestEquipmentDropTester : MonoBehaviour
    {
        [Header("드랍 설정")]
        [SerializeField] private SwTestEquipmentDropTableSO dropTable;
        [SerializeField] private List<ItemDefinitionSO> itemDefinitions = new List<ItemDefinitionSO>();

        [Header("에디터 자동 수집")]
        [SerializeField] private bool collectGeneratedItemsOnStart = true;
        [SerializeField] private string itemDefinitionFolder = "Assets/SW/TEST/ItemTablePipeline/GeneratedAssets/Items";

        [Header("시뮬레이션")]
        [SerializeField] private SwTestMonsterDropGrade simulationGrade = SwTestMonsterDropGrade.Normal;
        [SerializeField] private int simulationCount = 1000;

        private void Start()
        {
#if UNITY_EDITOR
            if (collectGeneratedItemsOnStart && itemDefinitions.Count == 0)
                CollectGeneratedItemDefinitions();
#endif
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                RollOnce(SwTestMonsterDropGrade.Normal);

            if (keyboard.digit2Key.wasPressedThisFrame)
                RollOnce(SwTestMonsterDropGrade.Champion);

            if (keyboard.digit3Key.wasPressedThisFrame)
                RollOnce(SwTestMonsterDropGrade.Elite);

            if (keyboard.digit4Key.wasPressedThisFrame)
                RollOnce(SwTestMonsterDropGrade.Boss);

            if (keyboard.f5Key.wasPressedThisFrame)
                Simulate(simulationGrade, simulationCount);
        }

        [ContextMenu("일반 몬스터 1회 드랍 테스트")]
        public void RollNormalOnce()
        {
            RollOnce(SwTestMonsterDropGrade.Normal);
        }

        [ContextMenu("대장급 일반 1회 드랍 테스트")]
        public void RollChampionOnce()
        {
            RollOnce(SwTestMonsterDropGrade.Champion);
        }

        [ContextMenu("엘리트 1회 드랍 테스트")]
        public void RollEliteOnce()
        {
            RollOnce(SwTestMonsterDropGrade.Elite);
        }

        [ContextMenu("보스 1회 드랍 테스트")]
        public void RollBossOnce()
        {
            RollOnce(SwTestMonsterDropGrade.Boss);
        }

        [ContextMenu("선택 등급 시뮬레이션")]
        public void SimulateSelectedGrade()
        {
            Simulate(simulationGrade, simulationCount);
        }

        public SwTestEquipmentDropResult RollOnce(SwTestMonsterDropGrade monsterGrade)
        {
            SwTestEquipmentDropResult result = SwTestEquipmentDropService.Roll(dropTable, itemDefinitions, monsterGrade);
            string monsterName = SwTestEquipmentDropService.GetMonsterGradeName(monsterGrade);

            if (!result.success)
            {
                Debug.Log($"[SW TEST 드랍] 몬스터 등급: {monsterName} / 결과: {result.failReason}");
                return result;
            }

            string itemName = result.itemDefinition != null ? result.itemDefinition.itemName : "아이템 없음";
            string itemId = result.itemDefinition != null ? result.itemDefinition.itemId : "ID 없음";
            string rarityName = SwTestEquipmentDropService.GetRarityName(result.rarity);
            string itemKindName = SwTestEquipmentDropService.GetItemKindName(result.itemKind);

            Debug.Log($"[SW TEST 드랍] 몬스터 등급: {monsterName} / 아이템 등급: {rarityName} / 종류: {itemKindName} / 아이템: {itemName} ({itemId})");
            return result;
        }
        private static bool IsNoCandidateItemFail(SwTestEquipmentDropResult result)
        {
            return !result.success
                && !string.IsNullOrEmpty(result.failReason)
                && result.failReason.StartsWith(SwTestEquipmentDropService.NoCandidateItemReasonPrefix);
        }
        public void Simulate(SwTestMonsterDropGrade monsterGrade, int count)
        {
            count = Mathf.Max(1, count);
            Dictionary<ItemRarity, int> rarityCounts = CreateEnumCountMap<ItemRarity>();
            Dictionary<SwTestDropItemKind, int> kindCounts = CreateEnumCountMap<SwTestDropItemKind>();
            int successCount = 0;
            int noDropCount = 0;
            int failCount = 0;
            Dictionary<string, int> failReasonCounts = new Dictionary<string, int>();

            int seed = System.Guid.NewGuid().GetHashCode();
            System.Random random = new System.Random(seed);

            int validRollCount = 0;
            int rerollCount = 0;

            while (validRollCount < count)
            {
                SwTestEquipmentDropResult result = SwTestEquipmentDropService.Roll(dropTable, itemDefinitions, monsterGrade, random);

                // 후보 아이템이 없는 조합은 테스트상 무효 처리하고 다시 굴림
                if (IsNoCandidateItemFail(result))
                {
                    rerollCount++;
                    continue;
                }

                // 여기까지 왔으면 이번 시도는 정상 시도로 카운트
                validRollCount++;

                if (result.success)
                {
                    successCount++;
                    rarityCounts[result.rarity]++;
                    kindCounts[result.itemKind]++;
                }
                else if (result.failReason == SwTestEquipmentDropService.NoItemDroppedReason)
                {
                    noDropCount++;
                }
                else
                {
                    failCount++;
                }
            }

            Debug.Log(BuildSimulationLog(monsterGrade, count, seed, successCount, noDropCount, failCount, rarityCounts, kindCounts, failReasonCounts));
        }

#if UNITY_EDITOR
        [ContextMenu("생성된 아이템 정의 자동 수집")]
        public void CollectGeneratedItemDefinitions()
        {
            itemDefinitions.Clear();
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { itemDefinitionFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ItemDefinitionSO item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (item == null)
                    continue;

                if (item.category == ItemCategory.Relic)
                    continue;

                itemDefinitions.Add(item);
            }

            EditorUtility.SetDirty(this);
            Debug.Log($"[SW TEST 드랍] 아이템 정의 수집 완료: {itemDefinitions.Count}개 / 대상 폴더: {itemDefinitionFolder} / 유물 제외");
        }
#endif

        private static Dictionary<TEnum, int> CreateEnumCountMap<TEnum>() where TEnum : System.Enum
        {
            Dictionary<TEnum, int> result = new Dictionary<TEnum, int>();
            TEnum[] values = (TEnum[])System.Enum.GetValues(typeof(TEnum));
            for (int i = 0; i < values.Length; i++)
                result[values[i]] = 0;
            return result;
        }

private static string BuildSimulationLog(
            SwTestMonsterDropGrade monsterGrade,
            int totalCount,
            int seed,
            int successCount,
            int noDropCount,
            int failCount,
            Dictionary<ItemRarity, int> rarityCounts,
            Dictionary<SwTestDropItemKind, int> kindCounts,
            Dictionary<string, int> failReasonCounts)
        {
            StringBuilder builder = new StringBuilder();
            string monsterName = SwTestEquipmentDropService.GetMonsterGradeName(monsterGrade);

            builder.AppendLine($"[SW TEST 드랍 시뮬레이션] 몬스터 등급: {monsterName} / 반복 횟수: {totalCount} / 시드: {seed}");
            builder.AppendLine($"드랍 성공: {successCount}회 ({ToPercent(successCount, totalCount)}) / 미드랍: {noDropCount}회 ({ToPercent(noDropCount, totalCount)}) / 실패: {failCount}회");
            if (failCount > 0)
            {
                builder.AppendLine("실패 사유 분포:");
                foreach (KeyValuePair<string, int> pair in failReasonCounts)
                {
                    builder.AppendLine($"- {pair.Key}: {pair.Value}회 ({ToPercent(pair.Value, failCount)})");
                }
            }
            builder.AppendLine("아이템 등급 분포:");
            foreach (KeyValuePair<ItemRarity, int> pair in rarityCounts)
            {
                string rarityName = SwTestEquipmentDropService.GetRarityName(pair.Key);
                builder.AppendLine($"- {rarityName}: {pair.Value}회 ({ToPercent(pair.Value, Mathf.Max(1, successCount))})");
            }

            builder.AppendLine("아이템 종류 분포:");
            foreach (KeyValuePair<SwTestDropItemKind, int> pair in kindCounts)
            {
                string itemKindName = SwTestEquipmentDropService.GetItemKindName(pair.Key);
                builder.AppendLine($"- {itemKindName}: {pair.Value}회 ({ToPercent(pair.Value, Mathf.Max(1, successCount))})");
            }

            return builder.ToString();
        }

        private static string ToPercent(int value, int total)
        {
            if (total <= 0)
                return "0.0%";

            return ((float)value / total * 100f).ToString("0.0") + "%";
        }
    }
}
