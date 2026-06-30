using UnityEditor;
using UnityEngine;

namespace ItemSystem.EditorScripts
{
    /// <summary>
    /// ItemDataStorage 전용 커스텀 인스펙터.
    /// 기본 fold-out 대신 ItemDropTester의 BuildLog()와 같은 형태로 정리해서 보여준다.
    /// 이 스크립트는 반드시 "Editor" 폴더 안에 있어야 함 (런타임 빌드에 포함되면 안 됨).
    /// </summary>
    [CustomEditor(typeof(ItemDataStorage))]
    public class ItemDataStorageEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var storage = (ItemDataStorage)target;

            // iconRenderer 필드는 기본 방식 그대로 노출 (직접 연결해야 하는 값이라 GUI 자동화 대상 아님)
            serializedObject.Update();
            var iconProp = serializedObject.FindProperty("iconRenderer");
            EditorGUILayout.PropertyField(iconProp);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            var item = storage.Item;
            if (item == null || item.definition == null)
            {
                EditorGUILayout.HelpBox("아직 데이터가 주입되지 않았습니다. (Init() 호출 전)", MessageType.Info);
                return;
            }

            var def = item.definition;

            EditorGUILayout.LabelField("아이템 정보", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("이름", def.itemName);
            EditorGUILayout.LabelField("분류", def.category.ToString());
            EditorGUILayout.LabelField("등급", def.grade.ToString());
            EditorGUILayout.LabelField("강화 수치", $"+{item.upgradeLevel}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("메인 옵션 (강화 적용)", EditorStyles.boldLabel);
            var effectiveMain = item.GetEffectiveMainOptions();
            if (effectiveMain.Count == 0)
            {
                EditorGUILayout.LabelField("(없음)");
            }
            else
            {
                foreach (var main in effectiveMain)
                    EditorGUILayout.LabelField(main.statType.ToString(), $"{main.value:F2}{(main.IsPercent ? "%" : "")}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("서브 옵션 (랜덤 결과, 속성 보너스 포함)", EditorStyles.boldLabel);
            if (item.rolledSubStats == null || item.rolledSubStats.Count == 0)
            {
                EditorGUILayout.LabelField("(없음)");
            }
            else
            {
                foreach (var stat in item.rolledSubStats)
                    EditorGUILayout.LabelField(stat.statType.ToString(), $"{stat.value:F2}{(stat.IsPercent ? "%" : "")}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("속성 결과", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("rolledElement", item.rolledElement.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("고유 효과", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(def.uniqueEffect != null ? def.uniqueEffect.EffectDescription : "(없음)");
        }
    }
}
