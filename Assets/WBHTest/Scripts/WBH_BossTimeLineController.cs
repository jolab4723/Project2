using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class WBH_BossTimeLineController : MonoBehaviour
{
    [SerializeField] private WBH_EnemySpawner enemySpawner;

    [Header("TimeLine")]
    [SerializeField] private PlayableDirector director;

    [Header("Gameplay")] // 바인드 확인용
    [SerializeField] private T_PlayerController player;
    [SerializeField] private WBH_EnemyController boss;

    [Header("Component disabled during Timeline")]
    [SerializeField] private Behaviour[] playerBehaviours;
    [SerializeField] private WBH_HighEnemyHpbarView enemyHpbar;
    [SerializeField] private KY_HUDAnimator[] hudAnimators;

    [Header("Localization")]
    [Tooltip("적 이름 다국어 DB. 비워두면 Resources에서 공용 DB를 자동으로 찾아 쓴다.")]
    [SerializeField] private EnemyLabelDatabaseSO enemyLabels;
    [SerializeField] private TMP_Text bossEnemyNameText;

    private Renderer[] bossRenderers;
    private bool[] previousBossRendererStates;

    private bool[] previousInputStates;
    private bool isCutscenePlaying;

    // 타임라인의 플레이어 트랙은 원점 기준(ApplyTransformOffsets)이라 런타임 플레이어를
    // 현재 위치의 임시 앵커 아래에 두고 바인딩한다. (멀티의 MirrorBossIntro와 같은 방식)
    private const string PlayerTrackName = "Player_AnimationTrack";
    private AnimationTrack bindPlayerTrack;
    private Transform playerAnchor;
    private Transform playerOriginalParent;


    private void OnEnable()
    {
        if (director != null)
        {
            director.stopped += HandleDirectorStopped;
        }
        if(enemySpawner != null)
        {
            enemySpawner.BossSpawn += HandleBossSpawn;
        }
    }

    private void OnDisable()
    {
        if(director != null)
        {
            director.stopped -= HandleDirectorStopped;
        }
        if (enemySpawner != null)
        {
            enemySpawner.BossSpawn -= HandleBossSpawn;
        }

        ReleaseCutscene();
    }

    private void HandleBossSpawn(WBH_EnemyController spawnBoss)
    {
        BindBoss(spawnBoss);
        SetBossName(boss.Info);
        BeginCutscene();
    }

    public void BindBoss(WBH_EnemyController runtimeBoss)
    {
        boss = runtimeBoss;
    }

    private void BindPlayer()
    {
        if(player == null)
        {
            player = FindFirstObjectByType<T_PlayerController>(); // !@차후 멀티플레이에서 각 플레이어 Context 불러오는 코드로 변경 필요
        }

        if (player == null)
            return;

        playerBehaviours = new Behaviour[]
        {
            player.GetComponent<PlayerActionInputHandler>(),
            player.GetComponent<WBH_PlayerInputHandler>()
        };
    }

    public void BeginCutscene()
    {
        if (isCutscenePlaying)
            return;

        if(director == null || director.playableAsset == null)
        {
            Log.Error("PlayableDirector, 타임라인이 없습니다");
            return;
        }

        BindPlayer();

        if (player == null || boss == null)
        {
            Log.Error("플레이어, 보스 참조가 없습니다");
            return;
        }

        if (!TryBindPlayerTrack())
            return;

        isCutscenePlaying = true;

        SetPlayerInputBlocked(true);

        player.SetCutSceneControlBlock(true);
        player.SetCutSceneDamageBlock(true);

        boss.SetCutSceneControlBlock(true);
        boss.SetCutSceneDamageBlock(true);

        HideRuntimeBoss();

        director.time = 0d;
        director.Play();
    }

    public void SkipCutscene()
    {
        if (!isCutscenePlaying)
            return;
        director.Stop();
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director)
            return;

        ReleaseCutscene();
    }

    private void ReleaseCutscene()
    {
        if (!isCutscenePlaying)
            return;

        isCutscenePlaying = false;

        if(boss != null)
        {
            ShowRuntimeBoss();

            boss.SetCutSceneDamageBlock(false);
            boss.SetCutSceneControlBlock(false);
        }

        ReleasePlayerTrack();

        if(player != null)
        {
            player.SetCutSceneDamageBlock(false);
            player.SetCutSceneControlBlock(false);
        }

        SetPlayerInputBlocked(false);
        RestoreBossHpbar();
        RestoreHud();
    }

    private void SetPlayerInputBlocked(bool blocked)
    {
        if (playerBehaviours == null)
            return;

        if(blocked)
        {
            previousInputStates = new bool[playerBehaviours.Length];

            for(int i = 0; i < playerBehaviours.Length; i++)
            {
                Behaviour inputBehaviour = playerBehaviours[i];

                if (inputBehaviour == null)
                    continue;

                previousInputStates[i] = inputBehaviour.enabled;
                inputBehaviour.enabled = false;
            }
            return;
        }

        if (previousInputStates == null)
            return;

        int count = Mathf.Min(playerBehaviours.Length, previousInputStates.Length);

        for(int i = 0; i < count; i++ )
        {
            Behaviour inputBehaviour = playerBehaviours[i];

            if(inputBehaviour != null)
            {
                inputBehaviour.enabled = previousInputStates[i];
            }
        }

        previousInputStates = null;
    }

    private bool TryBindPlayerTrack()
    {
        if(director.playableAsset is not TimelineAsset timelineAsset)
        {
            Log.Error("PlayableDirector 에 타임라인이 연결되어 있지 않습니다.");
            return false;
        }

        bindPlayerTrack = FindPlayerTrack(timelineAsset);

        Animator playerAnimator = player.GetComponent<Animator>();
        
        if (bindPlayerTrack == null || playerAnimator == null)
            return false;

        Transform playerTransform = player.transform;
        playerAnchor = new GameObject("BossIntro Player Anchor").transform;
        playerAnchor.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
        playerOriginalParent = playerTransform.parent;
        playerTransform.SetParent(playerAnchor, true);

        // 컷씬 동안 NavMeshAgent가 타임라인 이동을 덮어쓰지 않게 한다.
        if (player.agent != null)
            player.agent.updatePosition = false;

        director.SetGenericBinding(bindPlayerTrack, playerAnimator);

        return true;
    }

    /// <summary>
    /// SW 수정: 보스 인트로 타임라인의 플레이어 트랙 규격(트랙 이름)을 한 곳에 둔다.
    /// 멀티(MirrorBossIntro)도 이 함수로 찾으므로 Act마다 Inspector에 트랙을 꽂지 않아도 된다.
    /// </summary>
    public static AnimationTrack FindPlayerTrack(TimelineAsset timelineAsset)
    {
        if (timelineAsset == null)
            return null;

        foreach (TrackAsset outputTrack in timelineAsset.GetOutputTracks())
        {
            if (outputTrack is AnimationTrack animationTrack && outputTrack.name == PlayerTrackName)
                return animationTrack;
        }
        return null;
    }

    private void ReleasePlayerTrack()
    {
        if (bindPlayerTrack != null && director != null)
            director.ClearGenericBinding(bindPlayerTrack);
        bindPlayerTrack = null;

        if (playerAnchor == null)
            return;

        if (player != null)
        {
            player.transform.SetParent(playerOriginalParent, true);

            if (player.agent != null)
            {
                player.agent.updatePosition = true;
                if (player.agent.isActiveAndEnabled)
                    player.agent.Warp(player.transform.position);
            }
        }

        Destroy(playerAnchor.gameObject);
        playerAnchor = null;
        playerOriginalParent = null;
    }

    /// <summary>언어 반응형 적 이름 DB가 있으면 그 값을, 없으면 스폰 시점에 저장된 이름을 그대로 반환한다.</summary>
    private void SetBossName(WBH_EnemyInfo info)
    {
        // 씬에서 직접 안 배선해도(다른 맵/스테이지 씬 등) Resources의 공용 DB를 자동으로 찾아 쓴다.
        if (enemyLabels == null)
            enemyLabels = Resources.Load<EnemyLabelDatabaseSO>("DataFiles/EnemyData/3. GeneratedAssets/LabelData/EnemyLabelDatabase");

        if (info == null)
            return;

        bossEnemyNameText.text = enemyLabels != null ? enemyLabels.GetName(info.enemyId) : info.enemyName;

        return;
    }

    // HUD 위치 복구 메서드.
    private void RestoreHud()
    {
        if (hudAnimators == null)
            return;

        foreach(KY_HUDAnimator hudAnimator in hudAnimators)
        {
            if(hudAnimator != null && hudAnimator.gameObject.activeInHierarchy)
            {
                hudAnimator.SlideIn();
            }
        }
    }

    private void RestoreBossHpbar()
    {
        if (boss == null)
            return;

        if(enemyHpbar != null)
        {
            enemyHpbar.BindBoss(boss);
        }
    }

    private void HideRuntimeBoss()
    {
        if (boss == null)
            return;

        bossRenderers = boss.GetComponentsInChildren<Renderer>(true);
        previousBossRendererStates = new bool[bossRenderers.Length];

        for (int i = 0; i < bossRenderers.Length; i ++)
        {
            Renderer bossRenderer = bossRenderers[i];

            if (bossRenderer == null)
                continue;

            previousBossRendererStates[i] = bossRenderer.enabled;
            bossRenderer.enabled = false;
        }
    }

    private void ShowRuntimeBoss()
    {
        if (bossRenderers == null || previousBossRendererStates == null)
            return;

        int count = Mathf.Min(bossRenderers.Length, previousBossRendererStates.Length);

        for (int i = 0; i < count; i++)
        {
            if (bossRenderers[i] != null)
            {
                bossRenderers[i].enabled = previousBossRendererStates[i];
            }
        }

        bossRenderers = null;
        previousBossRendererStates = null;
    }

    // 컷씬 스킵 테스트.
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape) && isCutscenePlaying)
            SkipCutscene();
    }

    // 보험용 강제 종료 메서드 현재는 사용 X
    public void CompleteCutscene()
    {
        if (!isCutscenePlaying)
            return;

        if (director != null)
        {
            director.Stop();
        }

        ReleaseCutscene();
    }
}
