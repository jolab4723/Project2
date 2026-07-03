using UnityEngine;

// 몬스터 프리팹에 달아주세요! (반드시 Collider가 함께 붙어있어야 작동합니다)
public class EnemyHighlight : MonoBehaviour
{
    private Outline outline; // Quick Outline 컴포넌트를 담을 변수

    void Start()
    {
        // 내 몸에 붙어있는 Outline 컴포넌트를 찾아옵니다.
        outline = GetComponent<Outline>();

        // 처음 시작할 때는 외곽선이 안 보이도록 꺼둡니다.
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    // 마우스 커서가 내 Collider 위로 딱 올라오는 순간 유니티가 자동으로 실행해 줍니다.
    void OnMouseEnter()
    {
        if (outline != null)
        {
            outline.enabled = true; // 빨간 테두리 켜기!
        }
    }

    // 마우스 커서가 내 Collider 밖으로 나가는 순간 유니티가 자동으로 실행해 줍니다.
    void OnMouseExit()
    {
        if (outline != null)
        {
            outline.enabled = false; // 빨간 테두리 끄기!
        }
    }
}