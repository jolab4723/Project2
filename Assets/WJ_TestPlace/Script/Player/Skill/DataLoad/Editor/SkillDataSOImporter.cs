using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace DataSystem
{
    /// <summary>
    /// SkillDataExcelToJson 결과물(JSON)을 읽어서 Assets/WJ_TestPlace/Data/Skill/의 기존
    /// SkillDefinitionSO 에셋을 찾아 데미지 계수/쿨타임/범위만 갱신한다(진화 관련 evo* 필드,
    /// 아이콘 등 이 시트에 없는 값은 그대로 보존됨 - EnemyDataSOImporter와 달리 스킬 에셋은 이미
    /// 씬(FighterSkillController)에 직접 연결돼 있어서 GUID를 유지해야 하므로 찾아서 갱신하는
    /// 방식을 쓴다). skillId가 일치하는 에셋이 없으면 새로 만든다.
    /// </summary>
    public static class SkillDataSOImporter
    {
        private const string DefaultJsonFolder = "Assets/Resources/DataFiles/SkillData/JSONFile";
        private const string OutputFolder = "Assets/WJ_TestPlace/Data/Skill";

        [MenuItem("DataLoader/Skill Data/2. Generate SO From JSON")]
        public static void GenerateSoFromJsonFromMenu()
        {
            string defaultAbsoluteFolder = AssetPathToAbsolutePath(DefaultJsonFolder);
            string jsonPath = EditorUtility.OpenFilePanel("Select skill data JSON", defaultAbsoluteFolder, "json");
            if (string.IsNullOrEmpty(jsonPath))
                return;

            GenerateAllFromJson(jsonPath);
        }

        [MenuItem("DataLoader/Skill Data/0. Run All Steps")]
        public static void RunAllSteps()
        {
            const string excelPath = "Assets/Resources/DataFiles/SkillData/ExcelFile/SkillData.xlsx";
            const string jsonPath = "Assets/Resources/DataFiles/SkillData/JSONFile/SkillData.json";

            string excelAbsolute = AssetPathToAbsolutePath(excelPath);
            string jsonAbsolute = AssetPathToAbsolutePath(jsonPath);

            SkillDataExcelToJson.Convert(excelAbsolute, jsonAbsolute);
            GenerateAllFromJson(jsonAbsolute);
        }

        public static void GenerateAllFromJson(string jsonPath)
        {
            List<SkillDataRow> rows = LoadJson(jsonPath);
            if (rows == null)
                return;

            int updated = 0;
            int created = 0;

            foreach (SkillDataRow row in rows)
            {
                SkillDefinitionSO asset = CreateOrUpdate(row, out bool isNew);
                if (asset == null)
                    continue;

                EditorUtility.SetDirty(asset);

                if (isNew)
                    created++;
                else
                    updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkillData] SO 갱신 완료. 신규 {created}개, 기존 갱신 {updated}개");
        }

        private static SkillDefinitionSO CreateOrUpdate(SkillDataRow row, out bool isNew)
        {
            isNew = false;

            if (!Enum.TryParse(row.skillId, true, out ActiveSkillId skillId))
            {
                Debug.LogWarning($"[SkillData] skillId '{row.skillId}'를 ActiveSkillId로 해석하지 못했습니다. ({row.skillName})");
                return null;
            }

            SkillDefinitionSO asset = FindExistingAsset(skillId);

            if (asset == null)
            {
                isNew = true;
                asset = ScriptableObject.CreateInstance<SkillDefinitionSO>();
                asset.skillId = skillId;

                string fileName = SanitizeFileName("Skill_" + skillId);
                string path = CombineAssetPath(OutputFolder, fileName + ".asset");
                AssetDatabase.CreateAsset(asset, path);
            }

            // shapeType은 생성 시점뿐 아니라 매번 시트 값으로 동기화한다 - 스킬 컨셉을 바꿔서 셰이프
            // 자체를 교체하는 경우(예: 부채꼴 히트스캔 -> 스택형 투사체)도 다시 실행하면 반영되게 하기 위함.
            // 기존엔 신규 생성 시에만 반영돼서, 이미 만든 에셋의 shapeType을 시트에서 바꿔도 무시되는
            // 문제가 있었다(직접 겪어서 발견 - WJ 이우진).
            if (Enum.TryParse(row.shapeType, true, out SkillShapeType parsedShape))
                asset.shapeType = parsedShape;
            else
                Debug.LogWarning($"[SkillData] shapeType '{row.shapeType}'를 해석하지 못해 기존 값을 유지합니다. ({row.skillName})");

            asset.skillName = row.skillName;
            asset.damageMultiplier = row.damageMultiplier;
            asset.cooldownSeconds = row.cooldownSeconds;
            asset.manaCost = row.manaCost;
            asset.evolution1ManaCost = row.evolution1ManaCost;
            asset.evolution2ManaCost = row.evolution2ManaCost;
            asset.evolution3ManaCost = row.evolution3ManaCost;

            switch (asset.shapeType)
            {
                case SkillShapeType.SectorSlash:
                    asset.sectorRange = row.range;
                    asset.sectorAngle = row.rangeWidthOrAngle;
                    break;

                case SkillShapeType.LineSlam:
                    asset.lineLength = row.range;
                    asset.lineWidth = row.rangeWidthOrAngle;
                    break;

                case SkillShapeType.Dash:
                    asset.dashDistance = row.range;
                    break;

                case SkillShapeType.ArcProjectile:
                    asset.projectileMaxDistance = row.range;
                    // maxStacks/stackRechargeSeconds/projectileSpeed/explosionRadius/arcProjectilePrefab은
                    // 이 시트의 2개 범용 컬럼(range/rangeWidthOrAngle)으로 표현하기엔 항목이 너무 많아서
                    // SkillDefinitionSO의 C# 기본값(6스택/4초/15/1유닛)을 그대로 쓰고, 프리팹은 코드에서 직접 연결한다.
                    break;

                case SkillShapeType.BombThrow:
                    asset.bombThrowRange = row.range;
                    // bombThrowSpeed/bombArcHeight/bombFuseSeconds/bombExplosionRadius/bombPrefab도 ArcProjectile과
                    // 같은 이유로 시트 컬럼에 안 담고 C# 기본값(10/2/2초/3유닛)을 그대로 쓰며, 프리팹은 코드에서 직접 연결한다.
                    break;

                case SkillShapeType.BackstepShot:
                    asset.backstepDistance = row.range;
                    // backstepDuration/backstepConeRange/backstepConeAngle도 같은 이유로 시트 컬럼에 안 담고
                    // C# 기본값(0.15초/5유닛/90도)을 그대로 쓴다.
                    break;
            }

            return asset;
        }

        /// <summary>Assets/WJ_TestPlace/Data/Skill/에서 같은 skillId를 가진 기존 에셋을 찾는다(GUID 유지 목적).</summary>
        private static SkillDefinitionSO FindExistingAsset(ActiveSkillId skillId)
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillDefinitionSO", new[] { OutputFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillDefinitionSO asset = AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(path);

                if (asset != null && asset.skillId == skillId)
                    return asset;
            }

            return null;
        }

        private static List<SkillDataRow> LoadJson(string jsonPath)
        {
            string absoluteJsonPath = jsonPath.StartsWith("Assets/") ? AssetPathToAbsolutePath(jsonPath) : jsonPath;
            if (!File.Exists(absoluteJsonPath))
            {
                Debug.LogError($"[SkillData] JSON file not found: {absoluteJsonPath}");
                return null;
            }

            string json = File.ReadAllText(absoluteJsonPath);
            List<SkillDataRow> rows = JsonConvert.DeserializeObject<List<SkillDataRow>>(json);
            if (rows == null)
                Debug.LogError("[SkillData] JSON parse failed.");

            return rows;
        }

        private static string CombineAssetPath(string left, string right)
        {
            return (left.TrimEnd('/') + "/" + right.TrimStart('/')).Replace("\\", "/");
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Unnamed" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result;
        }
    }
}
