#if UNITY_EDITOR

using EnemySystem;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// WBH_EnemyDataProvider 의 테스트용 능력치 부여 기능을 인스펙터를 통해 편하게 쓰기 위한 인스펙터 에디터 스크립트
/// </summary>
[CustomEditor(typeof(WBH_EnemyDataProvider))]
public sealed class WBH_EnemyDataProviderEditor : Editor
{
    private SerializedProperty scriptProperty;
    private SerializedProperty infoSourceProperty;
    private SerializedProperty enemyDatabaseProperty;
    private SerializedProperty testInfosProperty;

    private void OnEnable()
    {
        scriptProperty = serializedObject.FindProperty("m_Script");
        infoSourceProperty = serializedObject.FindProperty("infoSource");
        enemyDatabaseProperty = serializedObject.FindProperty("enemyDatabase");
        testInfosProperty = serializedObject.FindProperty("testInfos");
    }

    // 인스펙터 화면을 갱신
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true)) // 인스펙터에서 스크립트 항목을 보여주되 수정하지 못하게 만드는 코드
        {
            EditorGUILayout.PropertyField(scriptProperty);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Data Source", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(infoSourceProperty);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Enemy Database", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(enemyDatabaseProperty);

        // EnemyInfoSource.GeneratedData = 0
        // EnemyInfoSource.TestOverride = 1
        if (infoSourceProperty.enumValueIndex == 1) // TestOverride 상태일 때만 TestInfos 표시
        {
            EditorGUILayout.Space(8f);
            DrawTestInfos();
        }

        serializedObject.ApplyModifiedProperties(); // 사용자가 변경한 값을 실제 컴포넌트 데이터에 반영
    }

    // TestInfos 배열 전체 그림
    private void DrawTestInfos()
    {
        EditorGUILayout.LabelField("Test Override", EditorStyles.boldLabel);

        EnemyDatabaseSO database = enemyDatabaseProperty.objectReferenceValue as EnemyDatabaseSO;

        if (database == null)
        {
            EditorGUILayout.HelpBox("TestInfo에서 적을 선택하려면 " + "EnemyDatabaseSO를 지정해야 합니다.",
                MessageType.Warning);
        }

        List<EnemyDefinitionSO> definitions =  CollectDefinitions(database);

        int removeIndex = -1;

        for (int i = 0; i < testInfosProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = testInfosProperty.GetArrayElementAtIndex(i);

            SerializedProperty definitionProperty = entryProperty.FindPropertyRelative("testDef");

            SerializedProperty infoProperty = entryProperty.FindPropertyRelative("info");

            EnemyDefinitionSO currentDefinition = definitionProperty.objectReferenceValue as EnemyDefinitionSO;

            string title = currentDefinition != null
                ? $"{currentDefinition.enemyName} " + $"({currentDefinition.enemyId})"
                : $"Element {i} - 선택 안 함";

            entryProperty.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(entryProperty.isExpanded, title);

            if (entryProperty.isExpanded)
            {
                EditorGUI.indentLevel++;

                DrawEnemyDropdown(definitions, definitionProperty,infoProperty);

                EditorGUILayout.Space(3f);

                using (new EditorGUI.DisabledScope( definitionProperty.objectReferenceValue == null))
                {
                    if (GUILayout.Button("선택한 적의 기본값 다시 불러오기"))
                    {
                        EnemyDefinitionSO definition = definitionProperty.objectReferenceValue as EnemyDefinitionSO;

                        CopyDefinitionToInfo(definition, infoProperty);
                    }
                }

                EditorGUILayout.Space(5f);
                DrawEnemyInfo(infoProperty);

                EditorGUILayout.Space(3f);

                if (GUILayout.Button("이 항목 제거"))
                {
                    removeIndex = i;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4f);
        }

        if (removeIndex >= 0)
        {
            testInfosProperty.DeleteArrayElementAtIndex(removeIndex);
        }

        if (GUILayout.Button("TestInfo 추가"))
        {
            AddTestInfo();
        }
    }

    // allEnemies SO 에서 유효한 EnemyDefnitionSO 만 모으는 메서드. 모아서 드롭다운에 표시하는데 사용
    private static List<EnemyDefinitionSO> CollectDefinitions(EnemyDatabaseSO database)
    {
        var result = new List<EnemyDefinitionSO>();

        if (database == null || database.allEnemies == null)
        {
            return result;
        }

        foreach (EnemyDefinitionSO definition in database.allEnemies)
        {
            if (definition == null || string.IsNullOrWhiteSpace( definition.enemyId))
            {
                continue;
            }

            result.Add(definition);
        }

        return result;
    }

    // EnemyDatabase 목록을 드롭다운으로 표출
    private static void DrawEnemyDropdown(List<EnemyDefinitionSO> definitions,SerializedProperty definitionProperty, SerializedProperty infoProperty)
    {
        string[] labels = new string[definitions.Count + 1];

        labels[0] = "선택 안 함";

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinitionSO definition = definitions[i];

            labels[i + 1] =  $"{definition.enemyName} " + $"({definition.enemyId})";
        }

        EnemyDefinitionSO current = definitionProperty.objectReferenceValue as EnemyDefinitionSO;

        int currentIndex = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] == current)
            {
                currentIndex = i + 1;
                break;
            }
        }

        EditorGUI.BeginChangeCheck();

        int selectedIndex = EditorGUILayout.Popup( "Enemy", currentIndex, labels);

        if (!EditorGUI.EndChangeCheck())
            return;

        EnemyDefinitionSO selected =
            selectedIndex > 0
                ? definitions[selectedIndex - 1]
                : null;

        definitionProperty.objectReferenceValue = selected;

        // 새로운 적을 선택했을 때 한 번만 기본값을 복사한다.
        if (selected != null)
        {
            CopyDefinitionToInfo(selected, infoProperty);
        }
    }

    // EnemyDefinitionSO 의 기본값을 WBH_EnemyInfo 로 전달
    private static void CopyDefinitionToInfo(
        EnemyDefinitionSO definition,
        SerializedProperty info)
    {
        if (definition == null || info == null)
            return;

        SetString(info, "enemyId", definition.enemyId);
        SetString(info, "enemyName", definition.enemyName);

        SetInt(
            info,
            "enemyGrade",
            (int)definition.enemyGrade);

        SetInt(
            info,
            "enemyAttackType",
            (int)definition.attackType);

        SetFloat(info, "maxHP", definition.baseHealth);
        SetFloat(
            info,
            "attack",
            definition.baseAttackPower);
        SetFloat(
            info,
            "defense",
            definition.baseDefensePower);
        SetFloat(
            info,
            "moveSpeed",
            definition.baseMoveSpeed);
        SetFloat(
            info,
            "attackSpeed",
            definition.baseAttackSpeed);
        SetFloat(
            info,
            "attackRange",
            definition.attackRange);
        SetFloat(
            info,
            "attackCoolTime",
            definition.attackCooldown);
        SetFloat(
            info,
            "pen",
            definition.penetration);
        SetFloat(
            info,
            "projectileSpeed",
            definition.projectileSpeed);

        SetInt(
            info,
            "exp",
            Mathf.RoundToInt(definition.expReward));
        SetInt(
            info,
            "credit",
            Mathf.RoundToInt(
                definition.creditReward));

        SetInt(
            info,
            "patternID",
            definition.patternId);
    }

    // EnemyInfo 의 항목을 인스펙터에 출력
    private static void DrawEnemyInfo(
       SerializedProperty info)
    {
        if (info == null)
        {
            EditorGUILayout.HelpBox( "EnemyInfo를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField(
            "Override Info",
            EditorStyles.boldLabel);

        // ID는 선택한 EnemyDefinitionSO에서 결정한다.
        using (new EditorGUI.DisabledScope(true))
        {
            DrawProperty(info, "enemyId");
        }

        DrawProperty(info, "enemyName");
        DrawProperty(info, "enemyGrade");
        DrawProperty(info, "enemyAttackType");

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField( "Stats",EditorStyles.miniBoldLabel);

        DrawProperty(info, "maxHP");
        DrawProperty(info, "attack");
        DrawProperty(info, "defense");
        DrawProperty(info, "moveSpeed");
        DrawProperty(info, "pen");

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "Combat",
            EditorStyles.miniBoldLabel);

        DrawProperty(info, "attackRange");
        DrawProperty(info, "attackCoolTime");
        DrawProperty(info, "attackSpeed");
        DrawProperty(info, "projectileSpeed");

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "Reward / Pattern",
            EditorStyles.miniBoldLabel);

        DrawProperty(info, "exp");
        DrawProperty(info, "credit");
        DrawProperty(info, "patternID");
    }

    // TestInfo 에 새 배열 추가
    private void AddTestInfo()
    {
        int index = testInfosProperty.arraySize;

        testInfosProperty.InsertArrayElementAtIndex(index);

        SerializedProperty entry =
            testInfosProperty.GetArrayElementAtIndex(index);

        entry.isExpanded = true;

        SerializedProperty definition =
            entry.FindPropertyRelative("testDef");

        SerializedProperty info =
            entry.FindPropertyRelative("info");

        definition.objectReferenceValue = null;
        ClearInfo(info);
    }

    // 기존 배열 제거
    private static void ClearInfo( SerializedProperty info)
    {
        if (info == null)
            return;

        SetString(info, "enemyId", string.Empty);
        SetString(info, "enemyName", string.Empty);

        SetInt(info, "enemyGrade", 0);
        SetInt(info, "enemyAttackType", 0);

        SetFloat(info, "maxHP", 0f);
        SetFloat(info, "attack", 0f);
        SetFloat(info, "defense", 0f);
        SetFloat(info, "moveSpeed", 0f);
        SetFloat(info, "pen", 0f);

        SetFloat(info, "attackRange", 0f);
        SetFloat(info, "attackCoolTime", 0f);
        SetFloat(info, "attackSpeed", 0f);
        SetFloat(info, "projectileSpeed", 0f);

        SetInt(info, "exp", 0);
        SetInt(info, "credit", 0);
        SetInt(info, "patternID", 0);
    }

    private static void DrawProperty(
        SerializedProperty parent,
        string propertyName)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            EditorGUILayout.PropertyField(property);
    }

    private static void SetString(
        SerializedProperty parent,
        string propertyName,
        string value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.stringValue = value;
    }

    private static void SetFloat(
        SerializedProperty parent,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    private static void SetInt(
        SerializedProperty parent,
        string propertyName,
        int value)
    {
        SerializedProperty property  =
            parent.FindPropertyRelative(propertyName);

        if (property !=  null)
            property.intValue = value;
    }
}

#endif
