using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>서버 시각으로 보스 인트로를 재생한다. Timeline은 연출용 복제본만 제어한다.</summary>
[DisallowMultipleComponent]
public sealed class MirrorBossIntro_MirrorTest : NetworkBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private GameObject hud;
    [SerializeField] private AnimationTrack playerTrack;
    [SerializeField] private Animator fighterVisual;
    [SerializeField] private Animator gunnerVisual;
    [SerializeField] private Transform[] playerPoints;

    [SyncVar] private double startsAt;
    [SyncVar] private double endsAt;
    [SyncVar] private bool complete;
    [SyncVar] private byte occupiedSlots;
    [SyncVar] private byte gunnerSlots;
    private bool presenting;
    private bool hudWasActive;
    private readonly List<GameObject> actors = new();
    private readonly Dictionary<Renderer, bool> hiddenRenderers = new();
    private MirrorSpawnedPlayerBinder localBinder;

    public bool IsComplete => complete;
    public bool IsPresenting => presenting;
    public double StartsAt => startsAt;
    public double EndsAt => endsAt;
    public double PlaybackTime => director != null ? director.time : 0d;
    public int VisualActorCount => actors.Count;

    [Server]
    public bool ServerBegin(MirrorTestNetworkManager manager, float leadIn)
    {
        if (startsAt > 0d || manager == null || director == null || director.playableAsset == null ||
            presentationRoot == null || fighterVisual == null || gunnerVisual == null ||
            playerTrack == null || playerPoints == null || playerPoints.Length != 4 ||
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
    }

    private void BeginPresentation()
    {
        presenting = true;
        hudWasActive = hud != null && hud.activeSelf;
        if (hud != null) hud.SetActive(false);
        Animator primary = null;
        for (int slot = 0; slot < 4; slot++)
        {
            if ((occupiedSlots & (1 << slot)) == 0) continue;
            var template = (gunnerSlots & (1 << slot)) != 0 ? gunnerVisual : fighterVisual;
            var actor = Instantiate(template, playerPoints[slot].position, playerPoints[slot].rotation, presentationRoot.transform);
            actor.name = $"Intro Player Slot {slot}";
            actor.gameObject.SetActive(true);
            actors.Add(actor.gameObject);
            if (primary == null) primary = actor;
        }
        director.SetGenericBinding(playerTrack, primary);
        presentationRoot.SetActive(true);
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        director.time = 0d;
        director.Play();
        Debug.Log($"[MirrorBossIntro] client presentation started actors={actors.Count}");
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
        foreach (var actor in actors) if (actor != null) Destroy(actor);
        actors.Clear();
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
