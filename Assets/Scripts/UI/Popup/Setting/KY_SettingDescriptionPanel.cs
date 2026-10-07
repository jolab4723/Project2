using UnityEngine;
using TMPro;

/// <summary>
/// 설정 화면에서 항목에 커서를 올렸을 때 우측에 설명을 띄우는 패널.
///
/// !! 예전엔 제목(NameText)·아이콘·배경 슬롯도 있었는데, 제목이 설명과 완전히 겹친 위치(둘 다 y=325,
///    800x50)에 놓여 있었고 아이콘·배경은 크기가 0이라 실제로는 쓰이지 않았다. 실질적으로 설명 한 줄만
///    쓰는 구조였어서 해당 오브젝트와 필드를 정리했다.
/// </summary>
public class KY_SettingDescriptionPanel : MonoBehaviour
{
    public static KY_SettingDescriptionPanel Instance;

    public TextMeshProUGUI descriptionText;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 설정 항목에 커서를 올렸을 때 오른쪽에 띄울 설명. 이미 번역된 문자열을 받는다.
    /// (번역 조회는 KY_SettingItem이 담당한다.)
    /// </summary>
    public void Show(string description)
    {
        gameObject.SetActive(true);
        descriptionText.text = description;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
