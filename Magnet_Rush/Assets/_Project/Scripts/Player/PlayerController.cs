using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerSettings settings;

    private CharacterController cc;
    private PlayerInputHandler input;
    private Vector3 velocity;
    private Transform cam;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        input = GetComponent<PlayerInputHandler>();
    }

    void Start()
    {
        cam = Camera.main.transform;
    }

    void Update()
    {
        HandleMovement();
        HandleGravity();
    }

    private void HandleMovement()
    {
        Vector2 moveInput = input.MoveInput;
        if (moveInput.sqrMagnitude < 0.01f) return;

        Vector3 forward = cam.forward;
        Vector3 right = cam.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDir = forward * moveInput.y + right * moveInput.x;
        moveDir.Normalize();

        cc.Move(moveDir * settings.moveSpeed * Time.deltaTime);

        if (moveDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDir),
                10f * Time.deltaTime
            );
        }
    }

    private void HandleGravity()
    {
        if (cc.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += settings.gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
