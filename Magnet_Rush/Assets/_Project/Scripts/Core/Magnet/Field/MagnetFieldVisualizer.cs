using UnityEngine;

/// <summary>
/// 磁力範囲をワイヤーフレーム（LineRenderer）で可視化する。
/// Sphere/Box/Cylinderの3形状に対応。フルパワー圏（inner）のみ描画。
/// リングは回転固定コンテナの子として生成し、親が回転してもワールド軸に平行を保つ。
/// </summary>
public class MagnetFieldVisualizer : MonoBehaviour
{
    private static Material s_sharedLineMaterial;
    private const int k_Segments = 48;
    private static readonly float[] s_latitudes = { -60f, -30f, 0f, 30f, 60f };
    private const int k_MeridianCount = 4;

    private Transform m_fieldContainer;

    /// <summary>
    /// MagnetFieldSettingsに基づいて形状を描画する。
    /// </summary>
    public void Show(MagneticPole pole, MagnetFieldSettings settings)
    {
#if !DEBUG && !UNITY_EDITOR
        return;
#endif
        if (settings == null) { ChannelLogger.LogGuardReturn("Magnet", "Visualizer設定なし"); return; }
        Hide();

        Color innerColor = pole == MagneticPole.S
            ? new Color(0.2f, 0.4f, 1f, 0.8f)
            : new Color(1f, 0.2f, 0.2f, 0.8f);
        Color outerColor = new Color(innerColor.r, innerColor.g, innerColor.b, 0.3f);

        var containerGO = new GameObject("MagnetFieldContainer");
        m_fieldContainer = containerGO.transform;
        m_fieldContainer.SetParent(transform);
        m_fieldContainer.localPosition = Vector3.zero;
        m_fieldContainer.localRotation = Quaternion.identity;

        float outer = settings.EffectiveOuterRadius;

        switch (settings.shape)
        {
            case FieldShape.Box:
                DrawBox(innerColor, settings.size);
                DrawBox(outerColor, settings.size + Vector3.one * (outer - settings.innerRadius) * 2f);
                break;

            case FieldShape.Cylinder:
                DrawCylinder(innerColor, settings.cylinderRadius, settings.cylinderHeight);
                float diff = outer - settings.innerRadius;
                DrawCylinder(outerColor, settings.cylinderRadius + diff, settings.cylinderHeight + diff * 2f);
                break;

            default:
                DrawSphere(innerColor, settings.innerRadius);
                DrawSphere(outerColor, outer);
                break;
        }
    }

    private void Hide()
    {
        if (m_fieldContainer != null)
        {
            Destroy(m_fieldContainer.gameObject);
            m_fieldContainer = null;
        }
    }

    // --- Sphere ---

    private void DrawSphere(Color color, float radius)
    {
        Vector3 c = Vector3.zero;

        foreach (float lat in s_latitudes)
        {
            float rad = lat * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad) * radius;
            float r = Mathf.Cos(rad) * radius;
            CreateRing(c + Vector3.up * h, color, Vector3.up, r);
        }

        for (int i = 0; i < k_MeridianCount; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            CreateRing(c, color, normal, radius);
        }
    }

    // --- Box ---

    private void DrawBox(Color color, Vector3 size)
    {
        Vector3 half = size * 0.5f;

        // 上面・下面の矩形
        DrawRect(Vector3.up * half.y, color, half.x, half.z);
        DrawRect(Vector3.down * half.y, color, half.x, half.z);

        // 4本の縦辺
        CreateLine(new Vector3(half.x, half.y, half.z), new Vector3(half.x, -half.y, half.z), color);
        CreateLine(new Vector3(-half.x, half.y, half.z), new Vector3(-half.x, -half.y, half.z), color);
        CreateLine(new Vector3(half.x, half.y, -half.z), new Vector3(half.x, -half.y, -half.z), color);
        CreateLine(new Vector3(-half.x, half.y, -half.z), new Vector3(-half.x, -half.y, -half.z), color);
    }

    private void DrawRect(Vector3 center, Color color, float halfX, float halfZ)
    {
        Vector3 a = center + new Vector3(halfX, 0, halfZ);
        Vector3 b = center + new Vector3(-halfX, 0, halfZ);
        Vector3 c = center + new Vector3(-halfX, 0, -halfZ);
        Vector3 d = center + new Vector3(halfX, 0, -halfZ);

        CreateLine(a, b, color);
        CreateLine(b, c, color);
        CreateLine(c, d, color);
        CreateLine(d, a, color);
    }

    // --- Cylinder ---

    private void DrawCylinder(Color color, float radius, float height)
    {
        float halfH = height * 0.5f;
        Vector3 top = Vector3.up * halfH;
        Vector3 bottom = Vector3.down * halfH;

        // 上面・下面の円
        CreateRing(top, color, Vector3.up, radius);
        CreateRing(bottom, color, Vector3.up, radius);

        // 4本の縦辺
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            CreateLine(top + offset, bottom + offset, color);
        }
    }

    // --- 描画プリミティブ ---

    private void CreateRing(Vector3 localPosition, Color color, Vector3 normal, float radius)
    {
        var ringGO = new GameObject("MagnetRing");
        ringGO.transform.SetParent(m_fieldContainer);
        ringGO.transform.localPosition = localPosition;
        ringGO.transform.localRotation = Quaternion.identity;

        var lr = ringGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.05f;
        SetupLineMaterial(lr, color);

        lr.positionCount = k_Segments;

        Vector3 up = normal.normalized;
        Vector3 forward = Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.99f
            ? Vector3.Cross(up, Vector3.up).normalized
            : Vector3.Cross(up, Vector3.forward).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        for (int i = 0; i < k_Segments; i++)
        {
            float a = (float)i / k_Segments * Mathf.PI * 2f;
            lr.SetPosition(i, forward * (Mathf.Cos(a) * radius) + right * (Mathf.Sin(a) * radius));
        }
    }

    private void CreateLine(Vector3 from, Vector3 to, Color color)
    {
        var lineGO = new GameObject("MagnetLine");
        lineGO.transform.SetParent(m_fieldContainer);
        lineGO.transform.localPosition = Vector3.zero;
        lineGO.transform.localRotation = Quaternion.identity;

        var lr = lineGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.widthMultiplier = 0.05f;
        SetupLineMaterial(lr, color);

        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
    }

    private void SetupLineMaterial(LineRenderer lr, Color color)
    {
        if (s_sharedLineMaterial == null)
            s_sharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = s_sharedLineMaterial;
        lr.startColor = color;
        lr.endColor = color;
    }

    void LateUpdate()
    {
        if (m_fieldContainer != null)
            m_fieldContainer.rotation = Quaternion.identity;
    }

    void OnDestroy()
    {
        Hide();
    }
}
