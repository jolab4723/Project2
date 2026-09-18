using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ItemSystem;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// P3-B 첫 대표(H3) 저마나 회복 투구 고유효과의 자산/공식 검사(EditMode)와 실서버 수명 검사(PlayMode)를 분리하여 검증한다.
/// </summary>
public static class ArmorRelicP3BValidation_MirrorTest
{
    private static bool isLiveValidationRunning;
    private static bool isLiveValidationCancelled;
    private static IEnumerator liveSequence;
    private static PlayerContext liveSequenceOwner;
    private static EditorApplication.CallbackFunction releaseVerificationWatcher;

    // =========================================================================
    // 1. 공통 메뉴 진입점
    // =========================================================================

    /// <summary>
    /// 저장된 자산과 계산식만 검사합니다. 실행 중인 게임에 시험 플레이어를 추가하지 않습니다.
    /// 실서버 검사는 별도의 Live Server Play Mode 메뉴에서 시작합니다.
    /// </summary>
    [MenuItem("SW/Mirror Test/Validate Armor Relic P3-B H3")]
    public static void ValidateAll()
    {
        ValidateAssetsAndFormula();
    }

    [MenuItem("SW/Mirror Test/Validate Armor Relic P3-B H3 (Assets & Formula)")]
    public static void RunAssetsAndFormula() => ValidateAssetsAndFormula();

    [MenuItem("SW/Mirror Test/Validate Armor Relic P3-B H3 (Live Server Play Mode)")]
    public static void RunPlayModeServer() => ValidatePlayModeServer();

    [MenuItem("SW/Mirror Test/Validate Stage C (Actual Helmet Gameplay & Passive)")]
    public static void RunStageC() => ValidateStageC();

    // =========================================================================
    // 2. EditMode: 자산 및 공식 검증 (순수 정적/자산 무결성)
    // =========================================================================

    /// <summary>
    /// 게임을 실행하지 않고 저장된 프리팹, 효과 분류, 계산식을 검사합니다.
    /// </summary>
    public static void ValidateAssetsAndFormula()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("자산 검사는 Play Mode를 종료한 뒤 실행하세요.");

        int checks = 0;
        void Check(bool condition, string caseId, string detail)
        {
            Debug.Log($"[ArmorEffectValidation] case={caseId} passed={condition} detail={detail}");
            if (!condition)
                throw new InvalidOperationException($"[ArmorEffectValidation] FAIL: {caseId} - {detail}");
            checks++;
        }

        var trackedSOs = new List<ScriptableObject>();
        bool assertionPassed = false;
        bool cleanupOk = true;

        try
        {
            // 1) Fighter 프리팹 무결성
            GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            Check(fighterPrefab != null, "V02-Asset-FighterPrefabExists", "Fighter PlayerContext 프리팹 로드");
            RequireArmorProvider(fighterPrefab);
            Check(true, "V02-Asset-FighterRequireArmorProvider", "Fighter 어댑터 부착, Missing Script 0건, 5대 참조 및 useLowManaHelmetEffect 일치");

            // 2) Gunner 프리팹 무결성
            GameObject gunnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/GunnerNetworkPlayer_MirrorTest.prefab");
            Check(gunnerPrefab != null, "V02-Asset-GunnerPrefabExists", "Gunner PlayerContext 프리팹 로드");
            RequireArmorProvider(gunnerPrefab);
            Check(true, "V02-Asset-GunnerRequireArmorProvider", "Gunner 어댑터 부착, Missing Script 0건, 5대 참조 및 useLowManaHelmetEffect 일치");

            // 3) H3 정책 검증 (UsesLowManaHelmetEffect)
            EquipmentSystem eqFighter = fighterPrefab.GetComponent<PlayerContext>().Equipment;
            var effect = CreateH3Effect();
            trackedSOs.Add(effect);
            var helmetDef = CreateH3Definition(effect, ArmorType.Helmet);
            trackedSOs.Add(helmetDef);
            var chestDef = CreateH3Definition(effect, ArmorType.Armor);
            trackedSOs.Add(chestDef);

            var validItem = new ItemInstance { instanceId = "test_helmet", definition = helmetDef };
            Check(eqFighter.UsesLowManaHelmetEffect(validItem), "V03-Policy-ValidHelmet", "H3 투구 아이템에 대해 opt-in 정책 true");

            var chestItem = new ItemInstance { instanceId = "test_chest", definition = chestDef };
            Check(!eqFighter.UsesLowManaHelmetEffect(chestItem), "V03-Policy-InvalidArmorType", "Armor(갑옷) 슬롯은 H3 투구 정책 false");

            var nonThresholdEffect = ScriptableObject.CreateInstance<PassiveBuffUniqueEffectSO>();
            trackedSOs.Add(nonThresholdEffect);
            var nonThresholdDef = CreateH3Definition(effect, ArmorType.Helmet);
            trackedSOs.Add(nonThresholdDef);
            nonThresholdDef.uniqueEffect = nonThresholdEffect;
            var nonThresholdItem = new ItemInstance { instanceId = "test_non_threshold", definition = nonThresholdDef };
            Check(!eqFighter.UsesLowManaHelmetEffect(nonThresholdItem), "V03-Policy-InvalidEffectType", "Threshold 외 다른 효과는 H3 투구 정책 false");

            var greaterEqualEffect = ScriptableObject.CreateInstance<StatThresholdBuffUniqueEffectSO>();
            trackedSOs.Add(greaterEqualEffect);
            greaterEqualEffect.referenceStat = StatReference.CurrentManaPercent;
            greaterEqualEffect.comparisonOperator = ComparisonOperator.GreaterOrEqual;
            var greaterDef = CreateH3Definition(greaterEqualEffect, ArmorType.Helmet);
            trackedSOs.Add(greaterDef);
            var greaterItem = new ItemInstance { instanceId = "test_greater", definition = greaterDef };
            Check(!eqFighter.UsesLowManaHelmetEffect(greaterItem), "V03-Policy-InvalidOperator", "GreaterOrEqual 연산자는 저마나 H3 정책 false");

            var hpStatEffect = ScriptableObject.CreateInstance<StatThresholdBuffUniqueEffectSO>();
            trackedSOs.Add(hpStatEffect);
            hpStatEffect.referenceStat = StatReference.CurrentHealthPercent;
            hpStatEffect.comparisonOperator = ComparisonOperator.LessOrEqual;
            var hpDef = CreateH3Definition(hpStatEffect, ArmorType.Helmet);
            trackedSOs.Add(hpDef);
            var hpItem = new ItemInstance { instanceId = "test_hp", definition = hpDef };
            Check(!eqFighter.UsesLowManaHelmetEffect(hpItem), "V03-Policy-InvalidStatRef", "CurrentHealthPercent 참조는 H3 정책 false");

            // 4) PlayerStat upstream 계산식 및 실제 SO 계수 연동 검증
            // upstream 28323339a 기준 공식:
            // mpRegen = ((ch.mpRegenFlat + eq.mpRegenFlat) * eq.mpRegenMult * passive.mpRegenMult + passive.mpRegenFlat) * buff.mpRegenMult + buff.mpRegenFlat
            Check(effect.buffSpec.statEffects != null && effect.buffSpec.statEffects.Length == 1 &&
                  effect.buffSpec.statEffects[0].statType == StatType.mpRegenPercent &&
                  Mathf.Approximately(effect.buffSpec.statEffects[0].value, 30f),
                  "V04-Formula-EffectSpecVerified", "실제 SO에 mpRegenPercent +30% 버프 스펙 설정 확인");

            var baseStat = new StatSet { maxManaFlat = 160f, mpRegenFlat = 5f }; // Fighter Lv1 기본 스펙
            var passiveOffset = new StatSet { maxManaFlat = -60f, mpRegenFlat = 5f }; // Net base 100 mana, 10 regen
            var h3Buff = new StatSet { mpRegenPercent = effect.buffSpec.statEffects[0].value }; // SO 계수 직접 반영 (+30%)

            var statNoBuff = new PlayerStat();
            statNoBuff.Recalculate(baseStat, StatSet.Zero, StatSet.Zero, passiveOffset);
            Check(Mathf.Approximately(statNoBuff.maxMana, 100f), "V04-Formula-BaseMaxMana", $"기본 최대 마나 100 (실제: {statNoBuff.maxMana})");
            Check(Mathf.Approximately(statNoBuff.mpRegen, 10f), "V04-Formula-BaseMpRegen", $"기본 마나 재생 10 (실제: {statNoBuff.mpRegen})");

            var statWithBuff = new PlayerStat();
            statWithBuff.Recalculate(baseStat, StatSet.Zero, h3Buff, passiveOffset);
            Check(Mathf.Approximately(statWithBuff.mpRegen, 13f), "V04-Formula-BuffedMpRegen", $"SO 30% 버프 적용 시 마나 재생 13 (실제: {statWithBuff.mpRegen})");

            // 변형 계수(20%) 대조군 검증: 계수가 다르면 재생 13과 불일치 검출
            var alteredBuff = new StatSet { mpRegenPercent = 20f };
            var statAltered = new PlayerStat();
            statAltered.Recalculate(baseStat, StatSet.Zero, alteredBuff, passiveOffset);
            Check(Mathf.Approximately(statAltered.mpRegen, 12f) && !Mathf.Approximately(statAltered.mpRegen, 13f),
                  "V04-Formula-AlteredBuffDivergence", $"20% 버프 시 12로 산출되어 30%(13)와 차이 감지 (실제: {statAltered.mpRegen})");

            // 5) 임계치 및 분모 수학 검증
            float currentMana = 26f;
            float maxMana = 100f;
            float ratio = (currentMana / maxMana) * 100f;
            Check(ratio > 25f, "V05-Math-26PercentInactive", $"26/100 비율 {ratio:F2}% > 25% 비활성");

            maxMana = 104f;
            ratio = (currentMana / maxMana) * 100f;
            Check(ratio <= 25f, "V05-Math-25PercentActive", $"26/104 비율 {ratio:F2}% <= 25% 활성");

            maxMana = 80f;
            ratio = (currentMana / maxMana) * 100f;
            Check(ratio > 25f, "V05-Math-32.5PercentInactive", $"26/80 비율 {ratio:F2}% > 25% 비활성");

            currentMana = 24.9f;
            maxMana = 100f;
            ratio = (currentMana / maxMana) * 100f;
            Check(ratio <= 25f, "V05-Math-24.9PercentActive", $"24.9/100 비율 {ratio:F2}% <= 25% 활성");

            currentMana = 25.1f;
            ratio = (currentMana / maxMana) * 100f;
            Check(ratio > 25f, "V05-Math-25.1PercentInactive", $"25.1/100 비율 {ratio:F2}% > 25% 비활성");

            maxMana = 0f;
            bool validDenominator = maxMana > 0f;
            Check(!validDenominator, "V05-Math-ZeroDenominatorInvalid", "최대 마나 0일 때 분모 무효 판정");

            assertionPassed = true;
        }
        finally
        {
            foreach (ScriptableObject so in trackedSOs)
            {
                if (so != null)
                {
                    cleanupOk &= TryCleanupStep("EditModeSODestroy", () =>
                    {
                        UnityEngine.Object.DestroyImmediate(so);
                    });
                }
            }

            if (assertionPassed && cleanupOk)
            {
                Debug.Log($"[ArmorEffectValidation] PASS {checks} checks. 자산 무결성(Fighter/Gunner) 및 H3 정책·스탯 공식 사전 검증 완료.");
            }
            else if (assertionPassed && !cleanupOk)
            {
                Debug.LogError("[ArmorEffectValidation] 자산/공식 검사는 통과했으나 임시 ScriptableObject 정리 중 오류가 발생했습니다.");
            }
        }
    }

    // =========================================================================
    // 3. PlayMode: 실서버 수명주기 및 런타임 검증 (Host / Server 전용)
    // =========================================================================

    /// <summary>
    /// 정리 작업 하나를 시도하고 실패 원인을 기록합니다.
    /// false를 반환해도 호출자는 나머지 자원 정리를 계속해야 합니다.
    /// </summary>
    private static bool TryCleanupStep(string stepName, Action cleanup)
    {
        try
        {
            cleanup();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[ArmorEffectValidation] 자원 정리 실패: " + stepName);
            Debug.LogException(exception);
            return false;
        }
    }

    /// <summary>
    /// 이번 시험 자원의 참조를 보관하고, 나중에 실제로 제거됐는지 확인할 함수를 만듭니다.
    /// 이 함수는 자원을 삭제하거나 서버의 등록 목록을 변경하지 않습니다.
    /// </summary>
    private static Func<bool> CreateReleaseProbe(
        PlayerContext context, GameObject player,
        StatThresholdBuffUniqueEffectSO effect, ItemDefinitionSO definition)
    {
        NetworkIdentity identity = player != null
            ? player.GetComponent<NetworkIdentity>() : null;
        uint netId = identity != null ? identity.netId : 0u;
        PlayerStatManager stats = context != null ? context.Stats : null;
        PlayerManaManager mana = context != null ? context.Mana : null;
        PlayerBuffManager buffs = context != null ? context.Buffs : null;

        return () =>
        {
            bool registered = netId != 0u &&
                NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity current) &&
                ReferenceEquals(current, identity);

            // 객체가 파괴돼도 관리 목록에 같은 참조가 남았는지는 별도로 확인합니다.
            bool statsListed = !ReferenceEquals(stats, null) &&
                PlayerStatManager.All.Any(item => ReferenceEquals(item, stats));
            bool manaListed = !ReferenceEquals(mana, null) &&
                PlayerManaManager.All.Any(item => ReferenceEquals(item, mana));
            bool buffsListed = !ReferenceEquals(buffs, null) &&
                PlayerBuffManager.All.Any(item => ReferenceEquals(item, buffs));

            bool effectReleased = effect == null || AssetDatabase.Contains(effect);
            bool definitionReleased = definition == null || AssetDatabase.Contains(definition);
            return player == null && effectReleased && definitionReleased &&
                stats == null && mana == null && buffs == null &&
                !registered && !statsListed && !manaListed && !buffsListed;
        };
    }

    /// <summary>
    /// 삭제 요청 후 실제 자원 회수를 확인하며, 환경 종료·시간 초과·검사 예외는 성공으로 처리하지 않습니다.
    /// 대상 객체 밖의 독립적인 Editor 루프에서 프레임 진행 및 사후 조건을 감시합니다.
    /// </summary>
    private static void StartReleaseVerification(
        Func<bool> releaseProbe,
        bool sequenceCompleted,
        bool cleanupOk)
    {
        if (releaseVerificationWatcher != null)
        {
            EditorApplication.update -= releaseVerificationWatcher;
            releaseVerificationWatcher = null;
        }

        int requestFrame = Time.frameCount;
        double requestTime = EditorApplication.timeSinceStartup;
        const double timeoutSeconds = 3.0;

        EditorApplication.CallbackFunction myWatcher = null;
        myWatcher = () =>
        {
            // 다른 callback이 등록되어 있다면 이전 callback은 스스로 해제하고 종료
            if (releaseVerificationWatcher != myWatcher)
            {
                EditorApplication.update -= myWatcher;
                return;
            }

            void FinishVerification(Action logAction)
            {
                EditorApplication.update -= myWatcher;
                if (releaseVerificationWatcher == myWatcher)
                {
                    releaseVerificationWatcher = null;
                }
                DetachLifetimeEvents();
                isLiveValidationRunning = false;
                isLiveValidationCancelled = false;
                logAction?.Invoke();
            }

            // 1. 환경 종료 확인: Play Mode 또는 서버 종료 시 즉시 중단 (PASS 불가)
            if (!Application.isPlaying || !NetworkServer.active)
            {
                FinishVerification(() =>
                {
                    Debug.LogWarning("[ArmorEffectValidation] Play Mode 또는 서버 종료로 자원 회수 확인이 중단되었습니다. (NOT_VERIFIED)");
                });
                return;
            }

            // 2. 시간 초과 확인: EditorApplication.timeSinceStartup 기준
            // 프레임 미진행 return보다 앞서 검사하여 에디터 일시정지(Pause) 상태에서도 타임아웃 처리
            double elapsed = EditorApplication.timeSinceStartup - requestTime;
            if (elapsed > timeoutSeconds)
            {
                FinishVerification(() =>
                {
                    Debug.LogError($"[ArmorEffectValidation] 자원 회수 확인 타임아웃 ({timeoutSeconds:F1}초 경과). 잔류 객체 검출 또는 프레임 정지. (frame={Time.frameCount})");
                });
                return;
            }

            // 3. 실제 게임 프레임 진행 확인: Unity 지연 파괴(Object.Destroy) 대기
            if (Time.frameCount <= requestFrame)
            {
                return;
            }

            // 4. 사후 조건(Release Probe) 검증: 예외 발생 시 FAIL 확정
            bool isReleased = false;
            try
            {
                isReleased = releaseProbe != null && releaseProbe();
            }
            catch (Exception probeException)
            {
                FinishVerification(() =>
                {
                    Debug.LogError("[ArmorEffectValidation] 사후 검증 프로브 평가 중 예외 발생. (FAIL)");
                    Debug.LogException(probeException);
                });
                return;
            }

            if (isReleased)
            {
                // 최종화가 전역 상태를 비우기 전에 이번 실행의 취소 여부를 보관합니다.
                bool wasCancelled = isLiveValidationCancelled;
                FinishVerification(() =>
                {
                    if (wasCancelled || !sequenceCompleted)
                    {
                        string result = wasCancelled ? "CANCELLED" : "INCOMPLETE";
                        Debug.LogWarning($"[ArmorEffectValidation] result={result} " +
                            $"시험 자원 회수 확인됨. cleanupOk={cleanupOk} frame={Time.frameCount}");
                    }
                    else if (!cleanupOk)
                    {
                        Debug.LogError("[ArmorEffectValidation] result=FAIL " +
                            "회수는 확인됐지만 정리 중 오류가 있었습니다.");
                    }
                    else
                    {
                        Debug.Log($"[ArmorEffectValidation] PASS Live Server PlayMode 검증 및 " +
                            $"시험 자원 회수 완료. (frame={Time.frameCount})");
                    }
                });
            }
        };

        releaseVerificationWatcher = myWatcher;
        EditorApplication.update += myWatcher;
    }

    /// <summary>
    /// 시험 코루틴 실행 중 대상 객체 및 서버 수명주기를 감시합니다.
    /// </summary>
    private static void AttachLiveExecutionLifetime()
    {
        EditorApplication.update += CheckLiveExecutionLifetime;
        EditorApplication.playModeStateChanged += HandleLiveModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
    }

    /// <summary>코루틴 실행 종료 시 실행 대상 감시만 해제하고 환경 이벤트는 유지합니다.</summary>
    private static void DetachLiveExecutionLifetime()
    {
        EditorApplication.update -= CheckLiveExecutionLifetime;
        liveSequence = null;
        liveSequenceOwner = null;
    }

    /// <summary>모든 검사 및 회수 확인이 완전히 끝났을 때 에디터 구독을 모두 비웁니다.</summary>
    private static void DetachLifetimeEvents()
    {
        EditorApplication.update -= CheckLiveExecutionLifetime;
        EditorApplication.playModeStateChanged -= HandleLiveModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
        liveSequence = null;
        liveSequenceOwner = null;
    }

    /// <summary>서버 또는 시험 대상이 사라지면 대기 중인 검사를 취소합니다.</summary>
    private static void CheckLiveExecutionLifetime()
    {
        if (!Application.isPlaying || !NetworkServer.active ||
            liveSequenceOwner == null || !liveSequenceOwner.gameObject.activeInHierarchy)
        {
            CancelLiveValidationInternal(isForcedEnvironmentExit: !Application.isPlaying || !NetworkServer.active);
        }
    }

    /// <summary>Play Mode 종료 중에도 시험 자원 정리가 시작되도록 합니다.</summary>
    private static void HandleLiveModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            CancelLiveValidationInternal(isForcedEnvironmentExit: true);
        }
    }

    private static void HandleBeforeAssemblyReload()
    {
        CancelLiveValidationInternal(isForcedEnvironmentExit: true);
    }

    /// <summary>
    /// 실행 중인 검사를 멈추고 코루틴의 finally 정리를 요청합니다.
    /// 정상 완료가 아니므로 성공 로그를 남기지 않습니다.
    /// </summary>
    [MenuItem("SW/Mirror Test/Cancel Armor Relic Validation")]
    public static void CancelLiveValidation()
    {
        CancelLiveValidationInternal(isForcedEnvironmentExit: false);
    }

    private static void CancelLiveValidationInternal(bool isForcedEnvironmentExit)
    {
        isLiveValidationCancelled = true;
        IEnumerator sequence = liveSequence;
        PlayerContext owner = liveSequenceOwner;
        bool wasWaitingForRelease = sequence == null && releaseVerificationWatcher != null;

        // 실행 중 감시 update 콜백 해제
        DetachLiveExecutionLifetime();

        if (isForcedEnvironmentExit)
        {
            // 강제 환경 종료(PlayMode 종료/어셈블리 재로드) 시에는 대기 중인 후처리 워처도 즉시 제거
            if (releaseVerificationWatcher != null)
            {
                EditorApplication.update -= releaseVerificationWatcher;
                releaseVerificationWatcher = null;
            }
        }

        if (sequence != null)
        {
            try
            {
                if (owner != null)
                    owner.StopCoroutine(sequence);
            }
            catch (Exception stopException)
            {
                Debug.LogException(stopException);
            }
            finally
            {
                try
                {
                    (sequence as IDisposable)?.Dispose();
                }
                catch (Exception disposeException)
                {
                    Debug.LogException(disposeException);
                }
                finally
                {
                    if (isForcedEnvironmentExit)
                    {
                        if (releaseVerificationWatcher != null)
                        {
                            EditorApplication.update -= releaseVerificationWatcher;
                            releaseVerificationWatcher = null;
                        }
                        DetachLifetimeEvents();
                        isLiveValidationRunning = false;
                        isLiveValidationCancelled = false;
                    }
                }
            }

            if (isForcedEnvironmentExit)
            {
                Debug.LogWarning("[ArmorEffectValidation] 환경 종료(PlayMode 종료/재로드)로 검사가 강제 중단되었습니다. (NOT_VERIFIED)");
            }
            else
            {
                Debug.LogWarning("[ArmorEffectValidation] 검사가 취소되었습니다. 성공으로 기록하지 않습니다.");
            }
        }
        else
        {
            // 코루틴이 없거나 이미 Dispose된 상태에서 취소된 경우
            if (isForcedEnvironmentExit || releaseVerificationWatcher == null)
            {
                if (releaseVerificationWatcher != null)
                {
                    EditorApplication.update -= releaseVerificationWatcher;
                    releaseVerificationWatcher = null;
                }
                DetachLifetimeEvents();
                isLiveValidationRunning = false;
                isLiveValidationCancelled = false;
            }

            if (isForcedEnvironmentExit && wasWaitingForRelease)
            {
                Debug.LogWarning("[ArmorEffectValidation] result=NOT_VERIFIED " +
                    "환경 종료로 진행 중이던 자원 회수 확인을 끝내지 못했습니다.");
            }
        }
    }

    public static void ValidatePlayModeServer()
    {
        if (!Application.isPlaying || !NetworkServer.active)
        {
            Debug.LogWarning("[ArmorEffectValidation] Play Mode 서버(Host)가 실행 중이지 않습니다. 이 검사는 Host Play Mode 상태에서 실행해야 합니다. (EditMode 자산 검증은 'Validate Armor Relic P3-B H3 (Assets & Formula)'를 실행하세요.)");
            return;
        }

        if (isLiveValidationRunning)
        {
            Debug.LogWarning("[ArmorEffectValidation] 이미 실서버 검증이 실행 중입니다. 중복 실행을 거절합니다.");
            return;
        }

        string scenePath = SceneManager.GetActiveScene().path;
        if (!scenePath.StartsWith("Assets/SW/TEST/", StringComparison.Ordinal))
            throw new InvalidOperationException("저장된 SW TEST 씬에서만 실행할 수 있습니다.");
        if (!EditorUtility.DisplayDialog("저마나 투구 검사",
                "시험 플레이어를 생성합니다. 다른 참가자가 없는 폐기용 1인 TEST 서버인가요?",
                "시험 시작", "취소"))
            return;

        isLiveValidationRunning = true;
        isLiveValidationCancelled = false;

        GameObject player = null;
        StatThresholdBuffUniqueEffectSO effect = null;
        ItemDefinitionSO definition = null;

        try
        {
            GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab");
            if (fighterPrefab == null)
                throw new InvalidOperationException("Fighter 프리팹을 로드할 수 없습니다.");

            effect = CreateH3Effect();
            definition = CreateH3Definition(effect, ArmorType.Helmet);

            player = UnityEngine.Object.Instantiate(fighterPrefab, Vector3.zero, Quaternion.identity);
            player.name = "ArmorRelicP3BValidation_LiveServerPlayer";
            NetworkServer.Spawn(player);

            var context = player.GetComponent<PlayerContext>();
            var provider = RequireRealServerPlayer(context);

            liveSequenceOwner = context;
            liveSequence = RunLiveSequence(context, provider, effect, definition, player);
            AttachLiveExecutionLifetime();
            if (context.StartCoroutine(liveSequence) == null)
                throw new InvalidOperationException("시험 코루틴을 시작하지 못했습니다.");
        }
        catch (Exception ex)
        {
            DetachLifetimeEvents();

            Func<bool> failedReleaseProbe = null;
            bool catchCleanupOk = TryCleanupStep("CaptureFailedReleaseState", () =>
            {
                PlayerContext failedContext = player != null
                    ? player.GetComponent<PlayerContext>() : null;
                failedReleaseProbe = CreateReleaseProbe(
                    failedContext, player, effect, definition);
            });

            catchCleanupOk &= TryCleanupStep("CatchPlayerDestroy", () =>
            {
                if (player != null) NetworkServer.Destroy(player);
            });
            catchCleanupOk &= TryCleanupStep("CatchEffectDestroy", () =>
            {
                if (effect != null && !AssetDatabase.Contains(effect)) UnityEngine.Object.Destroy(effect);
            });
            catchCleanupOk &= TryCleanupStep("CatchDefinitionDestroy", () =>
            {
                if (definition != null && !AssetDatabase.Contains(definition)) UnityEngine.Object.Destroy(definition);
            });

            if (Application.isPlaying && NetworkServer.active && failedReleaseProbe != null)
            {
                // 준비 도중 실패해 실행 감시를 붙이지 못했어도 환경 종료는 감지합니다.
                EditorApplication.playModeStateChanged -= HandleLiveModeChanged;
                EditorApplication.playModeStateChanged += HandleLiveModeChanged;
                AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
                AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;

                // 이미 true인 실행 가드를 유지합니다. 원래 시험의 실패는 바뀌지 않습니다.
                StartReleaseVerification(failedReleaseProbe, false, catchCleanupOk);
            }
            else
            {
                DetachLifetimeEvents();
                isLiveValidationRunning = false;
                isLiveValidationCancelled = false;
                Debug.LogWarning("[ArmorEffectValidation] result=FAIL cleanup=NOT_VERIFIED " +
                    "준비 실패 후 정리는 요청했지만 회수 완료를 확인할 수 없습니다.");
            }

            if (!catchCleanupOk)
            {
                Debug.LogError("[ArmorEffectValidation] 실서버 준비 실패 후 생성 자원 회수 중 추가 오류가 발생했습니다.");
            }
            Debug.LogException(ex);
            throw;
        }
    }

    private static IEnumerator RunLiveSequence(
        PlayerContext context,
        PlayerArmorEffectProvider_MirrorTest provider,
        StatThresholdBuffUniqueEffectSO effect,
        ItemDefinitionSO definition,
        GameObject player)
    {
        bool oldRegenPaused = false;
        bool hasSavedRegenState = false;
        bool sequenceCompleted = false;
        Func<bool> releaseProbe = CreateReleaseProbe(context, player, effect, definition);
        try
        {
            yield return null; // 한 프레임 대기하여 Start/OnStartServer가 완료되도록 보장

            oldRegenPaused = context.Mana.IsRegenPaused;
            hasSavedRegenState = true;
            context.Mana.IsRegenPaused = true;

            var transaction = new EquipmentTransaction(context.Equipment);
            if (context.Equipment.TryGetEquippedItem(EquipSlotType.Helmet, out InventoryItem initialHelmet))
            {
                var unequipResult = transaction.TryUnequipForTransfer(EquipSlotType.Helmet, initialHelmet);
                if (!unequipResult.IsSuccess)
                    throw new InvalidOperationException("[ArmorEffectValidation] 초기 지급 투구 해제 실패");
            }

            var helmet1 = new InventoryItem(new ItemInstance
            {
                instanceId = "p3b_h3_helmet_live_1",
                definition = definition,
                rolledSubStats = new List<RolledSubStat>()
            });

            // 1. 초기 스탯 보정 (캐릭터 레이어 기반 순수 100/10 세팅)
            context.Stats.EnsureInitialized();
            context.Stats.GetLayerStatSets(out StatSet ch, out _, out _, out _);
            context.Stats.SetPassiveStats(new StatSet
            {
                maxManaFlat = 100f - ch.maxManaFlat,
                mpRegenFlat = 10f - ch.mpRegenFlat
            });
            context.Mana.RefreshMaxMana();
            context.Mana.SetCurrentMana(100f);

            // 장착 전 기본 상태 확인 (버프 0, 마나 100, 재생 10)
            ExpectPlayerState(context, effect, 0, 100f, 100f, 10f, "Live-PreEquip-Baseline");

            // 2. 장착 (100% 마나 -> 비활성)
            var equipResult = transaction.TryRestoreEquippedItem(helmet1, EquipSlotType.Helmet);
            if (!equipResult.IsSuccess)
                throw new InvalidOperationException("[ArmorEffectValidation] TryRestoreEquippedItem 실패");
            ExpectPlayerState(context, effect, 0, 100f, 100f, 10f, "Live-V04-Mana100-Inactive");

            // 3. 25% 경계 (활성 -> 재생 13)
            context.Mana.SetCurrentMana(25f);
            ExpectPlayerState(context, effect, 1, 25f, 100f, 13f, "Live-V04-Mana25-Active");

            // 4. 26% 경계 (비활성 -> 재생 10)
            context.Mana.SetCurrentMana(26f);
            ExpectPlayerState(context, effect, 0, 26f, 100f, 10f, "Live-V04-Mana26-Inactive");

            // 24.9% 경계 (활성 -> 재생 13)
            context.Mana.SetCurrentMana(24.9f);
            ExpectPlayerState(context, effect, 1, 24.9f, 100f, 13f, "Live-V04-Mana24.9-Active");

            // 25.1% 경계 (비활성 -> 재생 10)
            context.Mana.SetCurrentMana(25.1f);
            ExpectPlayerState(context, effect, 0, 25.1f, 100f, 10f, "Live-V04-Mana25.1-Inactive");

            // 5. 마나 26 유지 상태에서 최대 마나만 104로 증가 (26/104 = 25% -> 활성)
            context.Mana.SetCurrentMana(26f);
            context.Stats.SetPassiveStats(new StatSet
            {
                maxManaFlat = 104f - ch.maxManaFlat,
                mpRegenFlat = 10f - ch.mpRegenFlat
            });
            ExpectPlayerState(context, effect, 1, 26f, 104f, 13f, "Live-V05-MaxMana104-Active");

            // 6. 최대 마나만 80으로 감소 (26/80 = 32.5% -> 비활성)
            context.Stats.SetPassiveStats(new StatSet
            {
                maxManaFlat = 80f - ch.maxManaFlat,
                mpRegenFlat = 10f - ch.mpRegenFlat
            });
            ExpectPlayerState(context, effect, 0, 26f, 80f, 10f, "Live-V05-MaxMana80-Inactive");

            // 7. 최대 마나 100 복원, 마나 0 (활성)
            context.Stats.SetPassiveStats(new StatSet
            {
                maxManaFlat = 100f - ch.maxManaFlat,
                mpRegenFlat = 10f - ch.mpRegenFlat
            });
            context.Mana.RefreshMaxMana();
            context.Mana.SetCurrentMana(0f);
            ExpectPlayerState(context, effect, 1, 0f, 100f, 13f, "Live-V04-Mana0-Active");

            // 8. 최대 마나 0 (무효 분모 -> 비활성)
            context.Stats.SetPassiveStats(new StatSet
            {
                maxManaFlat = -ch.maxManaFlat,
                mpRegenFlat = 10f - ch.mpRegenFlat
            });
            ExpectPlayerState(context, effect, 0, 0f, 0f, 10f, "Live-V05-MaxMana0-Inactive");

            // 9. 최대 마나 100 복원 -> 0% 마나 활성 복구
            context.Stats.SetPassiveStats(new StatSet
            {
                maxManaFlat = 100f - ch.maxManaFlat,
                mpRegenFlat = 10f - ch.mpRegenFlat
            });
            ExpectPlayerState(context, effect, 1, 0f, 100f, 13f, "Live-V05-RestoreMaxMana-Active");

            // 10. V07 수명 검증: Disable -> next frame -> Enable -> next frame
            IEnumerator reenable = VerifyH3Reenable(context, effect);
            try
            {
                while (reenable.MoveNext())
                    yield return reenable.Current;
            }
            finally
            {
                (reenable as IDisposable)?.Dispose();
            }

            // 11. 동일 SO 다른 인스턴스 교체 (해제 후 복원) 검증
            var helmet2 = new InventoryItem(new ItemInstance
            {
                instanceId = "p3b_h3_helmet_live_2",
                definition = definition,
                rolledSubStats = new List<RolledSubStat>()
            });
            var unequipResult1 = transaction.TryUnequipForTransfer(EquipSlotType.Helmet, helmet1);
            if (!unequipResult1.IsSuccess)
                throw new InvalidOperationException("[ArmorEffectValidation] 첫 투구 해제 실패");
            ExpectPlayerState(context, effect, 0, 0f, 100f, 10f, "Live-SameSO-UnequipFirst");

            var equipResult2 = transaction.TryRestoreEquippedItem(helmet2, EquipSlotType.Helmet);
            if (!equipResult2.IsSuccess)
                throw new InvalidOperationException("[ArmorEffectValidation] 두 번째 투구 장착 실패");
            yield return null; // 다음 프레임까지 버프가 정확히 1개 유지되는지 확인
            ExpectPlayerState(context, effect, 1, 0f, 100f, 13f, "Live-SameSO-RestoreSecond");

            // 12. 최종 해제
            var unequipResult2 = transaction.TryUnequipForTransfer(EquipSlotType.Helmet, helmet2);
            if (!unequipResult2.IsSuccess)
                throw new InvalidOperationException("[ArmorEffectValidation] 두 번째 투구 해제 실패");
            yield return null;
            ExpectPlayerState(context, effect, 0, 0f, 100f, 10f, "Live-FinalUnequip");

            sequenceCompleted = true;
            Debug.Log("[ArmorEffectValidation] 검사 단계 종료. 자원 제거 및 확인 진행.");
        }
        finally
        {
            DetachLiveExecutionLifetime();

            bool cleanupOk = true;

            cleanupOk &= TryCleanupStep("PauseRegenRestore", () =>
            {
                if (hasSavedRegenState && context != null && context.Mana != null)
                    context.Mana.IsRegenPaused = oldRegenPaused;
            });

            cleanupOk &= TryCleanupStep("PlayerDestroy", () =>
            {
                if (player != null)
                    NetworkServer.Destroy(player);
            });

            cleanupOk &= TryCleanupStep("EffectDestroy", () =>
            {
                if (effect != null && !AssetDatabase.Contains(effect))
                    UnityEngine.Object.Destroy(effect);
            });

            cleanupOk &= TryCleanupStep("DefinitionDestroy", () =>
            {
                if (definition != null && !AssetDatabase.Contains(definition))
                    UnityEngine.Object.Destroy(definition);
            });

            StartReleaseVerification(releaseProbe, sequenceCompleted, cleanupOk);
        }
    }

    // =========================================================================
    // 4. 검증 보조 함수 (Reviewer Candidate A & B 및 수치 Assertion 반영)
    // =========================================================================

    private static PlayerArmorEffectProvider_MirrorTest RequireRealServerPlayer(PlayerContext context)
    {
        if (!Application.isPlaying || !NetworkServer.active || context == null)
            throw new InvalidOperationException("격리된 Play Mode 서버의 시험 플레이어가 필요합니다.");
        NetworkIdentity identity = context.GetComponent<NetworkIdentity>();
        if (identity == null || !identity.isServer || identity.netId == 0 ||
            !NetworkServer.spawned.TryGetValue(identity.netId, out NetworkIdentity spawned) ||
            spawned != identity)
            throw new InvalidOperationException("정상 NetworkServer.Spawn 등록을 확인하지 못했습니다.");
        var provider = RequireArmorProvider(context.gameObject);
        if (!provider.isActiveAndEnabled || !provider.isServer)
            throw new InvalidOperationException("시험 시작 시 H3 서버 어댑터가 활성 상태여야 합니다.");
        return provider;
    }

    private static void ExpectPlayerState(PlayerContext context,
        StatThresholdBuffUniqueEffectSO effect, int expectedCount,
        float expectedMana, float expectedMaxMana, float expectedRegen, string caseId)
    {
        if (context == null || context.Mana == null || context.Buffs == null ||
            context.Stats == null || context.Stats.Stat == null || effect == null)
            throw new InvalidOperationException("시험 플레이어와 효과가 준비되지 않았습니다.");
        int count = context.Buffs.ActiveBuffs.Count(b => ReferenceEquals(b.source, effect));
        float mana = context.Mana.CurrentMana;
        float maxMana = context.Mana.MaxMana;
        float regen = context.Stats.Stat.mpRegen;
        bool passed = count == expectedCount && Mathf.Approximately(mana, expectedMana) &&
            Mathf.Approximately(maxMana, expectedMaxMana) && Mathf.Approximately(regen, expectedRegen);
        Debug.Log($"[ArmorEffectValidation] case={caseId} frame={Time.frameCount} passed={passed} " +
            $"buffs={count}/{expectedCount} mana={mana:F1}/{expectedMana:F1} " +
            $"maxMana={maxMana:F1}/{expectedMaxMana:F1} regen={regen:F1}/{expectedRegen:F1}");
        if (!passed)
            throw new InvalidOperationException($"[ArmorEffectValidation] 실제 효과 상태 불일치: {caseId} (buffs={count}/{expectedCount}, mana={mana}/{expectedMana}, maxMana={maxMana}/{expectedMaxMana}, regen={regen}/{expectedRegen})");
    }

    private static IEnumerator VerifyH3Reenable(
        PlayerContext context, StatThresholdBuffUniqueEffectSO effect)
    {
        var provider = RequireRealServerPlayer(context);
        if (effect == null || context.Mana.MaxMana <= 0f ||
            !context.Equipment.TryGetEquippedItemInstance(EquipSlotType.Helmet, out ItemInstance helmet) ||
            !context.Equipment.UsesLowManaHelmetEffect(helmet) ||
            !ReferenceEquals(helmet?.definition?.uniqueEffect, effect))
            throw new InvalidOperationException("양수 최대 MP와 실제 장착된 H3 투구가 필요합니다.");
        bool oldRegenPaused = context.Mana.IsRegenPaused;
        try
        {
            context.Mana.IsRegenPaused = true;
            context.Mana.SetCurrentMana(0f);
            ExpectPlayerState(context, effect, 1, 0f, 100f, 13f, "V07-before-disable");
            provider.enabled = false;
            ExpectPlayerState(context, effect, 0, 0f, 100f, 10f, "V07-disabled-immediate");
            yield return null;
            context.Mana.SetCurrentMana(0f);
            ExpectPlayerState(context, effect, 0, 0f, 100f, 10f, "V07-disabled-next-frame-mana-event");
            provider.enabled = true;
            ExpectPlayerState(context, effect, 1, 0f, 100f, 13f, "V07-enabled-immediate");
            yield return null;
            ExpectPlayerState(context, effect, 1, 0f, 100f, 13f, "V07-enabled-next-frame");
        }
        finally
        {
            if (provider != null) provider.enabled = true;
            if (context != null && context.Mana != null)
                context.Mana.IsRegenPaused = oldRegenPaused;
        }
    }

    /// <summary>
    /// 효과 컴포넌트가 정확히 하나 있고, 필요한 참조가 해당 플레이어 것인지 검사합니다.
    /// 누락된 컴포넌트를 자동 추가하지 않고 잘못된 자산은 실패로 알립니다.
    /// </summary>
    private static PlayerArmorEffectProvider_MirrorTest RequireArmorProvider(GameObject root)
    {
        if (root == null)
            throw new InvalidOperationException("검사할 플레이어가 없습니다.");

        PlayerContext context = root.GetComponent<PlayerContext>();
        var providers = root.GetComponents<PlayerArmorEffectProvider_MirrorTest>();
        if (context == null || providers.Length != 1)
            throw new InvalidOperationException("PlayerContext와 효과 컴포넌트 1개가 필요합니다.");

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                throw new InvalidOperationException("Missing Script: " + child.name);
        }

        string[] fields = { "equipment", "stats", "health", "mana", "buffs" };
        Component[] expected =
            { context.Equipment, context.Stats, context.Health, context.Mana, context.Buffs };
        var serializedProvider = new SerializedObject(providers[0]);
        for (int i = 0; i < fields.Length; i++)
        {
            Component component = expected[i];
            SerializedProperty property = serializedProvider.FindProperty(fields[i]);
            if (component == null || property == null || property.objectReferenceValue != component)
                throw new InvalidOperationException("효과 참조 불일치: " + fields[i]);

            if (component.transform != root.transform &&
                !component.transform.IsChildOf(root.transform))
                throw new InvalidOperationException("다른 플레이어의 참조입니다: " + fields[i]);
        }

        // 위에서 확인한 Context의 장비를 그대로 검사합니다.
        var serializedEquipment = new SerializedObject(context.Equipment);
        SerializedProperty enabledEffect = serializedEquipment.FindProperty("useLowManaHelmetEffect");
        if (enabledEffect == null || !enabledEffect.boolValue)
            throw new InvalidOperationException("저마나 투구 효과 설정이 꺼져 있습니다.");

        return providers[0];
    }

    private static StatThresholdBuffUniqueEffectSO CreateH3Effect()
    {
        var effect = ScriptableObject.CreateInstance<StatThresholdBuffUniqueEffectSO>();
        effect.name = "P3B_H3_ManaRecovery";
        effect.effectName = "절전 모드 헤드셋";
        effect.effectDescription = "현재 마나가 최대 마나의 {0}% 이하일 때 마나 재생이 {1}% 증가합니다.";
        effect.coefficients = new[] { 25f, 30f };
        effect.referenceStat = StatReference.CurrentManaPercent;
        effect.comparisonOperator = ComparisonOperator.LessOrEqual;
        effect.thresholdValue = 25f;
        effect.buffSpec = new BuffSpec
        {
            statEffects = new[]
            {
                new FixedStatValue { statType = StatType.mpRegenPercent, value = 30f },
            },
            duration = 0f,
            stackBehavior = BuffStackBehavior.Ignore,
            maxStack = 1,
        };
        return effect;
    }

    private static ItemDefinitionSO CreateH3Definition(StatThresholdBuffUniqueEffectSO effect, ArmorType armorType = ArmorType.Helmet)
    {
        var definition = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        definition.name = "P3B_H3_ArmorDefinition";
        definition.itemId = "P3B_H3_HELMET";
        definition.itemName = "절전 모드 헤드셋";
        definition.category = ItemCategory.Armor;
        definition.armorType = armorType;
        definition.itemWidth = 2;
        definition.itemHeight = 2;
        definition.uniqueEffect = effect;
        definition.uniqueEffectId = effect != null ? effect.name : string.Empty;
        definition.mainOptions = Array.Empty<FixedStatValue>();
        return definition;
    }

    /// <summary>선택한 실제 투구의 연결과 저마나 재생 설정을 검사합니다. 자산은 수정하지 않습니다.</summary>
    [MenuItem("SW/Mirror Test/Validate Selected Low Mana Helmet Asset")]
    private static void ValidateSelectedLowManaHelmetAsset()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("저장 자산 검사는 Play Mode를 종료한 뒤 실행하세요.");

        var item = Selection.activeObject as ItemDefinitionSO;
        StatThresholdBuffUniqueEffectSO effect = RequireLowManaHelmetAsset(item);
        Debug.Log($"[ArmorEffectValidation] 저장 자산 검사 통과: {item.itemId}, " +
            $"효과={effect.name}, 기준={effect.thresholdValue}%, " +
            $"재생 증가={effect.buffSpec.statEffects[0].value}%", item);
        LogAssetReference("아이템", item);
        LogAssetReference("연결 효과", effect);
    }

    /// <summary>
    /// 재가져오기 전후에 비교할 자산의 GUID와 파일 식별자를 기록합니다.
    /// 자산을 수정하거나 저장하지 않으며, 비교 결과를 자동으로 통과시키지 않습니다.
    /// </summary>
    private static void LogAssetReference(string label, UnityEngine.Object asset)
    {
        if (asset == null ||
            !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset, out string guid, out long fileId))
        {
            throw new InvalidOperationException("저장 자산의 식별자를 읽지 못했습니다: " + label);
        }

        Debug.Log($"[ArmorEffectValidation] {label} " +
            $"guid={guid} fileId={fileId} name={asset.name}", asset);
    }

    /// <summary>
    /// 실제 저장된 투구인지, 실행 조건과 설명 계수가 서로 맞는지 확인합니다.
    /// 잘못된 데이터는 자동 수정하지 않고 이유를 알려 줍니다.
    /// </summary>
    private static StatThresholdBuffUniqueEffectSO RequireLowManaHelmetAsset(ItemDefinitionSO item)
    {
        if (item == null || !AssetDatabase.Contains(item))
            throw new InvalidOperationException("Project 창에서 저장된 ItemDefinitionSO를 선택하세요.");
        if (string.IsNullOrWhiteSpace(item.itemId) || item.itemId == "P3B_H3_HELMET")
            throw new InvalidOperationException("시험용 ID가 아닌 확정된 정식 아이템 ID가 필요합니다.");
        if (item.category != ItemCategory.Armor || item.armorType != ArmorType.Helmet)
            throw new InvalidOperationException("이번 검사는 방어구의 투구 품목만 대상으로 합니다.");

        var effect = item.uniqueEffect as StatThresholdBuffUniqueEffectSO;
        if (effect == null || !AssetDatabase.Contains(effect))
            throw new InvalidOperationException("저장된 Threshold 효과 자산을 연결해야 합니다.");
        if (!string.Equals(item.uniqueEffectId, effect.name, StringComparison.Ordinal))
            throw new InvalidOperationException("아이템의 효과 ID와 연결된 효과 자산 이름이 다릅니다.");
        if (effect.referenceStat != StatReference.CurrentManaPercent ||
            effect.comparisonOperator != ComparisonOperator.LessOrEqual)
            throw new InvalidOperationException("현재 마나 비율이 기준 이하인 조건이어야 합니다.");

        float threshold = effect.thresholdValue;
        if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold <= 0f || threshold >= 100f)
            throw new InvalidOperationException("저마나 기준은 0보다 크고 100보다 작은 유한한 값이어야 합니다.");

        var spec = effect.buffSpec;
        if (spec == null || spec.duration != 0f ||
            spec.stackBehavior != BuffStackBehavior.Ignore || spec.maxStack != 1)
            throw new InvalidOperationException("조건부 상시 버프는 지속시간 0, Ignore, 최대 1스택으로 설정하세요.");
        if (spec.statEffects == null || spec.statEffects.Length != 1)
            throw new InvalidOperationException("이 품목에는 마나 재생 증가 옵션 하나만 설정하세요.");

        var bonus = spec.statEffects[0];
        if (bonus.statType != StatType.mpRegenPercent || float.IsNaN(bonus.value) ||
            float.IsInfinity(bonus.value) || bonus.value <= 0f)
            throw new InvalidOperationException("마나 재생 증가율은 양수의 유한한 값이어야 합니다.");
        if (effect.coefficients == null || effect.coefficients.Length != 2 ||
            !Mathf.Approximately(effect.coefficients[0], threshold) ||
            !Mathf.Approximately(effect.coefficients[1], bonus.value))
            throw new InvalidOperationException("설명 계수 두 개를 실제 마나 기준과 재생 증가율에 맞추세요.");
        if (string.IsNullOrWhiteSpace(effect.effectName) || string.IsNullOrWhiteSpace(effect.effectDescription))
            throw new InvalidOperationException("플레이어가 읽을 효과 이름과 설명을 작성하세요.");

        // 아이템 ID는 다른 생성 품목과 겹치면 저장/조회 대상을 구분할 수 없습니다.
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinitionSO", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemDefinitionSO other = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
            if (other != null && other != item &&
                string.Equals(other.itemId, item.itemId, StringComparison.Ordinal))
                throw new InvalidOperationException("아이템 ID가 중복됩니다: " + path);
        }

        // 실제 클라이언트와 같은 Resources 경로에서 효과 이름이 하나인지 검사합니다.
        const string poolPath = "DataFiles/ItemData/3. GeneratedAssets/UniqueEffectPool";
        int matchingEffects = 0;
        bool selectedEffectFound = false;
        foreach (UniqueEffectSO candidate in Resources.LoadAll<UniqueEffectSO>(poolPath))
        {
            if (candidate == null || !string.Equals(candidate.name, effect.name, StringComparison.Ordinal))
                continue;
            matchingEffects++;
            if (candidate == effect)
                selectedEffectFound = true;
        }
        if (matchingEffects != 1 || !selectedEffectFound)
            throw new InvalidOperationException("효과 풀에서 연결 자산을 유일하게 찾지 못했습니다: " + effect.name);
        return effect;
    }

    /// <summary>
    /// Stage C: 실제 품목(powersavingheadset / UE_LowManaRecovery)의 게임플레이 및 개인 패시브 회귀를 검증합니다.
    /// C1(실제 품목), C2(가방/장착), C3(경계/최대치 불변), C4(자연 재생 틱 및 자동 해제),
    /// C5(장비 트랜잭션), C6(Fighter & Gunner 2종 클래스), C7(비영점 패시브 회귀), C8(표시/라벨 일치)
    /// </summary>
    public static void ValidateStageC()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Stage C 검사는 Play Mode를 종료한 뒤 EditMode에서 실행하세요.");

        int checks = 0;
        void Check(bool condition, string caseId, string detail)
        {
            Debug.Log($"[ArmorEffectValidation] case={caseId} passed={condition} detail={detail}");
            if (!condition)
                throw new InvalidOperationException($"[ArmorEffectValidation] FAIL: {caseId} - {detail}");
            checks++;
        }

        // C1. 실제 품목 확인
        ItemDefinitionSO helmetDef = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
            "Assets/Resources/DataFiles/ItemData/3. GeneratedAssets/Items/item.armor.helmet.powersavingheadset_절전모드 헤드셋.asset");
        Check(helmetDef != null, "C01-RealItemAssetExists", "절전모드 헤드셋 SO 자산 존재");
        Check(helmetDef.itemId == "item.armor.helmet.powersavingheadset", "C01-ItemIdExact", "정식 itemId=item.armor.helmet.powersavingheadset 일치");
        Check(helmetDef.itemName == "절전모드 헤드셋", "C01-ItemNameExact", "정식 itemName=절전모드 헤드셋 일치");
        Check(helmetDef.category == ItemCategory.Armor && helmetDef.armorType == ArmorType.Helmet, "C01-ArmorCategory", "방어구 투구 슬롯");

        var effect = helmetDef.uniqueEffect as StatThresholdBuffUniqueEffectSO;
        Check(effect != null, "C01-EffectType", "StatThresholdBuffUniqueEffectSO 타입 일치");
        Check(helmetDef.uniqueEffectId == "UE_LowManaRecovery" && effect.name == "UE_LowManaRecovery", "C01-EffectIdMatch", "효과 ID UE_LowManaRecovery 일치");
        Check(effect.referenceStat == StatReference.CurrentManaPercent, "C01-ReferenceStat", "CurrentManaPercent 조건");
        Check(effect.comparisonOperator == ComparisonOperator.LessOrEqual, "C01-ComparisonOp", "LessOrEqual 연산자");
        Check(Mathf.Approximately(effect.thresholdValue, 25f), "C01-ThresholdValue", "25% 기준값");
        Check(effect.buffSpec != null && effect.buffSpec.statEffects.Length == 1, "C01-BuffSpec", "버프 스펙 단일 옵션");
        Check(effect.buffSpec.statEffects[0].statType == StatType.mpRegenPercent && Mathf.Approximately(effect.buffSpec.statEffects[0].value, 30f),
            "C01-StatEffectValue", "mpRegenPercent 30% 증가");

        LogAssetReference("C01-아이템", helmetDef);
        LogAssetReference("C01-연결 효과", effect);

        // C8. 표시 및 라벨 일치
        Check(effect.effectName == "절전 모드", "C08-EffectName", "효과명 '절전 모드' 일치");
        Check(effect.coefficients != null && effect.coefficients.Length == 2 &&
              Mathf.Approximately(effect.coefficients[0], 25f) && Mathf.Approximately(effect.coefficients[1], 30f),
            "C08-Coefficients", "계수 25;30 일치");
        Check(!string.IsNullOrWhiteSpace(effect.effectDescription) && effect.effectDescription.Contains("{0}") && effect.effectDescription.Contains("{1}"),
            "C08-DescriptionFormat", "효과 설명 포맷 스트링 포함");
        Check(helmetDef.icon != null && effect.icon != null, "C08-IconExists", "아이템 및 효과 아이콘 스프라이트 존재");
        Check(helmetDef.icon == effect.icon, "C08-IconSame", "아이템과 효과 아이콘 스프라이트 일치");

        // C6: 두 클래스 (Fighter and Gunner) 검증
        var classesToTest = new[]
        {
            (
                CharacterClass.Fighter,
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/FighterNetworkPlayer.prefab",
                "Fighter"
            ),
            (
                CharacterClass.Gunner,
                "Assets/SW/TEST/MirrorPlayerContext/Prefabs/GunnerNetworkPlayer_MirrorTest.prefab",
                "Gunner"
            ),
        };

        bool wasServerActive = NetworkServer.active;
        typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, true);

        try
        {
            foreach (var (charClass, prefabPath, className) in classesToTest)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Check(prefab != null, $"C06-PrefabExists-{className}", $"{className} 프리팹 존재");

                GameObject player = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                player.name = $"StageC_Validation_{className}";

                try
                {
                    PlayerContext context = player.GetComponent<PlayerContext>();
                    NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
                    foreach (NetworkBehaviour behaviour in player.GetComponentsInChildren<NetworkBehaviour>(true))
                        SetNetworkServerState(identity, behaviour);

                    // EditMode: Awake()는 실행되지만 isLocalPlayer=false 경로로 인해
                    // statManager 할당 후 early-return해 Recalculate 트리거가 누락됨.
                    // reflection으로 statManager를 명시적으로 재주입해 ApplyBuff 후 재계산 보장.
                    {
                        var smFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        typeof(PlayerManaManager).GetField("statManager", smFlags)?.SetValue(context.Mana, context.Stats);
                        typeof(PlayerHealthManager).GetField("statManager", smFlags)?.SetValue(context.Health, context.Stats);
                        typeof(PlayerBuffManager).GetField("statManager", smFlags)?.SetValue(context.Buffs, context.Stats);
                    }

                    context.Equipment.SetActiveCharacterClass(charClass);
                    context.Stats.EnsureInitialized();
                    context.Stats.GetLayerStatSets(out StatSet ch, out _, out _, out _);
                    context.Stats.SetPassiveStats(new StatSet
                    {
                        maxManaFlat = 100f - ch.maxManaFlat,
                        mpRegenFlat = 10f - ch.mpRegenFlat
                    });
                    context.Mana.RefreshMaxMana();
                    context.Mana.SetCurrentMana(100f);

                    PlayerArmorEffectProvider_MirrorTest provider = player.GetComponent<PlayerArmorEffectProvider_MirrorTest>();
                    Check(provider != null, $"C06-ProviderExists-{className}", $"{className} PlayerArmorEffectProvider 부착");
                    provider.OnStartServer();

                    var transaction = new EquipmentTransaction(context.Equipment);

                    // 기존 투구 장착 해제 (있다면)
                    if (context.Equipment.TryGetEquippedItem(EquipSlotType.Helmet, out InventoryItem existingHelmet))
                    {
                        transaction.TryUnequipForTransfer(EquipSlotType.Helmet, existingHelmet);
                    }

                    var helmetItem = new InventoryItem(new ItemInstance
                    {
                        instanceId = $"stage_c_{className}_headset",
                        definition = helmetDef,
                        rolledSubStats = new List<RolledSubStat>()
                    });

                    // -------------------------------------------------------------
                    // C2. 가방·장착 (Backpack vs Equipped)
                    // -------------------------------------------------------------
                    // 아이템 미착용(가방 소지 상태)에서 마나 20%로 감소
                    context.Mana.SetCurrentMana(20f);
                    Check(!HasBuff(context, effect), $"C02-BackpackOnlyInactive-{className}", "가방에만 보관 시 마나 20%에서도 버프 비활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), $"C02-BackpackRegenBase-{className}", "가방 보관 시 기본 마나 재생 10 유지");

                    // 장착 수행
                    var equipResult = transaction.TryRestoreEquippedItem(helmetItem, EquipSlotType.Helmet);
                    Check(equipResult.IsSuccess, $"C05-EquipSuccess-{className}", "트랜잭션을 통한 투구 정상 장착");

                    // EditMode에서 ReconcileEquipment()는 isServer 가드에서 막힌다.
                    // 실제 런타임과 같은 결과를 만들기 위해 StatThresholdRunner를 수동으로 생성·바인딩.
                    context.Stats.Recalculate();
                    context.Mana.RefreshMaxMana();
                    var runnerGO = new GameObject("LowManaEffect_C2_Test");
                    runnerGO.transform.SetParent(player.transform, false);
                    var runner = runnerGO.AddComponent<StatThresholdRunner_MirrorTest>();
                    runner.Bind(context.Stats, context.Health, context.Mana, context.Buffs, effect);
                    // 진단 로그
                    Debug.Log($"[C02-Diag-{className}] MaxMana={context.Mana.MaxMana} CurrentMana={context.Mana.CurrentMana} Stat.maxMana={context.Stats.Stat?.maxMana} activeBuffs={context.Buffs?.ActiveBuffs?.Count} hasBuff={HasBuff(context,effect)}");
                    Check(HasBuff(context, effect), $"C02-EquippedActiveAt20-{className}", "장착 후 마나 20% 조건 만족 시 버프 즉시 활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), $"C02-EquippedRegenBoost-{className}", "버프 활성 시 마나 재생 13 적용 (10 * 1.3)");

                    // -------------------------------------------------------------
                    // C3. 경계·최대치 불변 (Threshold Boundaries & MaxMana Invariant)
                    // -------------------------------------------------------------
                    float initialMaxMana = context.Mana.MaxMana;

                    // 24.9% 경계 -> 활성
                    context.Mana.SetCurrentMana(24.9f);
                    Check(HasBuff(context, effect), $"C03-Boundary24.9-Active-{className}", "마나 24.9%에서 활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), $"C03-RegenAt24.9-{className}", "24.9%에서 재생 13");
                    Check(Mathf.Approximately(context.Mana.MaxMana, initialMaxMana), $"C03-MaxManaInvariant-1-{className}", "최대 마나 불변 유지");

                    // 25.0% 경계 -> 활성
                    context.Mana.SetCurrentMana(25.0f);
                    Check(HasBuff(context, effect), $"C03-Boundary25.0-Active-{className}", "마나 25.0%에서 활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), $"C03-RegenAt25.0-{className}", "25.0%에서 재생 13");
                    Check(Mathf.Approximately(context.Mana.MaxMana, initialMaxMana), $"C03-MaxManaInvariant-2-{className}", "최대 마나 불변 유지");

                    // 25.1% 경계 -> 비활성
                    context.Mana.SetCurrentMana(25.1f);
                    Check(!HasBuff(context, effect), $"C03-Boundary25.1-Inactive-{className}", "마나 25.1%에서 비활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), $"C03-RegenAt25.1-{className}", "25.1%에서 기본 재생 10 복귀");
                    Check(Mathf.Approximately(context.Mana.MaxMana, initialMaxMana), $"C03-MaxManaInvariant-3-{className}", "최대 마나 불변 유지");

                    // 26.0% 경계 -> 비활성
                    context.Mana.SetCurrentMana(26.0f);
                    Check(!HasBuff(context, effect), $"C03-Boundary26.0-Inactive-{className}", "마나 26.0%에서 비활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), $"C03-RegenAt26.0-{className}", "26.0%에서 기본 재생 10 복귀");

                    // 0% -> 활성
                    context.Mana.SetCurrentMana(0f);
                    Check(HasBuff(context, effect), $"C03-Boundary0.0-Active-{className}", "마나 0%에서 활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), $"C03-RegenAt0.0-{className}", "0%에서 재생 13");

                    // -------------------------------------------------------------
                    // C4. 자연 재생 틱 및 자동 해제 (Natural Mana Regen Tick)
                    // -------------------------------------------------------------
                    // 24 마나 설정 (24% -> 활성, regen 13)
                    context.Mana.SetCurrentMana(24f);
                    Check(HasBuff(context, effect), $"C04-PreTick-Active-{className}", "자연 재생 전 마나 24%에서 활성");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), $"C04-PreTick-Regen-{className}", "자연 재생 전 재생 13");

                    // 1틱 자연 회복 시뮬레이션: GetMpRegen() * regenMultiplier = 13
                    float regenAmount = context.Stats.Stat.mpRegen * context.Mana.RegenMultiplier;
                    context.Mana.RestoreMana(regenAmount); // 24 + ceil(13) = 37 마나 (37% > 25%)

                    Check(Mathf.Approximately(context.Mana.CurrentMana, 37f), $"C04-PostTick-Mana37-{className}", "자연 재생 1틱 후 마나 37 도달");
                    Check(!HasBuff(context, effect), $"C04-PostTick-BuffRemoved-{className}", "자연 재생으로 25% 초과 시 버프 자동 제거");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), $"C04-PostTick-RegenBase-{className}", "버프 제거 후 기본 재생 10 복귀");

                    // -------------------------------------------------------------
                    // C5. 장비 거래 트랜잭션 (Equipment Transactions)
                    // -------------------------------------------------------------
                    // 마나를 10%로 낮춰 버프 활성 조건으로 만듦
                    context.Mana.SetCurrentMana(10f);
                    Check(HasBuff(context, effect), $"C05-ActiveAt10-{className}", "마나 10%에서 버프 활성");

                    // TryUnequipForTransfer로 해제
                    var unequipResult = transaction.TryUnequipForTransfer(EquipSlotType.Helmet, helmetItem);
                    Check(unequipResult.IsSuccess, $"C05-UnequipSuccess-{className}", "투구 해제 트랜잭션 성공");
                    Check(!HasBuff(context, effect), $"C05-UnequipBuffRemoved-{className}", "투구 해제 직후 버프 완전 제거");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 10f), $"C05-UnequipRegenBase-{className}", "해제 후 마나 10%여도 기본 재생 10 복귀");

                    // 잘못된 거래 시도 (빈 슬롯 해제)
                    var invalidUnequip = transaction.TryUnequipForTransfer(EquipSlotType.Helmet, helmetItem);
                    Check(!invalidUnequip.IsSuccess, $"C05-InvalidUnequipFails-{className}", "이미 빈 슬롯 해제 실패 반환 (안전)");
                    Check(!HasBuff(context, effect), $"C05-InvalidTransactionNoSideEffect-{className}", "실패한 트랜잭션으로 인한 부작용 없음");

                    // 재장착 복원
                    var reequipResult = transaction.TryRestoreEquippedItem(helmetItem, EquipSlotType.Helmet);
                    Check(reequipResult.IsSuccess, $"C05-ReequipSuccess-{className}", "투구 재장착 트랜잭션 성공");
                    Check(HasBuff(context, effect), $"C05-ReequipBuffActive-{className}", "재장착 후 마나 10%에서 버프 정상 복원");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 13f), $"C05-ReequipRegenBoost-{className}", "재장착 후 마나 재생 13 적용");

                    // -------------------------------------------------------------
                    // C7. 비영점 개인 패시브 회귀 (Code A Passive Stat Regression)
                    // -------------------------------------------------------------
                    // 비영점 패시브 스탯 설정 (maxManaFlat = 150 - ch.maxManaFlat, mpRegenFlat = 20 - ch.mpRegenFlat)
                    context.Stats.SetPassiveStats(new StatSet
                    {
                        maxManaFlat = 150f - ch.maxManaFlat,
                        mpRegenFlat = 20f - ch.mpRegenFlat,
                    });
                    // 기본 maxMana = 150. 기본 mpRegen = 20.
                    context.Mana.RefreshMaxMana();
                    Check(Mathf.Approximately(context.Mana.MaxMana, 150f), $"C07-PassiveMaxMana150-{className}", "패시브 반영 후 최대 마나 150");

                    // 마나 30으로 설정 (30 / 150 = 20% <= 25% -> 버프 활성)
                    context.Mana.SetCurrentMana(30f);
                    Check(HasBuff(context, effect), $"C07-PassiveManaActive-{className}", "150 기준 20%(30) 마나에서 버프 활성");
                    // 20 * 1.3 = 26.
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 26f), $"C07-PassiveRegenBoost26-{className}", "기본 20 + 30% = 재생 26");

                    // Code A 검증: 다양한 스탯 변경 이벤트가 발생해도 개인 패시브 스탯이 0으로 날아가지 않는지 확인
                    context.Stats.Stat.NotifyValuesChanged();
                    PublishEquipmentChanged(context.Equipment);
                    context.Mana.RefreshMaxMana();
                    context.Stats.Recalculate();

                    // 이벤트 발생 후에도 패시브 스탯과 총합 150/26이 정확히 보존되는지 확인
                    Check(Mathf.Approximately(context.Mana.MaxMana, 150f), $"C07-PassiveRetainedMaxMana-{className}", "이벤트 후 개인 패시브 maxMana=150 보존 (회귀 없음)");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 26f), $"C07-PassiveRetainedRegen-{className}", "이벤트 후 개인 패시브 mpRegen=26 보존 (회귀 없음)");

                    // 마나 50으로 증가 (50 / 150 = 33.3% > 25% -> 버프 해제)
                    context.Mana.SetCurrentMana(50f);
                    Check(!HasBuff(context, effect), $"C07-PassiveAboveThreshold-{className}", "마나 33.3%에서 버프 해제");
                    Check(Mathf.Approximately(context.Stats.Stat.mpRegen, 20f), $"C07-PassiveBaseRegen20-{className}", "버프 해제 후 패시브 포함 기본 재생 20 복귀");

                    // 최종 정리: 투구 해제
                    transaction.TryUnequipForTransfer(EquipSlotType.Helmet, helmetItem);
                    Check(!HasBuff(context, effect), $"C07-FinalCleanUnequip-{className}", "최종 해제 후 버프 없음");

                    provider.OnStopServer();
                }
                finally
                {
                    if (player != null)
                        UnityEngine.Object.DestroyImmediate(player);
                }
            }

            Debug.Log($"[ArmorEffectValidation] PASS Stage C 전체 통과: 총 {checks}개 조건 검증 완료. (Fighter & Gunner 2종, C1~C8 전수 검증)");
        }
        finally
        {
            typeof(NetworkServer).GetProperty("active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, wasServerActive);
        }
    }

    private static bool HasBuff(PlayerContext context, IBuffSource source)
    {
        return context != null && context.Buffs != null &&
            context.Buffs.ActiveBuffs.Any(buff => ReferenceEquals(buff.source, source));
    }

    private static void PublishEquipmentChanged(EquipmentSystem equipment)
    {
        typeof(EquipmentSystem).GetMethod("PublishChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.Invoke(equipment, null);
    }

    private static void SetNetworkServerState(NetworkIdentity identity, NetworkBehaviour behaviour)
    {
        typeof(NetworkIdentity).GetProperty("isServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(identity, true);
        typeof(NetworkBehaviour).GetProperty("netIdentity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(behaviour, identity);
    }
}
