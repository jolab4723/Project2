using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(AudioSource))]
public class YJ_PlayerFootstepLoop : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.01f)]
    private float minimumMoveSpeed = 0.1f;

    private AudioSource footstepSource;
    private NavMeshAgent agent;
    private WBH_PlayerStateMachine stateMachine;

    [Header("Playback Speed")]
    [SerializeField, Min(0.01f)] private float referenceMoveSpeed = 5f;

    [SerializeField, Range(0.1f, 3f)] private float minimumPitch = 0.5f;

    [SerializeField, Range(0.1f, 3f)] private float maximumPitch = 2f;

    private void Awake()
    {
        footstepSource = GetComponent<AudioSource>();
        agent = GetComponentInParent<NavMeshAgent>();
        stateMachine = GetComponentInParent<WBH_PlayerStateMachine>();

        footstepSource.playOnAwake = false;
        footstepSource.loop = true;
        footstepSource.dopplerLevel = 0f;

        if (agent == null || stateMachine == null)
        {
            Debug.LogWarning($"{name}: 발소리에 필요한 플레이어 컴포넌트가 없습니다.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if ( ! ShouldPlay())
        {
            footstepSource.Stop();
            return;
        }

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        float speedRatio = velocity.magnitude / Mathf.Max(0.01f, referenceMoveSpeed);
        float lowerPitch = Mathf.Clamp(minimumPitch, 0.1f, 3f);
        float upperPitch = Mathf.Clamp(maximumPitch, lowerPitch, 3f);

        footstepSource.pitch = Mathf.Clamp(speedRatio, lowerPitch, upperPitch);

        // 재생 중에는 pitch만 변경하고 재시작하지 않는다.
        if ( ! footstepSource.isPlaying)
            footstepSource.Play();
    }

    private bool ShouldPlay()
    {
        if (footstepSource == null ||
            !footstepSource.isActiveAndEnabled ||
            footstepSource.clip == null)
            return false;

        if (Time.timeScale <= 0f || AudioListener.pause)
            return false;

        if (agent == null ||
            !agent.isActiveAndEnabled ||
            !agent.isOnNavMesh ||
            agent.isStopped)
            return false;

        if (stateMachine == null ||
            !stateMachine.IsAnyState(PlayerState.Move, PlayerState.Chase))
            return false;

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        return velocity.sqrMagnitude >
               minimumMoveSpeed * minimumMoveSpeed;
    }

    private void OnDisable()
    {
        if (footstepSource != null)
            footstepSource.Stop();
    }
}
