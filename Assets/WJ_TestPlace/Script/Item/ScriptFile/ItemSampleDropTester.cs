using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ItemSystem
{
    /// <summary>
    /// 테스트용: 인스펙터에 등록해둔 아이템을 플레이어 위치에 드롭한다.
    /// 적을 잡지 않고도 특정 아이템의 월드 드롭·획득·툴팁을 바로 확인할 때 쓴다.
    ///
    /// 실제 드롭은 게임이 쓰는 경로를 그대로 탄다.
    ///   Core.ItemManager.DropSpecificItem -> ItemSystemController.DropGeneratedItem
    /// 그래서 등급별 옵션 굴림, 월드 아이템 생성, 획득 처리가 실제 드롭과 동일하게 동작한다.
    ///
    /// 드롭 위치는 플레이어의 PlayerItemDropOrigin(적 사망 드롭과 같은 기준점)을 우선 사용하고,
    /// 없으면 플레이어 발밑 위치로 대체한다.
    /// </summary>
    public class ItemSampleDropTester : MonoBehaviour
    {
        [Header("드롭할 아이템")]
        [Tooltip("여기 등록한 아이템을 드롭한다. 순서대로 하나씩 드롭된다.")]
        [SerializeField] private List<ItemDefinitionSO> itemsToDrop = new List<ItemDefinitionSO>();

        [Header("입력")]
        [Tooltip("목록에서 다음 아이템을 하나 드롭한다.")]
        [SerializeField] private Key dropNextKey = Key.B;

        [Tooltip("목록의 아이템을 전부 드롭한다.")]
        [SerializeField] private Key dropAllKey = Key.N;

        [Header("위치")]
        [Tooltip("플레이어 기준점에서 이만큼 떨어진 곳에 떨군다. 0이면 정확히 기준점 위치.")]
        [SerializeField] private float scatterRadius = 0.5f;

        /// <summary>dropNextKey로 다음에 드롭할 항목 인덱스.</summary>
        private int nextIndex;

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current[dropNextKey].wasPressedThisFrame)
                DropNext();

            if (Keyboard.current[dropAllKey].wasPressedThisFrame)
                DropAll();
        }

        /// <summary>목록에서 다음 아이템 하나를 드롭한다. 끝까지 가면 처음으로 돌아온다.</summary>
        [ContextMenu("다음 아이템 드롭")]
        public void DropNext()
        {
            if (!HasItems())
                return;

            // 비어있는 칸이 섞여 있어도 멈추지 않도록 유효한 항목을 찾을 때까지 한 바퀴 돈다.
            for (int i = 0; i < itemsToDrop.Count; i++)
            {
                ItemDefinitionSO definition = itemsToDrop[nextIndex];
                nextIndex = (nextIndex + 1) % itemsToDrop.Count;

                if (definition != null)
                {
                    Drop(definition);
                    return;
                }
            }

            Debug.LogWarning("[ItemSampleDropTester] 등록된 항목이 전부 비어 있습니다.");
        }

        /// <summary>등록된 아이템을 전부 드롭한다.</summary>
        [ContextMenu("전체 드롭")]
        public void DropAll()
        {
            if (!HasItems())
                return;

            int dropped = 0;
            foreach (ItemDefinitionSO definition in itemsToDrop)
            {
                if (definition == null)
                    continue;

                Drop(definition);
                dropped++;
            }

            if (dropped == 0)
                Debug.LogWarning("[ItemSampleDropTester] 등록된 항목이 전부 비어 있습니다.");
        }

        private void Drop(ItemDefinitionSO definition)
        {
            if (Core.ItemManager.Instance == null)
            {
                Debug.LogWarning("[ItemSampleDropTester] ItemManager.Instance가 없어 드롭하지 못했습니다.");
                return;
            }

            if (!TryGetDropPosition(out Vector3 position))
            {
                Debug.LogWarning("[ItemSampleDropTester] 플레이어를 찾지 못해 드롭 위치를 정할 수 없습니다.");
                return;
            }

            Core.ItemManager.Instance.DropSpecificItem(definition, position);
            Debug.Log($"[ItemSampleDropTester] '{definition.itemName}' 드롭 ({definition.itemId})");
        }

        /// <summary>
        /// 적 사망 드롭과 같은 기준점(PlayerItemDropOrigin)을 우선 쓰고, 없으면 플레이어 위치로 대체한다.
        /// 여러 개를 한 번에 떨굴 때 겹치지 않도록 scatterRadius만큼 흩뿌린다.
        /// </summary>
        private bool TryGetDropPosition(out Vector3 position)
        {
            position = default;

            var origin = Object.FindFirstObjectByType<PlayerItemDropOrigin>();
            if (origin != null && origin.TryGetSpawnPose(out Vector3 originPos, out _))
            {
                position = originPos;
            }
            else
            {
                // PlayerItemDropOrigin이 없는 씬에서도 동작하도록 플레이어 위치로 대체한다.
                var player = PlayerStatManager.Instance;
                if (player == null)
                    return false;

                position = player.transform.position;
            }

            if (scatterRadius > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * scatterRadius;
                position += new Vector3(offset.x, 0f, offset.y);
            }

            return true;
        }

        private bool HasItems()
        {
            if (itemsToDrop != null && itemsToDrop.Count > 0)
                return true;

            Debug.LogWarning("[ItemSampleDropTester] 드롭할 아이템이 등록되지 않았습니다. 인스펙터에서 지정해주세요.");
            return false;
        }
    }
}
