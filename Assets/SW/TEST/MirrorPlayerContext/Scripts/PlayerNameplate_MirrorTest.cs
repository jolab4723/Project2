using Mirror;
using TMPro;
using UnityEngine;

/// <summary>서버가 확정한 닉네임을 해당 플레이어 머리 위에 표시하는 클라이언트 전용 UI다.</summary>
public sealed class PlayerNameplate_MirrorTest : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI playerNumber;
    [SerializeField] private Vector3 worldOffset = new(0f, 2f, 0f);
    private MirrorSpawnedPlayerBinder owner;
    private RectTransform canvasRect;
    private string displayedName;

    public string DisplayedName => label != null && playerNumber != null ? playerNumber.text + " " + label.text : string.Empty;
    public MirrorSpawnedPlayerBinder BoundPlayer => owner;

    /// <summary>이 이름표가 따라갈 네트워크 플레이어를 연결한다.</summary>
    public void Bind(MirrorSpawnedPlayerBinder player)
    {
        owner = player;
        canvasRect = (RectTransform)transform;
        label.richText = false;
        panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        var session = NetworkManager.singleton as MirrorTestNetworkManager;
        Camera camera = Camera.main;
        bool visible = owner != null && camera != null && session != null &&
            session.CurrentSessionRoute is MirrorSessionRoute.Combat or MirrorSessionRoute.Camp &&
            !owner.IsTemporarilyAbsent && !string.IsNullOrWhiteSpace(owner.ParticipantDisplayName);
        if (!visible)
        {
            panel.gameObject.SetActive(false);
            return;
        }
        Vector3 point = camera.WorldToScreenPoint(owner.transform.position + worldOffset);
        visible = point.z > 0f && point.x >= 0f && point.x <= Screen.width && point.y >= 0f && point.y <= Screen.height;
        panel.gameObject.SetActive(visible);
        if (!visible) return;
        // 동일 닉네임도 로비의 고정 참가자 번호로 구별한다.
        string name = owner.ParticipantDisplayName;
        playerNumber.text = $"P{owner.ParticipantSlot + 1}";
        if (name != displayedName)
        {
            displayedName = name;
            label.text = name;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp(label.GetPreferredValues(name).x + 58f, 100f, 340f));
        }
        label.color = Color.white;
        playerNumber.color = new Color(0.4f, 0.9f, 1f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out Vector2 position))
            panel.anchoredPosition = position;
    }
}
