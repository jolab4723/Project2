using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ItemSystem.EditorScripts
{
    /// <summary>
    /// ItemDefinitionSO 전용 커스텀 인스펙터.
    /// - category가 Weapon이면 characterClass/weaponType만, Armor면 armorType만 보여준다.
    /// - weaponType은 선택된 characterClass에 해당하는 목록만 드롭다운에 노출한다.
    /// 이 스크립트는 반드시 "Editor" 폴더 안에 있어야 함 (런타임 빌드에 포함되면 안 됨).
    /// </summary>
    [CustomEditor(typeof(ItemDefinitionSO))]
    public class ItemDefinitionSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var categoryProp = serializedObject.FindProperty("category");
            var category = (ItemCategory)categoryProp.enumValueIndex;

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.name == "characterClass" && category != ItemCategory.Weapon) continue;
                if (prop.name == "armorType" && category != ItemCategory.Armor) continue;

                if (prop.name == "weaponType")
                {
                    if (category != ItemCategory.Weapon) continue;
                    DrawFilteredWeaponType(prop);
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // characterClass에 맞는 WeaponType만 드롭다운에 노출
        void DrawFilteredWeaponType(SerializedProperty weaponTypeProp)
        {
            var classProp = serializedObject.FindProperty("characterClass");
            var charClass = (CharacterClass)classProp.enumValueIndex;
            var validTypes = ClassWeaponTable.WeaponsByClass[charClass];

            string[] options = validTypes.Select(t => t.ToString()).ToArray();
            int currentIndex = validTypes.IndexOf((WeaponType)weaponTypeProp.enumValueIndex);
            if (currentIndex < 0) currentIndex = 0; // 클래스에 안 맞는 값이 저장돼 있으면 첫 번째로 보정

            int selected = EditorGUILayout.Popup("Weapon Type", currentIndex, options);
            weaponTypeProp.enumValueIndex = (int)validTypes[selected];
        }
    }
}
