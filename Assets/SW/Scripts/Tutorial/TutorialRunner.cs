using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core;
using ItemSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Condition = TutorialSequenceSO.Condition;

/// <summary>
/// 튜토리얼 씬 진행기. TutorialSequenceSO의 단계를 순서대로 오버레이에 표시하고, 완료 조건은 기존 이벤트로 판정한다.
/// 플레이어 생성·초기화는 씬의 YJ_PlayerSpawner와 YJ_StageManager(전투 없는 노드)가 그대로 맡고,
/// 이 컴포넌트는 그 준비가 끝난 뒤 시작한다. 진행 상태는 DataManager의 격리 런이라 저장에 남지 않으며,
/// 종료 시 프로필의 튜토리얼 완료 플래그만 저장한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialRunner : MonoBehaviour
{
    private const string UiLabelResourcePath = "DataFiles/UIData/3. GeneratedAssets/UILabelDatabase";

    [SerializeField] private TutorialSequenceSO sequence;
    [SerializeField] private KY_TutorialOverlay overlay;
    [Tooltip("일반 팝업이 열린 동안 안내창 전체를 가릴 그룹. 오버레이 바깥(튜토리얼 캔버스)에 둔다.")]
    [SerializeField] private CanvasGroup overlayGroup;
    [Tooltip("사이드 팝업(인벤토리 등)이 열린 동안 옮길 설명창.")]
    [SerializeField] private RectTransform guidePanel;
    [Tooltip("사이드 팝업이 열린 동안 설명창의 가로 위치. 사이드 팝업은 화면 중앙 기준 오른쪽(+260부터)에 붙으므로 그 왼쪽 빈 공간에 맞춘다.")]
    [SerializeField] private float sidePopupPanelX = -250f;
    [SerializeField] private YJ_StageManager stageManager;
    [SerializeField] private YJ_PlayerSpawner playerSpawner;
    [SerializeField] private WBH_EnemySpawnManager enemySpawnManager;
    [SerializeField] private KY_PausePopup pausePopup;
    [SerializeField] private TutorialZone[] zones = Array.Empty<TutorialZone>();
    [Tooltip("장비·포션을 플레이어 앞 몇 m에 떨어뜨릴지.")]
    [SerializeField, Min(0.5f)] private float dropDistance = 2.5f;
    [SerializeField, Min(0f)] private float reviveDelay = 1.5f;
    [Tooltip("설명창 머리글에 '튜토리얼 n / 전체'를 표시할 글자. 비워 두면 표시하지 않는다.")]
    [SerializeField] private TMP_Text progressText;
    [Tooltip("마지막 완료 안내에서는 숨길 설명창의 건너뛰기 버튼.")]
    [SerializeField] private GameObject skipButton;
    [Tooltip("마지막 완료 안내에서 '완료'로 바꿀 다음 버튼 글자.")]
    [SerializeField] private TMP_Text nextButtonLabel;
    [Tooltip("실제 적과 싸우는 웨이브가 시작되면 숨길 허수아비.")]
    [SerializeField] private GameObject trainingDummy;

    private readonly List<TutorialSequenceSO.Step> steps = new();
    private UILabelDatabaseSO labels;
    private T_PlayerController player;
    private WBH_PlayerStateMachine stateMachine;
    private PlayerHealthManager health;
    private PotionUseManager potions;
    private InventoryController inventory;
    private EquipmentSystem equipment;
    private float guidePanelX;
    private int stepIndex = -1;
    private int progress;
    private int potionChargesAtStart;
    private bool finished;
    private bool quitting;

    private TutorialSequenceSO.Step Current => stepIndex >= 0 && stepIndex < steps.Count ? steps[stepIndex] : null;
    private bool Is(Condition condition) => !finished && Current != null && Current.condition == condition;

    private void Awake()
    {
        // 진입 흐름이 격리 런을 열고 이 씬을 불러온다. 에디터에서 씬을 바로 실행하면 저장된 캐릭터로 연다.
        DataManager data = DataManager.Instance;
        if (!data.IsIsolatedRun && !(data.TryGetSavedCharacter(out CharacterClass saved) && data.BeginIsolatedRun(saved)))
        {
            Debug.LogError("[TutorialRunner] 격리 런을 시작하지 못해 튜토리얼을 중단합니다.", this);
            // 실제 런을 복원·저장하는 경로를 타지 않게 스테이지 초기화도 멈춘다.
            stageManager.enabled = false;
            enabled = false;
            return;
        }

        labels = Resources.Load<UILabelDatabaseSO>(UiLabelResourcePath);
    }

    private IEnumerator Start()
    {
        // YJ_StageManager가 플레이어 상태 복원(격리 런이라 새 게임)과 외형 준비, 입력 허용을 끝낼 때까지 기다린다.
        // 실패하면 StageManager가 이미 오류를 남긴다.
        while (!stageManager.IsGameplayReady)
            yield return null;

        player = playerSpawner.SpawnedPlayer;
        stateMachine = player.GetComponent<WBH_PlayerStateMachine>();
        health = player.GetComponent<PlayerHealthManager>();
        potions = player.GetComponent<PotionUseManager>();
        inventory = InventoryController.Instance;
        equipment = inventory.EquipmentSystem;

        DataManager.Instance.TryGetSavedCharacter(out CharacterClass character);
        foreach (TutorialSequenceSO.Step step in sequence.Steps)
        {
            if (step.Matches(character))
                steps.Add(step);
        }

        stateMachine.OnEnterState += HandleEnterState;
        inventory.OnItemAdded += HandleItemAdded;
        equipment.OnEquipmentChanged += HandleEquipmentChanged;
        enemySpawnManager.WaveCompleted += HandleWaveCompleted;
        overlay.NextRequested += HandleNext;
        overlay.Skipped += ConfirmSkip;
        foreach (TutorialZone zone in zones)
            zone.Entered += HandleZoneEntered;
        // 장착 단계처럼 인벤토리를 연 채 안내문을 읽어야 하므로, 사이드 팝업이 열리면 설명창을 그 왼쪽으로 비킨다.
        if (guidePanel != null)
            guidePanelX = guidePanel.anchoredPosition.x;
        KY_GameEvents.OnSidePopupOpened += HandleSidePopupOpened;
        KY_GameEvents.OnSidePopupClosed += HandleSidePopupClosed;

        // 일시정지의 포기·저장 후 종료는 실제 런을 정산·초기화하므로 튜토리얼 종료로 바꾼다.
        if (pausePopup != null)
            pausePopup.BindExitActions(ExitDialog(), ExitDialog());

        // ponytail: 튜토리얼 사망은 기존 부활 경로로 되살린다. 부활 횟수를 1 이상으로 유지해 사망 정산(YJ_PlayerDead)을 타지 않는다.
        player.reviveCount = 1;
        YJ_BgmPlayer.Instance?.Play(YJ_BgmPlayer.YJ_BgmType.Act1Bgm);
        BeginStep(0);
    }

    private void Update()
    {
        // 일반 팝업(일시정지·설정·휴식·확인창)이 열려 있으면 안내창을 숨긴다. 바깥 그룹만 가리므로 오버레이의 단계 상태는 그대로다.
        // 인벤토리 같은 사이드 팝업은 장착 안내를 보면서 써야 하므로 숨기지 않고, 캔버스 정렬 순서로 안내창 위에 그린다.
        if (overlayGroup != null)
        {
            KY_PopupManager popups = KY_PopupManager.Instance;
            bool visible = popups == null || !popups.HasOpenModalPopup;
            overlayGroup.alpha = visible ? 1f : 0f;
            overlayGroup.blocksRaycasts = visible;
        }

        if (Is(Condition.PotionUsed) && potions.CurrentCharges < potionChargesAtStart)
            BeginStep(stepIndex + 1);
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        // Finish 없이 씬을 벗어나도 격리 런이 남아 실제 런의 저장을 막지 않게 한다.
        if (!finished && !quitting)
            DataManager.Instance?.EndIsolatedRun();
        if (stateMachine != null) stateMachine.OnEnterState -= HandleEnterState;
        if (inventory != null) inventory.OnItemAdded -= HandleItemAdded;
        if (equipment != null) equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        if (enemySpawnManager != null) enemySpawnManager.WaveCompleted -= HandleWaveCompleted;
        if (overlay != null)
        {
            overlay.NextRequested -= HandleNext;
            overlay.Skipped -= ConfirmSkip;
        }
        foreach (TutorialZone zone in zones)
        {
            if (zone != null)
                zone.Entered -= HandleZoneEntered;
        }
        KY_GameEvents.OnSidePopupOpened -= HandleSidePopupOpened;
        KY_GameEvents.OnSidePopupClosed -= HandleSidePopupClosed;
    }

    private void HandleSidePopupOpened() => MoveGuidePanel(sidePopupPanelX);

    private void HandleSidePopupClosed() => MoveGuidePanel(guidePanelX);

    private void MoveGuidePanel(float x)
    {
        if (guidePanel != null)
            guidePanel.anchoredPosition = new Vector2(x, guidePanel.anchoredPosition.y);
    }

    private void BeginStep(int index)
    {
        stepIndex = index;
        progress = 0;
        TutorialSequenceSO.Step step = Current;
        if (step == null)
        {
            Finish();
            return;
        }

        Present(step);
        switch (step.condition)
        {
            case Condition.PotionUsed:
                // 체력이 가득하면 포션이 효과 대상이 없어 사용되지 않으므로 절반으로 낮춰 둔다.
                if (health.CurrentHealth >= health.MaxHealth)
                    health.SetCurrentHealth(health.MaxHealth * 0.5f);
                potionChargesAtStart = potions.CurrentCharges;
                break;
            case Condition.WaveCleared:
                if (trainingDummy != null)
                    trainingDummy.SetActive(false);
                if (!enemySpawnManager.TrySetWaves(new[] { step.wave }) || !enemySpawnManager.TrySpawnNextWave())
                    Debug.LogError("[TutorialRunner] 튜토리얼 웨이브를 생성하지 못했습니다.", this);
                break;
            case Condition.ItemAcquired:
                if (ItemSystemController.Instance == null ||
                    !ItemSystemController.Instance.TryDropGeneratedItem(step.item, DropPosition(), out _, out _))
                    Debug.LogError($"[TutorialRunner] 튜토리얼 장비를 드랍하지 못했습니다: {step.item}", this);
                break;
        }
    }

    /// <summary>
    /// 웨이브를 정리한 그 자리에서 바로 보이도록 플레이어 앞(진행 방향 +Z)에 떨어뜨린다.
    /// 다리 끝처럼 앞이 막혀 있으면 NavMesh 위 가장 가까운 지점을, 그마저 없으면 플레이어 위치를 쓴다.
    /// </summary>
    private Vector3 DropPosition()
    {
        Vector3 origin = player.transform.position;
        return UnityEngine.AI.NavMesh.SamplePosition(origin + Vector3.forward * dropDistance, out UnityEngine.AI.NavMeshHit hit, dropDistance, UnityEngine.AI.NavMesh.AllAreas)
            ? hit.position
            : origin;
    }

    private void Present(TutorialSequenceSO.Step step)
    {
        if (progressText != null)
            progressText.text = $"{Label("tutorial.progress", "튜토리얼")}  {stepIndex + 1} / {steps.Count}";
        // 마지막 단계는 다 끝낸 뒤라 건너뛸 것이 없으므로 건너뛰기를 숨기고 다음 버튼을 '완료'로 바꾼다.
        bool last = stepIndex == steps.Count - 1;
        if (skipButton != null)
            skipButton.SetActive(!last);
        if (nextButtonLabel != null)
            nextButtonLabel.text = last ? Label("tutorial.finish", "완료") : Label("tutorial.next", "다음");
        overlay.ShowExternalStep(Text(step), step.showDim, null, step.condition == Condition.Next, step.pause);
    }

    private void HandleNext()
    {
        if (Is(Condition.Next))
            BeginStep(stepIndex + 1);
    }

    private void HandleZoneEntered(TutorialZone zone)
    {
        if (Is(Condition.ZoneEnter) && zone.ZoneId == Current.zoneId)
            BeginStep(stepIndex + 1);
    }

    private void HandleEnterState(PlayerState state)
    {
        if (state == PlayerState.Dead)
            StartCoroutine(ReviveAfterDelay());
        else if (state == PlayerState.Revive)
            player.reviveCount = Mathf.Max(player.reviveCount, 1);
        else if (Is(Condition.StateEntered) && state == Current.state && ++progress >= Current.count)
            BeginStep(stepIndex + 1);
    }

    private IEnumerator ReviveAfterDelay()
    {
        yield return new WaitForSeconds(reviveDelay);
        // 부활 패시브가 이미 되살렸으면 사망 상태가 아니라서 아무것도 하지 않는다.
        player.TryBeginPassiveRevive(1f);
    }

    private void HandleWaveCompleted()
    {
        if (Is(Condition.WaveCleared))
            BeginStep(stepIndex + 1);
    }

    private void HandleItemAdded(InventoryItem item)
    {
        if (Is(Condition.ItemAcquired) && item.itemData.definition == Current.item)
            BeginStep(stepIndex + 1);
    }

    private void HandleEquipmentChanged(EquippedItemInfo[] equipped)
    {
        if (Is(Condition.ItemEquipped) && Array.Exists(equipped, info => info.itemId == Current.item.itemId))
            BeginStep(stepIndex + 1);
    }

    private void ConfirmSkip()
    {
        if (KY_PopupManager.Instance == null)
        {
            Finish();
            return;
        }

        KY_DialogData dialog = ExitDialog();
        dialog.onNo = () => { if (Current != null && !finished) Present(Current); };
        KY_PopupManager.Instance.ShowConfirm(dialog);
    }

    private KY_DialogData ExitDialog() => new()
    {
        title = Label("tutorial.exit.title", "튜토리얼 종료"),
        message = Label("tutorial.exit.message", "튜토리얼을 마치고 나가시겠습니까?"),
        onYes = Finish,
    };

    /// <summary>완료 플래그를 저장하고 격리 런을 끝낸 뒤 진입 경로의 다음 씬으로 이동한다. 완료와 건너뛰기가 같은 경로를 쓴다.</summary>
    private void Finish()
    {
        if (finished)
            return;
        SceneLoader loader = SceneLoader.Instance;
        if (loader == null || loader.IsLoading)
        {
            // 씬을 옮기지 못하면 격리 런을 유지한 채 튜토리얼에 남는다.
            Debug.LogError("[TutorialRunner] SceneLoader를 사용할 수 없어 튜토리얼을 종료하지 못했습니다.", this);
            return;
        }
        finished = true;
        overlay.Close();

        DataManager data = DataManager.Instance;
        PlayerProfileData profile = PassiveSkillManager.Instance != null ? PassiveSkillManager.Instance.CurrentProfile : null;
        if (profile != null && !profile.tutorialCompleted)
        {
            profile.tutorialCompleted = true;
            try
            {
                data.SavePassiveData();
            }
            catch (Exception exception)
            {
                // 저장에 실패해도 다음 실행에서 튜토리얼이 다시 열릴 뿐이므로 종료는 계속한다.
                Debug.LogError($"[TutorialRunner] 튜토리얼 완료 저장 실패: {exception.Message}", this);
            }
        }

        data.EndIsolatedRun();
        TutorialEntry.Exit(loader);
    }

    private string Text(TutorialSequenceSO.Step step)
    {
        // ponytail: 공용 TMP Settings(외부 에셋)가 한글을 글자 단위로 줄바꿈하므로, 이 설명창만 한글이 든 어절을 <nobr>로 묶어
        // 띄어쓰기에서 줄을 바꾼다. 공백이 없는 일본어·중국어는 묶지 않아 글자 단위 줄바꿈을 유지한다.
        // 프로젝트 전체가 어절 줄바꿈을 원하면 TMP Settings의 Modern Hangul 옵션으로 옮긴다.
        string text = Regex.Replace(Label(step.labelKey, step.fallbackText), @"\S*[가-힣]\S*", "<nobr>$0</nobr>");
        foreach (InputAction action in KeyBindingService.InputActions.Player.Get().actions)
            text = text.Replace("{" + action.name + "}", $"<color=#5CD6FF>[{KY_KeyTextUtil.GetKeyText(action)}]</color>");
        return text;
    }

    private string Label(string key, string fallback)
    {
        string value = labels != null && !string.IsNullOrEmpty(key) ? labels.GetLabel(key) : null;
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }
}
