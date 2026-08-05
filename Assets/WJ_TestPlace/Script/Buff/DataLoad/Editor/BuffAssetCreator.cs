using System.IO;
using ItemSystem;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// 엑셀을 거치지 않고 버프/디버프 에셋을 바로 하나 만들어주는 도구.
    /// 시험 삼아 값을 만져보거나 일회성 버프가 필요할 때 쓴다.
    ///
    /// !! 팀이 공유하고 계속 관리할 버프는 BuffTable.xlsx에 적고
    ///    `DataLoader/Buff Table/0. Run All Steps`로 만드는 쪽이 맞다.
    ///    이 도구로 만든 에셋은 엑셀에 없으므로 파이프라인을 다시 돌려도 갱신되지 않는다.
    ///    (나중에 엑셀로 옮기려면 같은 buffId로 행을 추가하고 파일명을 맞추면 그 에셋을 그대로 갱신한다)
    /// </summary>
    public static class BuffAssetCreator
    {
        private const string OutputFolder = "Assets/Resources/DataFiles/BuffData/3. GeneratedAssets";

        [MenuItem("DataLoader/Buff Table/새 버프 에셋 만들기")]
        public static void CreateBuff()
        {
            Create("buff.new_buff", "새 버프", "설명을 입력하세요.", StatType.attackPowerPercent, 10f);
        }

        [MenuItem("DataLoader/Buff Table/새 디버프 에셋 만들기")]
        public static void CreateDebuff()
        {
            // 디버프는 별도 타입이 아니라 스탯 값이 음수인 버프다.
            Create("debuff.new_debuff", "새 디버프", "설명을 입력하세요.", StatType.moveSpeedPercent, -20f);
        }

        private static void Create(string baseId, string displayName, string description,
            StatType sampleStat, float sampleValue)
        {
            EnsureAssetFolder(OutputFolder);

            string id = MakeUniqueId(baseId);
            string path = CombineAssetPath(OutputFolder, id + ".asset");

            var asset = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            asset.buffId = id;
            asset.buffName = displayName;
            asset.description = description;
            asset.duration = 10f;
            asset.stackBehavior = BuffStackBehavior.RefreshDuration;
            asset.maxStack = 0;
            asset.statEffects = new[]
            {
                new FixedStatValue { statType = sampleStat, value = sampleValue }
            };

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 만든 직후 바로 인스펙터에서 값을 고칠 수 있도록 선택 상태로 둔다.
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[BuffAssetCreator] 생성했습니다: {path}\n" +
                      "인스펙터에서 값을 수정한 뒤, 계속 쓸 버프라면 BuffTable.xlsx에도 같은 buffId로 옮겨두는 걸 권장합니다.");
        }

        /// <summary>같은 이름이 있으면 뒤에 번호를 붙여 기존 에셋을 덮어쓰지 않게 한다.</summary>
        private static string MakeUniqueId(string baseId)
        {
            string id = baseId;
            int suffix = 1;

            while (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                       CombineAssetPath(OutputFolder, id + ".asset")) != null)
            {
                suffix++;
                id = baseId + "_" + suffix;
            }

            return id;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string normalized = assetFolder.Replace("\\", "/");
            string parent = Path.GetDirectoryName(normalized).Replace("\\", "/");
            string folderName = Path.GetFileName(normalized);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string CombineAssetPath(string left, string right)
        {
            return (left.TrimEnd('/') + "/" + right.TrimStart('/')).Replace("\\", "/");
        }
    }
}
