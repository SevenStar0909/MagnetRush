using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// タイムライン上で VFX（パーティクル等のプレハブ）を出すエフェクト。クリップの頭で生成する。
/// 出す位置は Anchor（シーンの Transform）。未設定ならワールド原点。
/// </summary>
[Serializable]
public class VfxEffect : TimelineEffect
{
    [Tooltip("生成する VFX プレハブ")]
    public GameObject prefab;

    [Tooltip("出す位置の基準になる Transform。未設定ならワールド原点に出る")]
    public ExposedReference<Transform> anchor;

    [Tooltip("Anchor からのローカルオフセット")]
    public Vector3 localOffset;

    [Tooltip("生成時のスケール倍率")]
    public float scale = 1f;

    [Tooltip("クリップを抜けたら消すか。パーティクルを自然消滅させたいなら OFF")]
    public bool destroyOnExit = false;

    private Transform m_anchor;
    private GameObject m_spawned;

    public override void Resolve(IExposedPropertyTable resolver) => m_anchor = anchor.Resolve(resolver);

    public override void OnEnter(in EffectContext ctx)
    {
        // VFX 生成は実行中のみ（プレビューのスクラブでシーンにゴミを残さない）。
        if (!ctx.IsPlaying || prefab == null) return;

        Vector3 pos = m_anchor != null ? m_anchor.TransformPoint(localOffset) : localOffset;
        Quaternion rot = m_anchor != null ? m_anchor.rotation : Quaternion.identity;
        m_spawned = UnityEngine.Object.Instantiate(prefab, pos, rot);
        if (scale > 0f && Mathf.Abs(scale - 1f) > 0.001f) m_spawned.transform.localScale *= scale;
    }

    public override void OnExit(in EffectContext ctx)
    {
        if (destroyOnExit && m_spawned != null) UnityEngine.Object.Destroy(m_spawned);
        m_spawned = null;
    }
}
