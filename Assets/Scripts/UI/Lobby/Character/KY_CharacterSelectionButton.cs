using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>캐릭터 선택 카드의 Button 클릭을 캐릭터 ID 이벤트로 전달한다.</summary>
[RequireComponent(typeof(Button))]
public class KY_CharacterSelectionButton : MonoBehaviour
{
    [SerializeField] private KY_CharacterId characterId;
    [SerializeField] private Button button;

    public Sprite portraitImage;

    public event Action<KY_CharacterId> OnClicked;

    /// <summary>같은 오브젝트의 Button을 찾아 클릭 이벤트를 연결한다.</summary>
    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError("[KY_CharacterSelectionButton] Button 컴포넌트가 없습니다.", this);
            enabled = false;
            return;
        }

        button.onClick.AddListener(HandleClick);
    }

    /// <summary>파괴될 때 Button 클릭 이벤트를 해제한다.</summary>
    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    /// <summary>선택된 캐릭터 ID를 로비 화면에 알린다.</summary>
    private void HandleClick()
    {
        OnClicked?.Invoke(characterId);
    }
}
