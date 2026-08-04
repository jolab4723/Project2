using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Threading.Tasks;

public class YJ_PortalActive : MonoBehaviour
{
    [SerializeField] private Light targetLight;

    [SerializeField] private float maxIntensity = 100f;
    [SerializeField] private float duration = 3f;
    private float activationDelay = 0.5f;
    [SerializeField] private bool isCamp = false;

    [SerializeField, Min(0f)] private float searchRadius = 8f;
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 2f;
    [SerializeField, Min(1)] private int maxAttempts = 30;
    [SerializeField] private float groundOffset;
    [SerializeField, Min(0f)] private float playerClearance = 0.5f;
    [SerializeField] private LayerMask playerLayerMask;

    private SphereCollider sphereCollider;
    private Vector3 searchCenter;
    private int activationRequestId;

    void Awake()
    {
        targetLight = GetComponentInChildren<Light>();
        sphereCollider = GetComponentInChildren<SphereCollider>();
        searchCenter = transform.position;
    }

    void Start()
    {
        gameObject.SetActive(isCamp);

        if (targetLight != null)
            targetLight.intensity = isCamp ? maxIntensity : 0f;
    }

    public void Active(bool active)
    {
        int requestId = ++activationRequestId;

        if (!active)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(false);
        _ = ActivateAfterDelayAsync(requestId);
    }

    private async Task ActivateAfterDelayAsync(int requestId)
    {
        int delayMilliseconds = Mathf.Max(0, Mathf.RoundToInt(activationDelay * 1000f));
        await Task.Delay(delayMilliseconds);

        if (this == null || requestId != activationRequestId)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Log.Warning("Portal 활성화를 위한 Player 오브젝트를 찾지 못했습니다.");
            return;
        }

        if (!TryActivate(player.transform.position))
        {
            Log.Warning("Portal을 활성화할 수 있는 NavMesh 위치를 찾지 못했습니다.");
            return;
        }

        if (targetLight != null)
            StartCoroutine(LightOn());
    }

    private IEnumerator LightOn()
    {
        targetLight.intensity = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsedTime / duration);
            targetLight.intensity = Mathf.Lerp(0f, maxIntensity, ratio);

            yield return null;
        }
        targetLight.intensity = maxIntensity;
    }

    private bool TryActivate(Vector3 playerPosition)
    {
        if (sphereCollider == null)
            sphereCollider = GetComponentInChildren<SphereCollider>();

        if (sphereCollider == null)
            return false;

        gameObject.SetActive(false);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 randomOffset = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * searchRadius;
            Vector3 candidate = searchCenter + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if ( ! NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            transform.position = hit.position + Vector3.up * groundOffset;

            if (IsOverlappingPlayer(playerPosition))
                continue;

            gameObject.SetActive(true);
            return true;
        }

        return false;
    }

    private bool IsOverlappingPlayer(Vector3 playerPosition)
    {
        Transform colliderTransform = sphereCollider.transform;
        Vector3 scale = colliderTransform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        float worldRadius = sphereCollider.radius * largestScale + playerClearance;
        Vector3 worldCenter = colliderTransform.TransformPoint(sphereCollider.center);

        Vector2 portalPosition = new Vector2(worldCenter.x, worldCenter.z);
        Vector2 currentPlayerPosition = new Vector2(playerPosition.x, playerPosition.z);
        if ((portalPosition - currentPlayerPosition).sqrMagnitude < worldRadius * worldRadius)
            return true;

        return Physics.CheckSphere(
            worldCenter,
            worldRadius,
            playerLayerMask,
            QueryTriggerInteraction.Collide);
    }
}
