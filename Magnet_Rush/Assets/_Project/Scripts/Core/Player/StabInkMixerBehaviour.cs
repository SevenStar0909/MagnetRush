using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// StabInkTrack のミキサー。各クリップの重み（両端イーズ込み）で StabInkEffect.Strength を 0↔strength に補間する。
/// クリップが効いていない区間は 0（通常画面）へ戻す。Edit モードでは適用しない（StabInk は Play 中のみ）。
/// </summary>
public class StabInkMixerBehaviour : PlayableBehaviour
{
    private bool m_applied;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying) return;

        int n = playable.GetInputCount();
        float blend = 0f;
        float weighted = 0f;
        for (int i = 0; i < n; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;
            var sp = (ScriptPlayable<StabInkBehaviour>)playable.GetInput(i);
            weighted += w * sp.GetBehaviour().strength;
            blend += w;
        }

        if (blend <= 0f)
        {
            if (m_applied) Restore();
            return;
        }

        float strength = weighted / blend;                          // クリップが重なった時の平均強さ
        StabInkEffect.Strength = Mathf.Lerp(0f, strength, Mathf.Clamp01(blend));  // イーズ区間は 0→strength へ部分適用
        m_applied = true;
    }

    public override void OnGraphStop(Playable playable) => Restore();
    public override void OnPlayableDestroy(Playable playable) => Restore();

    private void Restore()
    {
        StabInkEffect.Strength = 0f;
        m_applied = false;
    }
}
