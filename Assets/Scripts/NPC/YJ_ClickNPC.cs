using UnityEngine;
using UnityEngine.Events;

public class YJ_ClickNPC : MonoBehaviour
{
    private YJ_OutlineOnMouseHover hover;
    [SerializeField] private UnityEvent onClicked;

    private void Awake()
    {
        hover = GetComponent<YJ_OutlineOnMouseHover>();
    }

    void Start()
    {
        
    }
    private void OnMouseDown()
    {
        if (! hover.IsHovered)
            return;

        onClicked?.Invoke();
        Log.Print("NPC 클릭");
    }
}
