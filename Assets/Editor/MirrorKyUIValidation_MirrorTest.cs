using System;
using System.IO;
using System.Collections.Generic;
using Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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
        if (!popup.gameObject.activeInHierarchy)
            throw new InvalidOperationException("패시브 팝업을 연 뒤 다음 프레임에 검사를 실행하세요.");
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
        Button Control(string field) => (Button)new SerializedObject(popup).FindProperty(field).objectReferenceValue;
        void Click(Component target)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)target.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == target.gameObject,
                "actual pointer target: " + target.name);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
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

            var profile = new PlayerProfileData { playerId = Guid.NewGuid().ToString("N"), credit = 10000 };
            manager.SetActiveProfile(profile);
            popup.Bind(manager);
            KY_PopupManager.Instance.Show(PopupType.PassiveSkill);
            Check(popup.allSkills.Count == 12, "actual database rows");
            foreach (PassiveSkillId id in Enum.GetValues(typeof(PassiveSkillId)))
            {
                Click(popup.slots[(int)id]);
                Check(popup.descriptionView.nameText.text == manager.GetDefinition(id).displayName, "select " + id);
            }
            int attackIndex = (int)PassiveSkillId.AttackPower;
            var left = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var right = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };

            // 현재 화면은 슬롯 클릭으로 선택하고 + 버튼으로 미리보기 단계를 올린다.
            popup.slots[attackIndex].OnPointerClick(left);
            Click(Control("levelUpButton"));
            Check(profile.credit == 10000 && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0, "preview does not spend");
            int cost = manager.GetUnlockCostToLevel(PassiveSkillId.AttackPower, 1);
            popup.OnClickConfirm();
            Check(profile.credit == 10000 - cost && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 1, "confirmed purchase");
            Check(DataManager.Instance.LoadSinglePlayerSlot().profile.credit == profile.credit, "existing save API");
            popup.slots[attackIndex].OnPointerClick(right);
            popup.OnClickConfirm();
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0 && manager.GetUnlockedLevel(PassiveSkillId.AttackPower) == 1, "deactivate preserves unlock");
            popup.slots[attackIndex].OnPointerClick(left);
            Click(Control("levelUpButton"));
            popup.OnClickConfirm();
            Check(profile.credit == 10000 - cost, "unlocked rank is free");

            // 2. 단계 조절 버튼 및 모달 팝업 라이프사이클 검증 (내쪽 작업)
            Click(popup.slots[attackIndex]);
            Click(Control("levelDownButton"));
            Click(Control("confirmButton"));
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0 && manager.GetUnlockedLevel(PassiveSkillId.AttackPower) == 1, "button deactivate preserves unlock");
            Click(Control("levelUpButton"));
            Click(Control("confirmButton"));
            Check(profile.credit == 10000 - cost, "button unlocked rank is free");
            Click(popup.transform.Find("btnClosePopup").GetComponent<Button>());
            Check(!popup.gameObject.activeSelf && !KY_PopupManager.Instance.HasOpenModalPopup, "close releases background input");
            KY_PopupManager.Instance.Show(PopupType.PassiveSkill);
            Click(popup.slots[attackIndex]);
            Click(Control("levelUpButton"));
            int secondCost = manager.GetUnlockCostToLevel(PassiveSkillId.AttackPower, 2);
            Click(Control("confirmButton"));
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 2 && profile.credit == 10000 - cost - secondCost,
                "reopened popup applies one level once");

            // 3. 크레딧 부족 및 미정 효과 차단 검증 (main + 내쪽 통합)
            profile.credit = 0;
            manager.SetActiveProfile(profile);
            Click(Control("levelUpButton"));
            Check(!Control("confirmButton").interactable, "insufficient credit disables purchase");
            Click(Control("confirmButton"));
            Check(profile.credit == 0 && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 2, "insufficient credit rejected");
            popup.slots[attackIndex].OnPointerClick(left);
            popup.OnClickConfirm();
            Check(profile.credit == 0 && manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 2, "insufficient credit pointer click rejected");
            Click(popup.slots[(int)PassiveSkillId.Undecided]);
            Check(!Control("levelUpButton").interactable && !Control("confirmButton").interactable, "undefined effect controls disabled");
            popup.slots[(int)PassiveSkillId.Undecided].OnPointerClick(left);
            popup.OnClickConfirm();
            Check(manager.GetUnlockedLevel(PassiveSkillId.Undecided) == 0, "undefined effect cannot be purchased");
            popup.Bind(manager, false, "읽기 전용 검사");
            Click(popup.slots[attackIndex]);
            Check(!Control("resetButton").interactable && !Control("levelDownButton").interactable, "ready lock disables controls");
            Click(Control("resetButton"));
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 2, "read only protects profile");
            popup.Bind(manager);
            Click(Control("resetButton"));
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == 0 && manager.GetUnlockedLevel(PassiveSkillId.AttackPower) == 2, "reset preserves purchase");
            profile.credit = 100000;
            manager.SetActiveProfile(profile);
            Click(popup.slots[attackIndex]);
            int maxLevel = manager.GetDefinition(PassiveSkillId.AttackPower).maxLevel;
            for (int level = 0; level < maxLevel; level++) Click(Control("levelUpButton"));
            Check(!Control("levelUpButton").interactable, "maximum level stops preview");
            Click(Control("confirmButton"));
            Check(manager.GetCurrentLevel(PassiveSkillId.AttackPower) == maxLevel, "maximum level purchase");
            var levelLabel = (TMPro.TMP_Text)new SerializedObject(popup.slots[attackIndex]).FindProperty("levelLabel").objectReferenceValue;
            Check(levelLabel.text == "M", "maximum badge");
            Check(DataManager.Instance.LoadSinglePlayerSlot().profile.passiveSkillTree.learnedSkills
                .Find(entry => entry.id == PassiveSkillId.AttackPower).currentLevel == maxLevel, "saved level round trip");
            SessionState.SetString("MirrorLatestPassiveValidatedProfile", JsonUtility.ToJson(profile));
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
            if (!originallyOpen && popup.gameObject.activeSelf) KY_PopupManager.Instance.Hide();
            PlayerPrefs.DeleteKey("KeyBindings." + keyProfile + ".A");
            PlayerPrefs.DeleteKey("KeyBindings." + keyProfile + ".B");
            PlayerPrefs.Save();
            KeyBindingService.ConfigureProfile("Mirror." + MirrorReconnectProfile_MirrorTest.GetProfileName());
            Time.timeScale = originalTimeScale;
            if (pauseObject != null) Object.DestroyImmediate(pauseObject);
            if (inputObject != null) Object.DestroyImmediate(inputObject);
        }
        string report = $"[MirrorKyUIValidation] PASS {checks} checks; original profile and storage restored";
        Directory.CreateDirectory("Temp/MirrorValidation");
        File.WriteAllText("Temp/MirrorValidation/PassiveUI.txt", report);
        Debug.Log(report);
    }
}

