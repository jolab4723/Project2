using UnityEngine;

/// <summary>
/// 클래스(파이터/거너)별 HUD 스킬 슬롯 아이콘 보정용. 캐릭터 프리팹에 붙인다.
///
/// 평소 슬롯 아이콘은 스킬 데이터(SkillDefinitionSO.icon)에서 가져오지만, 아직 스킬 데이터가 없는
/// 슬롯(예: 궁극기 Skill4)은 클래스가 바뀌어도 씬에 박힌 이미지가 그대로 남는다. 그런 슬롯의
/// 아이콘을 클래스별로 미리 지정해두기 위한 컴포넌트다.
///
/// !! 스킬 데이터에 아이콘이 있으면 그쪽이 우선이다. 여기서는 비어 있는 칸만 메운다.
///    궁극기가 실제로 구현되면 그 SkillDefinitionSO.icon으로 옮기고 이 칸은 비우면 된다.
/// </summary>
public class ClassSkillIconSet : MonoBehaviour
{
    [Tooltip("HUD 스킬 슬롯 인덱스 순서(0=Skill1 … 3=Skill4). 비워둔 칸은 스킬 데이터의 아이콘을 그대로 쓴다.")]
    [SerializeField] private Sprite[] slotIcons = new Sprite[4];

    /// <summary>해당 슬롯에 지정해둔 아이콘. 지정이 없으면 null.</summary>
    public Sprite GetSlotIcon(int index)
    {
        if (slotIcons == null || index < 0 || index >= slotIcons.Length)
            return null;

        return slotIcons[index];
    }
}
