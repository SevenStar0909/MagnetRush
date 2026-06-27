using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 磁力で加速されたPhysicsObjectがEntityBodyに衝突した際にダメージを与える。
/// OnCollisionEnterで検出。PhysicsObject×EntityBody=ONのMatrix制御。
/// 子コライダーから親のHitbox(IHittable)を辿ってOnHit()を呼ぶ。
/// </summary>
public class MagneticContactDamage : MonoBehaviour
{
    public static event Action<float, float, float> OnEnemyDamageCameraShake;
    public static bool IsHitStopActive => s_hitStopRoutine != null;

    [SerializeField] private ContactDamageSettings m_settings;

    /// <summary>この物理オブジェクトがボス本体に当たった時に与えるスタン値の蓄積率（％）。設定SOから取得。</summary>
    public int StunGaugePercent => m_settings != null ? m_settings.stunGaugePercent : 0;

    private Magnetizable m_magnetizable;
    private Rigidbody m_rb;
    private readonly HashSet<IHittable> m_hitTargets = new HashSet<IHittable>();
    private bool m_wasActive;
    private Vector3 m_pullStartPosition;

    private static Coroutine s_hitStopRoutine;
    private static float s_hitStopEndTime;
    private static float s_hitStopTimeScale;
    private static float s_restoreTimeScale;
    private static float s_restoreFixedDeltaTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnEnemyDamageCameraShake = null;
        s_hitStopRoutine = null;
        s_hitStopEndTime = 0f;
        s_hitStopTimeScale = 0f;
        s_restoreTimeScale = 1f;
        s_restoreFixedDeltaTime = 0.02f;
    }

    void Awake()
    {
        m_magnetizable = GetComponentInParent<Magnetizable>();
        m_rb = GetComponentInParent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (m_magnetizable == null) { ChannelLogger.LogGuardReturn("Magnet", "Magnetizableなし"); return; }

        // 磁力が切れたらヒット記録をリセット（再度当たれるように）
        if (!m_magnetizable.IsActive && m_wasActive)
        {
            m_hitTargets.Clear();
            m_wasActive = false;
        }
        else if (m_magnetizable.IsActive && !m_wasActive)
        {
            // 磁化された瞬間の位置を記録。ここから衝突地点までの移動距離でダメージが決まる
            m_pullStartPosition = transform.position;
            m_wasActive = true;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (m_magnetizable == null || !m_magnetizable.IsActive) { ChannelLogger.LogGuardReturn("ContactDmg", "磁化非アクティブ"); return; }
        if (m_settings == null) { ChannelLogger.LogGuardReturn("ContactDmg", "設定なし"); return; }

        float vel = collision.relativeVelocity.magnitude;
        if (vel < m_settings.minVelocity) { ChannelLogger.LogGuardReturn("ContactDmg", $"速度不足 {vel:F1}<{m_settings.minVelocity}"); return; }

        if (collision.collider.transform.IsChildOf(m_magnetizable.transform)) { ChannelLogger.LogGuardReturn("ContactDmg", "自己衝突"); return; }
        if (IsOwnDamageOwner(collision.collider.gameObject)) { ChannelLogger.LogGuardReturn("ContactDmg", "所有者への自分の武器ダメージは無効"); return; }

        var hittable = collision.collider.GetComponentInParent<IHittable>();
        if (hittable == null && collision.rigidbody != null)
            hittable = collision.rigidbody.GetComponentInChildren<IHittable>();
        if (hittable == null) { ChannelLogger.LogGuardReturn("ContactDmg", $"IHittable なし: {collision.collider.name}"); return; }

        // プレイヤーには物理オブジェクトの接触ダメージを与えない（ボスのスタン蓄積・敵への加害は維持）
        if (hittable.HitGroup == HitGroup.Player) { ChannelLogger.LogGuardReturn("ContactDmg", "プレイヤーへの接触ダメージは無効"); return; }

        // 磁化された位置から衝突地点までの直線距離をカーブに通してダメージを決める（近くから=弱、遠くから飛んできた=強）
        float pulledDistance = Vector3.Distance(transform.position, m_pullStartPosition);
        int damage = Mathf.Max(0, Mathf.RoundToInt(m_settings.damageByDistance.Evaluate(pulledDistance)));
        if (damage <= 0) { ChannelLogger.LogGuardReturn("ContactDmg", $"飛距離{pulledDistance:F1}mのカーブ値が0のためダメージなし"); return; }

        // HIT のみ目立つログで残す
        Debug.Log($"<color=#FF5722>[ContactDmg]</color> {name} → {((MonoBehaviour)hittable).name} dmg={damage} dist={pulledDistance:F1}m vel={vel:F1}");

        // 同一対象への重複ダメージ防止（磁力切れるまで1回のみ）
        if (m_hitTargets.Add(hittable))
        {
            var targetBehaviour = (MonoBehaviour)hittable;
            var targetHealth = targetBehaviour.GetComponentInParent<Health>();
            int healthBefore = targetHealth != null ? targetHealth.CurrentHealth : 0;

            hittable.OnHit(new HitData
            {
                damage = damage,
                hitPoint = collision.GetContact(0).point,
                knockbackDir = collision.relativeVelocity.normalized,
                source = gameObject
            });

            bool dealtDamage = targetHealth != null && targetHealth.CurrentHealth < healthBefore;
            if (dealtDamage && hittable.HitGroup == HitGroup.Enemy)
                RequestEnemyDamageFeedback(damage);
        }
    }

    private bool IsOwnDamageOwner(GameObject target)
    {
        var owner = GetComponentInParent<MagneticDamageOwner>();
        return owner != null && owner.IsOwnedBy(target);
    }

    private void RequestEnemyDamageFeedback(int damage)
    {
        RequestEnemyDamageCameraShake(damage);
        RequestEnemyDamageHitStop(damage);
        RequestEnemyDamageRumble(damage);
    }

    private void RequestEnemyDamageRumble(int damage)
    {
        if (!m_settings.enableRumble) return;

        float low = Mathf.Clamp01(m_settings.rumbleLowByDamage.Evaluate(damage));
        float high = Mathf.Clamp01(m_settings.rumbleHighByDamage.Evaluate(damage));
        float duration = Mathf.Max(0f, m_settings.rumbleDurationByDamage.Evaluate(damage));
        if (duration <= 0f || (low <= 0f && high <= 0f)) return;

        Rumble.Pulse(low, high, duration);
    }

    private void RequestEnemyDamageCameraShake(int damage)
    {
        if (!m_settings.enableCameraShake) return;

        float amplitude = Mathf.Max(0f, m_settings.cameraShakeAmplitudeByDamage.Evaluate(damage));
        float duration = Mathf.Max(0f, m_settings.cameraShakeDurationByDamage.Evaluate(damage));
        if (amplitude <= 0f || duration <= 0f) return;

        float frequency = Mathf.Max(0f, m_settings.cameraShakeFrequency);
        OnEnemyDamageCameraShake?.Invoke(amplitude, duration, frequency);
    }

    private void RequestEnemyDamageHitStop(int damage)
    {
        if (!m_settings.enableHitStop) return;

        float duration = Mathf.Max(0f, m_settings.hitStopDurationByDamage.Evaluate(damage));
        if (duration <= 0f) return;

        float timeScale = Mathf.Clamp01(m_settings.hitStopTimeScale);
        StartOrExtendHitStop(duration, timeScale);
    }

    private void StartOrExtendHitStop(float duration, float timeScale)
    {
        float requestedEndTime = Time.realtimeSinceStartup + duration;

        if (s_hitStopRoutine == null)
        {
            s_restoreTimeScale = Time.timeScale;
            s_restoreFixedDeltaTime = Time.fixedDeltaTime;
            s_hitStopTimeScale = timeScale;
            s_hitStopEndTime = requestedEndTime;
            s_hitStopRoutine = StartCoroutine(HitStopRoutine());
            return;
        }

        s_hitStopEndTime = Mathf.Max(s_hitStopEndTime, requestedEndTime);
        s_hitStopTimeScale = Mathf.Min(s_hitStopTimeScale, timeScale);
        ApplyHitStopTimeScale();
    }

    private static IEnumerator HitStopRoutine()
    {
        ApplyHitStopTimeScale();

        while (Time.realtimeSinceStartup < s_hitStopEndTime)
        {
            ApplyHitStopTimeScale();
            yield return null;
        }

        Time.timeScale = s_restoreTimeScale;
        Time.fixedDeltaTime = s_restoreFixedDeltaTime;
        s_hitStopRoutine = null;
    }

    private static void ApplyHitStopTimeScale()
    {
        Time.timeScale = s_hitStopTimeScale;
        Time.fixedDeltaTime = Mathf.Max(0.0001f, s_restoreFixedDeltaTime * Mathf.Max(0.0001f, s_hitStopTimeScale));
    }
}
