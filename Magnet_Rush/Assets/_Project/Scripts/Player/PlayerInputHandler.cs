using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Input System入力をポーリングで読み取る。
/// InputActionAssetを直接参照する方式。
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    [FormerlySerializedAs("actions")]
    [SerializeField] private InputActionAsset m_actions;

    private InputAction m_move;
    private InputAction m_look;
    private InputAction m_attack;
    private InputAction m_aim;
    private InputAction m_switchPole;
    private InputAction m_reload;

    /// <summary>
    /// 移動入力のベクトル（左スティック）。
    /// </summary>
    public Vector2 MoveInput => m_move.ReadValue<Vector2>();

    /// <summary>
    /// 視点入力のベクトル（右スティック）。
    /// </summary>
    public Vector2 LookInput => m_look.ReadValue<Vector2>();

    /// <summary>
    /// エイムボタン（LT）が押されているかどうか。
    /// </summary>
    public bool AimHeld => m_aim.ReadValue<float>() > 0.5f;

    /// <summary>
    /// 攻撃ボタン（RT）が押されたフレームならtrueを返す。
    /// </summary>
    public bool ConsumeFire() => m_attack.WasPressedThisFrame();

    /// <summary>
    /// 極性切替ボタン（Y）が押されたフレームならtrueを返す。
    /// </summary>
    public bool ConsumeSwitchPole() => m_switchPole.WasPressedThisFrame();

    /// <summary>
    /// リロードボタンが押されたフレームならtrueを返す。
    /// </summary>
    public bool ConsumeReload() => m_reload.WasPressedThisFrame();

    void Awake()
    {
        if (m_actions == null)
        {
            // PlayerInputコンポーネントからアクションを取得（フォールバック）
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) m_actions = playerInput.actions;
        }

        if (m_actions != null)
        {
            m_move = m_actions["Move"];
            m_look = m_actions["Look"];
            m_attack = m_actions["Attack"];
            m_aim = m_actions["Aim"];
            m_switchPole = m_actions["SwitchPole"];
            m_reload = m_actions["Reload"];
        }
    }

    void OnEnable() => m_actions?.Enable();
    void OnDisable() => m_actions?.Disable();
}
