using System.Collections.Generic;
using ItemSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 적용 중인 버프를 아이콘으로 나열해서 보여주는 HUD UI.
/// 배치는 이 오브젝트(또는 slotParent)에 붙은 GridLayoutGroup이 담당하고, 이 스크립트는
/// PlayerBuffManager.ActiveBuffs를 슬롯 목록에 맞춰 채우기만 한다.
///
/// PlayerBuffManager.OnBuffsChanged(버프가 추가/제거될 때만 발행)를 구독해서 슬롯 구성을 다시 만들고,
/// 이미 떠 있는 슬롯의 남은시간/스택처럼 매 프레임 바뀌는 값은 Update에서 직접 갱신한다.
/// </summary>
public class BuffIconUIContainer : MonoBehaviour
{
    [SerializeField] private BuffIconSlot iconSlotPrefab;

    [Tooltip("슬롯을 실제로 배치할 부모(GridLayoutGroup이 붙은 곳). 비워두면 이 오브젝트 자신을 쓴다.")]
    [SerializeField] private Transform slotParent;

    private PlayerBuffManager buffManager;
    private bool explicitOwner;
    public PlayerBuffManager BoundBuffManager => buffManager;
    private readonly List<BuffIconSlot> pool = new List<BuffIconSlot>();

    private void Awake()
    {
        if (slotParent == null)
            slotParent = transform;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (buffManager != null)
            buffManager.OnBuffsChanged -= Rebuild;
    }

    private void Update()
    {
        if (!explicitOwner && !MirrorNetworkManager.OwnsGameplay && buffManager != PlayerBuffManager.Instance)
        {
            TrySubscribe();
            return;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].gameObject.activeSelf)
                pool[i].Refresh();
        }
    }

    /// <summary>PlayerBuffManager.Instance는 Awake 순서에 따라 이 컴포넌트보다 늦게 준비될 수 있어 매번 확인한다.</summary>
    private void TrySubscribe()
    {
        if (buffManager != null)
            buffManager.OnBuffsChanged -= Rebuild;

        if (!explicitOwner && !MirrorNetworkManager.OwnsGameplay) buffManager = PlayerBuffManager.Instance;

        if (buffManager != null)
        {
            if (isActiveAndEnabled) buffManager.OnBuffsChanged += Rebuild;
            Rebuild();
        }
    }

    /// <summary>SW 수정: 공통 버프 아이콘을 지정한 플레이어의 버프에 연결합니다.</summary>
    public void Bind(PlayerBuffManager owner)
    {
        if (buffManager != null) buffManager.OnBuffsChanged -= Rebuild;
        explicitOwner = true;
        buffManager = owner;
        TrySubscribe();
        if (owner == null) foreach (var slot in pool) if (slot != null) slot.gameObject.SetActive(false);
    }

    /// <summary>SW 수정: 플레이어 교체·씬 종료 때 이전 소유자의 아이콘과 구독을 해제합니다.</summary>
    public void Unbind() => Bind(null);

    /// <summary>현재 활성 버프 목록에 맞춰 아이콘 슬롯을 다시 배치한다. 슬롯 오브젝트는 풀링해서 재사용한다.</summary>
    private void Rebuild()
    {
        if (buffManager == null || iconSlotPrefab == null)
            return;

        IReadOnlyList<BuffInstance> active = buffManager.ActiveBuffs;

        int before = pool.Count;

        while (pool.Count < active.Count)
            pool.Add(Instantiate(iconSlotPrefab, slotParent));

        for (int i = 0; i < pool.Count; i++)
        {
            bool inUse = i < active.Count;
            pool[i].gameObject.SetActive(inUse);
            if (inUse)
                pool[i].Bind(active[i]);
        }

        // 새로 만든 슬롯은 GridLayoutGroup이 다음 레이아웃 갱신에서야 자리를 잡아준다. 그전까지는
        // 프리팹에 저장된 위치(컨테이너 정중앙)에 그려져서, 아이콘이 가운데서 튀어나와 제자리로
        // 날아가는 것처럼 보인다. 슬롯이 늘어난 경우에만 즉시 레이아웃을 돌려 그 한 프레임을 없앤다.
        if (pool.Count > before)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotParent as RectTransform);
    }
}
