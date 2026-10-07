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
        // WJ 이우진 수정(2026-10-01): 증감 스탯량은 HUD 아이콘 툴팁이 보여주고, 팝업은 상세 효과 문장만 보여준다.
        //    문장은 고유효과면 UniqueEffectLabelDatabase, 그 외는 BuffLabelDatabase의 description(다국어)에서 온다.
        //    상세 문장이 없는 버프만 스탯 줄로 대신한다.
        nameText.text = BuffTextComposer.BuildBaseName(data.source);
        descriptionText.text = BuffTextComposer.BuildDetailDescription(data.source, data.stackCount);
    }

    public void Clear()
    {
        nameText.text = "";
        descriptionText.text = "";
    }
}