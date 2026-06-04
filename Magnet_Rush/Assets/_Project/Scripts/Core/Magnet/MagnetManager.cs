using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 磁力システムの中枢。全Magnetizableを管理し、ペア間の引力/反発を計算・適用する。
/// 接触距離に入った異極ペアにはOnMagnetContactを通知する。
/// </summary>
[DefaultExecutionOrder(-50)]
public class MagnetManager : Singleton<MagnetManager>
{
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

    // 弾近接検出用レジストリ
    private readonly List<IBulletProximity> m_bulletRegistry = new();

    // スナップ解決
    private MagneticSnapResolver m_snapResolver;

    // PD ホルダー管理（Entity絡みペアの双方向保持）
    private class HeldPair
    {
        public Magnetizable a;
        public Magnetizable b;
        // worldOffset はワールド座標で固定する (local 座標ではない)。
        // MagnetRush は TPS で Player がカメラに合わせて頻繁に旋回するため、
        // local 追従だと箱が Player の回転で振り回されて軌道を描く。見た目として不自然。
        // 「くっついた位置のままぶら下がる」挙動を意図している。
        public Vector3 worldOffset;
    }
    private readonly Dictionary<long, HeldPair> m_heldPairs = new();

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

    public void RegisterBullet(IBulletProximity bullet)
    {
        if (bullet != null && !m_bulletRegistry.Contains(bullet))
            m_bulletRegistry.Add(bullet);
    }

    public void UnregisterBullet(IBulletProximity bullet)
    {
        if (bullet != null)
            m_bulletRegistry.Remove(bullet);
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
        // ① 全磁化 Entity の holdVelocity を毎フレームゼロリセット（ProcessHold が再計算で上書き）
        //    heldPairs ベースではなく m_cachedList 全走査する（CleanupExpiredHolds で除外された
        //    Entity も確実に 0 に戻すため。幽霊運動の構造的防止）
        ResetAllHoldVelocities();

        // ② PD 保持ペアのクリーンアップ（距離超過・片方無効化）
        CleanupExpiredHolds();

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

        // 溜めた磁力を集約して適用。加算0で一番強い磁力源1件だけ、1で全件加算（重なりの加算量を調整）
        float overlapWeight = m_settings != null ? m_settings.overlapForceWeight : 1f;
        for (int i = 0; i < m_cachedList.Count; i++)
        {
            if (!m_cachedList[i].IsActive) continue;
            m_cachedList[i].ResolveMagneticForces(overlapWeight);
        }

        // Entity ↔ Field 割り当て（nearest-wins）
        AssignFieldsToEntities();

        // 接触Exit判定
        m_activeContacts.IntersectWith(contactsThisFrame);

        // 弾同士の近接検出（Trigger×Trigger非発火のため距離ベース）
        ProcessBulletProximity();
    }

    /// <summary>
    /// 異極の弾が近接した場合にダメージ蓄積を処理する。
    /// Trigger×TriggerはOnTriggerEnter非発火のため、距離ベースで検出。
    /// </summary>
    private void ProcessBulletProximity()
    {
        // 破棄済みを除去
        m_bulletRegistry.RemoveAll(b => b == null || b.gameObject == null);
        if (m_bulletRegistry.Count < 2) { ChannelLogger.LogGuardReturn("Magnet", "近接検出対象の弾が2個未満"); return; }

        float threshold = m_settings != null ? m_settings.bulletProximityRange : 1f;
        float thresholdSq = threshold * threshold;

        for (int i = 0; i < m_bulletRegistry.Count; i++)
        {
            var a = m_bulletRegistry[i];
            if (a.IsStuck) continue;

            for (int j = i + 1; j < m_bulletRegistry.Count; j++)
            {
                var b = m_bulletRegistry[j];
                if (b.IsStuck) continue;

                if (a.Pole == b.Pole || a.Pole == MagneticPole.None || b.Pole == MagneticPole.None) continue;

                float distSq = (a.transform.position - b.transform.position).sqrMagnitude;
                if (distSq > thresholdSq) continue;

                var fieldB = b.GetField();
                if (fieldB != null)
                    fieldB.AccumulateDamage(1);

                Destroy(a.gameObject);
                break;
            }
        }
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
        Vector3 delta = b.Position - a.Position;
        float distance = delta.magnitude;

        // ハードカットオフ（パフォーマンス用）
        if (distance > m_settings.magnetRange || distance < 0.01f) { ChannelLogger.LogGuardReturn("Magnet", "距離がmagnetRange外または近接しすぎ"); return; }

        // 距離解除されたペアは力もスナップも完全スキップ（ワープ防止）
        if (m_snapResolver != null && m_snapResolver.IsBroken(a, b)) { ChannelLogger.LogGuardReturn("Magnet", "brokenペアのためスキップ"); return; }

        // 球vs球判定/減衰: 中心点距離 vs (相手のinner球と自分のouter球が重なる距離)
        //   フルパワー境界 = 両inner球が表面接触 = A.inner + B.inner
        //   ゼロ境界       = 相手inner球が自分outer球の表面に触れる = max(A.outer+B.inner, B.outer+A.inner)
        float effectiveOuter = Mathf.Max(
            a.FieldOuterRadius + b.FieldInnerRadius,
            b.FieldOuterRadius + a.FieldInnerRadius);
        float effectiveInner = a.FieldInnerRadius + b.FieldInnerRadius;

        // フィールドがない場合はグローバル値にフォールバック
        if (effectiveOuter <= 0f) effectiveOuter = m_settings.magnetRange;
        if (effectiveInner <= 0f) effectiveInner = effectiveOuter * 0.8f;

        // フィールド範囲外なら力を適用しない
        if (distance > effectiveOuter) { ChannelLogger.LogGuardReturn("Magnet", "有効フィールド範囲外（球vs球）"); return; }

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

        bool isOpposite = a.Pole != b.Pole && a.Pole != MagneticPole.None && b.Pole != MagneticPole.None;
        bool isSame = a.Pole == b.Pole;

        // ドーム内オブジェクト同士のような「同じグループとして付与された」ペアは
        // 同極反発をスキップする。両方フラグが立ってる時だけ無視し、片方だけならそのまま反発する
        if (isSame && a.RepulsionDisabled && b.RepulsionDisabled)
        { ChannelLogger.LogGuardReturn("Magnet", "両者RepulsionDisabled同極のため反発スキップ"); return; }

        // 引き寄せ/反発で別係数を使う。同極反発は通常 attract より弱めに調整される
        float baseForce;
        float forceCap;
        if (isOpposite)
        {
            baseForce = m_settings.attractForce;
            forceCap = m_settings.attractMaxForcePerPair;
        }
        else
        {
            baseForce = m_settings.repelForce;
            forceCap = m_settings.repelMaxForcePerPair;
        }
        float forceMagnitude = baseForce * strength;
        if (forceCap > 0f) forceMagnitude = Mathf.Min(forceMagnitude, forceCap);

        // Entity 絡みの異極ペアが holdEngageDistance 内なら PD ホルダー経路に分岐（通常引力はスキップ）
        bool entityInvolved = a.CachedEntity != null || b.CachedEntity != null;
        if (isOpposite && entityInvolved && distance <= m_settings.holdEngageDistance)
        {
            ProcessHold(a, b);
            RegisterContact(a, b, contactsThisFrame);
            return;
        }

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
            a.ApplyForce(dirAtoB * forceMagnitude * ratioA, b.Position);
            b.ApplyForce(-dirAtoB * forceMagnitude * ratioB, a.Position);
        }
        else if (isSame)
        {
            a.ApplyForce(-dirAtoB * forceMagnitude * ratioA, b.Position);
            b.ApplyForce(dirAtoB * forceMagnitude * ratioB, a.Position);
        }

        // 接触判定 / FixedJoint スナップ（Object×Object 専用。Entity絡みは上の PD 分岐で処理済み）
        if (isOpposite && distance < m_settings.snapDistance)
        {
            m_snapResolver?.Resolve(a, b, Time.fixedDeltaTime);
            RegisterContact(a, b, contactsThisFrame);
        }
    }

    /// <summary>
    /// ペアを接触フレームに登録し、Enter判定なら NotifyContact を発火する。
    /// PD ホルダー経路と SnapResolver 経路の両方から呼ばれる共通ヘルパー。
    /// </summary>
    private void RegisterContact(Magnetizable a, Magnetizable b, HashSet<long> contactsThisFrame)
    {
        long pairKey = GetPairKey(a, b);
        contactsThisFrame.Add(pairKey);

        // Enter判定: 前フレームに接触していなかったら通知
        if (m_activeContacts.Add(pairKey))
        {
            a.NotifyContact(b);
            b.NotifyContact(a);
        }
    }

    /// <summary>
    /// PD ホルダー本体。双方向 PD 力を計算して a/b に配布する。
    /// Player が kinematic で物理反作用が発生しないため、手動で ±pdForce を両側に配布する。
    /// </summary>
    private void ProcessHold(Magnetizable argA, Magnetizable argB)
    {
        // 正規化: InstanceID の小さい側を pair.a に固定（GetPairKey / MakePairKey の convention と一致）。
        // ProcessPair のイテレーションで (a,b) と (b,a) のフレームが混在し得るため、
        // 関数引数ではなく安定した順序 (first/second) を使って worldOffset の符号反転を防ぐ。
        Magnetizable first, second;
        if (argA.GetInstanceID() < argB.GetInstanceID())
        {
            first = argA; second = argB;
        }
        else
        {
            first = argB; second = argA;
        }

        long key = GetPairKey(first, second);
        if (!m_heldPairs.TryGetValue(key, out var pair))
        {
            // 接触方向を維持しつつ、両者の contactRadius 合計で密着距離を算出
            Vector3 dir = (second.Position - first.Position).normalized;
            pair = new HeldPair
            {
                a = first,
                b = second,
                worldOffset = dir * (first.contactRadius + second.contactRadius)
            };
            m_heldPairs.Add(key, pair);

            // NavMeshAgent 停止通知（両側 Entity）
            NotifyMagneticMoverHold(first, true);
            NotifyMagneticMoverHold(second, true);
        }

        // 相対速度（damping が anchor 移動を誤検出しないように差分を取る）
        Vector3 velA = GetPairVelocity(pair.a);
        Vector3 velB = GetPairVelocity(pair.b);
        Vector3 relVel = velB - velA;

        // target position は pair.a 基準 + 初回固定の worldOffset
        Vector3 target = pair.a.Position + pair.worldOffset;
        Vector3 error = target - pair.b.Position;

        // PD 力（質量比配分なし、ApplyHoldForce 内で /mass 変換）
        // mass 源はペア種別依存: Entity=Magnetizable.mass, Object=Rigidbody.mass（設計決定参照）
        Vector3 pdForce = error * m_settings.holdStiffness - relVel * m_settings.holdDamping;

        // 双方向配布（Player が kinematic で物理反作用が発生しないため手動配布）
        pair.a.ApplyHoldForce(-pdForce);
        pair.b.ApplyHoldForce(pdForce);
    }

    /// <summary>
    /// ペアの前フレーム速度を取得する。Entity は velocity+external+hold 全成分、Object は rb.velocity。
    /// </summary>
    private Vector3 GetPairVelocity(Magnetizable m)
    {
        if (m == null) return Vector3.zero;
        var entity = m.CachedEntity;
        if (entity != null)
            return entity.velocity + entity.externalVelocity + entity.holdVelocity;

        var rb = m.GetComponent<Rigidbody>();
        if (rb != null) return rb.linearVelocity;
        return Vector3.zero;
    }

    /// <summary>
    /// MagneticMover が付いていれば SetHoldActive で AI 停止/復帰を通知する。
    /// </summary>
    private void NotifyMagneticMoverHold(Magnetizable m, bool active)
    {
        if (m == null) return;
        var mover = m.GetComponent<MagneticMover>();
        if (mover != null) mover.SetHoldActive(active);
    }

    /// <summary>
    /// 全磁化 Entity の holdVelocity を毎 FixedUpdate 冒頭でゼロリセットする。
    /// heldPairs ではなく m_cachedList 全走査で確実にゼロ戻し（幽霊運動の構造的防止）。
    /// </summary>
    private void ResetAllHoldVelocities()
    {
        for (int i = 0; i < m_cachedList.Count; i++)
        {
            var m = m_cachedList[i];
            if (m == null) continue;
            var entity = m.CachedEntity;
            if (entity != null) entity.holdVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// PD 保持ペアのクリーンアップ。距離超過 / 片方無効化 で除外し、NavMeshAgent を復帰通知する。
    /// </summary>
    private void CleanupExpiredHolds()
    {
        if (m_heldPairs.Count == 0) return;

        float maxDist = m_settings != null ? m_settings.holdMaxDistance : 3f;
        float maxDistSqr = maxDist * maxDist;

        List<long> toRemove = null;
        foreach (var kvp in m_heldPairs)
        {
            var pair = kvp.Value;
            if (pair == null || pair.a == null || pair.b == null
                || !pair.a.IsActive || !pair.b.IsActive
                || (pair.a.Pole == pair.b.Pole))
            {
                (toRemove ??= new List<long>()).Add(kvp.Key);
                continue;
            }

            float sqr = (pair.b.Position - pair.a.Position).sqrMagnitude;
            if (sqr > maxDistSqr)
            {
                (toRemove ??= new List<long>()).Add(kvp.Key);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (m_heldPairs.TryGetValue(toRemove[i], out var pair))
                {
                    NotifyMagneticMoverHold(pair.a, false);
                    NotifyMagneticMoverHold(pair.b, false);
                }
                m_heldPairs.Remove(toRemove[i]);
            }
        }
    }

    /// <summary>
    /// 指定 Magnetizable に関連する PD 保持を全解除する。Magnetizable.OnDisable から呼ぶ。
    /// </summary>
    public void ReleaseHolds(Magnetizable m)
    {
        if (m == null || m_heldPairs.Count == 0) return;

        List<long> toRemove = null;
        foreach (var kvp in m_heldPairs)
        {
            var pair = kvp.Value;
            if (pair == null || pair.a == m || pair.b == m)
                (toRemove ??= new List<long>()).Add(kvp.Key);
        }

        if (toRemove == null) return;

        for (int i = 0; i < toRemove.Count; i++)
        {
            if (m_heldPairs.TryGetValue(toRemove[i], out var pair))
            {
                // 相手側のみ NavMeshAgent 復帰通知（消える本人は対象外）
                if (pair.a != null && pair.a != m) NotifyMagneticMoverHold(pair.a, false);
                if (pair.b != null && pair.b != m) NotifyMagneticMoverHold(pair.b, false);
            }
            m_heldPairs.Remove(toRemove[i]);
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
        if (field == null || field.StoredDamage <= 0f) { ChannelLogger.LogGuardReturn("Magnet", "磁場なしまたは蓄積ダメージゼロ"); return; }

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
