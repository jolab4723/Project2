using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KY_SkillSlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI keyText;

    [Tooltip("쿨타임 표시용. Image Type을 Filled/Radial 360으로 설정해서 써야 한다. 남은 비율만큼 채워져서 쿨타임 끝나면 0이 된다.")]
    public Image cooldownFillImage;

    [Tooltip("아이콘 가운데에 남은 쿨타임(초)을 숫자로 표시. 쿨타임 없을 땐 빈 텍스트로 숨김.")]
    public TextMeshProUGUI cooldownText;

    [Tooltip("대시 진화2(2스택)처럼 스택으로 관리되는 스킬의 현재 보유 스택 수. 스택 모드가 아닌 스킬에서는 비활성화된다.")]
    public TextMeshProUGUI stackText;

    public void SetIcon(Sprite sprite)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    public void ClearIcon()
    {
        icon.sprite = null;
        icon.enabled = false;
    }

    public void SetKeyText(string key)
    {
        keyText.text = key;
    }

    /// <summary>남은 쿨타임/최대 쿨타임 비율로 라디얼 필을 갱신하고, 가운데 숫자 텍스트도 같이 갱신한다.
    /// total이 0 이하면(쿨타임 없는 스킬 등) 채우지 않는다. 남은 시간은 올림한 정수 초로 표시하고,
    /// 0이 되면(스킬 사용 가능) 텍스트를 비워서 아이콘이 그대로 보이게 한다.</summary>
    public void SetCooldown(float remaining, float total)
    {
        if (cooldownFillImage != null)
            cooldownFillImage.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;

        if (cooldownText != null)
            cooldownText.text = remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
    }

    /// <summary>스택 모드 스킬의 현재 보유 스택 수를 표시한다. stacks가 null이면(스택 모드 아님) 숨긴다.</summary>
    public void SetStacks(int? stacks)
    {
        if (stackText == null)
            return;

        bool show = stacks.HasValue;
        stackText.gameObject.SetActive(show);
        if (show)
            stackText.text = stacks.Value.ToString();
    }
}