using UnityEngine;
using UnityEngine.UI;

namespace SW.Test.RandomDrop
{
    public enum SwTestButtonMode
    {
        TeamOriginal,
        SWRandomDrop
    }

    /// <summary>
    /// 씬의 테스트 버튼을 팀원 기존 기능과 SW 랜덤 드랍 테스트 기능 사이에서 전환해주는 스위처입니다.
    /// 버튼 OnClick은 이 스크립트만 호출하고, 실제 실행 대상은 mode 값으로 선택합니다.
    /// </summary>
    public class SwTestButtonModeSwitcher : MonoBehaviour
    {
        [Header("현재 버튼 모드")]
        [SerializeField] private SwTestButtonMode mode = SwTestButtonMode.SWRandomDrop;

        [Header("팀원 기존 기능")]
        [SerializeField] private TestButtonController testbuttonController;

        [Header("SW 랜덤 드랍 기능")]
        [SerializeField] private SwTestRandomDropAcquireTester randomDropTester;

        [Header("버튼 상태 표시")]
        [SerializeField] private Button getItemButton;
        [SerializeField] private bool autoResolveReferences = true;

        public SwTestButtonMode Mode => mode;

        private void Awake()
        {
            if (autoResolveReferences)
                AutoResolveReferences();

            RefreshButtonState();
        }

        public void OnClickDropItem()
        {
            if (autoResolveReferences)
                AutoResolveReferences();

            switch (mode)
            {
                case SwTestButtonMode.TeamOriginal:
                    if (testbuttonController == null)
                    {
                        Debug.LogWarning("[SW TEST 버튼 스위처] TestButtonController가 연결되지 않았습니다.");
                        return;
                    }

                    testbuttonController.OnClickDropItem();
                    break;

                case SwTestButtonMode.SWRandomDrop:
                    if (randomDropTester == null)
                    {
                        Debug.LogWarning("[SW TEST 버튼 스위처] SwTestRandomDropAcquireTester가 연결되지 않았습니다.");
                        return;
                    }

                    randomDropTester.DropRandomItemToField();
                    break;
            }

            RefreshButtonState();
        }

        public void OnClickGetItem()
        {
            if (autoResolveReferences)
                AutoResolveReferences();

            switch (mode)
            {
                case SwTestButtonMode.TeamOriginal:
                    if (testbuttonController == null)
                    {
                        Debug.LogWarning("[SW TEST 버튼 스위처] TestButtonController가 연결되지 않았습니다.");
                        return;
                    }

                    testbuttonController.OnClickGetItem();
                    break;

                case SwTestButtonMode.SWRandomDrop:
                    if (randomDropTester == null)
                    {
                        Debug.LogWarning("[SW TEST 버튼 스위처] SwTestRandomDropAcquireTester가 연결되지 않았습니다.");
                        return;
                    }

                    randomDropTester.AcquireLastDroppedItem();
                    break;
            }

            RefreshButtonState();
        }

        [ContextMenu("SW TEST/팀원 기존 버튼 모드 사용")]
        public void UseTeamOriginalMode()
        {
            mode = SwTestButtonMode.TeamOriginal;
            RefreshButtonState();
            Debug.Log("[SW TEST 버튼 스위처] 팀원 기존 버튼 모드로 변경했습니다.");
        }

        [ContextMenu("SW TEST/SW 랜덤 드랍 버튼 모드 사용")]
        public void UseSWRandomDropMode()
        {
            mode = SwTestButtonMode.SWRandomDrop;
            RefreshButtonState();
            Debug.Log("[SW TEST 버튼 스위처] SW 랜덤 드랍 버튼 모드로 변경했습니다.");
        }

        [ContextMenu("SW TEST/버튼 모드 토글")]
        public void ToggleMode()
        {
            mode = mode == SwTestButtonMode.TeamOriginal
                ? SwTestButtonMode.SWRandomDrop
                : SwTestButtonMode.TeamOriginal;

            RefreshButtonState();
            Debug.Log($"[SW TEST 버튼 스위처] 현재 버튼 모드: {mode}");
        }

        public void RefreshButtonState()
        {
            if (autoResolveReferences)
                AutoResolveReferences();

            if (getItemButton == null)
                return;

            if (mode == SwTestButtonMode.SWRandomDrop)
            {
                getItemButton.interactable = randomDropTester != null && randomDropTester.HasLastDroppedItem;
            }
        }

        private void AutoResolveReferences()
        {
            if (testbuttonController == null)
                testbuttonController = Object.FindFirstObjectByType<TestButtonController>(FindObjectsInactive.Include);

            if (randomDropTester == null)
                randomDropTester = Object.FindFirstObjectByType<SwTestRandomDropAcquireTester>(FindObjectsInactive.Include);

            if (getItemButton == null)
            {
                GameObject getButtonObject = GameObject.Find("btn_GetItem");
                if (getButtonObject != null)
                    getItemButton = getButtonObject.GetComponent<Button>();
            }
        }
    }
}
