using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerSettings settings;
    [SerializeField] private Transform player;

    private float yaw;
    private float pitch;
    private PlayerInputHandler input;

    void Start()
    {
        if (player != null)
            input = player.GetComponent<PlayerInputHandler>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (input == null || settings == null) return;

        Vector2 look = input.LookInput;
        yaw += look.x * settings.cameraSensitivityX * Time.deltaTime;
        pitch -= look.y * settings.cameraSensitivityY * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -40f, 70f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
