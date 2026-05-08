using UnityEngine;
using System;

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

    public MagneticPole Pole => m_pole;
    public bool IsActive => m_isActive;

    /// <summary>質量。壁に固定された弾はInfinity。力の分配に使用。</summary>
    public float mass { get; set; } = 1f;

    /// <summary>PD保持時の接触半径。Awake でコライダーから自動算出。密着距離 = 自分 + 相手。</summary>
    public float contactRadius { get; private set; }

    public event Action<MagneticPole> OnPoleChanged;

    /// <summary>磁化オブジェクト同士が接触距離に入った時に発火。</summary>
    public event Action<Magnetizable> OnMagnetContact;

    // キャッシュ
    private Rigidbody m_rb;
    private IMagnetTarget m_magnetTarget;
    private IMagneticResponse m_magneticResponse;
    private Entity m_cachedEntity;
    private float m_totalForceThisFrame;
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

        // 全Rendererにアウトラインを適用
        if (m_isActive && m_renderers.Length > 0)
        {
            m_mpb.SetFloat(s_poleIDProperty, newPole == MagneticPole.S ? 1f : 0f);
            for (int i = 0; i < m_renderers.Length; i++)
            {
                if (m_renderers[i] == null) continue;
                m_renderers[i].renderingLayerMask |= RenderingLayers.Magnetized;
                m_renderers[i].SetPropertyBlock(m_mpb);
            }
        }
        else if (m_isActive)
        {
            Debug.LogWarning($"[Magnetizable] {gameObject.name} にRendererがありません。アウトライン表示できません。");
        }

        OnPoleChanged?.Invoke(m_pole);
    }

    public void Deactivate()
    {
        m_pole = MagneticPole.None;
        m_isActive = false;

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
    /// 力を適用する。IMagneticResponse → IMagnetTarget → Rigidbody の優先順で判別。
    /// 同時にm_totalForceThisFrameに蓄積する。
    /// </summary>
    public void ApplyForce(Vector3 force, Vector3 sourcePosition)
    {
        m_totalForceThisFrame += force.magnitude;

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
            Vector3 contactPoint = m_collider != null ? m_collider.ClosestPoint(sourcePosition) : transform.position;
            m_rb.AddForceAtPosition(force * m_rb.mass, contactPoint, ForceMode.Force);
            return;
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
    }
}
