using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerSettings settings;


    // [＋追加] 弾の発射に必要な設定
    [Header("Shooting Settings")]
    [SerializeField] private GameObject bulletPrefab; // 弾のプレハブ
    [SerializeField] private Transform firePoint;     // 弾が出る位置（銃口など）


    private CharacterController cc;
    private PlayerInputHandler input;
    private Vector3 velocity;
    private Transform cam;

    // [＋追加] 初期状態はN極
    private MagneticPole currentPole = MagneticPole.N;

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

        // [＋追加] 毎フレーム発射入力をチェックする
        HandleShooting();
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

    // [＋追加] 射撃の入力判定を行うメソッド
    private void HandleShooting()
    {
        if (input.SwitchPolePressed)
        {
            TogglePole();
        }

        if (input.FirePressed)
        {
            Shoot(currentPole);
        }
    }


    // [＋追加] 弾の極性を変えるメソッド
    private void TogglePole()
    {
        // NならSに、SならNに切り替える
        currentPole = (currentPole == MagneticPole.N) ? MagneticPole.S : MagneticPole.N;

        // UIの更新やエフェクト（銃の色を変えるなど）をここに入れると親切です
        Debug.Log("現在の極: " + currentPole);
    }

    // [＋追加] 実際に弾を生成して飛ばすメソッド
    private void Shoot(MagneticPole pole)
    {
        // マネージャーに撃てるか確認（上限チェック）
        if (BulletManager.Instance != null && !BulletManager.Instance.CanShoot())
        {
            Debug.Log("弾の数が上限に達しています！");
            return;
        }

        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("BulletPrefab または FirePoint が設定されていません！");
            return;
        }

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        MagnetBullet bullet = bulletObj.GetComponent<MagnetBullet>();

        if (bullet != null)
        {
            // 弾の初期化（プレイヤーの向いている正面方向に飛ばす）
            bullet.Initialize(pole, transform.forward);

            // マネージャーに弾を登録（残弾管理）
            if (BulletManager.Instance != null)
            {
                BulletManager.Instance.RegisterBullet(bullet);
            }
        }
    }
}
