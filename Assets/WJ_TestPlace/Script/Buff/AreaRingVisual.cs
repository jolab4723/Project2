using UnityEngine;

/// <summary>
/// 원형 영역을 바닥에 링으로 그려서 실제 플레이 화면에서도 보이게 한다.
/// (에디터 기즈모는 Scene 뷰에서만 보이고 실제 게임 화면/빌드에는 안 나온다)
/// LineRenderer로 그려서 별도 메시·텍스처 에셋 없이 동작한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AreaRingVisual : MonoBehaviour
{
    [SerializeField] private int segments = 48;
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private Color color = new Color(0.4f, 0.8f, 1f, 0.8f);

    [Tooltip("바닥 메시에 파묻히지 않도록 살짝 띄우는 높이.")]
    [SerializeField] private float heightOffset = 0.05f;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.widthMultiplier = lineWidth;
        line.positionCount = segments;
        line.startColor = color;
        line.endColor = color;

        // 내장 Sprites/Default는 URP/빌트인 어디서나 언릿로 바로 보여서, 별도 머티리얼 에셋 없이 동작한다.
        line.material = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>원 반지름(월드 유닛)을 설정한다. FieldAuraUniqueEffectSO처럼 반경이 나중에 정해지는 경우를 위해 분리했다.</summary>
    public void SetRadius(float radius)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, heightOffset, Mathf.Sin(angle) * radius));
        }
    }

    public void SetColor(Color newColor)
    {
        color = newColor;
        if (line != null)
        {
            line.startColor = color;
            line.endColor = color;
        }
    }
}
