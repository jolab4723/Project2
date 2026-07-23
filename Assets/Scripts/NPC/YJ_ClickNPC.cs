using UnityEngine;

public class YJ_ClickNPC : MonoBehaviour
{
    private YJ_OutlineOnMouseHover hover;

    private void Awake()
    {
        hover = GetComponent<YJ_OutlineOnMouseHover>();
    }

    void Start()
    {
        
    }
    private void OnMouseDown()
    {
        if ( ! hover.IsHovered)
            return;

        Log.Print("NPC 클릭");
    }
}
