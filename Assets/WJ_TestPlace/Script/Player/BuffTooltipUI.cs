using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ItemSystem
{
    /// <summary>
    /// 버프 아이콘에 마우스를 올리면 이름/설명을 보여주는 간단한 툴팁.
    /// SW님의 TooltipManager(아이템 비교·장비 연동까지 포함된 무거운 시스템)와는 별개로,
    /// 버프는 비교·장비 로직이 필요 없어서 훨씬 가벼운 전용 컴포넌트로 새로 만들었다.
    /// </summary>
    public class BuffTooltipUI : MonoBehaviour
    {
        public static BuffTooltipUI Instance { get; private set; }

        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

        private bool visible;

        public bool IsVisible => visible;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // 루트 오브젝트 자체는 항상 활성 상태로 둔다(비활성 오브젝트는 Awake가 호출되지 않아서
            // 싱글톤 등록 자체가 안 되는 문제가 있었다). 대신 CanvasGroup 알파로 표시 여부를 제어한다.
            if (root != null)
                root.gameObject.SetActive(true);

            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!IsVisible)
                return;

            UpdatePosition();
        }

        public void Show(string displayName, string description)
        {
            if (root == null)
                return;

            if (nameText != null)
                nameText.text = displayName;

            if (descriptionText != null)
            {
                bool hasDescription = !string.IsNullOrEmpty(description);
                descriptionText.gameObject.SetActive(hasDescription);
                if (hasDescription)
                    descriptionText.text = description;
            }

            visible = true;
            SetGroupVisible(true);
            UpdatePosition();
        }

        public void Hide()
        {
            visible = false;
            SetGroupVisible(false);
        }

        private void SetGroupVisible(bool value)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = value ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // Canvas_HUD가 Screen Space - Overlay라 스크린 좌표를 그대로 앵커 위치로 쓸 수 있다.
        private void UpdatePosition()
        {
            if (root == null || Mouse.current == null)
                return;

            root.position = (Vector3)Mouse.current.position.ReadValue() + (Vector3)offset;
        }
    }
}
