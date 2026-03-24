using System.Collections.Generic;
using UnityEngine;
using MagnetRush.Common;

/// <summary>
/// 磁力システムの中枢。全Magnetizableを管理し、ペア間の引力/反発を計算・適用する。
/// </summary>
[DefaultExecutionOrder(-50)]
public class MagnetManager : Singleton<MagnetManager>
{
    [SerializeField] private MagnetSettings settings;

    private readonly HashSet<Magnetizable> registry = new();

    public void Register(Magnetizable m)
    {
        if (m != null) registry.Add(m);
    }

    public void Unregister(Magnetizable m)
    {
        if (m != null) registry.Remove(m);
    }

    void FixedUpdate()
    {
        // 破棄済みオブジェクトを除去
        registry.RemoveWhere(m => m == null || !m.gameObject.activeInHierarchy);

        var list = new List<Magnetizable>(registry);

        // 全有効ペアをイテレート
        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].IsActive) continue;

            for (int j = i + 1; j < list.Count; j++)
            {
                if (!list[j].IsActive) continue;

                var pair = new MagnetPair(list[i], list[j]);
                ProcessPair(pair);
            }
        }
    }

    private void ProcessPair(MagnetPair pair)
    {
        float distance = pair.Distance;
        if (distance > settings.magnetRange || distance < 0.01f) return;

        // F = magnetForce / (distance ^ forceDecayPower)
        float forceMagnitude = settings.magnetForce / Mathf.Pow(distance, settings.forceDecayPower);

        // 合力上限
        if (settings.maxForcePerObject > 0f)
        {
            forceMagnitude = Mathf.Min(forceMagnitude, settings.maxForcePerObject);
        }

        Vector3 dirAtoB = pair.DirectionAtoB;

        if (pair.IsOppositePole)
        {
            // 異極: 引力（互いに近づく）
            pair.a.ApplyForce(dirAtoB * forceMagnitude);
            pair.b.ApplyForce(-dirAtoB * forceMagnitude);
        }
        else if (pair.IsSamePole)
        {
            // 同極: 反発（互いに離れる）
            pair.a.ApplyForce(-dirAtoB * forceMagnitude);
            pair.b.ApplyForce(dirAtoB * forceMagnitude);
        }
    }

    public float GetMagnetRange()
    {
        return settings != null ? settings.magnetRange : 10f;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || settings == null) return;

        foreach (var m in registry)
        {
            if (m == null || !m.IsActive) continue;

            // 磁力範囲を円エッジ×3で描画（水平・正面・側面）
            Color color = m.Pole == MagneticPole.S
                ? new Color(1f, 0.2f, 0.2f, 0.8f)
                : new Color(0.2f, 0.4f, 1f, 0.8f);
            Gizmos.color = color;

            float r = settings.magnetRange;
            Vector3 p = m.transform.position;

            // 水平（赤道）
            DrawCircle(p, Vector3.up, r, 32);
            // 正面（経線）
            DrawCircle(p, Vector3.forward, r, 32);
            // 側面（経線）
            DrawCircle(p, Vector3.right, r, 32);
            // 斜め経線×2（球体感を出す）
            DrawCircle(p, (Vector3.forward + Vector3.right).normalized, r, 32);
            DrawCircle(p, (Vector3.forward - Vector3.right).normalized, r, 32);
            // 上下の緯度線
            DrawCircle(p + Vector3.up * r * 0.5f, Vector3.up, r * 0.866f, 24);
            DrawCircle(p - Vector3.up * r * 0.5f, Vector3.up, r * 0.866f, 24);
        }

        // ペア間のライン
        var list = new List<Magnetizable>(registry);
        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].IsActive) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                if (!list[j].IsActive) continue;
                var pair = new MagnetPair(list[i], list[j]);
                if (pair.Distance > settings.magnetRange) continue;

                // 引力=白、斥力=黄
                Gizmos.color = pair.IsOppositePole ? Color.white : Color.yellow;
                Gizmos.DrawLine(list[i].transform.position, list[j].transform.position);
            }
        }
    }

    /// <summary>
    /// 指定した法線軸を中心に円を描画する。
    /// </summary>
    private void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Quaternion rot = Quaternion.LookRotation(normal);
        Vector3 prev = center + rot * (Vector3.up * radius);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 next = center + rot * (new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
