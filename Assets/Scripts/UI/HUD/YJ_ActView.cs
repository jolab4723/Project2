using TMPro;
using UnityEngine;

public class YJ_ActView : MonoBehaviour
{
    [SerializeField] private TMP_Text actText;

    private void Awake()
    {
        if (actText == null)
            actText = GetComponent<TMP_Text>();

        if (actText == null)
        {
            Debug.LogError("[YJ_ActView] 현재 Act를 표시할 TMP_Text가 없습니다.", this);
            enabled = false;
        }
    }

    private System.Collections.IEnumerator Start()
    {
        // StageSelectManager.Start()의 Act 복원이 끝난 뒤 표시
        yield return null;

        var manager = YJ_StageSelectManager.Instance;
        if (actText == null || manager == null)
            yield break;

        actText.text = $"ACT{(int)manager.CurrentAct}";
    }
}
