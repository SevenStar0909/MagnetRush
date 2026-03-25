using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System入力をバッファリングする。
/// ボタン入力はConsumeパターンで1回だけ読み取り可能。
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool AimHeld { get; private set; }

    // ボタン入力バッファ（Consumeで読み取り後にリセット）
    private bool fireBuffer;
    private bool switchPoleBuffer;
    private bool reloadBuffer;

    /// <summary>射撃入力を消費する。1回呼ぶとfalseに戻る。</summary>
    public bool ConsumeFire()
    {
        if (!fireBuffer) return false;
        fireBuffer = false;
        return true;
    }

    /// <summary>磁極切替入力を消費する。</summary>
    public bool ConsumeSwitchPole()
    {
        if (!switchPoleBuffer) return false;
        switchPoleBuffer = false;
        return true;
    }

    /// <summary>リロード入力を消費する。</summary>
    public bool ConsumeReload()
    {
        if (!reloadBuffer) return false;
        reloadBuffer = false;
        return true;
    }

    // InputSystemメッセージ

    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        LookInput = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed) fireBuffer = true;
    }

    public void OnAim(InputValue value)
    {
        AimHeld = value.isPressed;
    }

    public void OnSwitchPole(InputValue value)
    {
        if (value.isPressed) switchPoleBuffer = true;
    }

    public void OnReload(InputValue value)
    {
        if (value.isPressed) reloadBuffer = true;
    }
}
