using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// TimeScaleTrack のミキサー。各クリップの重み（両端イーズ込み）で Time.timeScale を 1↔slow に補間する。
/// 物理のガタつきを抑えるため Time.fixedDeltaTime も倍率に合わせて伸縮し、効いていない時は等速へ戻す。
/// Edit モードでは Time.timeScale を一切触らない。Scene上のTimelineプレビュー速度は
/// Editor/TimeScaleTimelinePreview がPlayableGraphの速度だけを変更し、プロジェクト設定を汚さず再現する。
/// </summary>
public class TimeScaleMixerBehaviour : PlayableBehaviour
{
    private float m_baseFixedDelta;
    private bool m_captured;
    private bool m_applied;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying) return;

        if (!m_captured)
        {
            m_baseFixedDelta = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            m_captured = true;
        }

        int n = playable.GetInputCount();
        float blend = 0f;
        float weightedSlow = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;
            var sp = (ScriptPlayable<TimeScaleBehaviour>)playable.GetInput(i);
            weightedSlow += w * sp.GetBehaviour().slowTimeScale;
            blend += w;
        }

        if (blend <= 0f)
        {
            if (m_applied) Restore();
            return;
        }

        float slow = weightedSlow / blend;                          // クリップが重なった時の平均強さ
        float target = Mathf.Lerp(1f, slow, Mathf.Clamp01(blend));  // イーズ区間は 1→slow へ部分適用
        Time.timeScale = target;
        Time.fixedDeltaTime = m_baseFixedDelta * target;
        m_applied = true;
    }

    public override void OnGraphStop(Playable playable) => Restore();
    public override void OnPlayableDestroy(Playable playable) => Restore();

    private void Restore()
    {
        if (!Application.isPlaying) return;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_baseFixedDelta > 0f ? m_baseFixedDelta : 0.02f;
        m_applied = false;
    }
}
