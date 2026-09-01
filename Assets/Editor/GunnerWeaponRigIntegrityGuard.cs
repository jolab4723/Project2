using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using ItemSystem;

[InitializeOnLoad]
public sealed class GunnerWeaponRigIntegrityGuard : AssetPostprocessor, IPreprocessBuildWithReport
{
    private const string GunnerPrefabPath =
        "Assets/Resources/Prefabs/Character/Player/Gunner.prefab";

    private static bool validationQueued;

    static GunnerWeaponRigIntegrityGuard()
    {
        QueueValidation();
    }

    public int callbackOrder => 0;

    [MenuItem("SW/Equipment/거너 무기 리그 무결성 검사")]
    public static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("[GunnerWeaponRigIntegrityGuard] 거너 무기 소켓과 왼손 IK 연결이 정상입니다.");
    }

    public static void ValidateOrThrow()
    {
        if (!TryValidate(out string error))
            throw new InvalidOperationException(error);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!TryValidate(out string error))
            throw new BuildFailedException(error);
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets.Contains(GunnerPrefabPath) ||
            movedAssets.Contains(GunnerPrefabPath) ||
            deletedAssets.Contains(GunnerPrefabPath))
        {
            QueueValidation();
        }
    }

    private static void QueueValidation()
    {
        if (validationQueued)
            return;

        validationQueued = true;
        EditorApplication.delayCall += ValidateAfterImport;
    }

    private static void ValidateAfterImport()
    {
        validationQueued = false;

        if (!TryValidate(out string error))
            Debug.LogError(error);
    }

    private static bool TryValidate(out string error)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefabPath);
        if (root == null)
            return Fail("거너 프리팹을 찾을 수 없습니다.", out error);

        Transform weaponSocket = FindUnique(root, "WeaponSocket", out string socketError);
        if (weaponSocket == null)
            return Fail(socketError, out error);

        Transform leftContact = FindUnique(root, "LeftWeaponPalmContact", out string contactError);
        if (leftContact == null)
            return Fail(contactError, out error);

        Transform leftTarget = FindUnique(root, "LeftHandIKTarget", out string targetError);
        if (leftTarget == null)
            return Fail(targetError, out error);

        if (weaponSocket.parent == null || weaponSocket.parent.name != "RightHand_target")
            return Fail("WeaponSocket이 RightHand_target 바로 아래에 있지 않습니다.", out error);

        if (leftContact.parent == null || leftContact.parent.name != "hand_L")
            return Fail("LeftWeaponPalmContact가 hand_L 바로 아래에 있지 않습니다.", out error);

        if (leftTarget.parent != weaponSocket)
            return Fail("LeftHandIKTarget이 WeaponSocket 바로 아래에 있지 않습니다.", out error);

        RigBuilder rigBuilder = root.GetComponent<RigBuilder>();
        if (rigBuilder == null || rigBuilder.layers.Count == 0 || rigBuilder.layers[0].rig == null)
            return Fail("RigBuilder 또는 WeaponIKRig 레이어가 끊어졌습니다.", out error);

        TwoBoneIKConstraint constraint = root.GetComponentInChildren<TwoBoneIKConstraint>(true);
        if (constraint == null ||
            constraint.data.root == null ||
            constraint.data.mid == null ||
            constraint.data.tip == null ||
            constraint.data.target != leftTarget ||
            constraint.data.hint == null)
        {
            return Fail("LeftArmIK의 본, Target 또는 Hint 참조가 끊어졌습니다.", out error);
        }

        Rig constraintRig = constraint.GetComponentInParent<Rig>(true);
        if (constraintRig == null || rigBuilder.layers.All(layer => layer.rig != constraintRig))
            return Fail("LeftArmIK가 RigBuilder의 WeaponIKRig 레이어에 연결되지 않았습니다.", out error);

        if (constraint.data.root.name != "Arm_L" ||
            constraint.data.mid.name != "forearm_L" ||
            constraint.data.tip.name != "hand_L")
        {
            return Fail("LeftArmIK가 거너의 왼팔 본 체인을 가리키지 않습니다.", out error);
        }

        PlayerWeaponVisualPresenter presenter = root.GetComponent<PlayerWeaponVisualPresenter>();
        if (presenter == null)
            return Fail("PlayerWeaponVisualPresenter가 빠졌습니다.", out error);

        SerializedObject serializedPresenter = new SerializedObject(presenter);
        if (!References(serializedPresenter, "weaponMount", weaponSocket) ||
            !References(serializedPresenter, "leftHandContact", leftContact) ||
            !References(serializedPresenter, "leftHandIkTarget", leftTarget) ||
            !References(serializedPresenter, "leftHandIkConstraint", constraint) ||
            serializedPresenter.FindProperty("visualCatalog")?.objectReferenceValue == null ||
            serializedPresenter.FindProperty("characterAnimator")?.objectReferenceValue == null ||
            serializedPresenter.FindProperty("characterClass")?.enumValueIndex != (int)CharacterClass.Gunner)
        {
            return Fail("PlayerWeaponVisualPresenter의 거너 무기/IK 직렬화 참조가 끊어졌습니다.", out error);
        }

        error = null;
        return true;
    }

    private static Transform FindUnique(GameObject root, string objectName, out string error)
    {
        Transform[] matches = root
            .GetComponentsInChildren<Transform>(true)
            .Where(candidate => candidate.name == objectName)
            .ToArray();

        if (matches.Length == 1)
        {
            error = null;
            return matches[0];
        }

        error = matches.Length == 0
            ? $"{objectName}이(가) 빠졌습니다."
            : $"{objectName}이(가) {matches.Length}개라 참조가 모호합니다.";
        return null;
    }

    private static bool References(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object expected)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.objectReferenceValue == expected;
    }

    private static bool Fail(string reason, out string error)
    {
        error = $"[GunnerWeaponRigIntegrityGuard] {reason} " +
                $"'{GunnerPrefabPath}' 머지 결과를 확인하세요.";
        return false;
    }
}
