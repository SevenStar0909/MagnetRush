using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 磁力の影響を受けることを示すコンポーネント。
/// MagnetManagerに自動登録され、力の適用はオブジェクト種別に応じて自動判別する。
/// 磁化時はRendering Layer Maskを設定し、Edge Detection アウトラインの対象になる。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Magnetizable : MonoBehaviour, IMagnetPoleProvider
{
    [SerializeField] private MagneticPole m_pole = MagneticPole.None;
    [SerializeField] private bool m_isActive;
    [SerializeField] private float m_initialMass = 1f;

    [Tooltip("磁力を受けても動かさない（ボス等）。磁化・極性・接触判定・演出は通常どおりで、移動への力だけ無効化する。")]
    [SerializeField] private bool m_immovable;

    [Tooltip("磁力中心をGOからローカル空間でずらす。Collider.centerと同じ感覚。回転は装着GOに追従。\n距離・力計算・PD保持などMagnetizable同士のやり取りはこの位置で行う。")]
    [SerializeField] private Vector3 m_centerOffset = Vector3.zero;

    [Tooltip("OutlineRendererFeature の Edge Detection アウトラインを使うか。\nボスのように bone 単位の Hull アウトラインを別経路で描画する場合は false にする。")]
    [SerializeField] private bool m_useEdgeDetectionOutline = true;

    public MagneticPole Pole => m_pole;
    public bool IsActive => m_isActive;

    /// <summary>true なら磁力を受けても動かない（ボス等）。磁化・接触判定・演出は通常どおり。</summary>
    public bool Immovable { get => m_immovable; set => m_immovable = value; }

    /// <summary>
    /// true のとき、同じく RepulsionDisabled が true な相手との同極反発を MagnetManager がスキップする。
    /// ボス手キャストでドーム内オブジェクトに極を一斉付与した際、それら同士で反発し合わないようにするためのフラグ。
    /// 異極吸引やドーム外オブジェクトとの相互作用には影響しない。
    /// </summary>
    public bool RepulsionDisabled { get; set; }

    /// <summary>磁力中心のワールド座標。MagnetField がアタッチされている時はそのフィールドGO位置（着弾点）、無い時は装着GOからm_centerOffset分ローカル空間でずらした位置。</summary>
    public Vector3 Position
    {
        get
        {
            if (m_cachedField != null)
                return m_cachedField.transform.position;
            return transform.position + transform.rotation * m_centerOffset;
        }
    }

    /// <summary>磁力中心のローカルオフセット。</summary>
    public Vector3 CenterOffset => m_centerOffset;

    /// <summary>質量。壁に固定された弾はInfinity。力の分配に使用。</summary>
    public float mass { get; set; } = 1f;

    /// <summary>PD保持時の接触半径。Awake でコライダーから自動算出。密着距離 = 自分 + 相手。</summary>
    public float contactRadius { get; private set; }

    public event Action<MagneticPole> OnPoleChanged;

    /// <summary>磁化オブジェクト同士が接触距離に入った時に発火。</summary>
    public event Action<Magnetizable> OnMagnetContact;

    /// <summary>同極反発の力を受けた時に発火（反発中は毎FixedUpdate）。カメラ演出等が購読する。</summary>
    public event Action<Vector3> OnRepulsionForce;

    /// <summary>MagnetManagerが同極反発を適用した時に呼ぶ。購読者へ力を通知する。</summary>
    public void NotifyRepulsionForce(Vector3 force) => OnRepulsionForce?.Invoke(force);

    // キャッシュ
    private Rigidbody m_rb;
    private IMagnetTarget m_magnetTarget;
    private IMagneticResponse m_magneticResponse;
    private Entity m_cachedEntity;
    private float m_totalForceThisFrame;
    // 今フレームに受けた磁力を一旦溜める。ResolveMagneticForces で最強1件＋残りを重なり係数で合成して適用する
    private readonly List<PendingForce> m_pendingForces = new();
    private Renderer[] m_renderers;
    private MaterialPropertyBlock m_mpb;
    private Collider m_collider;
    private MagnetField m_cachedField;
    private RigidbodyConstraints m_savedConstraints;
    private bool m_isSettling;
    private float m_settlingTimer;
    private const float k_SettlingDuration = 0.5f;
    private const float k_AngularVelocityThreshold = 0.5f;
    private static readonly int s_poleIDProperty = Shader.PropertyToID("_PoleID");

    /// <summary>同一GOのEntityキャッシュ。MagnetManagerのフィールド割り当てで使用。</summary>
    public Entity CachedEntity => m_cachedEntity;

    /// <summary>このフレームに受けた磁力の合計。0なら今フレームは引っ張られていない（引っ張り距離の加算判定に使う）。LateUpdateでリセット。</summary>
    public float ForceThisFrame => m_totalForceThisFrame;

    /// <summary>フィールドのinnerRadius。フィールドがなければ0を返す。</summary>
    public float FieldInnerRadius => m_cachedField != null ? m_cachedField.InnerRadius : 0f;

    /// <summary>フィールドのouterRadius。フィールドがなければ0を返す。</summary>
    public float FieldOuterRadius => m_cachedField != null ? m_cachedField.OuterRadius : 0f;

    /// <summary>MagnetFieldが自身を登録する。</summary>
    public void SetField(MagnetField field) => m_cachedField = field;

    void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_magnetTarget = GetComponent<IMagnetTarget>();
        m_magneticResponse = GetComponent<IMagneticResponse>();
        m_cachedEntity = GetComponent<Entity>();
        m_renderers = GetComponentsInChildren<Renderer>(true);
        m_collider = GetComponent<Collider>();
        m_mpb = new MaterialPropertyBlock();
        mass = m_initialMass > 0f ? m_initialMass : (m_rb != null ? m_rb.mass : 1f);
        if (m_rb != null) m_savedConstraints = m_rb.constraints;

        // PD保持の接触半径をRendererから自動算出（見た目基準、水平方向の最大半径）
        if (m_renderers != null && m_renderers.Length > 0)
        {
            Bounds combined = m_renderers[0].bounds;
            for (int i = 1; i < m_renderers.Length; i++)
                if (m_renderers[i] != null) combined.Encapsulate(m_renderers[i].bounds);
            contactRadius = Mathf.Max(combined.extents.x, combined.extents.z);
        }
        else
        {
            contactRadius = 0.4f;
        }
    }

    void OnEnable()
    {
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (MagnetManager.Instance != null)
        {
            MagnetManager.Instance.SnapResolver?.ReleaseAllFor(this);
            MagnetManager.Instance.ReleaseHolds(this);
            MagnetManager.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// PDホルダーから保持力を受け取る。Entity 側は Magnetizable.mass、Object 側は Rigidbody.mass で /mass 変換。
    /// IMagneticResponse / IMagnetTarget の分岐は通さない（PDは独立経路）。
    /// </summary>
    public void ApplyHoldForce(Vector3 force)
    {
        if (m_immovable) { ChannelLogger.LogGuardReturn("Magnet", "Immovable: 保持力を無視（動かさない）"); return; }
        m_totalForceThisFrame += force.magnitude;

        if (m_cachedEntity != null)
        {
            // Entity: kinematic なので Magnetizable.mass (磁気重さ) を使用
            float entityMass = mass > 0f ? mass : 1f;
            m_cachedEntity.holdVelocity += force / entityMass * Time.fixedDeltaTime;
            return;
        }

        if (m_rb != null && !m_rb.isKinematic)
        {
            // Object: Rigidbody.mass (物理慣性) を使用。解除後の KE が物理エンジンと整合
            float rbMass = m_rb.mass > 0f ? m_rb.mass : 1f;
            m_rb.linearVelocity += force / rbMass * Time.fixedDeltaTime;
        }
    }

    public void SetPole(MagneticPole newPole)
    {
        m_pole = newPole;
        m_isActive = newPole != MagneticPole.None;

        // Awakeで取得できなかった場合のフォールバック
        if (m_renderers == null || m_renderers.Length == 0)
            m_renderers = GetComponentsInChildren<Renderer>(true);

        // 全Rendererに Edge Detection アウトライン (post-process) を適用。
        // m_useEdgeDetectionOutline=false の場合はスキップし、別経路 (Inverted Hull 等) で描画する想定。
        if (m_isActive && m_useEdgeDetectionOutline && m_renderers.Length > 0)
        {
            m_mpb.SetFloat(s_poleIDProperty, newPole == MagneticPole.S ? 1f : 0f);
            for (int i = 0; i < m_renderers.Length; i++)
            {
                if (m_renderers[i] == null) continue;
                m_renderers[i].renderingLayerMask |= RenderingLayers.Magnetized;
                m_renderers[i].SetPropertyBlock(m_mpb);
            }
        }
        else if (m_isActive && m_useEdgeDetectionOutline)
        {
            Debug.LogWarning($"[Magnetizable] {gameObject.name} にRendererがありません。アウトライン表示できません。");
        }

        OnPoleChanged?.Invoke(m_pole);
    }

    public void Deactivate()
    {
        m_pole = MagneticPole.None;
        m_isActive = false;
        m_cachedField = null; // フィールド参照を明示クリア（Position が確実にフォールバック経路に戻る）

        // 磁化解除時にbrokenペアをクリア（次回磁化で再スナップ可能にする）
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.SnapResolver?.ClearBrokenFor(this);

        // 全Rendererからアウトラインビットを解除
        if (m_renderers != null)
        {
            for (int i = 0; i < m_renderers.Length; i++)
            {
                if (m_renderers[i] != null)
                    m_renderers[i].renderingLayerMask &= ~RenderingLayers.Magnetized;
            }
        }

        // 回転制約はすぐ復元せず、セトリングフェーズ開始
        if (m_rb != null && m_savedConstraints != RigidbodyConstraints.None)
        {
            m_isSettling = true;
            m_settlingTimer = k_SettlingDuration;
        }

        OnPoleChanged?.Invoke(m_pole);
    }

    /// <summary>
    /// このフレームに受けた磁力の合計から影響度(0-1)を返す。
    /// maxForceを超えると1.0にクランプされる。
    /// </summary>
    public float GetInfluence(float maxForce)
    {
        if (maxForce <= 0f) return 0f;
        return Mathf.Clamp01(m_totalForceThisFrame / maxForce);
    }

    /// <summary>磁力接触コールバックを発火する（MagnetManagerから呼ばれる）。</summary>
    public void NotifyContact(Magnetizable other)
    {
        if (m_magneticResponse != null && m_magneticResponse.IsResponseActive)
            m_magneticResponse.OnMagnetContact(this, other);

        OnMagnetContact?.Invoke(other);
    }

    /// <summary>
    /// 磁力を1件バッファに溜める。実際の物理適用は ResolveMagneticForces でまとめて行う。
    /// 鈍化用の合計(m_totalForceThisFrame)はここで全件加算する（重なり係数の影響を受けない）。
    /// </summary>
    public void ApplyForce(Vector3 force, Vector3 sourcePosition)
    {
        if (m_immovable) { ChannelLogger.LogGuardReturn("Magnet", "Immovable: 磁力を無視（動かさない）"); return; }
        m_totalForceThisFrame += force.magnitude;
        m_pendingForces.Add(new PendingForce(force, sourcePosition));
    }

    /// <summary>
    /// 溜めた磁力を合成して適用する。一番強い1件はフル、残りは overlapWeight 倍で適用する。
    /// 磁場が重なって複数の磁力源から引かれた時、overlapWeight=0 なら最強1件だけ効き重なりの加算が消える。
    /// 1 なら全件フル加算（従来挙動）。MagnetManager がペア計算後に毎フレーム呼ぶ。
    /// </summary>
    public void ResolveMagneticForces(float overlapWeight)
    {
        int count = m_pendingForces.Count;
        if (count == 0) { ChannelLogger.LogGuardReturn("Magnet", "適用待ちの磁力なし"); return; }

        // 力の大きさが最大の1件を最強として全適用、それ以外を係数で減衰（引き寄せ/反発は区別しない）
        int strongestIndex = 0;
        float strongestSqr = m_pendingForces[0].force.sqrMagnitude;
        for (int i = 1; i < count; i++)
        {
            float sqr = m_pendingForces[i].force.sqrMagnitude;
            if (sqr > strongestSqr)
            {
                strongestSqr = sqr;
                strongestIndex = i;
            }
        }

        for (int i = 0; i < count; i++)
        {
            float scale = i == strongestIndex ? 1f : overlapWeight;
            if (scale <= 0f) continue;
            ApplyForceImmediate(m_pendingForces[i].force * scale, m_pendingForces[i].sourcePosition);
        }

        m_pendingForces.Clear();
    }

    /// <summary>
    /// 力を即座に適用する。IMagneticResponse → IMagnetTarget → Rigidbody の優先順で判別。
    /// </summary>
    private void ApplyForceImmediate(Vector3 force, Vector3 sourcePosition)
    {
        if (m_magneticResponse != null && m_magneticResponse.IsResponseActive)
        {
            m_magneticResponse.OnMagnetForce(force, sourcePosition);
            return;
        }

        if (m_magnetTarget != null)
        {
            m_magnetTarget.ApplyMagnetForce(force);
            return;
        }

        if (m_rb != null && !m_rb.isKinematic)
        {
            // 磁力中は回転制約を解除（角や辺でぶつかる自然な挙動のため）
            if (m_isActive && m_rb.constraints != RigidbodyConstraints.None)
            {
                m_savedConstraints = m_rb.constraints;
                m_rb.constraints = RigidbodyConstraints.None;
            }

            // ソース方向の表面最近点に力を適用（トルクが発生し回転する）
            Vector3 contactPoint = m_collider != null ? m_collider.ClosestPoint(sourcePosition) : Position;
            m_rb.AddForceAtPosition(force * m_rb.mass, contactPoint, ForceMode.Force);
            return;
        }
    }

    private readonly struct PendingForce
    {
        public readonly Vector3 force;
        public readonly Vector3 sourcePosition;
        public PendingForce(Vector3 force, Vector3 sourcePosition)
        {
            this.force = force;
            this.sourcePosition = sourcePosition;
        }
    }

    void Update()
    {
        if (!m_isSettling || m_rb == null) { ChannelLogger.LogGuardReturn("Magnet", "セトリング中でないまたはRigidbodyなし"); return; }
        // kinematic中はセトリング処理をスキップ（EntityControllerの押し処理中など）
        if (m_rb.isKinematic) { ChannelLogger.LogGuardReturn("Magnet", "kinematic中はセトリングスキップ"); return; }

        m_settlingTimer -= Time.deltaTime;

        Quaternion currentRot = m_rb.rotation;
        Quaternion uprightRot = Quaternion.Euler(0f, currentRot.eulerAngles.y, 0f);
        Quaternion deltaRot = uprightRot * Quaternion.Inverse(currentRot);
        deltaRot.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        if (Mathf.Abs(angle) > 1f)
        {
            m_rb.AddTorque(axis * (angle * Mathf.Deg2Rad * 10f), ForceMode.Acceleration);
            m_rb.angularVelocity *= 0.9f;
        }

        bool nearUpright = Mathf.Abs(angle) < 2f;
        bool angularSlow = m_rb.angularVelocity.magnitude < k_AngularVelocityThreshold;
        bool timedOut = m_settlingTimer <= 0f;

        if ((nearUpright && angularSlow) || timedOut)
        {
            m_rb.rotation = uprightRot;
            m_rb.angularVelocity = Vector3.zero;
            m_rb.constraints = m_savedConstraints;
            m_savedConstraints = RigidbodyConstraints.None;
            m_isSettling = false;
        }
    }

    void LateUpdate()
    {
        m_totalForceThisFrame = 0f;
        // Resolve が呼ばれ損ねた場合の次フレーム持ち越しを防ぐ保険（通常は Resolve 内でクリア済み）
        m_pendingForces.Clear();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (m_centerOffset == Vector3.zero) return;
        Vector3 world = transform.position + transform.rotation * m_centerOffset;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(world, 0.1f);
        Gizmos.DrawLine(transform.position, world);
    }
#endif
}
