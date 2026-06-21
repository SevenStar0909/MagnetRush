using UnityEngine;

/// <summary>
/// FixedUpdate で動かされる対象 Transform の位置・回転を、サブフレーム補間して LateUpdate で自身の Transform に反映する。
/// 用途: KCC 系の transform.position 直書き Entity を、Rigidbody.Interpolate と同等の見た目滑らかさで描画する。
/// 設置: 補間させたいビジュアルメッシュ（FBX 子等）に付ける。target は通常親 Transform（KCC ルート）。
/// 設計: Glenn Fiedler "Fix Your Timestep!" / Unity TransformInterpolator パターン由来。
/// モード切替: スロー時のみ補間 ON、通常時は localPosition/localRotation を初期値に戻して親追従に任せる。
/// 通常時に補間を介在させると 60Hz/50Hz の不一致でバッチ起因のジッタが見える事があるため。
/// 制約: target は通常時 Update で動かす／スロー時 FixedUpdate で動かす想定。Player.cs と歩調を合わせる。
/// </summary>
[DefaultExecutionOrder(1000)]
public class TransformInterpolator : MonoBehaviour
{
    /// <summary>
    /// この値未満の Time.timeScale ならスロー判定し補間を有効化する。
    /// 数値は Player.cs の k_SlowMotionThreshold と歩調を合わせる必要がある（現在 0.99）。
    /// asmdef 循環を避けるため Player への直接参照は禁止。閾値はミラーで保持する。
    /// </summary>
    private const float k_SlowMotionThreshold = 0.99f;

    [Tooltip("補間元の Transform。未設定なら親 Transform を使用する。")]
    [SerializeField] private Transform m_target;

    private Vector3 m_prevPos;
    private Vector3 m_currPos;
    private Quaternion m_prevRot;
    private Quaternion m_currRot;

    private Vector3 m_originalLocalPos;
    private Quaternion m_originalLocalRot;
    private bool m_isSuspended;

    /// <summary>
    /// Timeline などが描画フレームごとに対象を直接駆動する間、FixedUpdate 用の補間を停止する。
    /// 停止時は補間で付いたローカル差分を即座に戻し、再開時は現在姿勢から補間履歴を作り直す。
    /// </summary>
    public void SetSuspended(bool suspended)
    {
        if (m_isSuspended == suspended) return;

        m_isSuspended = suspended;
        transform.localPosition = m_originalLocalPos;
        transform.localRotation = m_originalLocalRot;

        if (!suspended && m_target != null)
        {
            m_prevPos = m_currPos = m_target.position;
            m_prevRot = m_currRot = m_target.rotation;
        }
    }

    void Awake()
    {
        if (m_target == null) m_target = transform.parent;
        if (m_target == null)
        {
            ChannelLogger.LogGuardReturn("Player", "TransformInterpolator: target 未設定かつ親もなし");
            enabled = false;
            return;
        }

        m_originalLocalPos = transform.localPosition;
        m_originalLocalRot = transform.localRotation;

        m_prevPos = m_currPos = m_target.position;
        m_prevRot = m_currRot = m_target.rotation;
    }

    void OnEnable()
    {
        if (m_target == null) { ChannelLogger.LogGuardReturn("Player", "TransformInterpolator.OnEnable: target なし"); return; }
        m_prevPos = m_currPos = m_target.position;
        m_prevRot = m_currRot = m_target.rotation;
    }

    void FixedUpdate()
    {
        if (m_target == null) { ChannelLogger.LogGuardReturn("Player", "TransformInterpolator.FixedUpdate: target なし"); return; }
        if (m_isSuspended) return;
        m_prevPos = m_currPos;
        m_prevRot = m_currRot;
        m_currPos = m_target.position;
        m_currRot = m_target.rotation;
    }

    void LateUpdate()
    {
        if (m_target == null) { ChannelLogger.LogGuardReturn("Player", "TransformInterpolator.LateUpdate: target なし"); return; }
        if (m_isSuspended) return;

        if (Time.timeScale >= k_SlowMotionThreshold)
        {
            // 通常時は補間を介在させず親追従に戻す。直近の補間 LateUpdate で localPosition/localRotation
            // が逸れている可能性があるため毎フレーム初期値に書き戻す（軽量、Vector3 + Quaternion 代入のみ）。
            transform.localPosition = m_originalLocalPos;
            transform.localRotation = m_originalLocalRot;
            return;
        }

        float fixedDt = Time.fixedDeltaTime;
        float alpha = (fixedDt > 0f)
            ? Mathf.Clamp01((Time.time - Time.fixedTime) / fixedDt)
            : 1f;

        transform.position = Vector3.Lerp(m_prevPos, m_currPos, alpha);
        transform.rotation = Quaternion.Slerp(m_prevRot, m_currRot, alpha);
    }
}
