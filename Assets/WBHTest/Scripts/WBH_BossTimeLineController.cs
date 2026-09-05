using UnityEngine;
using UnityEngine.Playables;

public class WBH_BossTimeLineController : MonoBehaviour
{
    [SerializeField] private WBH_EnemySpawner enemySpawner;

    [Header("TimeLine")]
    [SerializeField] private PlayableDirector director;

    [Header("Gameplay")]
    [SerializeField] private T_PlayerController player;
    [SerializeField] private WBH_EnemyController boss;

    [Header("Component disabled during Timeline")]
    [SerializeField] private Behaviour[] playerBehaviours;

    private bool[] previousInputStates;
    private bool isCutscenePlaying;

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
        BeginCutscene();
    }

    public void BindBoss(WBH_EnemyController runtimeBoss)
    {
        boss = runtimeBoss;
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

        if(player == null || boss == null)
        {
            Log.Error("플레이어, 보스 참조가 없습니다");
            return;
        }

        isCutscenePlaying = true;

        SetPlayerInputBlocked(true);

        player.SetCutSceneControlBlock(true);
        player.SetCutSceneDamageBlock(true);

        boss.SetCutSceneControlBlock(true);
        boss.SetCutSceneDamageBlock(true);

        director.time = 0d;
        director.Play();
    }

    public void SkipCutscene()
    {
        if (!isCutscenePlaying)
            return;
        director.Stop();
    }

    public void CompleteCutscene()
    {
        if (!isCutscenePlaying)
            return;

        ReleaseCutscene();

        if(director != null)
        {
            director.Stop();
        }
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

        boss.SetCutSceneDamageBlock(false);
        boss.SetCutSceneControlBlock(false);

        player.SetCutSceneDamageBlock(false);
        player.SetCutSceneControlBlock(false);

        SetPlayerInputBlocked(false);
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

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.F8))
            SkipCutscene();
    }
}
