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
        nameText.text = data.source.BuffDisplayName;
        descriptionText.text = GetDescription(data.source);
    }

    string GetDescription(IBuffSource source)
    {
        // BuffDefinitionSO는 description 필드를 갖고 있지만, IBuffSource 인터페이스 자체엔 없음.
        // 다른 IBuffSource 구현체(UniqueEffectSO 계열 등)는 description이 없을 수 있어 안전하게 캐스팅.
        if (source is BuffDefinitionSO def)
            return def.description;

        return ""; // 설명이 없는 소스는 빈 텍스트로 (에러 없이)
    }

    public void Clear()
    {
        nameText.text = "";
        descriptionText.text = "";
    }
}