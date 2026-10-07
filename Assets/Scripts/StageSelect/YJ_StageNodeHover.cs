using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 노드의 마우스 Hover, 클릭, 선택 가능 여부와 시각 효과를 관리합니다.
/// </summary>
[DisallowMultipleComponent]
public class YJ_StageNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Hover Sound")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 1f;

    [Header("Click Sound")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 1f;

    [Header("Background")]
    // Hover 및 선택 상태에 따라 알파와 색상을 변경할 Background 이미지입니다.
    [SerializeField] private Image backgroundImage;
    // Background의 확대 효과와 활성화 상태를 제어할 RectTransform입니다.
    [SerializeField] private RectTransform backgroundRect;

    [Header("Icon")]
    // 클릭 축소와 Hover 부유 효과를 적용할 Icon의 RectTransform입니다.
    [SerializeField] private RectTransform iconRect;
    // 현재 선택할 수 없는 상태에서 회색 Tint를 적용할 Icon 이미지입니다.
    [SerializeField] private Image iconImage;

    [Header("Hover Effect")]
    // 선택 가능한 노드가 평상시 표시할 Background 알파입니다.
    [SerializeField, Range(0f, 1f)] private float normalAlpha = 0.1f;
    // 커서가 노드 위에 있을 때 표시할 Background 알파입니다.
    [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.5f;
    // 노드가 선택된 상태에서 표시할 Background 알파입니다.
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    // Hover 중 Background를 확대할 배율입니다.
    [SerializeField, Min(1f)] private float hoverScale = 1.1f;
    // Background 알파와 크기가 목표값까지 전환되는 시간입니다.
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.2f;

    [Header("Hover Float")]
    // Hover 중 Icon이 기준 위치에서 상하로 이동할 최대 거리입니다.
    [SerializeField, Min(0f)] private float hoverFloatDistance = 10f;
    // Icon 부유 애니메이션이 1초 동안 반복되는 횟수입니다.
    [SerializeField, Min(0.01f)] private float hoverFloatFrequency = 0.75f;
    // Hover 종료 후 Icon이 기준 위치로 돌아오는 시간입니다.
    [SerializeField, Min(0.01f)] private float hoverFloatReturnDuration = 0.15f;

    [Header("Click Effect")]
    // 클릭 순간 Icon을 축소할 배율입니다.
    [SerializeField, Range(0.1f, 1f)] private float pressedIconScale = 0.9f;
    // Icon이 클릭 크기까지 줄어드는 시간입니다.
    [SerializeField, Min(0.01f)] private float shrinkDuration = 0.08f;
    // 축소된 Icon이 원래 크기로 돌아오는 시간입니다.
    [SerializeField, Min(0.01f)] private float expandDuration = 0.12f;

    [Header("Selection")]
    // 클릭 결과를 전달하고 전체 노드 선택 상태를 관리하는 매니저입니다.
    [SerializeField] private YJ_StageSelectManager stageSelectManager;
    // 이 UI가 표현하는 노드의 맵 데이터입니다.
    [SerializeField] private YJ_StageNodeData nodeData;
    // 현재 노드가 Hover와 클릭 입력을 받을 수 있는지 나타냅니다.
    [SerializeField] private bool isInteractable = true;
    // 현재 노드가 선택된 상태인지 나타냅니다.
    [SerializeField] private bool isSelected;

    [Header("Disabled Tint")]
    // 선택하지 않은 과거 노드와 현재 경로에서 도달할 수 없는 미래 노드에 적용할 색상입니다.
    [SerializeField] private Color clearedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    // 현재 경로에서 앞으로 도달 가능하지만 아직 선택 차례가 아닌 노드에 적용할 색상입니다.
    [SerializeField] private Color unclearedTint = new Color(0.3f, 0.3f, 0.3f, 1f);
    // 원본 색상과 비활성 Tint 사이를 부드럽게 전환하는 시간입니다.
    [SerializeField, Min(0f)] private float tintTransitionDuration = 0.5f;

    // Background가 확대되지 않은 상태의 기준 크기입니다.
    private Vector3 normalScale;
    // Icon 클릭 애니메이션이 끝난 뒤 복원할 기준 크기입니다.
    private Vector3 normalIconScale;
    // Icon 부유 애니메이션이 끝난 뒤 복원할 기준 위치입니다.
    private Vector2 normalIconPosition;
    // 비활성 Tint를 해제할 때 복원할 Background 원본 색상입니다.
    private Color originalBackgroundColor;
    // 비활성 Tint를 해제할 때 복원할 Icon 원본 색상입니다.
    private Color originalIconColor;
    // 현재 커서가 노드 위에 있는지 나타냅니다.
    private bool isPointerInside;
    // 플레이어가 실제로 선택하여 클리어한 경로의 노드인지 나타냅니다.
    private bool isClearedNode;
    // 현재 노드가 클리어된 층에 포함되는지 나타냅니다.
    private bool isClearedFloor;
    // 현재 진행 경로에서 이 노드에 도달할 수 없는지 나타냅니다.
    private bool isPathBlocked;
    // Background 알파와 크기 전환을 실행 중인 코루틴입니다.
    private Coroutine transitionRoutine;
    // Icon 클릭 축소 효과를 실행 중인 코루틴입니다.
    private Coroutine clickRoutine;
    // Icon Hover 부유 효과를 실행 중인 코루틴입니다.
    private Coroutine hoverFloatRoutine;
    // 노드 Icon과 Background의 Tint 전환을 실행 중인 코루틴입니다.
    private Coroutine tintTransitionRoutine;

    // 외부에서 현재 선택 상태를 읽을 때 사용합니다.
    public bool IsSelected => isSelected;
    // 매니저가 클릭 가능 여부를 검사할 때 사용합니다.
    public bool IsInteractable => isInteractable;
    // 매니저가 이 UI에 연결된 노드 데이터를 조회할 때 사용합니다.
    public YJ_StageNodeData NodeData => nodeData;

    /// <summary>
    /// 컴포넌트를 Inspector에서 처음 추가하거나 Reset할 때 하위 UI 참조를 찾습니다.
    /// </summary>
    private void Reset()
    {
        FindReferences();
    }

    /// <summary>
    /// 런타임 시작 시 UI 참조와 복원에 사용할 원본 시각 값을 저장합니다.
    /// </summary>
    private void Awake()
    {
        FindReferences();

        if (backgroundRect == null || backgroundImage == null)
        {
            enabled = false;
            return;
        }

        normalScale = backgroundRect.localScale;
        originalBackgroundColor = backgroundImage.color;

        if (iconRect != null)
        {
            normalIconScale = iconRect.localScale;
            normalIconPosition = iconRect.anchoredPosition;
        }

        if (iconImage != null)
            originalIconColor = iconImage.color;

        isSelected = false;
        SetBackgroundVisual(normalAlpha, normalScale);
    }

    /// <summary>
    /// 노드가 비활성화될 때 실행 중인 효과를 중지하고 UI를 초기 상태로 복원합니다.
    /// </summary>
    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (clickRoutine != null)
        {
            StopCoroutine(clickRoutine);
            clickRoutine = null;
        }

        if (hoverFloatRoutine != null)
        {
            StopCoroutine(hoverFloatRoutine);
            hoverFloatRoutine = null;
        }

        if (tintTransitionRoutine != null)
        {
            StopCoroutine(tintTransitionRoutine);
            tintTransitionRoutine = null;
        }

        if (stageSelectManager != null)
            stageSelectManager.NotifyNodeDisabled(this);

        isPointerInside = false;
        isSelected = false;
        isInteractable = true;
        isClearedNode = false;
        isClearedFloor = false;
        isPathBlocked = false;
        ApplyDisabledTint();

        if (backgroundRect != null && backgroundImage != null)
        {
            backgroundRect.gameObject.SetActive(true);
            SetBackgroundVisual(normalAlpha, normalScale);
        }

        if (iconRect != null)
        {
            iconRect.localScale = normalIconScale;
            iconRect.anchoredPosition = normalIconPosition;
        }
    }

    /// <summary>
    /// 선택 가능한 노드에 커서가 진입하면 Hover 시각 효과와 Icon 부유 효과를 시작합니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !isInteractable)
            return;

        if (!isPointerInside)
            PlayUISound(hoverSound, hoverVolume);

        isPointerInside = true;
        RefreshBackgroundVisual();
        StartHoverFloat();
    }

    /// <summary>
    /// 커서가 노드에서 벗어나면 Hover 상태를 해제하고 원래 시각 상태로 전환합니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        RefreshBackgroundVisual();
    }

    /// <summary>
    /// 마우스 왼쪽 클릭을 받으면 매니저에 노드 선택을 요청하고 클릭 애니메이션을 실행합니다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !isInteractable || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (stageSelectManager == null)
            stageSelectManager = YJ_StageSelectManager.Instance;

        // 이미 선택된 노드의 재클릭은 매니저가 판단합니다(이어하기로 복원된 pending 노드만 진입).
        if (isSelected && (stageSelectManager == null || stageSelectManager.IsExternallyControlled))
            return;

        bool selected = stageSelectManager != null
            ? stageSelectManager.SelectNode(this)
            : SetSelectedWithoutManager();

        if (selected)
        {
            PlayUISound(clickSound, clickVolume);
            PlayClickAnimation();
        }
    }

    private static void PlayUISound(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f)
            return;

        var player = YJ_SfxPlayer.Instance;
        if (player != null)
            player.PlayUI(clip, volume);
    }

    /// <summary>
    /// 생성된 노드 UI에 대응하는 데이터와 선택 매니저를 연결합니다.
    /// </summary>
    public void Initialize(YJ_StageNodeData data, YJ_StageSelectManager manager)
    {
        nodeData = data;
        stageSelectManager = manager;
        isSelected = false;
    }

    /// <summary>
    /// 노드의 입력 가능 상태만 변경하면서 나머지 시각 상태는 유지합니다.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        ApplyState(isClearedNode, isClearedFloor, isPathBlocked, interactable);
    }

    /// <summary>
    /// 플레이어가 실제로 선택하여 클리어한 경로 노드인지 변경합니다.
    /// </summary>
    public void SetNodeCleared(bool cleared)
    {
        ApplyState(cleared, isClearedFloor, isPathBlocked, isInteractable);
    }

    /// <summary>
    /// 노드가 클리어된 층에 속하는지 변경하고 비활성 시각 상태를 갱신합니다.
    /// </summary>
    public void SetFloorCleared(bool cleared)
    {
        ApplyState(isClearedNode, cleared, isPathBlocked, isInteractable);
    }

    /// <summary>
    /// 현재 선택 경로에서 도달 불가능한 노드인지 변경합니다.
    /// </summary>
    public void SetPathBlocked(bool blocked)
    {
        ApplyState(isClearedNode, isClearedFloor, blocked, isInteractable);
    }

    /// <summary>
    /// 선택 경로, 클리어 층, 경로 차단 및 입력 가능 상태를 한 번에 적용합니다.
    /// </summary>
    public void ApplyState(
        bool nodeCleared,
        bool floorCleared,
        bool pathBlocked,
        bool interactable)
    {
        bool wasVisuallyDisabled = IsVisuallyDisabled();
        bool wasUsingClearedTint = UsesClearedTint();
        bool clearedNodeChanged = isClearedNode != nodeCleared;
        bool stateChanged = clearedNodeChanged ||
                            isClearedFloor != floorCleared ||
                            isPathBlocked != pathBlocked;
        bool interactionChanged = isInteractable != interactable;

        if (!stateChanged && !interactionChanged)
            return;

        isClearedNode = nodeCleared;
        isClearedFloor = floorCleared;
        isPathBlocked = pathBlocked;
        isInteractable = interactable;

        if (!isInteractable)
        {
            isPointerInside = false;
            isSelected = false;
        }

        bool disabledVisualChanged = wasVisuallyDisabled != IsVisuallyDisabled() ||
                                     wasUsingClearedTint != UsesClearedTint();

        if (clearedNodeChanged || disabledVisualChanged)
            RefreshDisabledState();
        else
            RefreshBackgroundVisual();
    }

    /// <summary>
    /// 현재 선택 가능 여부에 따라 Tint와 Background 활성 상태를 갱신합니다.
    /// </summary>
    private void RefreshDisabledState()
    {
        ApplyDisabledTint();

        if (backgroundRect != null)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            backgroundRect.localScale = normalScale;
            backgroundRect.gameObject.SetActive(!IsVisuallyDisabled());
        }

        RefreshBackgroundVisual();
    }

    /// <summary>
    /// 노드 선택 여부를 설정하고 선택 알파를 반영합니다.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        RefreshBackgroundVisual();
    }

    /// <summary>
    /// 입력, 선택, Hover 상태를 조합해 Background의 목표 알파와 크기를 결정합니다.
    /// </summary>
    private void RefreshBackgroundVisual()
    {
        if (IsVisuallyDisabled() || backgroundRect == null || !backgroundRect.gameObject.activeSelf)
            return;

        float targetAlpha = isSelected
            ? selectedAlpha
            : isPointerInside ? hoverAlpha : normalAlpha;

        Vector3 targetScale = isInteractable && isPointerInside
            ? normalScale * hoverScale
            : normalScale;

        StartTransition(targetAlpha, targetScale);
    }

    /// <summary>
    /// 기존 Background 전환을 중단하고 새로운 알파 및 크기 전환을 시작합니다.
    /// </summary>
    private void StartTransition(float targetAlpha, Vector3 targetScale)
    {
        if (!isActiveAndEnabled)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionRoutine(targetAlpha, targetScale));
    }

    /// <summary>
    /// Background 알파와 크기를 지정된 시간 동안 부드럽게 보간합니다.
    /// </summary>
    private IEnumerator TransitionRoutine(float targetAlpha, Vector3 targetScale)
    {
        float startAlpha = backgroundImage.color.a;
        Vector3 startScale = backgroundRect.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            float alpha = Mathf.Lerp(startAlpha, targetAlpha, easedT);
            Vector3 scale = Vector3.Lerp(startScale, targetScale, easedT);
            SetBackgroundVisual(alpha, scale);

            yield return null;
        }

        SetBackgroundVisual(targetAlpha, targetScale);
        transitionRoutine = null;
    }

    /// <summary>
    /// Icon을 잠시 축소했다가 복원하는 클릭 효과를 시작합니다.
    /// </summary>
    private void PlayClickAnimation()
    {
        if (iconRect == null)
            return;

        if (clickRoutine != null)
            StopCoroutine(clickRoutine);

        clickRoutine = StartCoroutine(ClickRoutine());
    }

    /// <summary>
    /// 기존 부유 효과를 교체하고 Hover용 Icon 상하 이동을 시작합니다.
    /// </summary>
    private void StartHoverFloat()
    {
        if (iconRect == null)
            return;

        if (hoverFloatRoutine != null)
            StopCoroutine(hoverFloatRoutine);

        hoverFloatRoutine = StartCoroutine(HoverFloatRoutine());
    }

    /// <summary>
    /// Hover 중 Icon을 사인 곡선으로 상하 이동시키고 종료 시 기준 위치로 복귀시킵니다.
    /// </summary>
    private IEnumerator HoverFloatRoutine()
    {
        float elapsedTime = 0f;

        while (isPointerInside && isInteractable)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float phase = elapsedTime * hoverFloatFrequency * Mathf.PI * 2f;
            float yOffset = Mathf.Sin(phase) * hoverFloatDistance;
            iconRect.anchoredPosition = normalIconPosition + Vector2.up * yOffset;
            yield return null;
        }

        Vector2 returnStartPosition = iconRect.anchoredPosition;
        elapsedTime = 0f;

        while (elapsedTime < hoverFloatReturnDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / hoverFloatReturnDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            iconRect.anchoredPosition = Vector2.Lerp(
                returnStartPosition,
                normalIconPosition,
                easedT);
            yield return null;
        }

        iconRect.anchoredPosition = normalIconPosition;
        hoverFloatRoutine = null;
    }

    /// <summary>
    /// Icon을 클릭 크기로 축소한 뒤 기준 크기로 확대합니다.
    /// </summary>
    private IEnumerator ClickRoutine()
    {
        Vector3 pressedScale = normalIconScale * pressedIconScale;

        yield return ScaleIcon(iconRect.localScale, pressedScale, shrinkDuration);
        yield return ScaleIcon(iconRect.localScale, normalIconScale, expandDuration);

        iconRect.localScale = normalIconScale;
        clickRoutine = null;
    }

    /// <summary>
    /// Icon 크기를 시작값에서 목표값까지 부드럽게 보간합니다.
    /// </summary>
    private IEnumerator ScaleIcon(Vector3 startScale, Vector3 targetScale, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            iconRect.localScale = Vector3.Lerp(startScale, targetScale, easedT);

            yield return null;
        }

        iconRect.localScale = targetScale;
    }

    /// <summary>
    /// Background 이미지 알파와 RectTransform 크기를 즉시 적용합니다.
    /// </summary>
    private void SetBackgroundVisual(float alpha, Vector3 scale)
    {
        Color color = backgroundImage.color;
        color.a = alpha;
        backgroundImage.color = color;
        backgroundRect.localScale = scale;
    }

    /// <summary>
    /// 프리팹의 Background, Icon과 런타임 매니저 및 노드 데이터 참조를 찾습니다.
    /// </summary>
    private void FindReferences()
    {
        if (backgroundRect == null)
        {
            Transform background = transform.Find("Background");
            if (background != null)
                backgroundRect = background as RectTransform;
        }

        if (backgroundImage == null && backgroundRect != null)
            backgroundImage = backgroundRect.GetComponent<Image>();

        if (iconRect == null)
        {
            Transform icon = transform.Find("Icon");
            if (icon != null)
                iconRect = icon as RectTransform;
        }

        if (iconImage == null && iconRect != null)
            iconImage = iconRect.GetComponent<Image>();

        if (stageSelectManager == null)
            stageSelectManager = YJ_StageSelectManager.Instance;

        if (nodeData == null)
            nodeData = GetComponent<YJ_StageNodeData>();
    }

    /// <summary>
    /// 매니저가 없는 단독 테스트 상황에서 이 노드만 선택 상태로 만듭니다.
    /// </summary>
    private bool SetSelectedWithoutManager()
    {
        if (isSelected)
            return false;

        SetSelected(true);
        return true;
    }

    /// <summary>
    /// 클리어, 경로 차단 또는 현재 선택 불가 때문에 비활성 표현이 필요한지 반환합니다.
    /// </summary>
    private bool IsVisuallyDisabled()
    {
        return isClearedFloor || isPathBlocked || !isInteractable;
    }

    /// <summary>
    /// 과거 층이거나 현재 경로에서 차단되어 Cleared Tint를 사용해야 하는지 반환합니다.
    /// </summary>
    private bool UsesClearedTint()
    {
        return isClearedFloor || isPathBlocked;
    }

    /// <summary>
    /// 선택하지 않은 비활성 노드에는 층 진행 상태에 맞는 Tint를 적용하고 지나온 노드는 원본 색상을 유지합니다.
    /// </summary>
    private void ApplyDisabledTint()
    {
        bool useDisabledTint = IsVisuallyDisabled() && !isClearedNode;
        Color disabledTint = UsesClearedTint()
            ? clearedTint
            : unclearedTint;

        Color targetBackgroundColor = useDisabledTint
            ? disabledTint
            : originalBackgroundColor;
        Color targetIconColor = useDisabledTint
            ? disabledTint
            : originalIconColor;

        if (backgroundImage != null)
            targetBackgroundColor.a = backgroundImage.color.a;

        if (iconImage != null)
            targetIconColor.a = originalIconColor.a;

        if (tintTransitionRoutine != null)
        {
            StopCoroutine(tintTransitionRoutine);
            tintTransitionRoutine = null;
        }

        if (!isActiveAndEnabled || tintTransitionDuration <= Mathf.Epsilon)
        {
            SetTintColors(targetBackgroundColor, targetIconColor);
            return;
        }

        tintTransitionRoutine = StartCoroutine(
            TintTransitionRoutine(targetBackgroundColor, targetIconColor));
    }

    /// <summary>
    /// 현재 노드 색상에서 목표 Tint까지 지정된 시간 동안 부드럽게 보간합니다.
    /// </summary>
    private IEnumerator TintTransitionRoutine(
        Color targetBackgroundColor,
        Color targetIconColor)
    {
        Color startBackgroundColor = backgroundImage != null
            ? backgroundImage.color
            : targetBackgroundColor;
        Color startIconColor = iconImage != null
            ? iconImage.color
            : targetIconColor;
        float elapsedTime = 0f;

        while (elapsedTime < tintTransitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / tintTransitionDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            SetTintColors(
                Color.LerpUnclamped(startBackgroundColor, targetBackgroundColor, easedT),
                Color.LerpUnclamped(startIconColor, targetIconColor, easedT));
            yield return null;
        }

        SetTintColors(targetBackgroundColor, targetIconColor);
        tintTransitionRoutine = null;
    }

    /// <summary>
    /// 계산된 Background와 Icon 색상을 존재하는 UI 참조에 즉시 적용합니다.
    /// </summary>
    private void SetTintColors(Color backgroundColor, Color iconColor)
    {
        if (backgroundImage != null)
            backgroundImage.color = backgroundColor;

        if (iconImage != null)
            iconImage.color = iconColor;
    }
}
