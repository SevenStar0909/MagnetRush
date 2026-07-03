using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// EffectTrack の共通エンジン。各 EffectClip の中身(TimelineEffect)を回して OnEnter/OnUpdate/OnExit を呼ぶ。
/// 「毎回踏んでた面倒事」をここ1箇所に集約する：
/// ・Edit Mode で動かす/動かさないの制御（effect.PreviewInEditMode）
/// ・クリップ範囲の入り/出りのエッジ検出（weight ベース。プレビューでも実機でも安定）
/// ・演出停止(director.Stop 等)で残っているエフェクトに必ず OnExit（鳴りっぱなし・位置が戻らない事故を防ぐ）
/// </summary>
public class EffectMixerBehaviour : PlayableBehaviour
{
    private bool[] m_wasActive;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        int count = playable.GetInputCount();
        if (m_wasActive == null || m_wasActive.Length != count) m_wasActive = new bool[count];

        bool isPlaying = Application.isPlaying;
        var ctx = new EffectContext(isPlaying);

        for (int i = 0; i < count; i++)
        {
            var input = (ScriptPlayable<EffectPlayableBehaviour>)playable.GetInput(i);
            var effect = input.GetBehaviour().effect;
            bool active = playable.GetInputWeight(i) > 0f;

            if (effect == null) { m_wasActive[i] = active; continue; }

            // Edit Mode で動かさないエフェクトは、プレビュー中は完全にスキップ（入り/出りも起こさない）。
            if (!isPlaying && !effect.PreviewInEditMode) { m_wasActive[i] = false; continue; }

            if (active && !m_wasActive[i]) effect.OnEnter(in ctx);
            if (active)
            {
                double dur = input.GetDuration();
                float t = dur > 0.0001 ? Mathf.Clamp01((float)(input.GetTime() / dur)) : 0f;
                effect.OnUpdate(in ctx, t, playable.GetInputWeight(i));
            }
            if (!active && m_wasActive[i]) effect.OnExit(in ctx);

            m_wasActive[i] = active;
        }
    }

    // 演出が止まった瞬間、まだ入っている扱いのエフェクトに OnExit を呼んで後始末する。
    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (m_wasActive == null) return;
        var ctx = new EffectContext(Application.isPlaying);
        int count = Mathf.Min(m_wasActive.Length, playable.GetInputCount());
        for (int i = 0; i < count; i++)
        {
            if (!m_wasActive[i]) continue;
            var input = (ScriptPlayable<EffectPlayableBehaviour>)playable.GetInput(i);
            input.GetBehaviour().effect?.OnExit(in ctx);
            m_wasActive[i] = false;
        }
    }
}
