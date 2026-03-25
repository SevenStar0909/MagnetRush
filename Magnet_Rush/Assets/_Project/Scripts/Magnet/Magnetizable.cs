using UnityEngine;
using MagnetRush.Common;
using System;

/// <summary>
/// 磁力の影響を受けることを示すコンポーネント。
/// MagnetManagerに自動登録され、力の適用はオブジェクト種別に応じて自動判別する。
/// </summary>
public class Magnetizable : MonoBehaviour
{
    [SerializeField] private MagneticPole pole = MagneticPole.None;
    [SerializeField] private bool isActive;

    public MagneticPole Pole => pole;
    public bool IsActive => isActive;

    public event Action<MagneticPole> OnPoleChanged;

    // キャッシュ（力の適用先判別用）
    private Rigidbody rb;
    private MagnetRush.Entity.IMagnetTarget magnetTarget;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        magnetTarget = GetComponent<MagnetRush.Entity.IMagnetTarget>();
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
    /// 力を適用する。IMagnetTarget → Rigidbody → 無視の優先順で判別（OCP準拠）。
    /// 新しい移動タイプはIMagnetTargetを実装すれば自動対応。
    /// </summary>
    public void ApplyForce(Vector3 force)
    {
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

        // 固定オブジェクト（壁等）は力を受けない
    }
}
