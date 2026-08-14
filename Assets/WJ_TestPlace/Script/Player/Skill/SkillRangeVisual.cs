using UnityEngine;

/// <summary>
/// 스킬 판정 범위를 짧게 화면에 표시한다(부채꼴/직선) - 실제로 판정에 쓴 것과 같은 모양·수치로
/// 그려서 "왜 맞았는지/왜 빗나갔는지" 시각적으로 확인할 수 있게 한다. 판정 자체와는 독립적이라
/// 이 비주얼을 지워도 피해 적용에는 영향이 없다.
/// </summary>
public static class SkillRangeVisual
{
    private static Material sharedTemplate;

    /// <summary>부채꼴(반원 등) 범위를 표시한다. angle은 전체 각도(180이면 정반원).</summary>
    public static void ShowSector(Vector3 origin, Vector3 forward, float range, float angle, Color color, float duration = 0.25f)
    {
        var go = new GameObject("[SkillRangeVisual] Sector");
        go.transform.position = origin + Vector3.up * 0.05f;
        Attach(go, BuildSectorMesh(forward, range, angle), color, duration);
    }

    /// <summary>정면 직선(사각형) 범위를 표시한다.</summary>
    public static void ShowLine(Vector3 origin, Vector3 forward, float length, float width, Color color, float duration = 0.25f)
    {
        var go = new GameObject("[SkillRangeVisual] Line");
        go.transform.position = origin + Vector3.up * 0.05f;
        Attach(go, BuildLineMesh(forward, length, width), color, duration);
    }

    /// <summary>
    /// 부채꼴(또는 angle=360이면 원형) 범위의 테두리를 지속적으로 표시한다. 차징처럼 끝나는 시점을
    /// 미리 알 수 없는 경우에 쓰며, 자동으로 사라지지 않으므로 호출자가 반환된 GameObject를 직접
    /// Destroy해서 종료 시점을 관리해야 한다. followTarget의 자식으로 붙어서 위치/회전을 따라간다.
    /// </summary>
    public static GameObject ShowPersistentSectorOutline(Transform followTarget, float range, float angle, Color color, float lineWidth = 0.08f)
    {
        var go = new GameObject("[SkillRangeVisual] ChargeOutline");
        go.transform.SetParent(followTarget, false);
        go.transform.localPosition = Vector3.up * 0.05f;
        go.transform.localRotation = Quaternion.identity;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = angle >= 360f;
        lr.widthMultiplier = lineWidth;
        lr.material = CreateMaterial(color);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Vector3[] points = BuildOutlinePoints(range, angle);
        lr.positionCount = points.Length;
        lr.SetPositions(points);

        return go;
    }

    private static void Attach(GameObject go, Mesh mesh, Color color, float duration)
    {
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.mesh = mesh;
        mr.material = CreateMaterial(color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Object.Destroy(go, duration);
    }

    private static Material CreateMaterial(Color color)
    {
        if (sharedTemplate == null)
            sharedTemplate = new Material(Shader.Find("Sprites/Default"));

        var mat = new Material(sharedTemplate);
        mat.color = color;
        return mat;
    }

    private static Mesh BuildSectorMesh(Vector3 forward, float range, float angle, int segments = 24)
    {
        var mesh = new Mesh();
        var vertices = new Vector3[segments + 2];
        var triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        Quaternion rot = Quaternion.LookRotation(forward);
        float startAngle = -angle * 0.5f;
        float step = angle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float a = (startAngle + step * i) * Mathf.Deg2Rad;
            Vector3 dir = rot * new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices[i + 1] = dir * range;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>부채꼴 테두리(또는 angle=360이면 원)를 구성하는 점 목록. 로컬 좌표(부모 forward = local +Z 기준).</summary>
    private static Vector3[] BuildOutlinePoints(float range, float angle, int segments = 32)
    {
        bool isFullCircle = angle >= 360f;
        var points = new System.Collections.Generic.List<Vector3>(segments + 3);

        float startAngle = -angle * 0.5f;
        float step = angle / segments;

        if (!isFullCircle)
            points.Add(Vector3.zero); // 부채꼴이면 중심에서 시작(파이 모양 테두리)

        for (int i = 0; i <= segments; i++)
        {
            float a = (startAngle + step * i) * Mathf.Deg2Rad;
            points.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * range);
        }

        if (!isFullCircle)
            points.Add(Vector3.zero); // 다시 중심으로 닫기

        return points.ToArray();
    }

    private static Mesh BuildLineMesh(Vector3 forward, float length, float width)
    {
        var mesh = new Mesh();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 half = right * (width * 0.5f);

        Vector3[] vertices =
        {
            -half,
            half,
            half + forward * length,
            -half + forward * length,
        };
        int[] triangles = { 0, 1, 2, 0, 2, 3 };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }
}
