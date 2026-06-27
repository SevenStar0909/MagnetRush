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

        // 弱い振動で強い振動を打ち消さない。鳴っている最中はより強い（か同等の）pulse だけ採用し、
        // 尺はその pulse 自身の長さにする。強度と尺を別々に Max すると、強い短 pulse の強度が
        // 弱い長 pulse の尺ぶん鳴り続けてしまうため、組で置き換える。
        if (m_active && Mathf.Max(low, high) < Mathf.Max(m_low, m_high)) return;

        m_low = low;
        m_high = high;
        m_endTime = Time.unscaledTime + duration;
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
        // 終了時刻も必ず戻す。残すと、次の短い pulse が古い endTime まで延びて鳴り続ける。
        m_endTime = 0f;
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

    // アプリ中断・終了時はモーターを止める（鳴りっぱなし防止）。状態も戻して再開後に
    // 古い endTime まで無音のまま m_active が残らないようにする。
    private void OnApplicationPause(bool paused)
    {
        if (paused) StopNow();
    }

    private void OnApplicationQuit()
    {
        StopNow();
    }
}
