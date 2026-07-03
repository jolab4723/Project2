using UnityEngine;

public class BeamAnimator : MonoBehaviour
{
    [SerializeField] private Renderer beamRenderer;
    [SerializeField] private Renderer ringRenderer;
    [SerializeField] private Camera targetCamera;
    [Header("Color")]
    [SerializeField] private Color gradeColor = new Color(0.75f, 0.25f, 1f, 1f);

    [Header("Pulse")]
    [SerializeField] private float baseIntensity = 2.5f;
    [SerializeField] private float pulseAmount = 0.15f;
    [SerializeField] private float pulseSpeed = 1.5f;


    private MaterialPropertyBlock block;
    private MaterialPropertyBlock ringblock;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        ringblock = new MaterialPropertyBlock();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Update()
    {
        float pulse = baseIntensity + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        Color finalColor = gradeColor * pulse;
        finalColor.a = gradeColor.a;


            block.SetColor(BaseColorID, finalColor);
            ringblock.SetColor(BaseColorID, gradeColor);
            beamRenderer.SetPropertyBlock(block);
            ringRenderer.SetPropertyBlock(ringblock);
    }

    public void SetColor(Color color, float intensity)
    {
        gradeColor = color;
        baseIntensity = intensity;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        transform.forward = targetCamera.transform.forward;
    }
}