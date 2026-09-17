using Core;
using ItemSystem;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class YJ_PlayerFace : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite fighterFace;
    [SerializeField] private Sprite gunnerFace;

    private void Start()
    {
        DataManager dataManager = DataManager.Instance;

        if (dataManager == null)
        {
            Log.Error("플레이어 생성 실패: DataManager가 없습니다. 부트 씬의 초기화를 확인하세요.");
            return;
        }

        if ( ! dataManager.TryGetSavedCharacter(out CharacterClass character))
            return;

        switch (character)
        {
            case CharacterClass.Fighter:
                SetFaceImage(1);
                break;

            case CharacterClass.Gunner:
                SetFaceImage(2);
                break;

            default:
                break;
        }
    }

    private void SetFaceImage(int classNum)
    {
        switch (classNum)
        {
            case 0:
                break;

            case 1:
                image.sprite = fighterFace;
                break;

            case 2:
                image.sprite = gunnerFace;
                break;
        }
    }
}

