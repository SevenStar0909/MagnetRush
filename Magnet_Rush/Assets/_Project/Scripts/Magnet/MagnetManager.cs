using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 磁力システムの中枢。全Magnetizableを管理し、ペア間の引力/反発を計算・適用する。
/// 接触距離に入った異極ペアにはOnMagnetContactを通知する。
/// </summary>
[DefaultExecutionOrder(-50)]
public class MagnetManager : Singleton<MagnetManager>
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private MagnetSettings m_settings;

    private readonly HashSet<Magnetizable> m_registry = new();
    private readonly List<Magnetizable> m_cachedList = new();
    private bool m_listDirty = true;

    // MagnetField レジストリ
    private readonly HashSet<MagnetField> m_fieldRegistry = new();
    private readonly List<MagnetField> m_cachedFields = new();
    private bool m_fieldsDirty = true;

    // 接触中ペアの追跡（Enter/Exit判定用）
    private readonly HashSet<long> m_activeContacts = new();

    // スナップ解決
    private MagneticSnapResolver m_snapResolver;

    protected override void Awake()
    {
        base.Awake();
        m_snapResolver = new MagneticSnapResolver(m_settings);
    }

    public MagneticSnapResolver SnapResolver => m_snapResolver;

    public void Register(Magnetizable m)
    {
        if (m != null && m_registry.Add(m))
            m_listDirty = true;
    }

    public void Unregister(Magnetizable m)
    {
        if (m != null && m_registry.Remove(m))
            m_listDirty = true;
    }

    public void RegisterField(MagnetField f)
    {
        if (f != null && m_fieldRegistry.Add(f))
        {
            m_fieldsDirty = true;
            f.OnFieldExpired += () =>
            {
                HandleFieldExplosion(f);
                m_snapResolver?.ReleaseAllForField(f);
            };
        }
    }

    public void UnregisterField(MagnetField f)
    {
        if (f != null && m_fieldRegistry.Remove(f))
            m_fieldsDirty = true;
    }

    /// <summary>弾道吸引用。アクティブなフィールド一覧を返す。</summary>
    public List<MagnetField> GetActiveFields() => m_cachedFields;

    void FixedUpdate()
    {
        m_snapResolver?.CleanupDestroyedJoints();

        // 破棄済みオブジェクト除去
        int removed = m_registry.RemoveWhere(m => m == null || !m.gameObject.activeInHierarchy);
        if (removed > 0) m_listDirty = true;

        int removedFields = m_fieldRegistry.RemoveWhere(f => f == null);
        if (removedFields > 0) m_fieldsDirty = true;

        // キャッシュ更新（dirty時のみ）
        if (m_listDirty)
        {
            m_cachedList.Clear();
            m_cachedList.AddRange(m_registry);
            m_listDirty = false;
        }

        if (m_fieldsDirty)
        {
            m_cachedFields.Clear();
            m_cachedFields.AddRange(m_fieldRegistry);
            m_fieldsDirty = false;
        }

        // 今フレームの接触ペアを追跡
        var contactsThisFrame = new HashSet<long>();

        // 全有効ペアをイテレート（点力計算）
        for (int i = 0; i < m_cachedList.Count; i++)
        {
            if (!m_cachedList[i].IsActive) continue;

            for (int j = i + 1; j < m_cachedList.Count; j++)
            {
                if (!m_cachedList[j].IsActive) continue;

                ProcessPair(m_cachedList[i], m_cachedList[j], contactsThisFrame);
            }
        }

        // Entity ↔ Field 割り当て（nearest-wins）
        AssignFieldsToEntities();

        // 接触Exit判定
        m_activeContacts.IntersectWith(contactsThisFrame);
    }

    /// <summary>
    /// 各Fieldのトリガー検知結果から、EntityにGetStrengthAtが最大のフィールドを割り当てる。
    /// </summary>
    private void AssignFieldsToEntities()
    {
        // まず全Entityのフィールドをクリア
        for (int i = 0; i < m_cachedList.Count; i++)
        {
            var entity = m_cachedList[i].CachedEntity;
            if (entity != null)
                entity.magnetField = null;
        }

        // 各Fieldのトリガー検知済みEntityから最強フィールドを割り当て
        for (int i = 0; i < m_cachedFields.Count; i++)
        {
            var field = m_cachedFields[i];
            var entities = field.GetEntitiesInRange();

            for (int j = 0; j < entities.Count; j++)
            {
                var entity = entities[j];
                if (entity == null) continue;

                float strength = field.GetStrengthAt(entity.transform.position);

                if (entity.magnetField == null)
                {
                    entity.magnetField = field;
                }
                else
                {
                    float currentStrength = ((MagnetField)entity.magnetField).GetStrengthAt(entity.transform.position);
                    if (strength > currentStrength)
                        entity.magnetField = field;
                }
            }
        }
    }

    private void ProcessPair(Magnetizable a, Magnetizable b, HashSet<long> contactsThisFrame)
    {
        Vector3 delta = b.transform.position - a.transform.position;
        float distance = delta.magnitude;

        // ハードカットオフ（パフォーマンス用）
        if (distance > m_settings.magnetRange || distance < 0.01f) return;

        // 距離解除されたペアは力もスナップも完全スキップ（ワープ防止）
        if (m_snapResolver != null && m_snapResolver.IsBroken(a, b)) return;

        // フィールド個別範囲で有効距離を決定（大きい方を採用）
        float effectiveOuter = Mathf.Max(a.FieldOuterRadius, b.FieldOuterRadius);
        float effectiveInner = Mathf.Max(a.FieldInnerRadius, b.FieldInnerRadius);

        // フィールドがない場合はグローバル値にフォールバック
        if (effectiveOuter <= 0f) effectiveOuter = m_settings.magnetRange;
        if (effectiveInner <= 0f) effectiveInner = effectiveOuter * 0.8f;

        // フィールド範囲外なら力を適用しない
        if (distance > effectiveOuter) return;

        Vector3 dirAtoB = delta / distance;

        // inner/outer線形減衰: inner内=フルパワー、inner〜outer間=線形減衰、outer外=ゼロ
        float strength;
        if (distance <= effectiveInner)
        {
            strength = 1f;
        }
        else
        {
            strength = 1f - (distance - effectiveInner) / (effectiveOuter - effectiveInner);
        }

        float forceMagnitude = m_settings.magnetForce * strength;

        if (m_settings.maxForcePerObject > 0f)
            forceMagnitude = Mathf.Min(forceMagnitude, m_settings.maxForcePerObject);

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
            a.ApplyForce(dirAtoB * forceMagnitude * ratioA, b.transform.position);
            b.ApplyForce(-dirAtoB * forceMagnitude * ratioB, a.transform.position);
        }
        else if (isSame)
        {
            a.ApplyForce(-dirAtoB * forceMagnitude * ratioA, b.transform.position);
            b.ApplyForce(dirAtoB * forceMagnitude * ratioB, a.transform.position);
        }

        // 接触判定（異極のみ、snapDistance内）
        // snapDistance内ではSnapResolverのSmoothDampが位置を制御するため、
        // 上で適用した力は実質無視される（意図的な設計：吸着フェーズでは滑らかな接近を優先）
        if (isOpposite && distance < m_settings.snapDistance)
        {
            m_snapResolver?.Resolve(a, b, Time.fixedDeltaTime);

            long pairKey = GetPairKey(a, b);
            contactsThisFrame.Add(pairKey);

            // Enter判定: 前フレームに接触していなかったら通知
            if (m_activeContacts.Add(pairKey))
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
        for (int i = 0; i < m_cachedList.Count; i++)
        {
            var entity = m_cachedList[i].GetComponent<Entity>();
            if (entity == null || entity.m_health == null) continue;

            float dist = Vector3.Distance(entity.transform.position, center);
            if (dist > radius) continue;

            // 距離減衰ダメージ
            float damageRatio = 1f - dist / radius;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * damageRatio));
            entity.m_health.Damage(finalDamage);
        }
    }

    public MagnetSettings Settings => m_settings;

}
