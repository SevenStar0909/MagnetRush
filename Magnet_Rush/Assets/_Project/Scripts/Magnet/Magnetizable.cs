using UnityEngine;
using System;

/// <summary>
/// 磁力の影響を受けることを示すコンポーネント。
/// MagnetManagerに自動登録され、力の適用はオブジェクト種別に応じて自動判別する。
/// </summary>
public class Magnetizable : MonoBehaviour
{
    [SerializeField] private MagneticPole pole = MagneticPole.None;
    [SerializeField] private bool isActive;
    [SerializeField] private float initialMass = 1f;

    public MagneticPole Pole => pole;
    public bool IsActive => isActive;

    /// <summary>質量。壁に固定された弾はInfinity。力の分配に使用。</summary>
    public float mass { get; set; } = 1f;

    public event Action<MagneticPole> OnPoleChanged;

    /// <summary>磁化オブジェクト同士が接触距離に入った時に発火。</summary>
    public event Action<Magnetizable> OnMagnetContact;

    // キャッシュ（力の適用先判別用）
    private Rigidbody rb;
    private IMagnetTarget magnetTarget;
    private IMagneticResponse magneticResponse;
    private float totalForceThisFrame;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        magnetTarget = GetComponent<IMagnetTarget>();
        magneticResponse = GetComponent<IMagneticResponse>();
        mass = initialMass > 0f ? initialMass : (rb != null ? rb.mass : 1f);
    }

    void OnEnable()
    {
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (MagnetManager.Instance != null)
            MagnetManager.Instance.Unregister(this);
    }

    public void SetPole(MagneticPole newPole)
    {
        pole = newPole;
        isActive = newPole != MagneticPole.None;
        OnPoleChanged?.Invoke(pole);
    }

    public void Deactivate()
    {
        pole = MagneticPole.None;
        isActive = false;
        OnPoleChanged?.Invoke(pole);
    }

    /// <summary>
    /// このフレームに受けた磁力の合計から影響度(0-1)を返す。
    /// maxForceを超えると1.0にクランプされる。
    /// </summary>
    public float GetInfluence(float maxForce)
    {
        if (maxForce <= 0f) return 0f;
        return Mathf.Clamp01(totalForceThisFrame / maxForce);
    }

    /// <summary>磁力接触コールバックを発火する（MagnetManagerから呼ばれる）。</summary>
    public void NotifyContact(Magnetizable other)
    {
        if (magneticResponse != null && magneticResponse.IsResponseActive)
            magneticResponse.OnMagnetContact(this, other);

        OnMagnetContact?.Invoke(other);
    }

    /// <summary>
    /// 力を適用する。IMagneticResponse → IMagnetTarget → Rigidbody の優先順で判別。
    /// 同時にtotalForceThisFrameに蓄積する。
    /// </summary>
    public void ApplyForce(Vector3 force, Vector3 sourcePosition)
    {
        totalForceThisFrame += force.magnitude;

        if (magneticResponse != null && magneticResponse.IsResponseActive)
        {
            magneticResponse.OnMagnetForce(force, sourcePosition);
            return;
        }

        if (magnetTarget != null)
        {
            magnetTarget.ApplyMagnetForce(force);
            return;
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(force, ForceMode.Force);
            return;
        }
    }

    /// <summary>旧シグネチャの後方互換オーバーロード。</summary>
    public void ApplyForce(Vector3 force) => ApplyForce(force, transform.position);

    void LateUpdate()
    {
        totalForceThisFrame = 0f;
    }
}
