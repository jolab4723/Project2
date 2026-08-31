using UnityEngine;
using UnityEngine.EventSystems;
using System;

// 캐릭터 선택 버튼에 붙이는 스크립트

public class KY_LobbyCharacterButton : MonoBehaviour, IPointerClickHandler
{
    public KY_CharacterId characterId;
    public Sprite portraitImage;

    public event Action<KY_LobbyCharacterData> OnClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        KY_LobbyCharacterData data = KY_LobbyCharacterDatabase.GetById(characterId);
        OnClicked?.Invoke(data);
    }
}