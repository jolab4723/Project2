# P2-B 인페르노 표시 결함 — round-04 설계 검토 및 C# 교체안

- author: GPT
- reviewer_session_label: GPT-arcblade-r04-presentation-20260916 (대화 구분용 별칭)
- task_id: task-p2-review-arcblade-001
- round: round-04
- scope: tasks/task-p2-review-arcblade-001/round-04
- reviewed_at: 2026-09-16T16:16:08+09:00
- reviewed_request_ready: READY / captured_at=2026-09-16T16:05:33+09:00
- evidence_captured_at: 2026-09-16T16:05:03+09:00
- source_branch_reported: codex/unity-6000-3-22-test
- source_base_commit_reported: 54aabb1aff855ed4ac645bd3fecad3219282352c
- verdict: REVISE_PLAN
- deliverable: 기존 4개 C# 파일의 필드·메서드 교체 코드와 연결·검증 조건
- implementation_status: 문서 내 구현안 작성. 원본 프로젝트 적용·Unity 컴파일·Play/네트워크 실행은 NOT_PERFORMED.

## 1. 결론과 원인 확인

**타격마다 값을 전달하는 ClientRpc 전환과 기존 Presentation 재사용 방향은 채택할 만하다.** 전투 계산·공격 자격·피해 계수·동기 후속 큐·처치 보상은 이번 수정에서 바꾸지 않는다. 다만 요청서의 “LateUpdate 전체 삭제”, “lastDamage 삭제”, “큐 등록 완료 시 VFX”, “10줄 이내면 완료”는 그대로 적용하면 안 된다. 아래 교체안은 이 네 부분을 보정한다. [EN HandleDamaged/PlayDeathPresentationOnce; VIEW LateUpdate; IT TryFireInfernoExtraHit; RES DrainPendingQueue]

요청서가 보고한 체력 차이는 `6826.08 - 6561.56 = 264.52`이고 두 결과의 합도 `219.10 + 45.42 = 264.52`다. **이 표본에서는 피해 누락보다 표시 누락이라는 해석과 일치한다.** 이번 리뷰어가 Play Mode를 재실행하거나 캡처 원문을 독립 취득한 것은 아니므로, 이 한 표본으로 모든 전투 상황의 정상 동작을 인증하지 않는다. [REQ §2]

EN은 매 피격마다 LastDamage를 갱신하고 카운터를 올린다. VIEW는 LateUpdate에서 카운터가 달라졌는지만 보고 마지막 피해 하나를 표시한다. 그러므로 같은 갱신 사이에 219.10과 45.42가 들어오면 마지막 값만 읽는다. 카운터 차이가 2라고 같은 45.42를 두 번 표시해도 원래 사건은 복원되지 않는다. SyncVar는 상태 동기화이고 모든 변경 이력을 저장하는 사건 목록이 아니다. Hook이나 syncInterval 조정만으로 이 문제를 해결하지 않는다. [EN; VIEW; W1]

인페르노의 성공 콜백은 현재 infernoResolvedHitCount만 증가시킨다. PRE에는 번개 표현만 있다. 화염 연출 미연결이라는 분석도 코드와 일치한다. **새 VFX는 enqueue 성공이 아니라 Resolver의 onResolved 콜백에서 보내야 한다.** 등록 후 사망 등으로 건너뛴 후속타에는 VFX가 없어야 하며, 추가타가 적을 처치한 경우에는 VFX가 있어야 한다. [IT; PRE; RES]

## 2. 수정 범위와 지적 사항

| ID | 중요도 | 근거 위치 | 보정할 내용 | 확인 기준 |
| --- | --- | --- | --- | --- |
| R04-01 | 필수 | VIEW LateUpdate/RefreshHealthBar | 데미지 폴링만 제거한다. 체력바 갱신·빌보드와 그 안의 보스 페이즈 갱신은 유지한다. | 숫자 2개, 체력바·보스 페이즈 회귀 없음. |
| R04-02 | 필수 | EN LastDamage/PlayDeathPresentationOnce; VALI | LastDamage/LastDamageCritical/ReceivedDamagePresentationCount는 현재 진단·사망 표현·검증의 소비자가 있다. 필드와 공개 API는 이번에 유지한다. | 기존 소비자 컴파일 및 사망 연출 유지. |
| R04-03 | 필수 | IT TryFireInfernoExtraHit; RES DrainPendingQueue | 성공 콜백에서만 VFX를 보낸다. WBH_ICombat에는 Transform을 당연히 기대할 수 없으므로 Component에서 위치를 미리 복사한다. | 직접 처치·스킵은 0회, 성공한 후속 처치는 1회. |
| R04-04 | 필수 | VIEW ShowDamage | RPC가 2개여도 같은 위치·같은 풀 객체 재사용이면 겹쳐 보인다. 작은 로컬 배치 보정과 서로 다른 대여 객체 확인이 필요하다. | 219와 45가 별도 활성 객체로 읽힌다. |
| R04-05 | 필수 | PRE/IT; manifest | 화염 프리팹·사운드·기존 VFX 풀 API 사본이 없다. 가짜 경로나 메서드를 만들지 말고 명시적 프리팹 참조를 연결한다. | 모든 플레이어 복제본에 설정된 PRE, 실제 VFX/SFX 및 정상 회수. |
| R04-06 | 필수 | EN/IT RPC; W2 | Host 별도 직접 재생을 추가하지 않는다. 미Spawn Editor 객체에서는 새 RPC 전송을 생략하고 로직 검사를 유지한다. | Host 중복 없음, 원격 수신 확인, 미Spawn RPC 경고 없음. |

아래 코드 블록은 **해당 멤버의 전체 추가/교체 코드**다. 나머지 멤버는 보존한다. 위치는 제출본의 메서드명과 고유 구문으로 지정했다. manifest 집계와 도구의 공백 포함 줄 수가 달라 절대 줄 번호만으로 패치하지 않는다. 파일 경로는 각 절에 정확히 적었다. 코드의 `Presented...Count`는 로컬 표시 확인용이며 SyncVar가 아니다.

## 3. 코드 1 — 적 Authority: 각 피해 사건을 값으로 전송

대상: `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs`

클래스 필드 영역에 다음 두 필드를 추가한다.

```csharp
private NetworkEnemyCombatView_MirrorTest combatView;
private bool missingCombatViewReported;
```

`HandleDamaged(WBH_DamageResult result)`의 시작 부분에서 기존 세 줄을 아래 블록으로 교체한다. **뒤의 공격자 귀속·ItemTriggers 호출·상태 갱신은 그대로 둔다.** 이벤트 인자를 먼저 보내므로 뒤의 콜백에서 다른 피해가 발생해도 해당 표시값을 다시 LastDamage에서 읽지 않는다.

```csharp
lastDamage = result.FinalDamage;
lastDamageCritical = result.IsCritical;
receivedDamagePresentationCount++;

// 실제 Spawn된 적만 전송한다. Editor의 미Spawn 로직 검사는 그대로 실행한다.
if (netId != 0 &&
    NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity spawnedIdentity) &&
    spawnedIdentity == netIdentity)
{
    RpcShowDamage(result.FinalDamage, result.IsCritical,
        result.ElementType, transform.position);
}
```

같은 클래스에 다음 메서드를 추가한다. `isServer`이면 반환하는 가드를 넣지 않는다. 이 RPC에서는 Host도 정상 수신자다. 서버 측에서 combatView.ShowDamage를 별도로 직접 호출하지 않는다. [W2]

```csharp
[ClientRpc(channel = Channels.Reliable)]
private void RpcShowDamage(float damage, bool critical,
    ElementType element, Vector3 enemyPosition)
{
    if (combatView == null)
        combatView = GetComponent<NetworkEnemyCombatView_MirrorTest>();

    if (combatView != null)
    {
        combatView.ShowDamage(damage, critical, element, enemyPosition);
        return;
    }

    if (!missingCombatViewReported)
    {
        missingCombatViewReported = true;
        Debug.LogWarning("[NetworkEnemyAuthority_MirrorTest] 데미지 표시 View가 없습니다.", this);
    }
}
```

위치도 보내는 것은 수신 시 적 이동 위치를 타격 순간 위치로 오인하지 않기 위해서다. 숫자는 float 원값으로 전송하며 219/45 정수 표시는 기존 텍스트 서식에 맡긴다. 실제 판정 264.52를 정수 표시 합계 264와 혼동하지 않는다. 죽은 적이라는 이유로 이 RPC를 막지 않는다. 치사타 숫자도 정상 표시 대상이다.

## 4. 코드 2 — CombatView: 데미지 폴링 제거, 체력바 보존

대상: `Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyCombatView_MirrorTest.cs`

필드 `observedDamagePresentationCount`와 `initialized`를 삭제한다. 기존 `damageTextPool`, `mainCamera`, `missingPoolReported` 등은 유지하고 다음 필드를 추가한다.

```csharp
[SerializeField] private Vector2 damageTextSpacingPixels = new Vector2(56f, 28f);
private double damageTextBurstStartedAt = double.NegativeInfinity;
private int damageTextBurstIndex;
public uint PresentedDamageTextCount { get; private set; }
```

기존 `Start()`와 `LateUpdate()`를 각각 아래 메서드로 교체한다. `RefreshHealthBar`, `RefreshBossPhaseView`, `ResolveReferences`, `Awake`, `OnValidate`는 삭제하거나 바꾸지 않는다.

```csharp
private void Start()
{
    if (Mirror.NetworkClient.active)
        RefreshHealthBar();
}

private void LateUpdate()
{
    if (!Mirror.NetworkClient.active)
        return;

    if (mainCamera == null)
        mainCamera = Camera.main;
    RefreshHealthBar();

    if (healthBarRoot != null && healthBarRoot.activeSelf && mainCamera != null)
        healthBarRoot.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
}
```

기존 `private void ShowDamage(float damage, bool critical)` 전체를 아래 메서드로 교체한다. 제출본에서 실제 사용 중인 `WBH_FloatTextPoolManager.GetDamageText()`, `WBH_DamageText.Initialize/Show`만 재사용한다. 주석에 남은 DamageTextPoolManager라는 옛 이름을 새 API로 만들지 않는다. [VIEW ShowDamage]

```csharp
public void ShowDamage(float damage, bool critical,
    ElementType element, Vector3 enemyPosition)
{
    if (!Mirror.NetworkClient.active || !isActiveAndEnabled ||
        !float.IsFinite(damage) || damage <= 0f)
        return;

    if (mainCamera == null)
        mainCamera = Camera.main;
    if (mainCamera == null)
        return;

    if (damageTextPool == null)
        damageTextPool = FindFirstObjectByType<WBH_FloatTextPoolManager>(FindObjectsInactive.Exclude);
    if (damageTextPool == null)
    {
        if (!missingPoolReported)
        {
            missingPoolReported = true;
            Debug.LogWarning("[NetworkEnemyCombatView_MirrorTest] 씬의 데미지 텍스트 풀이 없습니다.", this);
        }
        return;
    }

    Vector3 anchorOffset = damageTextRoot != null
        ? damageTextRoot.position - transform.position
        : Vector3.up * 1.5f;
    Vector3 position = OffsetDamageText(enemyPosition + anchorOffset);
    WBH_DamageText damageText = damageTextPool.GetDamageText();
    if (damageText == null)
        return;

    damageText.Initialize(damageTextPool);
    damageText.Show(position, new WBH_DamageResult(null, damage, critical, element));
    PresentedDamageTextCount++;
}
```

같은 클래스에 다음 보조 메서드를 추가한다. 가까운 표시 사건을 0.12초의 고정 창 안에서 좌우·행으로 분리한다. 피해나 RPC 전송을 지연·누락·합산하지 않는 순수 로컬 배치다. 창 시작 시각을 매 호출 갱신하지 않아 지속 연타로 행 번호가 무한히 늘지 않게 한다. 간격은 화면 글자 크기에 맞춰 튜닝한다. [W3]

```csharp
private Vector3 OffsetDamageText(Vector3 position)
{
    double now = Time.unscaledTimeAsDouble;
    if (now < damageTextBurstStartedAt || now - damageTextBurstStartedAt >= 0.12d)
    {
        damageTextBurstStartedAt = now;
        damageTextBurstIndex = 0;
    }

    int index = damageTextBurstIndex++;
    Vector3 screen = mainCamera.WorldToScreenPoint(position);
    if (screen.z <= 0f)
        return position;

    screen.x += ((index & 1) == 0 ? -0.5f : 0.5f) * damageTextSpacingPixels.x;
    screen.y += (index / 2) * damageTextSpacingPixels.y;
    return mainCamera.ScreenToWorldPoint(screen);
}
```

**색상에 대한 정확한 범위:** 이 코드는 기존의 ElementType.None 고정을 제거하고 실제 속성을 넘긴다. WBH_DamageText 구현·색상표가 제출되지 않아 Fire를 실제로 주황색으로 매핑하는지는 미확인이다. 외부 TMP 필드를 추측해 수정하지 않는다. 해당 클래스의 Show에서 기존 속성 팔레트를 확인하는 것을 적용 검증에 포함한다. 기본타 자체도 Fire이면 두 숫자가 모두 화염색일 수 있다. 색만으로 Direct와 Effect를 구분한다는 약속은 하지 않는다.

카메라·풀이 없거나 풀이 null을 반환하면 기존과 같이 표시가 생략된다. 따라서 “정상 연결·표시 준비·풀 여유가 있는 관찰자에게 사건 2개를 각각 전달”하는 수정이지, 로딩·접속 종료·풀 고갈까지 무조건 복원하는 기록 시스템은 아니다. 풀은 첫 Show가 반환되기 전에 대여 객체를 사용 중으로 표시해야 한다. 두 GetDamageText가 같은 활성 인스턴스를 내주면 풀 쪽의 별도 보완이 필요하다.

## 5. 코드 3 — ItemTrigger: 실제 후속타 성공에서만 VFX RPC

대상: `Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs`

기존 `presentation` 필드는 유지하고 필드 하나를 추가한다.

```csharp
private bool missingInfernoPresenterReported;
```

`TryFireInfernoExtraHit` 전체를 아래 메서드로 교체한다. 기존 자격 조건과 Fire/Effect/같은 AttackId/비치명은 보존한다. Transform 유무는 표현에만 사용하며, 표시 컴포넌트 부재 때문에 피해를 취소하지 않는다.

```csharp
[Server]
private void TryFireInfernoExtraHit(in WBH_DamageResult result, WBH_ICombat firstTarget)
{
    context ??= GetComponent<PlayerContext>();
    if (result.AttackId == 0 || firstTarget?.Status == null || firstTarget.Status.IsDead ||
        context?.CombatAuthority == null ||
        !context.CombatAuthority.IsDirectTargetForAttack(result.AttackId, firstTarget) ||
        inventory?.EquipmentSystem == null ||
        !inventory.EquipmentSystem.TryGetEquippedItemInstance(EquipSlotType.Weapon, out ItemInstance weapon) ||
        weapon?.definition?.characterClass != CharacterClass.Fighter ||
        weapon.definition.uniqueEffect is not InfernoExtraHitUniqueEffectSO effect)
    {
        return;
    }

    // 인터페이스의 Transform을 가정하지 않는다. 처치·풀 반환 전에 값만 저장한다.
    Component targetComponent = firstTarget as Component;
    bool hasImpactPoint = targetComponent != null;
    Vector3 impactPoint = hasImpactPoint
        ? targetComponent.transform.position + Vector3.up
        : Vector3.zero;

    if (!WBH_CombatResolver_MirrorTest.EnqueueFollowUpDamage(
            context, firstTarget, ElementType.Fire, effect.damageMultiplier,
            null, DamageCause.Effect, result.AttackId,
            _ =>
            {
                infernoResolvedHitCount++;
                if (hasImpactPoint && netId != 0 &&
                    NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity spawnedIdentity) &&
                    spawnedIdentity == netIdentity)
                {
                    RpcPresentInfernoHit(impactPoint);
                }
            },
            canCrit: false))
    {
        return;
    }

    infernoTriggerCount++;
}
```

같은 클래스에 다음 RPC를 추가한다.

```csharp
[ClientRpc(channel = Channels.Reliable)]
private void RpcPresentInfernoHit(Vector3 position)
{
    if (presentation == null)
        presentation = GetComponent<UniqueEffectPresentation_MirrorTest>();
    if (presentation != null)
    {
        presentation.PresentInfernoHit(position);
        return;
    }

    if (!missingInfernoPresenterReported)
    {
        missingInfernoPresenterReported = true;
        Debug.LogWarning("[ItemTriggerManager_MirrorTest] 설정된 인페르노 Presenter가 없습니다.", this);
    }
}
```

이 메서드는 아래의 설정된 PRE가 플레이어 프리팹에 존재하는 것을 전제로 한다. 기존 RpcPresentChainLightning은 변경하지 않는다. 설정 없이 AddComponent로 생성하면 새 프리팹 참조가 비어 있으므로, 인페르노 RPC에는 그 자동 생성 경로를 복사하지 않는다.

## 6. 코드 4 — 기존 Presentation에 로컬 화염·사운드 수명 추가

대상: `Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs`

현재 `using`은 그대로 사용한다. 클래스 필드 영역에 아래를 추가한다. 프리팹은 실제 기존 화염 임팩트의 순수 표현 프리팹을 지정한다. 현재 자료에 실물이나 풀 API가 없어 경로·풀 메서드를 임의로 정하지 않았다. 코드에서는 명시적 참조와 Unity 표준 API만 사용한다.

```csharp
[SerializeField] private ParticleSystem infernoHitPrefab;
[SerializeField, Min(0.05f)] private float infernoHitLifetime = 2f;
private readonly List<GameObject> activeInfernoHits = new();
private bool missingInfernoPrefabReported;
public uint PresentedInfernoHitCount { get; private set; }
```

다음 두 메서드를 추가한다. 새 C# 컴포넌트 타입은 없다. 타격 위치에 부모 없이 생성하여 플레이어 이동·적 사망을 따라 움직이지 않게 하고, 이 PRE가 수명을 소유한다. `Play(true)`는 자식 ParticleSystem도 재생한다. 사운드는 해당 표현 프리팹 루트의 AudioSource/clip을 사용한다. [W4/W5]

```csharp
public void PresentInfernoHit(Vector3 position)
{
    if (!Application.isPlaying || !Mirror.NetworkClient.active || !isActiveAndEnabled)
        return;

    if (infernoHitPrefab == null)
    {
        if (!missingInfernoPrefabReported)
        {
            missingInfernoPrefabReported = true;
            Debug.LogWarning("[UniqueEffectPresentation_MirrorTest] 화염 임팩트 프리팹이 없습니다.", this);
        }
        return;
    }

    ParticleSystem hit = Instantiate(infernoHitPrefab, position, Quaternion.identity);
    GameObject instance = hit.gameObject;
    instance.hideFlags = HideFlags.DontSave;
    activeInfernoHits.Add(instance);
    float lifetime = float.IsFinite(infernoHitLifetime)
        ? Mathf.Max(0.05f, infernoHitLifetime) : 2f;
    StartCoroutine(ReleaseInfernoHitAfter(instance, lifetime));
    hit.Play(true);
    if (hit.TryGetComponent(out AudioSource audio) && audio.clip != null)
        audio.Play();
    PresentedInfernoHitCount++;
}

private IEnumerator ReleaseInfernoHitAfter(GameObject instance, float lifetime)
{
    yield return new WaitForSecondsRealtime(lifetime);
    activeInfernoHits.Remove(instance);
    if (instance != null)
        DestroyOwnedObject(instance);
}
```

기존 `ReleaseOwnedResources()` 전체를 아래로 교체한다. 기존 `OnDisable`, `OnDestroy`, `DestroyOwnedObject`와 번개 생성·회수 메서드는 그대로 유지한다. 프리팹의 별도 자동 반환 스크립트와 이 소유자가 동시에 회수하지 않도록 §7을 따른다. 실시간 대기는 시간 배율 0에서 코루틴 수명이 끝나지 않는 상황을 피한다. [W6]

```csharp
private void ReleaseOwnedResources()
{
    StopAllCoroutines();
    foreach (GameObject bolt in activeBolts)
    {
        if (bolt != null)
            DestroyOwnedObject(bolt);
    }
    activeBolts.Clear();

    foreach (GameObject hit in activeInfernoHits)
    {
        if (hit != null)
            DestroyOwnedObject(hit);
    }
    activeInfernoHits.Clear();

    if (chainLightningMaterial != null)
    {
        DestroyOwnedObject(chainLightningMaterial);
        chainLightningMaterial = null;
    }
}
```

이 시범 구현은 기존 **VFX 자산을 재사용하되 새 풀을 만들지 않는** 안이다. 이미 이 임팩트를 정상 대여·재생·회수하는 프로젝트 풀이 있다면 그 경계를 우선 사용하고 위 Instantiate/Destroy 부분만 대체한다. 그 경우 풀의 실제 API 사본을 먼저 확인한다. 풀에서 빌린 객체를 위 DestroyOwnedObject로 파괴해서는 안 된다. 수명 정리까지 생략하면서 “10줄”을 맞추는 것은 최소 침습이 아니다.

## 7. 프리팹·풀 연결은 코드와 함께 완료해야 한다

**현재 미제공 의존성:** WBH_FloatTextPoolManager, WBH_DamageText, 실제 화염 임팩트 프리팹·오디오, FighterNetworkPlayer 프리팹, 기존 VFX 풀의 임팩트 대여/반환 API. 따라서 임의 Resources 경로나 TMP 필드명을 코드에 넣지 않았다. 아래는 사용자가 승인한 테스트 프리팹/Variant에서 수행할 정확한 연결 조건이며 이번 리뷰가 실제 프리팹을 변경한 것은 아니다.

| 대상 | 필요한 설정 및 확인 |
| --- | --- |
| 실제 적/더미 프리팹 | NetworkEnemyCombatView_MirrorTest가 같은 루트에 활성 상태로 존재해야 한다. EN의 GetComponent 경계와 일치시킨다. 씬에 기존 숫자 풀이 있고 동시에 2개 이상 대여 가능한지 확인한다. |
| 실제 Fighter 네트워크 프리팹 | 기존 타입인 UniqueEffectPresentation_MirrorTest 인스턴스를 루트에 미리 배치하고 infernoHitPrefab을 연결한다. 로컬 플레이어만이 아니라 모든 클라이언트의 해당 플레이어 복제본에도 설정이 있어야 한다. 연결된 실행 중 인스턴스 하나만 고치지 않는다. |
| 화염 표현 프리팹 | **루트에 있는 ParticleSystem**을 참조한다. 활성 루트, Looping=false, PlayOnAwake=false, 자식 포함 순수 표현만 사용한다. NetworkIdentity·피해 Collider·Damage 스크립트·자체 풀 반환/파괴 동작은 없어야 한다. 기존 원본을 임의 수정하지 말고 필요한 경우 승인된 표현용 Variant로 분리한다. |
| 사운드 | 같은 루트의 AudioSource에 실제 짧은 화염 임팩트 clip, PlayOnAwake=false, Loop=false, 3D 거리·음량·기존 SFX 믹서 출력을 설정한다. 코드의 Play가 1회 재생한다. AudioSource/clip이 없으면 VFX만 나오며 SFX 완료가 아니다. |
| 수명 | infernoHitLifetime은 모든 자식의 지연+방출+입자 생존시간 및 clip 재생 길이를 포함해야 한다. 2초는 초기값이다. 별도 StopAction Destroy/Disable나 자동 풀 반환과 중복 소유하지 않는다. 시간 배율 0에서도 표현을 끝내려면 ParticleSystem의 Unscaled Time도 맞춘다. |
| 표시 품질 | 텍스트의 56×28픽셀 간격은 초기값이다. 실제 해상도·폰트·카메라·기존 Show의 위치 애니메이션에서 두 객체가 겹치지 않는지 확인한다. Fire 색상표 및 비치명 표현도 직접 확인한다. |

PRE가 플레이어 비활성/파괴 시 자신의 연출을 즉시 정리하는 기존 수명 정책은 유지한다. 적이 죽어도 PRE는 공격자 쪽에 있으므로 저장된 월드 위치에서 짧은 임팩트가 남을 수 있다. 반대로 공격자까지 종료되면 잔여 연출을 끝까지 보장하지 않는다. 이를 위해 새 전역 VFX 매니저를 만들지는 않는다.

## 8. 네트워크·성능 평가와 대안

이 수정은 **최신 상태는 SyncVar, 일회성 피격은 RPC**로 역할을 분리한다. 같은 값의 피해가 반복돼도 각 호출이 별도 사건이므로 값 변화 여부에 의존하지 않는다. 모든 표시는 서버에서 받은 결과만 사용하며 클라이언트가 피해를 다시 계산하거나 서버에 승인 요청을 보내지 않는다. 기존 EN이 사건을 한 번 발행한다는 전제가 필요하며, RPC가 상류의 중복 OnDamaged를 자동 제거하지는 않는다. [EN; W1/W2]

인페르노가 비치사 직접타 뒤 추가타 1회를 성공시키면 텍스트 RPC 2회와 VFX RPC 1회가 된다. 초당 이러한 직접 대상 처리 수를 H라 하면 서버의 추가 표시 호출은 3H회다. 예를 들어 4명×초당 2스윙×스윙당 생존 직접 대상 5명이라면 H=40, 텍스트 80회와 VFX 40회로 120회/초다. 원격 3명이 모두 관련 객체를 관찰하면 원격 수신 호출은 합계 최대 360회/초라는 계산이다. **가정에 따른 호출량이며 실제 패킷 수·대역폭·안전 성능 인증이 아니다.**

4인이라는 이유만으로 무조건 안전하거나 과부하라고 단정하지 않는다. 기존 상태/애니메이션 복제까지 포함한 전송 바이트, 서버·클라이언트 프레임, GC, 활성 숫자·입자 수, 풀 고갈, 지연·손실하 표시 지연을 측정한다. Reliable은 연결된 관찰자에게 사건을 전달하는 선택이지만 혼잡 시 지연이 늘 수 있다. 숫자 두 개를 보존하는 현재 요구를 해결하려고 먼저 Unreliable로 바꾸지는 않는다. [W2/W7]

**중요한 관찰 범위 차이:** 숫자 RPC는 적 객체의 관찰자에게, 화염 RPC는 공격자 객체의 관찰자에게 간다. 두 객체가 같은 전투 참가자에게 보이는 현재 테스트 조건에서는 사용할 수 있지만, AOI/거리 제한이 있다면 두 집합이 같다는 보장은 없다. 실제 관심 영역 설정을 확인한다. 차이가 확인되면 기존의 대상/위치 기반 표시 전송 경계로 통합하는 것이 다음 수정이며, 무조건 모든 플레이어에게 전역 방송하는 새 시스템을 먼저 만들지 않는다. [W2]

또한 RPC는 이미 끝난 피격을 늦게 참가한 클라이언트에게 재생하는 이력이 아니다. 최신 HP만 복원되는 것이 정상이다. 사망 직전 RPC→Destroy와 공격자 접속 종료 순서, 적/공격자가 새로 관찰될 때의 표시를 Host+원격 Client로 확인한다. 숫자 생성 여부를 authority.IsDead나 뒤늦게 복제된 LastDamage로 다시 판단하지 않는다. [EN HandleDead; W1/W2]

현재안보다 큰 대안은 당장 채택하지 않는다. 실제 전송 병목이 확인되면 짧은 묶음 전송에 **개별 피해 레코드 전체**를 담는 개선을 검토할 수 있다. 하나의 합산 숫자나 마지막 값으로 줄이면 이번 “두 숫자” 요구가 사라진다. VFX 반복 생성이 병목이면 먼저 확인된 기존 로컬 풀을 사용한다. 유효한 서버 피해를 줄이거나 중복 방지 규칙을 느슨하게 하는 최적화는 하지 않는다.

## 9. 적용 후 검증 — 기존 29/39/76을 표시 완료로 쓰지 않기

round-03 인계 사본은 이전 review의 내용을 보존한 문서다. 이를 수용했다는 사실과 검증기·원문 근거가 보완됐다는 사실은 다르다. 이번 표시 패치는 R03의 검증기 안전성·보상 수령자·실행 원문 항목을 자동 종결하지 않는다. 기존 Editor 러너는 미Spawn 객체를 사용하므로 새 RPC의 등록 객체 가드 때문에 네트워크 표시를 검사하지 않는 것이 정상이다. 피해 로직 회귀와 실제 표시 검사를 별도로 수행한다. [HAND3; VALI]

| 시험 | 합격 기준 |
| --- | --- |
| 동일 프레임의 219.10 + 45.42 | 서버 HP가 합계 264.52만 감소하고, 관찰 중인 각 클라이언트의 PresentedDamageTextCount가 2 증가한다. **서로 다른 활성 텍스트 객체 두 개**에 각각 원값이 전달되고 219/45로 읽히는지 확인한다. |
| 동일 값 45 + 45 | 값이 같아도 텍스트 2개다. SyncVar 값 변화에 기대지 않는다. |
| Host+원격 Client | 각 화면에서 두 숫자와 화염 1회. Host만 4숫자/2화염이 되거나 Host만 누락되면 불합격이다. |
| 직접 처치 | 숫자 1개, Inferno 성공 카운터 증가 없음, 화염 0회. |
| 추가타 처치 | 숫자 2개, 성공 카운터와 PresentedInfernoHitCount 각각 해당 경로에서 1 증가. 사망 뒤 Transform 접근 오류가 없고 화염이 저장한 위치에서 나온다. |
| 큐 등록 후 스킵 | 성공 콜백이 불리지 않아 화염 0회. 단지 enqueue=true였다는 이유로 재생하지 않는다. |
| 아크 회귀 | 정상 직격·연쇄 대상 각각 피해 사건 표시. 기존 번개 선의 생성·비치명·수명 및 공격당 1회 규칙 유지. |
| 다중 대상/4인 혼합 | 대상마다 모든 숫자가 표시되고 각 Inferno 성공에 대응하는 VFX가 있다. 서로 다른 대여 객체·읽을 수 있는 배치·풀 여유를 함께 확인한다. |
| 체력바/보스 | LateUpdate의 HP 비율·회전·사망 숨김·PhaseOne/TransitionArmor/PhaseTwo 처리가 유지된다. |
| 클라이언트 수명 | 카메라/풀 교체·플레이어 PRE 비활성/파괴·씬 전환 후 잔여 임팩트·사운드가 없다. Destroy 검사는 프레임 종료 이후에 한다. |
| 서버와 설정 누락 | 전용 서버에서 로컬 파티클/오디오/숫자를 생성하지 않는다. PRE/프리팹/풀 누락은 경고 후 피해 계산에 영향을 주지 않는다. 누락 상태 자체를 표시 시험 PASS로 인정하지 않는다. |
| 네트워크 경계 | AOI 차이, 적 사망/Destroy, 공격자 종료, 늦은 참가, 지연·손실을 확인하고 수신 대상·미재생 정책을 기록한다. |
| 전투 회귀 | 승인된 안전한 환경에서 Inferno/Arc/P1 회귀를 다시 실행하고 실제 함수·검사 수·로그를 남긴다. UI 수정으로 기대 피해나 검사 조건을 바꾸지 않는다. |

표시 카운터는 메서드 실행 확인 수단이다. 실제 픽셀·객체 식별·풀 대여 여부·오디오 재생까지 자동 증명하지 않으므로 화면과 객체 추적을 병행한다. 표시 검증을 위해 피해 함수를 두 번 호출하거나 추가타를 다음 프레임으로 미루지 않는다.

## 10. 실제 읽은 자료와 미확인 사항

경로는 모두 이번 `round-04/evidence/` 기준이다. REQ는 round-04/request.md, HAND3는 이번 evidence에 실린 이전 검토 사본이다. 인용은 파일 약어와 실제 함수/절을 사용하며, 이전 회차의 다른 증거 ID와 혼합하지 않는다.

| 약어 | 사본 경로 | 읽은 범위 |
| --- | --- | --- |
| EN | Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyAuthority_MirrorTest.cs | 전체 1,039줄 |
| VIEW | Assets/SW/TEST/MirrorCombat/Scripts/NetworkEnemyCombatView_MirrorTest.cs | 전체 198줄 |
| PRE | Assets/SW/TEST/MirrorPlayerContext/Scripts/UniqueEffectPresentation_MirrorTest.cs | 전체 105줄 |
| IT | Assets/SW/TEST/MirrorPlayerContext/Scripts/ItemTriggerManager_MirrorTest.cs | 전체 291줄 |
| RES | Assets/SW/TEST/MirrorPlayerContext/Scripts/WBH_CombatResolver_MirrorTest.cs | 전체 213줄 |
| VALI | Assets/Editor/InfernoExtraHitValidation_MirrorTest.cs | 전체 295줄 |
| HAND3 | Docs/Architecture/UniqueEffect_P2_ArcBlade_Inferno_Round03_Handover.md | 전체 238줄 |

이번에는 manifest 15종 중 표시 설계에 직접 필요한 위 7종과 요청·manifest·준비 표식·공통 운영 규칙을 읽었다. 나머지 8종을 새로 전문 검토하거나 무결성을 인증했다고 주장하지 않는다. manifest의 해시는 작성자 제공값이며 독립 해시 재계산·원본과의 동일성은 NOT_VERIFIED다. 실측치는 요청서의 작성자 보고다. 정확한 Mirror 패키지 버전·Transport 설정, 표시 풀 내부·임팩트 프리팹·사운드·씬 배치는 미확인이다.

### 공식 기술 근거

2026-09-16 확인. 일반 API 의미의 근거이며 프로젝트 실행 인증이 아니다.

| ID | 공식 자료 | 사용 범위 |
| --- | --- | --- |
| W1 | Mirror Synchronization / SyncVars — `https://mirror-networking.gitbook.io/docs/manual/guides/synchronization` 및 `https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars` | 변경된 현재 상태 직렬화, 늦은 참가자의 최신 상태. |
| W2 | Mirror Remote Actions — `https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions` | Spawn 객체, Host ClientRpc, 관찰자 전달, 인자 직렬화. |
| W3 | Unity 6000.3 Camera.ScreenToWorldPoint — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera.ScreenToWorldPoint.html` | 화면 픽셀 위치·깊이를 보존한 월드 위치 환산. |
| W4 | Unity 6000.3 ParticleSystem.Play — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.Play.html` | 자식 포함 파티클 재생. |
| W5 | Unity 6000.3 AudioSource.Play — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource.Play.html` | 지정된 clip 재생. |
| W6 | Unity 6000.3 WaitForSecondsRealtime — `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/WaitForSecondsRealtime.html` | 시간 배율과 분리된 표시 수명 대기. |
| W7 | Mirror Attributes — `https://mirror-networking.gitbook.io/docs/manual/guides/attributes` | 네트워크 전용 속성과 RPC의 역할. Reliable 채널 지원은 적용 시 설치된 Mirror에서 컴파일 확인. |

## 11. 저장 범위와 최종 권고

이번 산출물은 이 회차의 review.md이며, 전체 재읽기 뒤 REVIEW_READY.txt를 발행한다. request·evidence·CURRENT_ROUND·이전 회차·다른 태스크·공통 규칙은 수정하지 않는다. 사용자 PC의 원본 프로젝트·터미널·Git·Unity Editor는 실행하거나 변경하지 않았다.

**권고는 표시 경로의 좁은 4파일 변경과 명시적 자산 연결이다.** 숫자 전송은 EN 한 곳, 숫자 생성은 기존 VIEW/풀, 화염 발생 판단은 IT의 성공 콜백, 로컬 VFX/SFX 수명은 기존 PRE가 맡는다. 코드 블록은 제안 구현이며 적용·컴파일·네트워크 합격을 대신하지 않는다. 두 결함의 실제 완료선은 “서버 피해 유지 + 각 관찰자에게 분리된 숫자 2개 + 성공 추가타에 화염/소리 1회 + 정상 정리”다.
