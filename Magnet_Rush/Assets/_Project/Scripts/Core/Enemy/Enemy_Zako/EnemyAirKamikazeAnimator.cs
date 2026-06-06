using UnityEngine;

/// <summary>
/// Kamikaze air enemy animator driver.
/// Keeps direct Animator operations behind a small API for the AI.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAirBase))]
public class EnemyAirKamikazeAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Driven Animator. If unset, the first child Animator is used.")]
    [SerializeField] private Animator m_animator;

    [Tooltip("Visual root to tilt on local X. If unset, Animator.transform is used.")]
    [SerializeField] private Transform m_tiltRoot;

    [Header("Animator State Names")]
    [SerializeField] private string m_idleStateName = "Idel";
    [SerializeField] private string m_attackStateName = "Attack";
    [SerializeField] private string m_attackTriggerName = "Attack";

    [Header("Blend")]
    [SerializeField] private float m_crossFadeTime = 0.08f;

    [Header("Attack Tilt")]
    [SerializeField] private float m_maxTiltAngle = 45f;    // 最大傾き角度
    [SerializeField] private float m_tiltSpeed = 10f;       // ターゲットの角度に傾くまでの速度
    [SerializeField] private float m_returnSpeed = 3f;      // 元の角度に戻るまでの速度

    private int m_idleStateHash;
    private int m_attackStateHash;
    private int m_attackTriggerHash;
    private bool m_isAttacking;
    private Quaternion m_initialRotation;

    private void Awake()
    {
        if (m_animator == null)
            m_animator = GetComponentInChildren<Animator>(true);

        if (m_tiltRoot == null && m_animator != null)
            m_tiltRoot = m_animator.transform;

        m_idleStateHash = Animator.StringToHash(m_idleStateName);
        m_attackStateHash = Animator.StringToHash(m_attackStateName);
        m_attackTriggerHash = Animator.StringToHash(m_attackTriggerName);

        if (m_animator == null)
        {
            ChannelLogger.LogGuardReturn("Enemy", $"[{nameof(EnemyAirKamikazeAnimator)}] {name}: Animator was not found.");
            enabled = false;
            return;
        }

        if (m_tiltRoot != null)
            m_initialRotation = m_tiltRoot.localRotation;

        PlayIdle();
    }

    private void LateUpdate()
    {
        // 攻撃中は前方に傾ける。攻撃していないときは元に戻す。
        UpdateTilt(Time.deltaTime);
    }

    public void PlayIdle()
    {
        CrossFade(m_idleStateHash);
        m_isAttacking = false;
    }

    public void TriggerAttack()
    {
        SetAttacking(true);
    }

    // AIが呼ぶかもしれないのでメソッドは残すが、前傾のみなので中身は不要
    public void SetAttackTargetDirection(Vector3 worldDirection)
    {
    }

    public bool IsAttacking => m_isAttacking;

    public bool IsInAttackState
    {
        get
        {
            if (m_animator == null)
                return false;

            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == m_attackStateHash;
        }
    }

    public bool IsInIdleState
    {
        get
        {
            if (m_animator == null)
                return false;

            return m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == m_idleStateHash;
        }
    }

    public void SetAttacking(bool value)
    {
        if (m_animator == null)
            return;

        if (m_isAttacking == value)
            return;

        m_isAttacking = value;
        if (value)
        {
            m_animator.SetTrigger(m_attackTriggerHash);
        }

        CrossFade(value ? m_attackStateHash : m_idleStateHash);
    }

    private void CrossFade(int stateHash)
    {
        if (m_animator == null)
            return;

        if (m_crossFadeTime > 0f)
            m_animator.CrossFade(stateHash, m_crossFadeTime, 0);
        else
            m_animator.Play(stateHash, 0, 0f);
    }

    // 前方に傾ける処理
    private void UpdateTilt(float dt)
    {
        if (m_tiltRoot == null)
            return;

        bool shouldTilt = m_isAttacking || IsInAttackState;
        float targetAngle = shouldTilt ? m_maxTiltAngle : 0f;
        float speed = shouldTilt ? m_tiltSpeed : m_returnSpeed;

        // X軸回りの傾き角度
        Quaternion targetRotation = m_initialRotation * Quaternion.Euler(targetAngle, 0f, 0f);

        // 速度が0以下なら即座に回転を適用
        if (speed <= 0f)
        {
            m_tiltRoot.localRotation = targetRotation;
            return;
        }

        // 現在の回転からターゲットの回転へスムーズに補間
        m_tiltRoot.localRotation = Quaternion.Slerp(
            m_tiltRoot.localRotation,
            targetRotation,
            1f - Mathf.Exp(-speed * dt)
        );
    }
}
