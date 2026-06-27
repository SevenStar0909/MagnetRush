using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// コントローラー振動の実体。Rumble ファサードのフックに登録し、接続中のパッド（Gamepad.current）のモーターを駆動する。
/// 起動時に自動生成され DontDestroyOnLoad（シーンに配置不要）。
/// Time.unscaledTime 基準なのでヒットストップ中(timeScale=0)でも振動は鳴り切る。
/// 依存: Unity.InputSystem
/// </summary>
public class RumbleManager : MonoBehaviour
{
    private static RumbleManager s_instance;

    private float m_low;
    private float m_high;
    private float m_endTime;
    private bool m_active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (s_instance != null) return;
        var go = new GameObject("[RumbleManager]");
        s_instance = go.AddComponent<RumbleManager>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        Rumble.OnPulse = Pulse;
        Rumble.OnStop = StopNow;
    }

    private void OnDisable()
    {
        // 自分が登録したフックだけ解除する。
        if (Rumble.OnPulse == Pulse) Rumble.OnPulse = null;
        if (Rumble.OnStop == StopNow) Rumble.OnStop = null;
        ResetMotors();
    }

    private void Pulse(float low, float high, float duration)
    {
        low = Mathf.Clamp01(low);
        high = Mathf.Clamp01(high);
        if (duration <= 0f || (low <= 0f && high <= 0f)) return;

        // 振動が重なった時は強い方・長い方を採用（弱い振動が強い振動を打ち消さないように）。
        m_low = Mathf.Max(m_low, low);
        m_high = Mathf.Max(m_high, high);
        m_endTime = Mathf.Max(m_endTime, Time.unscaledTime + duration);
        m_active = true;
        Apply(m_low, m_high);
    }

    private void Update()
    {
        if (!m_active) return;
        // 実時間で終了判定（ヒットストップで timeScale=0 でも unscaledTime は進む）。
        if (Time.unscaledTime >= m_endTime) StopNow();
    }

    private void StopNow()
    {
        m_active = false;
        m_low = 0f;
        m_high = 0f;
        ResetMotors();
    }

    private void Apply(float low, float high)
    {
        Gamepad.current?.SetMotorSpeeds(low, high);
    }

    private void ResetMotors()
    {
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }

    // アプリ中断・終了時はモーターを止める（鳴りっぱなし防止）。
    private void OnApplicationPause(bool paused)
    {
        if (paused) ResetMotors();
    }

    private void OnApplicationQuit()
    {
        ResetMotors();
    }
}
