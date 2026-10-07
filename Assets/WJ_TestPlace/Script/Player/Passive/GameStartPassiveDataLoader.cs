using UnityEngine;
using UnityEngine.UI;
using Core;

/// <summary>
/// 싱글플레이어 로비에서 패시브 프로필(크레딧 포함)이 "게임 시작"을 누르기 전에도 바로 쓸 수 있어야
/// 해서, 특정 버튼 이벤트가 아니라 씬이 시작되는 시점에 미리 불러온다. "패시브 스킬" 버튼도 눌렀을 때
/// 패시브 팝업을 열어주는 역할을 겸한다.
///
/// !! "패시브 스킬" 버튼에는 캐릭터 선택 카드용 KY_CharacterSelectionButton이 2개(Fighter/Gunner
///    characterId, 의미 없음 - 아마 카드 복사 과정에서 같이 딸려온 잔여물)가 붙어있어서 그건 안 쓰고
///    Button.onClick에 직접 리스너를 건다.
/// </summary>
public class LobbyPassiveProfileLoader : MonoBehaviour
{
    [Tooltip("눌렀을 때 패시브 팝업을 여는 버튼")]
    [SerializeField] private Button passiveSkillButton;
    [Tooltip("열고 닫을 패시브 팝업 루트 오브젝트")]
    [SerializeField] private GameObject passiveSkillPopup;

    private void Start()
    {
        LoadProfile();

        if (passiveSkillButton != null)
            passiveSkillButton.onClick.AddListener(OpenPassivePopup);
    }

    private void OnDestroy()
    {
        if (passiveSkillButton != null)
            passiveSkillButton.onClick.RemoveListener(OpenPassivePopup);
    }

    private void OpenPassivePopup()
    {
        if (passiveSkillPopup != null)
            passiveSkillPopup.SetActive(true);
    }

    private void LoadProfile()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[LobbyPassiveProfileLoader] DataManager.Instance가 없어 패시브 프로필을 불러오지 못했습니다.");
            return;
        }

        bool loadedExisting = DataManager.Instance.LoadPassiveData();
        Debug.Log("[LobbyPassiveProfileLoader] 패시브 프로필 로드 완료(기존 저장 있음=" + loadedExisting + "). 크레딧="
            + (PassiveSkillManager.Instance != null && PassiveSkillManager.Instance.CurrentProfile != null
                ? PassiveSkillManager.Instance.CurrentProfile.credit.ToString()
                : "N/A"));
    }
}
