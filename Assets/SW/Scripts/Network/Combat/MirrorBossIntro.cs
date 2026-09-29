using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>서버 시각으로 보스 인트로를 재생한다. Timeline은 연출용 복제본만 제어한다.</summary>
[DisallowMultipleComponent]
public sealed class MirrorBossIntro : NetworkBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private GameObject hud;
    [SerializeField] private AnimationTrack playerTrack;
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
    private AnimationClip playerAnimationClip;
    private double playerAnimationStart;
    private double playerAnimationEnd;
    private double playerAnimationClipIn;
    private double playerAnimationTimeScale;

    public bool IsComplete => complete;
    public bool IsPresenting => presenting;
    public double StartsAt => startsAt;
    public double EndsAt => endsAt;
    public double PlaybackTime => director != null ? director.time : 0d;
    public int VisualActorCount => actors.Count;

    [Server]
    public bool ServerBegin(MirrorNetworkManager manager, float leadIn)
    {
        if (startsAt > 0d || manager == null || director == null || director.playableAsset == null ||
            presentationRoot == null || fighterVisual == null || gunnerVisual == null ||
            playerTrack == null || playerTimelineOrigin == null ||
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
        ResolvePlayerAnimation();
        Vector3 actorStartPosition = playerTimelineOrigin.InverseTransformPoint(playerPoints[0].position);
        Quaternion actorStartRotation = Quaternion.Inverse(playerTimelineOrigin.rotation) * playerPoints[0].rotation;
        for (int slot = 0; slot < 4; slot++)
        {
            if ((occupiedSlots & (1 << slot)) == 0) continue;
            var template = (gunnerSlots & (1 << slot)) != 0 ? gunnerVisual : fighterVisual;
            var anchorObject = new GameObject($"Intro Player Slot {slot} Anchor");
            var anchor = anchorObject.transform;
            anchor.SetParent(presentationRoot.transform, false);
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
            actors.Add(actor.gameObject);
            if (primaryActor == null) primaryActor = actor;
        }
        director.SetGenericBinding(playerTrack, primaryActor);
        presentationRoot.SetActive(true);
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.time = 0d;
        director.Play();
        Debug.Log($"[MirrorBossIntro] client presentation started actors={actors.Count}");
    }

    private void ResolvePlayerAnimation()
    {
        playerAnimationClip = null;
        foreach (TimelineClip timelineClip in playerTrack.GetClips())
        {
            if (timelineClip.asset is not AnimationPlayableAsset animationAsset || animationAsset.clip == null) continue;
            playerAnimationClip = animationAsset.clip;
            playerAnimationStart = timelineClip.start;
            playerAnimationEnd = timelineClip.end;
            playerAnimationClipIn = timelineClip.clipIn;
            playerAnimationTimeScale = timelineClip.timeScale;
            break;
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
        director.Stop();
        director.ClearGenericBinding(playerTrack);
        presentationRoot.SetActive(false);
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
        playerAnimationClip = null;
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

    private void OnDisable() => ReleasePresentation();
}
