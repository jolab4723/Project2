using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class YJ_PointMove : MonoBehaviour
{
    private enum TriggerRole
    {
        Auto,
        Activation,
        Reset
    }

    [Header("트리거 역할")]
    [SerializeField] private TriggerRole triggerRole = TriggerRole.Auto;

    [Header("플랫폼")]
    [SerializeField] private Rigidbody platformRigidbody;
    [SerializeField] private GameObject block;

    [Header("이동 지점")]
    [SerializeField] private List<Transform> movePoints;
    [SerializeField] private float moveSpeed = 5f;

    [Header("도착 지점 NavMesh")]
    [SerializeField] private float navMeshSearchDistance = 1.5f;

    private int pointIndex;
    private bool isMoving;
    private bool canActivate = true;
    private bool resetRequested;

    private TriggerRole resolvedTriggerRole;
    private Collider triggerCollider;
    private YJ_PointMove activationController;
    private readonly HashSet<Collider> playerCollidersInResetArea = new();

    private Rigidbody passengerRigidbody;
    private NavMeshAgent passengerAgent;
    private T_PlayerController passengerController;

    private readonly WaitForFixedUpdate waitForFixedUpdate =
        new WaitForFixedUpdate();

    private void Awake()
    {
        resolvedTriggerRole = ResolveTriggerRole();
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null || !triggerCollider.isTrigger)
        {
            Log.Error($"{name}에 Trigger Collider가 필요합니다.");
            enabled = false;
            return;
        }

        if (resolvedTriggerRole == TriggerRole.Reset)
            activationController = FindActivationController();
    }

    private void Start()
    {
        if (resolvedTriggerRole != TriggerRole.Activation)
            return;

        if (platformRigidbody == null)
        {
            Log.Error("Platform Rigidbody가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        if (movePoints == null || movePoints.Count < 2)
        {
            Log.Error("이동 지점이 2개 이상 필요합니다.");
            enabled = false;
            return;
        }

        platformRigidbody.isKinematic = true;
        platformRigidbody.useGravity = false;
        platformRigidbody.position = movePoints[0].position;

        pointIndex = 0;

        if (block != null)
            block.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetPlayerRigidbody(other, out Rigidbody enteredRigidbody))
            return;

        if (resolvedTriggerRole == TriggerRole.Reset)
        {
            playerCollidersInResetArea.Add(other);
            return;
        }

        if (resolvedTriggerRole != TriggerRole.Activation ||
            isMoving ||
            !canActivate)
            return;

        passengerRigidbody = enteredRigidbody;
        passengerAgent = enteredRigidbody.GetComponent<NavMeshAgent>();
        passengerController =
            enteredRigidbody.GetComponent<T_PlayerController>();

        canActivate = false;
        resetRequested = false;
        triggerCollider.enabled = false;

        PreparePassenger();

        int endPointIndex = (pointIndex + 1) % movePoints.Count;
        StartCoroutine(Move(endPointIndex));
    }

    private void OnTriggerExit(Collider other)
    {
        if (resolvedTriggerRole != TriggerRole.Reset ||
            !TryGetPlayerRigidbody(other, out _))
            return;

        playerCollidersInResetArea.Remove(other);

        if (playerCollidersInResetArea.Count > 0)
            return;

        if (activationController == null)
            activationController = FindActivationController();

        if (activationController == null)
        {
            Log.Error("Collider1의 YJ_PointMove를 찾지 못했습니다.");
            return;
        }

        activationController.RequestActivationReset();
    }

    private void PreparePassenger()
    {
        // 기존 API를 이용해 이동 입력과 진행 중인 경로를 정지합니다.
        if (passengerController != null)
            passengerController.SetControlEnable(false);

        if (passengerAgent == null || !passengerAgent.enabled)
            return;

        passengerAgent.ResetPath();
        passengerAgent.isStopped = true;

        // 상승 중 NavMesh가 플레이어 위치를 되돌리지 못하게 합니다.
        passengerAgent.enabled = false;
    }

    private IEnumerator Move(int endPointIndex)
    {
        isMoving = true;

        if (block != null)
            block.SetActive(true);

        Vector3 endPosition = movePoints[endPointIndex].position;

        while ((platformRigidbody.position - endPosition).sqrMagnitude >
               0.0001f)
        {
            Vector3 currentPosition = platformRigidbody.position;

            Vector3 nextPosition = Vector3.MoveTowards(
                currentPosition,
                endPosition,
                moveSpeed * Time.fixedDeltaTime);

            Vector3 moveDelta = nextPosition - currentPosition;

            platformRigidbody.MovePosition(nextPosition);

            if (passengerRigidbody != null)
            {
                passengerRigidbody.MovePosition(
                    passengerRigidbody.position + moveDelta);
            }

            yield return waitForFixedUpdate;
        }

        platformRigidbody.position = endPosition;
        pointIndex = endPointIndex;

        RestorePassenger();

        if (block != null)
            block.SetActive(false);

        isMoving = false;

        if (resetRequested)
            EnableActivationTrigger();
    }

    private void RestorePassenger()
    {
        if (passengerRigidbody == null)
        {
            ClearPassenger();
            return;
        }

        if (passengerAgent != null)
        {
            Vector3 passengerPosition = passengerRigidbody.position;

            if (NavMesh.SamplePosition(
                passengerPosition,
                out NavMeshHit hit,
                navMeshSearchDistance,
                NavMesh.AllAreas))
            {
                passengerRigidbody.position = hit.position;

                passengerAgent.enabled = true;
                passengerAgent.Warp(hit.position);

                if (passengerController != null)
                    passengerController.SetControlEnable(true);
            }
            else
            {
                Log.Error(
                    $"엘리베이터 도착 지점 근처에서 NavMesh를 찾지 못했습니다: " +
                    $"{passengerPosition}");
            }
        }
        else if (passengerController != null)
        {
            passengerController.SetControlEnable(true);
        }

        ClearPassenger();
    }

    private void ClearPassenger()
    {
        passengerRigidbody = null;
        passengerAgent = null;
        passengerController = null;
    }

    private void RequestActivationReset()
    {
        if (resolvedTriggerRole != TriggerRole.Activation)
            return;

        if (isMoving)
        {
            resetRequested = true;
            return;
        }

        EnableActivationTrigger();
    }

    private void EnableActivationTrigger()
    {
        resetRequested = false;
        canActivate = true;

        if (triggerCollider != null)
            triggerCollider.enabled = true;
    }

    private TriggerRole ResolveTriggerRole()
    {
        if (triggerRole != TriggerRole.Auto)
            return triggerRole;

        if (gameObject.name == "Collider1")
            return TriggerRole.Activation;

        if (gameObject.name == "Collider2")
            return TriggerRole.Reset;

        Log.Error($"{name}의 트리거 역할을 판별할 수 없습니다.");
        return TriggerRole.Auto;
    }

    private YJ_PointMove FindActivationController()
    {
        Transform searchRoot = transform.parent != null
            ? transform.parent
            : transform;

        YJ_PointMove[] controllers =
            searchRoot.GetComponentsInChildren<YJ_PointMove>(true);

        foreach (YJ_PointMove controller in controllers)
        {
            if (controller != this &&
                controller.ResolveTriggerRole() == TriggerRole.Activation)
                return controller;
        }

        return null;
    }

    private static bool TryGetPlayerRigidbody(
        Collider other,
        out Rigidbody playerRigidbody)
    {
        playerRigidbody = other.attachedRigidbody;

        return playerRigidbody != null &&
               playerRigidbody.CompareTag("Player");
    }
}
