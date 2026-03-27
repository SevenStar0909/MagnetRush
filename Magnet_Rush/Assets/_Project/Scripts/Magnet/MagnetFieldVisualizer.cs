using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 磁力範囲をワイヤーフレーム球（LineRenderer）で可視化する。
/// MagnetBulletから分離されたSRP準拠の可視化専用クラス。
/// </summary>
public class MagnetFieldVisualizer : MonoBehaviour
{
    private readonly List<GameObject> rings = new();
    private static Material sharedLineMaterial;

    private const int Segments = 48;
    private static readonly float[] Latitudes = { -60f, -30f, 0f, 30f, 60f };
    private const int MeridianCount = 4;

    /// <summary>
    /// 指定位置にワイヤーフレーム球を生成する。
    /// </summary>
    public void Show(MagneticPole pole, float range)
    {
#if !DEBUG && !UNITY_EDITOR
        return;
#endif
        Hide();

        Color color = pole == MagneticPole.S
            ? new Color(0.2f, 0.4f, 1f, 0.8f)
            : new Color(1f, 0.2f, 0.2f, 0.8f);

        Vector3 center = transform.position;

        // 緯度線×5
        foreach (float lat in Latitudes)
        {
            float rad = lat * Mathf.Deg2Rad;
            float h = Mathf.Sin(rad) * range;
            float r = Mathf.Cos(rad) * range;
            CreateRing(center + Vector3.up * h, color, Vector3.up, r);
        }

        // 経線×4
        for (int i = 0; i < MeridianCount; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            CreateRing(center, color, normal, range);
        }
    }

    /// <summary>
    /// 全リングを削除する。
    /// </summary>
    public void Hide()
    {
        foreach (var ring in rings)
        {
            if (ring != null) Destroy(ring);
        }
        rings.Clear();
    }

    private void CreateRing(Vector3 worldPosition, Color color, Vector3 normal, float radius)
    {
        var ringGO = new GameObject("MagnetRing");
        ringGO.transform.position = worldPosition;
        ringGO.transform.rotation = Quaternion.identity;
        rings.Add(ringGO);

        var lr = ringGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.05f;

        if (sharedLineMaterial == null)
            sharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
        lr.material = sharedLineMaterial;
        lr.startColor = color;
        lr.endColor = color;

        lr.positionCount = Segments;

        Vector3 up = normal.normalized;
        Vector3 forward = Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.99f
            ? Vector3.Cross(up, Vector3.up).normalized
            : Vector3.Cross(up, Vector3.forward).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            Vector3 point = forward * (Mathf.Cos(angle) * radius) + right * (Mathf.Sin(angle) * radius);
            lr.SetPosition(i, point);
        }
    }

    void OnDestroy()
    {
        Hide();
    }
}
