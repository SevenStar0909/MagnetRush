using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 磁力システムの中枢。全Magnetizableを管理し、ペア間の引力/反発を計算・適用する。
/// 接触距離に入った異極ペアにはOnMagnetContactを通知する。
/// </summary>
[DefaultExecutionOrder(-50)]
public class MagnetManager : Singleton<MagnetManager>
{
    [SerializeField] private MagnetSettings settings;

    private readonly HashSet<Magnetizable> registry = new();
    private readonly List<Magnetizable> cachedList = new();
    private bool listDirty = true;

    // MagnetField レジストリ
    private readonly HashSet<MagnetField> fieldRegistry = new();
    private readonly List<MagnetField> cachedFields = new();
    private bool fieldsDirty = true;

    // 接触中ペアの追跡（Enter/Exit判定用）
    private readonly HashSet<long> activeContacts = new();

    public void Register(Magnetizable m)
    {
        if (m != null && registry.Add(m))
            listDirty = true;
    }

    public void Unregister(Magnetizable m)
    {
        if (m != null && registry.Remove(m))
            listDirty = true;
    }

    public void RegisterField(MagnetField f)
    {
        if (f != null && fieldRegistry.Add(f))
        {
            fieldsDirty = true;
            f.OnFieldExpired += () => HandleFieldExplosion(f);
        }
    }

    public void UnregisterField(MagnetField f)
    {
        if (f != null && fieldRegistry.Remove(f))
            fieldsDirty = true;
    }

    /// <summary>弾道吸引用。アクティブなフィールド一覧を返す。</summary>
    public List<MagnetField> GetActiveFields() => cachedFields;

    void FixedUpdate()
    {
        // 破棄済みオブジェクト除去
        int removed = registry.RemoveWhere(m => m == null || !m.gameObject.activeInHierarchy);
        if (removed > 0) listDirty = true;

        int removedFields = fieldRegistry.RemoveWhere(f => f == null);
        if (removedFields > 0) fieldsDirty = true;

        // キャッシュ更新（dirty時のみ）
        if (listDirty)
        {
            cachedList.Clear();
            cachedList.AddRange(registry);
            listDirty = false;
        }

        if (fieldsDirty)
        {
            cachedFields.Clear();
            cachedFields.AddRange(fieldRegistry);
            fieldsDirty = false;
        }

        // 今フレームの接触ペアを追跡
        var contactsThisFrame = new HashSet<long>();

        // 全有効ペアをイテレート（点力計算）
        for (int i = 0; i < cachedList.Count; i++)
        {
            if (!cachedList[i].IsActive) continue;

            // MagnetFieldを持つ弾は点力スキップ（フィールドが力を管理）
            if (cachedList[i].GetComponent<MagnetField>() != null) continue;

            for (int j = i + 1; j < cachedList.Count; j++)
            {
                if (!cachedList[j].IsActive) continue;
                if (cachedList[j].GetComponent<MagnetField>() != null) continue;

                ProcessPair(cachedList[i], cachedList[j], contactsThisFrame);
            }
        }

        // Entity ↔ Field 割り当て（nearest-wins）
        AssignFieldsToEntities();

        // 接触Exit判定
        activeContacts.IntersectWith(contactsThisFrame);
    }

    /// <summary>
    /// 各EntityにGetStrengthAtが最大のフィールドを割り当てる（nearest-wins）。
    /// </summary>
    private void AssignFieldsToEntities()
    {
        for (int i = 0; i < cachedList.Count; i++)
        {
            var mag = cachedList[i];
            var entity = mag.GetComponent<Entity>();
            if (entity == null) continue;

            MagnetField best = null;
            float bestStrength = 0f;

            for (int j = 0; j < cachedFields.Count; j++)
            {
                float s = cachedFields[j].GetStrengthAt(entity.transform.position);
                if (s > bestStrength)
                {
                    bestStrength = s;
                    best = cachedFields[j];
                }
            }

            entity.magnetField = best;
        }
    }

    private void ProcessPair(Magnetizable a, Magnetizable b, HashSet<long> contactsThisFrame)
    {
        Vector3 delta = b.transform.position - a.transform.position;
        float distance = delta.magnitude;
        if (distance > settings.magnetRange || distance < 0.01f) return;

        Vector3 dirAtoB = delta / distance;

        // F = magnetForce / (distance ^ forceDecayPower)
        float forceMagnitude = settings.magnetForce / Mathf.Pow(distance, settings.forceDecayPower);

        if (settings.maxForcePerObject > 0f)
            forceMagnitude = Mathf.Min(forceMagnitude, settings.maxForcePerObject);

        bool isOpposite = a.Pole != b.Pole && a.Pole != MagneticPole.None && b.Pole != MagneticPole.None;
        bool isSame = a.Pole == b.Pole;

        // 質量非対称: 軽い方が多く動く
        float massA = a.mass;
        float massB = b.mass;
        float ratioA, ratioB;

        if (float.IsInfinity(massA) && float.IsInfinity(massB))
        {
            ratioA = 0f; ratioB = 0f; // 両方固定 → どちらも動かない
        }
        else if (float.IsInfinity(massA))
        {
            ratioA = 0f; ratioB = 1f;
        }
        else if (float.IsInfinity(massB))
        {
            ratioA = 1f; ratioB = 0f;
        }
        else
        {
            float totalMass = massA + massB;
            ratioA = massB / totalMass;
            ratioB = massA / totalMass;
        }

        if (isOpposite)
        {
            a.ApplyForce(dirAtoB * forceMagnitude * ratioA);
            b.ApplyForce(-dirAtoB * forceMagnitude * ratioB);
        }
        else if (isSame)
        {
            a.ApplyForce(-dirAtoB * forceMagnitude * ratioA);
            b.ApplyForce(dirAtoB * forceMagnitude * ratioB);
        }

        // 接触判定（異極のみ、snapDistance内）
        if (isOpposite && distance < settings.snapDistance)
        {
            long pairKey = GetPairKey(a, b);
            contactsThisFrame.Add(pairKey);

            // Enter判定: 前フレームに接触していなかったら通知
            if (activeContacts.Add(pairKey))
            {
                a.NotifyContact(b);
                b.NotifyContact(a);
            }
        }
    }

    /// <summary>
    /// 2つのMagnetizableからユニークなペアキーを生成する。
    /// </summary>
    private static long GetPairKey(Magnetizable a, Magnetizable b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();
        if (idA > idB) (idA, idB) = (idB, idA);
        return ((long)idA << 32) | (uint)idB;
    }

    /// <summary>
    /// フィールド消滅時の爆発処理。蓄積ダメージを範囲内Entityに適用。
    /// </summary>
    private void HandleFieldExplosion(MagnetField field)
    {
        if (field == null || field.StoredDamage <= 0f) return;

        float radius = field.OuterRadius;
        Vector3 center = field.Center;
        float damage = field.StoredDamage;

        // 範囲内の全Entityにダメージ
        for (int i = 0; i < cachedList.Count; i++)
        {
            var entity = cachedList[i].GetComponent<Entity>();
            if (entity == null || entity.health == null) continue;

            float dist = Vector3.Distance(entity.transform.position, center);
            if (dist > radius) continue;

            // 距離減衰ダメージ
            float damageRatio = 1f - dist / radius;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * damageRatio));
            entity.health.Damage(finalDamage);
        }
    }

    public float GetMagnetRange()
    {
        return settings != null ? settings.magnetRange : 10f;
    }

    public MagnetSettings Settings => settings;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || settings == null) return;

        foreach (var m in registry)
        {
            if (m == null || !m.IsActive) continue;

            Color color = m.Pole == MagneticPole.S
                ? new Color(1f, 0.2f, 0.2f, 0.8f)
                : new Color(0.2f, 0.4f, 1f, 0.8f);
            Gizmos.color = color;

            float r = settings.magnetRange;
            Vector3 p = m.transform.position;

            DrawCircle(p, Vector3.up, r, 32);
            DrawCircle(p, Vector3.forward, r, 32);
            DrawCircle(p, Vector3.right, r, 32);
            DrawCircle(p, (Vector3.forward + Vector3.right).normalized, r, 32);
            DrawCircle(p, (Vector3.forward - Vector3.right).normalized, r, 32);
            DrawCircle(p + Vector3.up * r * 0.5f, Vector3.up, r * 0.866f, 24);
            DrawCircle(p - Vector3.up * r * 0.5f, Vector3.up, r * 0.866f, 24);
        }

        // ペア間のライン
        for (int i = 0; i < cachedList.Count; i++)
        {
            if (!cachedList[i].IsActive) continue;
            for (int j = i + 1; j < cachedList.Count; j++)
            {
                if (!cachedList[j].IsActive) continue;
                float dist = Vector3.Distance(cachedList[i].transform.position, cachedList[j].transform.position);
                if (dist > settings.magnetRange) continue;

                bool isOpposite = cachedList[i].Pole != cachedList[j].Pole
                    && cachedList[i].Pole != MagneticPole.None
                    && cachedList[j].Pole != MagneticPole.None;
                Gizmos.color = isOpposite ? Color.white : Color.yellow;
                Gizmos.DrawLine(cachedList[i].transform.position, cachedList[j].transform.position);
            }
        }
    }

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
