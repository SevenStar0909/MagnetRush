using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Input System入力をポーリングで読み取る。
/// 各 Consume* は入力先行バッファ(k_inputBufferTime)を持ち、ボタン押下から
/// 100ms以内に呼べば消費可能。Was*ReleasedThisFrame でリリース検出も提供する。
/// DefaultExecutionOrder(-100) で他Updateより先に押下時刻を記録する。
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerInputHandler : MonoBehaviour
{
    [FormerlySerializedAs("actions")]
    [SerializeField] private InputActionAsset m_actions;

    private InputAction m_move;
    private InputAction m_attack;
    private InputAction m_aim;
    private InputAction m_switchPole;
    private InputAction m_reload;
    private InputAction m_selfFire;

    // 入力先行受付の時間窓（秒）。100ms はゲーム業界の経験的な標準値
    private const float k_inputBufferTime = 0.1f;

    private float? m_firePressTime;
    private float? m_reloadPressTime;
    private float? m_switchPolePressTime;
    private float? m_selfFirePressTime;

    /// <summary>移動入力のベクトル（左スティック）</summary>
    public Vector2 MoveInput => m_move.ReadValue<Vector2>();

    /// <summary>エイムボタン（LT）が押されているかどうか</summary>
    public bool AimHeld => m_aim.ReadValue<float>() > 0.5f;

    /// <summary>エイムボタンが今フレームで離されたか</summary>
    public bool AimReleasedThisFrame => m_aim != null && m_aim.WasReleasedThisFrame();

    /// <summary>攻撃ボタンが今フレームで離されたか（チャージ攻撃等用）</summary>
    public bool FireReleasedThisFrame => m_attack != null && m_attack.WasReleasedThisFrame();

    /// <summary>リロードボタンが今フレームで離されたか</summary>
    public bool ReloadReleasedThisFrame => m_reload != null && m_reload.WasReleasedThisFrame();

    /// <summary>極性切替ボタンが今フレームで離されたか</summary>
    public bool SwitchPoleReleasedThisFrame => m_switchPole != null && m_switchPole.WasReleasedThisFrame();

    /// <summary>セルフファイアボタンが今フレームで離されたか</summary>
    public bool SelfFireReleasedThisFrame => m_selfFire != null && m_selfFire.WasReleasedThisFrame();

    /// <summary>攻撃ボタン（RT）押下を消費。バッファ内なら true を返してクリア</summary>
    public bool ConsumeFire() => ConsumeBuffered(ref m_firePressTime);

    /// <summary>極性切替ボタン（Y）押下を消費</summary>
    public bool ConsumeSwitchPole() => ConsumeBuffered(ref m_switchPolePressTime);

    /// <summary>リロードボタン押下を消費</summary>
    public bool ConsumeReload() => ConsumeBuffered(ref m_reloadPressTime);

    /// <summary>セルフファイアボタン（A / F）押下を消費</summary>
    public bool ConsumeSelfFire() => ConsumeBuffered(ref m_selfFirePressTime);

    /// <summary>攻撃ボタン押下がバッファ内か（消費しない）</summary>
    public bool IsFirePressed => HasBuffered(m_firePressTime);

    /// <summary>リロードボタン押下がバッファ内か（消費しない）</summary>
    public bool IsReloadPressed => HasBuffered(m_reloadPressTime);

    /// <summary>極性切替ボタン押下がバッファ内か（消費しない）</summary>
    public bool IsSwitchPolePressed => HasBuffered(m_switchPolePressTime);

    /// <summary>セルフファイアボタン押下がバッファ内か（消費しない）</summary>
    public bool IsSelfFirePressed => HasBuffered(m_selfFirePressTime);

    /// <summary>全 Consumable の入力バッファをクリア。死亡・シーン遷移時等に呼ぶ</summary>
    public void ClearBuffers()
    {
        m_firePressTime = null;
        m_reloadPressTime = null;
        m_switchPolePressTime = null;
        m_selfFirePressTime = null;
    }

    private static bool HasBuffered(float? pressTime) =>
        pressTime.HasValue && Time.time - pressTime.Value < k_inputBufferTime;

    private static bool ConsumeBuffered(ref float? pressTime)
    {
        if (HasBuffered(pressTime))
        {
            pressTime = null;
            return true;
        }
        return false;
    }

    void Awake()
    {
        if (m_actions == null)
        {
            ChannelLogger.LogGuardReturn("Player", "PlayerInputHandler: m_actions 未設定");
            return;
        }

        m_move = m_actions["Move"];
        m_attack = m_actions["Attack"];
        m_aim = m_actions["Aim"];
        m_switchPole = m_actions["SwitchPole"];
        m_reload = m_actions["Reload"];
        m_selfFire = m_actions["SelfFire"];
    }

    void Update()
    {
        // 各consumableボタンの押下を時刻として記録（DefaultExecutionOrderで他より先に走る）
        if (m_attack != null && m_attack.WasPressedThisFrame()) m_firePressTime = Time.time;
        if (m_reload != null && m_reload.WasPressedThisFrame()) m_reloadPressTime = Time.time;
        if (m_switchPole != null && m_switchPole.WasPressedThisFrame()) m_switchPolePressTime = Time.time;
        if (m_selfFire != null && m_selfFire.WasPressedThisFrame()) m_selfFirePressTime = Time.time;
    }

    void OnEnable() => m_actions?.Enable();
    void OnDisable() => m_actions?.Disable();
}
