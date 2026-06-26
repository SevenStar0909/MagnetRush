using UnityEngine;

/// <summary>
/// プレイヤーが同極磁力で反発（弾かれる）した瞬間にコントローラーを1発振動させる。
/// 反発力は反発中フレーム毎に来るので、一定時間途切れたら次を新しい反発として1回だけ鳴らす。
/// プレイヤーの GameObject に付け、その Magnetizable の反発通知を購読する。
/// 依存: Magnetizable.OnRepulsionForce, MagnetSettings(振動パラメータ), Rumble
/// </summary>
public class PlayerRepulsionRumble : MonoBehaviour
{
    [SerializeField] private MagnetSettings m_settings;
    [SerializeField] private Magnetizable m_magnetizable;

    private bool m_ready;
    private float m_lastRepulsionTime = -999f;

    private void Awake()
    {
        if (m_magnetizable == null) m_magnetizable = GetComponentInParent<Magnetizable>();
        m_ready = m_settings != null && m_magnetizable != null;
        if (!m_ready)
            ChannelLogger.LogGuardReturn("Magnet", "反発振動: MagnetSettings か Magnetizable 未設定のため無効");
    }

    private void OnEnable()
    {
        if (m_ready) m_magnetizable.OnRepulsionForce += OnRepulsionForce;
    }

    private void OnDisable()
    {
        if (m_ready) m_magnetizable.OnRepulsionForce -= OnRepulsionForce;
    }

    private void OnRepulsionForce(Vector3 force)
    {
        if (!m_settings.enableRepulsionRumble) return;
        if (force.magnitude < m_settings.repulsionRumbleMinForce) return;

        // 反発が途切れてから間隔以上空いた時だけ「新しい反発」として鳴らす（連続反発で鳴り続けない）。
        float now = Time.unscaledTime;
        bool newBurst = now - m_lastRepulsionTime > m_settings.repulsionRumbleRetriggerGap;
        m_lastRepulsionTime = now;
        if (!newBurst) return;

        Rumble.Pulse(m_settings.repulsionRumbleLow, m_settings.repulsionRumbleHigh, m_settings.repulsionRumbleDuration);
    }
}
