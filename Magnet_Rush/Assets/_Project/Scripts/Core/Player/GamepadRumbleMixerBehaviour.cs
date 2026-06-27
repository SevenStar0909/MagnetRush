using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

/// <summary>
/// GamepadRumbleClip の値を合算して接続中のゲームパッド（Gamepad.current）のモーターを駆動する。
/// クリップ内の経過に応じて減衰させ、突き刺さりの瞬間に強く出る振動を作る。
/// 物理的な振動なので Edit Mode のスクラブでは鳴らさず（誤爆防止）、再生中のみ適用する。
/// </summary>
public class GamepadRumbleMixerBehaviour : PlayableBehaviour
{
    private bool m_applied;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // Edit Mode のプレビュー中は実機を鳴らさない（スクラブ途中で振動が残る事故を避ける）。
        if (!Application.isPlaying) return;

        float low = 0f;
        float high = 0f;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var input = (ScriptPlayable<GamepadRumbleBehaviour>)playable.GetInput(i);
            var behaviour = input.GetBehaviour();

            // 振動の尺＝クリップ長。クリップの端を動かせば振動の長さを調整できる。
            float duration = Mathf.Max(0.0001f, (float)input.GetDuration());
            float elapsed = Mathf.Clamp((float)input.GetTime(), 0f, duration);
            float decay = Mathf.Pow(1f - elapsed / duration, behaviour.decayPower);

            low += behaviour.lowFrequency * decay * weight;
            high += behaviour.highFrequency * decay * weight;
        }

        Apply(Mathf.Clamp01(low), Mathf.Clamp01(high));
    }

    // 演出が止まった/クリップ範囲外に出た瞬間にモーターを必ず止める（鳴りっぱなし防止）。
    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        Apply(0f, 0f);
    }

    private void Apply(float low, float high)
    {
        var pad = Gamepad.current;
        if (pad == null) return; // パッド未接続（キーボード操作）なら何もしない

        if (low <= 0f && high <= 0f)
        {
            if (!m_applied) return; // 既に止まっている。毎フレーム SetMotorSpeeds を呼ばない。
            pad.SetMotorSpeeds(0f, 0f);
            m_applied = false;
            return;
        }

        pad.SetMotorSpeeds(low, high);
        m_applied = true;
    }
}
