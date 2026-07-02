using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 汎用エフェクトタイムラインの「中身」1つ分の基底。
/// EffectClip に [SerializeReference] で差し込み、種類ごとに派生する（音／VFX／オブジェクト移動／カメラ移動）。
/// 共通の面倒事（Edit Mode 制御・入り/出りのエッジ・停止リセット・ブレンド weight）は EffectMixerBehaviour が面倒を見るので、
/// 各エフェクトは OnEnter / OnUpdate / OnExit を書くだけでよい。
/// </summary>
[System.Serializable]
public abstract class TimelineEffect
{
    /// <summary>Edit Mode のプレビュー（スクラブ・プレビュー再生）でも動かすか。移動系は true、音・VFX は false 推奨。</summary>
    public virtual bool PreviewInEditMode => false;

    /// <summary>ExposedReference（シーンの対象参照）を使う派生はここで解決する。EffectClip.CreatePlayable から呼ばれる。</summary>
    public virtual void Resolve(IExposedPropertyTable resolver) { }

    /// <summary>クリップ範囲に入った最初のフレーム。1回だけ鳴らす音・生成する VFX はここ。</summary>
    public virtual void OnEnter(in EffectContext ctx) { }

    /// <summary>クリップ範囲内で毎フレーム。位置を補間する移動系はここ。t=0〜1、weight=ブレンド重み。</summary>
    public virtual void OnUpdate(in EffectContext ctx, float t, float weight) { }

    /// <summary>クリップ範囲から出た/演出が止まったフレーム。元に戻す・止めるはここ。</summary>
    public virtual void OnExit(in EffectContext ctx) { }
}

/// <summary>エフェクトへ渡す共通情報。</summary>
public readonly struct EffectContext
{
    /// <summary>実行中(Play)なら true、Edit Mode プレビューなら false。</summary>
    public readonly bool IsPlaying;

    public EffectContext(bool isPlaying)
    {
        IsPlaying = isPlaying;
    }
}
