using System;
using System.Collections.Generic;

namespace Core
{
    public static class SaveDataCatalog
    {
        private static readonly Dictionary<SaveDataCategory, SaveDataDefinition> Definitions =
            new Dictionary<SaveDataCategory, SaveDataDefinition>
            {
                {
                    SaveDataCategory.PlayerProfile,
                    new SaveDataDefinition(
                        SaveDataCategory.PlayerProfile,
                        "profile",
                        "player_profile.json",
                        SaveStorageLocation.CloudWithLocalCache,
                        1)
                },
                {
                    SaveDataCategory.Gameplay,
                    new SaveDataDefinition(
                        SaveDataCategory.Gameplay,
                        "gameplay",
                        "game_save.json",
                        SaveStorageLocation.CloudWithLocalCache,
                        1)
                },
                {
                    SaveDataCategory.Quest,
                    new SaveDataDefinition(
                        SaveDataCategory.Quest,
                        "quest",
                        "quest_save.json",
                        SaveStorageLocation.CloudWithLocalCache,
                        1)
                },
                {
                    SaveDataCategory.SystemOptions,
                    new SaveDataDefinition(
                        SaveDataCategory.SystemOptions,
                        "systemOptions",
                        "system_options.json",
                        SaveStorageLocation.LocalOnly,
                        1)
                },
                {
                    SaveDataCategory.StageMap,
                    new SaveDataDefinition(
                        SaveDataCategory.StageMap,
                        "stageMap",
                        "stage_map.json",
                        SaveStorageLocation.LocalOnly,
                        1)
                }
            };

        /// <summary>
        /// 저장 데이터 종류에 연결된 문서 ID, 파일명, 저장 위치 설정을 반환합니다.
        /// </summary>
        public static SaveDataDefinition GetDefinition(SaveDataCategory category)
        {
            if (!Definitions.TryGetValue(category, out SaveDataDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "등록되지 않은 저장 데이터 종류입니다.");
            }

            return definition;
        }

        /// <summary>
        /// 등록된 저장 데이터 설정을 안전하게 찾아 반환합니다.
        /// </summary>
        public static bool TryGetDefinition(
            SaveDataCategory category,
            out SaveDataDefinition definition)
        {
            return Definitions.TryGetValue(category, out definition);
        }
    }
}

