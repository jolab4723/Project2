using UnityEngine;
using TMPro;
using ItemSystem;

// 버프 팝업의 호버 설명 영역입니다. 판단 없이 받은 데이터를 그대로 표시합니다.
public class KY_BuffDescriptionView : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    public void Render(BuffInstance data)
    {
        // 이름/설명 모두 HUD 버프 아이콘 툴팁과 같은 조립기를 쓴다. SO에 적힌 원문(BuffDisplayName,
        // description)은 한국어 고정이라 언어를 따라가지 않는다.
        //
        // !! 설명은 번역된 문장을 찾아오는 게 아니라 StatEffects(증감 스탯과 수치)로 조립된다.
        //    BuffLabelDatabase가 이름만 담고 있는 이유이고, 밸런싱으로 수치를 바꿔도 문구가 따라온다.
        nameText.text = BuffTextComposer.BuildBaseName(data.source);
        descriptionText.text = BuffTextComposer.BuildDescription(data.source, data.stackCount);
    }

    public void Clear()
    {
        nameText.text = "";
        descriptionText.text = "";
    }
}