using UnityEngine;

/// <summary>캐릭터 선택 화면의 단일 3D 프리뷰 모델을 전환한다.</summary>
public class KY_CharacterSelectionPreviewController : MonoBehaviour
{
    [SerializeField] private GameObject fighterModel;
    [SerializeField] private GameObject gunnerModel;

    /// <summary>선택한 캐릭터 모델만 즉시 표시한다.</summary>
    public void ShowCharacter(KY_CharacterId id)
    {
        bool isFighter = id == KY_CharacterId.Fighter;
        fighterModel.SetActive(isFighter);
        gunnerModel.SetActive(!isFighter);
    }
}
