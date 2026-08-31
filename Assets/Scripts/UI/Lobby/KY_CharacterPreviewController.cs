using UnityEngine;

/// <summary>
/// 캐릭터 선택 시, 3D 프리뷰 스테이지에 있는 모델 중
/// 선택된 캐릭터의 모델만 켜고 나머지는 꺼서 보여주는 컨트롤러.
/// </summary>
public class KY_CharacterPreviewController : MonoBehaviour
{
    [SerializeField] private GameObject fighterModel;
    [SerializeField] private GameObject gunnerModel;

    public void ShowCharacter(KY_CharacterId id)
    {
        fighterModel.SetActive(id == KY_CharacterId.Fighter);
        gunnerModel.SetActive(id == KY_CharacterId.Gunner);
    }
}