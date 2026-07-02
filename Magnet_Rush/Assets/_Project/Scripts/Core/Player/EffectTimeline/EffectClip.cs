using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 汎用エフェクトクリップ。中身は [SerializeReference] で差し替える（音／VFX／オブジェクト移動／カメラ移動）。
/// クリップを置いて Inspector で種類を選ぶだけで、新しいトラックのコードを書かずにエフェクトを足せる。
/// クリップの長さ＝エフェクトの尺。
/// </summary>
[Serializable]
public class EffectClip : PlayableAsset, ITimelineClipAsset
{
    [SerializeReference] public TimelineEffect effect;

    public override double duration => 1d;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<EffectPlayableBehaviour>.Create(graph);
        // ExposedReference（移動対象など）はここで解決してエフェクトに埋め込む。
        effect?.Resolve(graph.GetResolver());
        playable.GetBehaviour().effect = effect;
        return playable;
    }
}

/// <summary>
/// EffectClip の中身(TimelineEffect)を運ぶだけの均一な PlayableBehaviour。
/// 種類が違っても PlayableBehaviour の型は常にこれ1つなので、Mixer が全クリップを同じ型で扱える。
/// </summary>
public class EffectPlayableBehaviour : PlayableBehaviour
{
    public TimelineEffect effect;
}
