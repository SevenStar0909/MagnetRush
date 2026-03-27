using UnityEngine;
using UnityEngine.Serialization;
using System;

/// <summary>
/// 磁力の影響を受けることを示すコンポーネント。
/// MagnetManagerに自動登録され、力の適用はオブジェクト種別に応じて自動判別する。
/// 磁化時はレイヤーを "Magnetized" に変更し、Edge Detection アウトラインの対象になる。
/// </summary>
public class Magnetizable : MonoBehaviour
{
    [FormerlySerializedAs("pole")]
    [SerializeField] private MagneticPole m_pole = MagneticPole.None;
    [FormerlySerializedAs("isActive")]
    [SerializeField] private bool m_isActive;
    [FormerlySerializedAs("initialMass")]
    [SerializeField] private float m_initialMass = 1f;

    public MagneticPole Pole => m_pole;
    public bool IsActive => m_isActive;

    /// <summary>質量。壁に固定された弾はInfinity。力の分配に使用。</summary>
    public float mass { get; set; } = 1f;

    public event Action<MagneticPole> OnPoleChanged;

    /// <summary>磁化オブジェクト同士が接触距離に入った時に発火。</summary>
    public event Action<Magnetizable> OnMagnetContact;

    // キャッシュ
    private Rigidbody m_rb;
    private IMagnetTarget m_magnetTarget;
    private IMagneticResponse m_magneticResponse;
    private Entity m_cachedEntity;
    private float m_totalForceThisFrame;
    private int m_originalLayer;

    /// <summary>同一GOのEntityキャッシュ。MagnetManagerのフィールド割り当てで使用。</summary>
    public Entity CachedEntity => m_cachedEntity;

    void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_magnetTarget = GetComponent<IMagnetTarget>();
        m_magneticResponse = GetComponent<IMagneticResponse>();
        m_cachedEntity = GetComponent<Entity>();
        mass = m_initialMass > 0f ? m_initialMass : (m_rb != null ? m_rb.mass : 1f);
        m_originalLayer = gameObject.layer;
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
            MagnetManager.Instance.Unregister(this);
        }
    }

    public void SetPole(MagneticPole newPole)
    {
        m_pole = newPole;
        m_isActive = newPole != MagneticPole.None;

        // Magnetized レイヤーに切り替え → Edge Detection の対象になる
        if (m_isActive)
            gameObject.layer = LayerMask.NameToLayer("Magnetized");

        OnPoleChanged?.Invoke(m_pole);
    }

    public void Deactivate()
    {
        m_pole = MagneticPole.None;
        m_isActive = false;

        // 元のレイヤーに戻す → アウトライン消える
        gameObject.layer = m_originalLayer;

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
            m_rb.AddForce(force, ForceMode.Acceleration);
            return;
        }
    }

    /// <summary>旧シグネチャの後方互換オーバーロード。</summary>
    public void ApplyForce(Vector3 force) => ApplyForce(force, transform.position);

    void LateUpdate()
    {
        m_totalForceThisFrame = 0f;
    }
}
