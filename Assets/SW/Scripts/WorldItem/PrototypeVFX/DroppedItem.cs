using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    [Header("Item Grade")]
    [SerializeField] private ItemGrade itemGrade = ItemGrade.Common;

    [Header("VFX")]
    [SerializeField] private DropItemVFXController vfxController;

    private void Awake()
    {
        // Inspector에서 직접 안 넣었을 경우, 자식 오브젝트에서 자동으로 찾음
        if (vfxController == null)
        {
            vfxController = GetComponentInChildren<DropItemVFXController>();
        }
    }

    private void Start()
    {
        ApplyGradeVFX();
    }

    private void ApplyGradeVFX()
    {
        if (vfxController == null)
        {
            Debug.LogWarning($"{name}에 DropItemVFXController가 연결되지 않았습니다.");
            return;
        }

        vfxController.SetGrade(itemGrade);
    }

    public void Init(ItemGrade grade)
    {
        itemGrade = grade;
        ApplyGradeVFX();
    }

    public void PickUp()
    {
        Destroy(gameObject);
    }
}