using System.Collections.Generic;
using ItemSystem;
using UnityEngine;

/// <summary>
/// WJ 원본 <see cref="BuffIconUIContainer"/>의 Mirror 테스트용 바인딩 복제본이다.
/// <para>원본은 <c>PlayerBuffManager.Instance</c>를 매 프레임 찾아 싱글플레이 HUD에 연결하지만,
/// 이 복제본은 <see cref="Bind"/>로 전달받은 로컬 <see cref="PlayerContext"/>의
/// <see cref="PlayerBuffManager"/>만 구독한다.</para>
/// <para>아이콘 한 칸의 표시와 호버 처리는 기존 <see cref="BuffIconSlot"/>을 그대로 재사용하므로
/// WJ 버프 이름·설명·디버프 색상 규칙과 동일하게 보인다. 이 컴포넌트는 버프 상태를 새로 저장하지 않는다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class BuffIconUIContainer_MirrorTest : MonoBehaviour
{
    [SerializeField] private BuffIconSlot iconSlotPrefab;

    [Tooltip("슬롯을 배치할 부모입니다. 비워두면 이 오브젝트 자신을 사용합니다.")]
    [SerializeField] private Transform slotParent;

    private readonly List<BuffIconSlot> pool = new();
    private PlayerBuffManager buffManager;
    private bool subscribed;

    public PlayerBuffManager BoundBuffManager => buffManager;

    private void Awake()
    {
        slotParent ??= transform;
    }

    private void OnEnable()
    {
        Subscribe();
        Rebuild();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        for (int index = 0; index < pool.Count; index++)
        {
            BuffIconSlot slot = pool[index];
            if (slot != null && slot.gameObject.activeSelf)
                slot.Refresh();
        }
    }

    /// <summary>
    /// 현재 클라이언트의 로컬 PlayerContext가 소유한 버프 Manager를 연결한다.
    /// 다른 플레이어 복제본의 버프가 바뀌어도 이 HUD에는 섞이지 않는다.
    /// </summary>
    public void Bind(PlayerBuffManager owner)
    {
        if (buffManager == owner)
        {
            Rebuild();
            return;
        }

        Unsubscribe();
        buffManager = owner;
        Subscribe();
        Rebuild();
    }

    /// <summary>이전 로컬 플레이어의 이벤트 구독과 화면 슬롯을 모두 정리한다.</summary>
    public void Unbind()
    {
        Unsubscribe();
        buffManager = null;
        HideAllSlots();
    }

    private void Subscribe()
    {
        if (subscribed || !isActiveAndEnabled || buffManager == null)
            return;

        buffManager.OnBuffsChanged += Rebuild;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (buffManager != null)
            buffManager.OnBuffsChanged -= Rebuild;

        subscribed = false;
    }

    /// <summary>
    /// 현재 활성 버프 수만큼 기존 슬롯을 재사용한다.
    /// 플레이어가 바뀔 때마다 UI 오브젝트를 파괴하지 않아 호버·쿨다운 표시 중 불필요한 할당을 만들지 않는다.
    /// </summary>
    private void Rebuild()
    {
        if (buffManager == null)
        {
            HideAllSlots();
            return;
        }

        if (iconSlotPrefab == null)
        {
            Debug.LogError(
                "[BuffIconUIContainer_MirrorTest] 버프 아이콘 슬롯 프리팹 참조가 비어 있습니다.",
                this);
            HideAllSlots();
            return;
        }

        IReadOnlyList<BuffInstance> activeBuffs = buffManager.ActiveBuffs;
        while (pool.Count < activeBuffs.Count)
            pool.Add(Instantiate(iconSlotPrefab, slotParent));

        for (int index = 0; index < pool.Count; index++)
        {
            bool inUse = index < activeBuffs.Count;
            BuffIconSlot slot = pool[index];
            slot.gameObject.SetActive(inUse);
            if (inUse)
                slot.Bind(activeBuffs[index]);
        }
    }

    private void HideAllSlots()
    {
        for (int index = 0; index < pool.Count; index++)
        {
            if (pool[index] != null)
                pool[index].gameObject.SetActive(false);
        }
    }
}
