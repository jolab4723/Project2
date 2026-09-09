using System;
using System.IO;
using Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

/// <summary>실제 로비 UI에서 패시브 적용·저장·읽기 전용, 키 설정 분리, 메뉴 시간 복원을 검사한다.</summary>
public static class MirrorKyUIValidation_MirrorTest
{
    [MenuItem("SW/Mirror Test/Validate KY UI In Play Mode")]
    public static void Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MirrorTestNetworkManager.SessionLobbyScene)
            throw new InvalidOperationException("Mirror 로비의 Play Mode에서 실행하세요.");
        var popup = Object.FindFirstObjectByType<KY_PassiveSkillPopup>(FindObjectsInactive.Include);
        var manager = PassiveSkillManager.Instance;
        if (popup == null || manager.GetDefinition(PassiveSkillId.AttackPower) == null)
            throw new InvalidOperationException("실제 패시브 팝업과 DB 연결이 필요합니다.");
        if (manager.CurrentProfile == null) DataManager.Instance.LoadPassiveData();
        PlayerProfileData originalProfile = manager.CurrentProfile;
        var popupData = new SerializedObject(popup);
        bool originalAllowChanges = popupData.FindProperty("allowChanges").boolValue;
        string originalReason = popupData.FindProperty("unavailableReason").stringValue;
        bool originallyOpen = popup.gameObject.activeSelf;
        string savePath = Path.Combine(Application.persistentDataPath, "profile_singleplayer.json");
        byte[] savedBytes = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
        DateTime savedTime = File.Exists(savePath) ? File.GetLastWriteTimeUtc(savePath) : default;
        string keyProfile = "KYValidation." + Guid.NewGuid().ToString("N");
        float originalTimeScale = Time.timeScale;
        GameObject pauseObject = null;
        GameObject inputObject = null;
        int checks = 0;
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("[MirrorKyUIValidation] " + name);
            checks++;
        }
        try
        {
            pauseObject = new GameObject("KY Pause Validation");
            var pause = pauseObject.AddComponent<KY_PausePopup>();
            Time.timeScale = 0.35f;
            pause.Open();
            Check(Time.timeScale == 0f, "single player pause");
            pause.Close();
            Check(Mathf.Approximately(Time.timeScale, 0.35f), "previous speed restored");
            pause.PauseGameTime = false;
            pause.Open();
            pause.Close();
            Check(Mathf.Approximately(Time.timeScale, 0.35f), "multiplayer menu preserves time");
            pause.PauseGameTime = true;
            pause.Open();
            pause.gameObject.SetActive(false);
            Check(Mathf.Approximately(Time.timeScale, 0.35f), "disabled popup restores time");
            Time.timeScale = originalTimeScale;

            KeyBindingService.ConfigureProfile(keyProfile + ".A");
            GameInputActions actions = KeyBindingService.InputActions;
            string defaultBinding = actions.Player.Skill1.bindings[0].effectivePath;
            actions.Player.Skill1.ApplyBindingOverride(0, "<Keyboard>/j");
            KeyBindingService.Save();
            KeyBindingService.ConfigureProfile(keyProfile + ".B");
            Check(actions.Player.Skill1.bindings[0].effectivePath == defaultBinding, "separate profile default");
            actions.Player.Skill1.ApplyBindingOverride(0, "<Keyboard>/k");
            KeyBindingService.Save();
            KeyBindingService.ConfigureProfile(keyProfile + ".A");
            Check(actions.Player.Skill1.bindings[0].effectivePath == "<Keyboard>/j", "profile A restored");
            KeyBindingService.ConfigureProfile(keyProfile + ".B");
            Check(actions.Player.Skill1.bindings[0].effectivePath == "<Keyboard>/k", "profile B restored");
            inputObject = new GameObject("KY Input Validation");
            var input = inputObject.AddComponent<PlayerActionInputHandler_MirrorTest>();
            input.enabled = false;
            Check(actions.Player.Skill1.enabled, "disabled player does not disable shared UI input");

            var profile = new PlayerProfileData { playerId = Guid.NewGuid().ToString("N"), gold = 10000 };
            manager.SetActiveProfile(profile);
            popup.Bind(manager);
            popup.Open();
            Check(popup.allSkills.Count == 12, "actual database rows");
            var left = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var right = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            int attackIndex = (int)PassiveSkillId.AttackPower;
            popup.slots[attackIndex].OnPointerClick(left);
            Check(profile.gold == 10000 && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0, "preview does not spend");
            int cost = manager.GetUnlockCostToLevel(PassiveSkillId.AttackPower, 1);
            popup.OnClickConfirm();
            Check(profile.gold == 10000 - cost && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 1, "confirmed purchase");
            Check(DataManager.Instance.LoadSinglePlayerSlot().profile.gold == profile.gold, "existing save API");
            popup.slots[attackIndex].OnPointerClick(right);
            popup.OnClickConfirm();
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0 && manager.GetUnlockedLevel(PassiveSkillId.AttackPower) == 1, "deactivate preserves unlock");
            popup.slots[attackIndex].OnPointerClick(left);
            popup.OnClickConfirm();
            Check(profile.gold == 10000 - cost, "unlocked rank is free");
            profile.gold = 0;
            popup.slots[attackIndex].OnPointerClick(left);
            popup.OnClickConfirm();
            Check(profile.gold == 0 && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 1, "insufficient gold rejected");
            popup.slots[(int)PassiveSkillId.Undecided].OnPointerClick(left);
            popup.OnClickConfirm();
            Check(manager.GetUnlockedLevel(PassiveSkillId.Undecided) == 0, "undefined effect cannot be purchased");
            popup.Bind(manager, false, "읽기 전용 검사");
            popup.OnClickReset();
            popup.slots[attackIndex].OnPointerClick(right);
            popup.OnClickConfirm();
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 1, "read only protects profile");
            popup.Bind(manager);
            popup.OnClickReset();
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0 && manager.GetUnlockedLevel(PassiveSkillId.AttackPower) == 1, "reset preserves purchase");
        }
        finally
        {
            // 검사 전에 읽은 실제 저장 파일과 프로필을 그대로 복원한다.
            if (savedBytes != null)
            {
                File.WriteAllBytes(savePath, savedBytes);
                File.SetLastWriteTimeUtc(savePath, savedTime);
            }
            else if (File.Exists(savePath)) File.Delete(savePath);
            manager.SetActiveProfile(originalProfile);
            popup.Bind(manager, originalAllowChanges, originalReason);
            if (!originallyOpen) popup.Close();
            PlayerPrefs.DeleteKey("KeyBindings." + keyProfile + ".A");
            PlayerPrefs.DeleteKey("KeyBindings." + keyProfile + ".B");
            PlayerPrefs.Save();
            KeyBindingService.ConfigureProfile("Mirror." + MirrorReconnectProfile_MirrorTest.GetProfileName());
            Time.timeScale = originalTimeScale;
            if (pauseObject != null) Object.DestroyImmediate(pauseObject);
            if (inputObject != null) Object.DestroyImmediate(inputObject);
        }
        Debug.Log($"[MirrorKyUIValidation] PASS {checks} checks; original profile and storage restored");
    }
}
