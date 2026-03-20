using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool FirePressed { get; private set; }
    public bool AimHeld { get; private set; }
    public bool SwitchPolePressed { get; private set; }
    public bool ReloadPressed { get; private set; }

    // InputSystem Messages (PlayerInput component calls these via SendMessages or Invoke Unity Events)

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
        // Attackを射撃(Fire)として使用
        FirePressed = value.isPressed;
    }

    public void OnAim(InputValue value)
    {
        AimHeld = value.isPressed;
    }

    public void OnSwitchPole(InputValue value)
    {
        SwitchPolePressed = value.isPressed;
    }

    public void OnReload(InputValue value)
    {
        ReloadPressed = value.isPressed;
    }

    void LateUpdate()
    {
        // 1フレームだけtrueのフラグをリセット
        FirePressed = false;
        SwitchPolePressed = false;
        ReloadPressed = false;
    }
}
