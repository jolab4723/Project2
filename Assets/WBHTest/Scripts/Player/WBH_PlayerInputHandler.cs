using UnityEngine;
using UnityEngine.EventSystems;

public class WBH_PlayerInputHandler : MonoBehaviour
{
    //[SerializeField] private PlayerSkillSystem skillSystem; // 우진님 스킬시스템 연결

    [SerializeField] private LayerMask inputBlockLayer; // 입력 방지 레이어. !@ worldItem 레이어 추가

    private Camera mainCamera;

    private T_PlayerController controller;
    private T_PlayerCombat combat;

    private WorldItemPickupInteractor pickupItem;


    private void Awake()
    {
        mainCamera = Camera.main;

        controller = GetComponent<T_PlayerController>();
        combat = GetComponent<T_PlayerCombat>();
        pickupItem = FindFirstObjectByType<WorldItemPickupInteractor>();

    }

    void Update()
    {
        if (!controller.IsControlEnabled)
            return;
        HandleMoveInput();
        HandleAttackInput();
        HandleDodgeInput();

        //HandlePortionInput();
        //HandleOpenUIInput();
        //HandleSkillInput(); // 스킬 연결 시, 활성화
    }

    // 이동
    private void HandleMoveInput()
    {
        if (!Input.GetMouseButton(1) || IsPointerOverUI())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (((1 << hit.collider.gameObject.layer) & inputBlockLayer) != 0)
                return;

            combat.CancelChase();
            controller.MoveCommand(hit.point);
        }
    }

    // 공격
    private void HandleAttackInput()
    {
        if (!Input.GetMouseButtonDown(0) || IsPointerOverUI())
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            if (((1 << hit.collider.gameObject.layer) & inputBlockLayer) != 0)
                return;
        }

        Plane plane = new Plane(Vector3.up, Vector3.zero);

        if(plane.Raycast(ray, out float distance))
        {
            Vector3 mousePos = ray.GetPoint(distance);

            combat.TryAttack(mousePos);
        }
    }

    // 회피
    private void HandleDodgeInput()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            controller.TryDodge();
        }
    }

    // 마우스 포인터가 UI위에 올라가있는지 체크하는 메서드
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }


    //// 포션 사용
    //private void HandlePortionInput()
    //{
    //    if (Input.GetKeyDown(KeyCode.Q))
    //    {
            
    //    }
    //}
    
    //// UI 단축키
    //private void HandleOpenUIInput()
    //{
    //    // 환경설정 
    //    if (Input.GetKeyDown(KeyCode.Escape))
    //    {

    //    }
    //    // 스킬트리
    //    if (Input.GetKeyDown(KeyCode.K))
    //    {

    //    }
    //    // 인벤토리
    //    if (Input.GetKeyDown(KeyCode.I))
    //    {

    //    }
    //    // 스탯패널
    //    if (Input.GetKeyDown(KeyCode.P))
    //    {

    //    }
    //    // 퀘스트 패널
    //    if (Input.GetKeyDown(KeyCode.O))
    //    {

    //    }
    //}




    // --- 우진님 스킬 즉각 사용 필요시, 활성화.  skillSystem.cs 에서 HandleSkillInput 메서드 public 화 필요
    //private void HandleSkillInput()
    //{
    //    if (skillSystem == null)
    //        return;

    //    skillSystem.HandleSkillInput(0, KeyCode.A);
    //    skillSystem.HandleSkillInput(1, KeyCode.S);
    //    skillSystem.HandleSkillInput(2, KeyCode.D);
    //}



    // --- 우진님 스킬 리팩토링 시, 활성화
    //private void HandleSkillInput(int index, KeyCode key)
    //{
    //    SkillBase skill = skillSystem.GetSkill(index);

    //    if (skill == null)
    //        return;

    //    if (skill.IsChargeSkill)
    //    {
    //        if (Input.GetKeyDown(key))
    //            skill.OnPress(gameObject);

    //        if (Input.GetKey(key))
    //            skill.OnHold(gameObject, Time.deltaTime);

    //        if (Input.GetKeyUp(key))
    //            skill.OnRelease(gameObject);
    //    }
    //    else
    //    {
    //        if (Input.GetKeyDown(key))
    //            skill.Excute(gameObject);
    //    }
    //}

}
