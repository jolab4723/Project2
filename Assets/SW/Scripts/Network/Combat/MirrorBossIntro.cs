using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 서버 시각으로 보스 인트로를 재생한다.
/// SW 수정: 싱글 보스 인트로(WBH_BossTimeLineController)의 PlayableDirector와 연출 객체를 그대로 쓰고,
/// 멀티 전용으로는 4인 대열 외형 복제본만 만든다. 플레이어 트랙은 싱글과 같은 규격(트랙 이름)으로 찾는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirrorBossIntro : NetworkBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private GameObject hud;
    [SerializeField] private Animator fighterVisual;
    [SerializeField] private Animator gunnerVisual;
    [SerializeField] private Transform playerTimelineOrigin;
    [SerializeField] private Transform[] playerPoints;
    [SerializeField] private TMPro.TMP_Text bossNameText;
    [SerializeField] private string bossEnemyId;

    [SyncVar] private double startsAt;
    [SyncVar] private double endsAt;
    [SyncVar] private bool complete;
    [SyncVar] private byte occupiedSlots;
    [SyncVar] private byte gunnerSlots;
    private bool presenting;
    private bool hudWasActive;
    private readonly List<GameObject> actors = new();
    private readonly List<GameObject> actorAnchors = new();
    private readonly Dictionary<Renderer, bool> hiddenRenderers = new();
    private MirrorSpawnedPlayerBinder localBinder;
    private Animator primaryActor;
    private AnimationTrack playerTrack;
    // SW 수정: 공유 디렉터는 싱글 카메라(멀티에서 꺼짐)에 묶여 있으므로 재생 중에만 현재 메인 카메라로 바꿔 묶는다.
    private readonly Dictionary<TrackAsset, Object> reboundTracks = new();
    private DirectorUpdateMode previousUpdateMode;
    private DirectorWrapMode previousWrapMode;

    public bool IsComplete => complete;
    /// <summary>서버가 인트로를 예약한 뒤 완료하기 전까지 true다. 신규 행동 승인 판정에 사용한다.</summary>
    public bool IsInProgress => startsAt > 0d && !complete;
    public bool IsPresenting => presenting;
    public double StartsAt => startsAt;
    public double EndsAt => endsAt;
    public double PlaybackTime => director != null ? director.time : 0d;
    public int VisualActorCount => actors.Count;

    [Server]
    public bool ServerBegin(MirrorNetworkManager manager, float leadIn)
    {
        if (startsAt > 0d || manager == null || director == null || ResolvePlayerTrack() == null ||
            fighterVisual == null || gunnerVisual == null || playerTimelineOrigin == null ||
            playerPoints == null || playerPoints.Length != 4 ||
            director.duration <= 0d || double.IsInfinity(director.duration))
        {
            Debug.LogError("[MirrorBossIntro] 인트로 참조 또는 시작 상태가 올바르지 않습니다.", this);
            return false;
        }

        foreach (var member in manager.ServerRoster.Members)
        {
            occupiedSlots |= (byte)(1 << member.Slot);
            if (member.CharacterClass == ItemSystem.CharacterClass.Gunner)
                gunnerSlots |= (byte)(1 << member.Slot);
        }
        startsAt = NetworkTime.time + Mathf.Max(0.5f, leadIn);
        endsAt = startsAt + director.duration;
        complete = false;
        Debug.Log($"[MirrorBossIntro] scheduled start={startsAt:F3} end={endsAt:F3} slots={occupiedSlots}");
        return true;
    }

    private void Update()
    {
        if (isServer && startsAt > 0d && !complete && NetworkTime.time >= endsAt)
        {
            complete = true;
            Debug.Log("[MirrorBossIntro] server completed; boss spawn unlocked");
        }
    }

    private void LateUpdate()
    {
        if (!isClient || startsAt <= 0d) return;
        if (complete)
        {
            ReleasePresentation();
            return;
        }
        // 재접속한 클라이언트는 현재 서버 시각에서 재생을 이어간다.
        if (!presenting) BeginPresentation();
        BindAndHidePlayers();
        director.time = System.Math.Max(0d, System.Math.Min(NetworkTime.time - startsAt, director.duration));
        director.Evaluate();
        ApplyPlayerFormationAnimation();
    }

    private void BeginPresentation()
    {
        if (actorAnchors.Count > 0)
        {
            foreach (var anchor in actorAnchors)
            {
                if (anchor != null)
                {
                    if (Application.isPlaying) Destroy(anchor);
                    else DestroyImmediate(anchor);
                }
            }
            actorAnchors.Clear();
            actors.Clear();
            primaryActor = null;
        }

        presenting = true;
        // 싱글 보스 연출과 같은 공용 DB에서 현재 언어의 이름을 가져온다.
        if (bossNameText != null && !string.IsNullOrEmpty(bossEnemyId))
        {
            var labels = Resources.Load<EnemyLabelDatabaseSO>("DataFiles/EnemyData/3. GeneratedAssets/LabelData/EnemyLabelDatabase");
            if (labels != null) bossNameText.text = labels.GetName(bossEnemyId);
        }
        hudWasActive = hud != null && hud.activeSelf;
        if (hud != null) hud.SetActive(false);
        ResolvePlayerTrack();
        Vector3 actorStartPosition = playerTimelineOrigin.InverseTransformPoint(playerPoints[0].position);
        Quaternion actorStartRotation = Quaternion.Inverse(playerTimelineOrigin.rotation) * playerPoints[0].rotation;
        for (int slot = 0; slot < 4; slot++)
        {
            if ((occupiedSlots & (1 << slot)) == 0) continue;
            var template = (gunnerSlots & (1 << slot)) != 0 ? gunnerVisual : fighterVisual;
            var anchorObject = new GameObject($"Intro Player Slot {slot} Anchor");
            var anchor = anchorObject.transform;
            anchor.SetParent(playerTimelineOrigin.parent, false);
            Vector3 formationOffset = playerPoints[0].InverseTransformPoint(playerPoints[slot].position);
            anchor.position = playerTimelineOrigin.TransformPoint(formationOffset);
            anchor.rotation = playerTimelineOrigin.rotation * Quaternion.Inverse(playerPoints[0].rotation) * playerPoints[slot].rotation;
            actorAnchors.Add(anchorObject);

            var actor = Instantiate(template, anchor);
            actor.transform.localPosition = actorStartPosition;
            actor.transform.localRotation = actorStartRotation;
            actor.transform.localScale = template.transform.localScale;
            actor.name = $"Intro Player Slot {slot}";
            actor.gameObject.SetActive(true);
            // SW 수정: 카메라 밖에서 시작한 대역도 수동 Timeline 평가로 다음 화면의 자세를 준비한다.
            actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actors.Add(actor.gameObject);
            if (primaryActor == null) primaryActor = actor;
        }
        director.SetGenericBinding(playerTrack, primaryActor);
        BindActiveCamera();
        previousUpdateMode = director.timeUpdateMode;
        previousWrapMode = director.extrapolationMode;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        // 서버 종료 시각까지 마지막 장면을 유지한다(싱글 디렉터는 끝나면 정지하도록 설정돼 있다).
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.time = 0d;
        director.Play();
        Debug.Log($"[MirrorBossIntro] client presentation started actors={actors.Count}");
    }

    private AnimationTrack ResolvePlayerTrack()
        => playerTrack = director != null
            ? WBH_BossTimeLineController.FindPlayerTrack(director.playableAsset as TimelineAsset) : null;

    /// <summary>현재 모드에서 꺼진 카메라에 묶인 트랙(Cinemachine·Audio 등)을 현재 메인 카메라의 같은 컴포넌트로 바꿔 묶는다.</summary>
    private void BindActiveCamera()
    {
        Camera main = Camera.main;
        if (main == null || director.playableAsset is not TimelineAsset timeline) return;
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (director.GetGenericBinding(track) is not Component bound || bound.gameObject.activeInHierarchy ||
                !bound.TryGetComponent(out Camera _) || !main.TryGetComponent(bound.GetType(), out Component active))
                continue;
            reboundTracks[track] = bound;
            director.SetGenericBinding(track, active);
        }
    }

    private void ApplyPlayerFormationAnimation()
    {
        if (primaryActor == null) return;
        for (int index = 1; index < actors.Count; index++)
        {
            GameObject actor = actors[index];
            if (actor == null) continue;
            CopyBoneHierarchy(primaryActor.transform, actor.transform);
        }
    }

    private static void CopyBoneHierarchy(Transform source, Transform target)
    {
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        int count = source.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform sChild = source.GetChild(i);
            Transform tChild = null;
            for (int j = 0; j < target.childCount; j++)
            {
                Transform candidate = target.GetChild(j);
                if (candidate.name == sChild.name)
                {
                    tChild = candidate;
                    break;
                }
            }
            if (tChild != null)
            {
                CopyBoneHierarchy(sChild, tChild);
            }
        }
    }

    private void BindAndHidePlayers()
    {
        var current = NetworkClient.localPlayer != null
            ? NetworkClient.localPlayer.GetComponent<MirrorSpawnedPlayerBinder>() : null;
        if (localBinder != current)
        {
            if (localBinder != null) localBinder.SetCutsceneInputBlocked(false);
            localBinder = current;
        }
        if (localBinder != null) localBinder.SetCutsceneInputBlocked(true);
        foreach (var identity in NetworkClient.spawned.Values)
        {
            if (identity == null || !identity.TryGetComponent<MirrorSpawnedPlayerBinder>(out _)) continue;
            foreach (var renderer in identity.GetComponentsInChildren<Renderer>(true))
            {
                if (!hiddenRenderers.ContainsKey(renderer)) hiddenRenderers.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }
    }

    private void ReleasePresentation()
    {
        if (!presenting) return;
        presenting = false;
        // 씬 이탈로 Director가 먼저 파괴돼도 실제 플레이어의 표시·입력 복원은 끝까지 수행한다.
        if (director != null)
        {
            director.Stop();
            if (playerTrack != null) director.ClearGenericBinding(playerTrack);
            foreach (var pair in reboundTracks) director.SetGenericBinding(pair.Key, pair.Value);
            director.timeUpdateMode = previousUpdateMode;
            director.extrapolationMode = previousWrapMode;
        }
        reboundTracks.Clear();
        foreach (var anchor in actorAnchors)
        {
            if (anchor != null)
            {
                if (Application.isPlaying) Destroy(anchor);
                else DestroyImmediate(anchor);
            }
        }
        actorAnchors.Clear();
        actors.Clear();
        primaryActor = null;
        foreach (var pair in hiddenRenderers) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        hiddenRenderers.Clear();
        if (localBinder != null) localBinder.SetCutsceneInputBlocked(false);
        localBinder = null;
        if (hud != null) hud.SetActive(hudWasActive);
        Debug.Log("[MirrorBossIntro] client camera/HUD/input restored");
    }

    public override void OnStopClient()
    {
        ReleasePresentation();
        base.OnStopClient();
    }

    private void OnEnable() => (NetworkManager.singleton as MirrorNetworkManager)?.SetCurrentBossIntro(this, true);

    private void OnDisable()
    {
        (NetworkManager.singleton as MirrorNetworkManager)?.SetCurrentBossIntro(this, false);
        ReleasePresentation();
    }
}
