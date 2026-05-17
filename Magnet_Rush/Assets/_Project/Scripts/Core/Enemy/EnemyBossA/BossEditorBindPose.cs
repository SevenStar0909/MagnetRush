using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EditMode 中のみボスを T-pose リファレンス FBX のポーズに固定する。
/// Edit Mode では Animator が AttackStance 等を自動再生してポーズを上書きするため、
/// Animator 自体を一時 disable した上で T-pose を毎 LateUpdate で書き込む。
/// Play モード入り時は Awake で Animator を再有効化し、通常の駆動に任せる。
///
/// 仕組み: T-pose FBX (Enemy_T_.fbx) のボーン階層から名前一致でボーン local TRS を取得し、
/// ボスのスケルトン各ボーンに適用する。FBX 側は読み取り専用、boss 側のみ書き換える。
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
public class BossEditorBindPose : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer m_smr;

    [Tooltip("T-pose の骨ローカルTRSを参照する FBX。Enemy_T_.fbx などを指定")]
    [SerializeField] private GameObject m_tPoseReference;

    [Tooltip("Edit Mode で T-pose に固定する")]
    [SerializeField] private bool m_lockInEditor = true;

    [Tooltip("空なら m_smr の祖先から自動取得")]
    [SerializeField] private Animator m_animator;

    private Dictionary<string, Transform> m_tposeBoneMap;

    void Awake()
    {
        // Play Mode 入り時に Animator を必ず有効化する。Edit Mode で disable されてた場合の復旧。
        if (Application.isPlaying)
        {
            ResolveAnimator();
            if (m_animator != null) m_animator.enabled = true;
        }
    }

    void OnEnable()
    {
        m_tposeBoneMap = null;
        ResolveAnimator();
        ApplyAnimatorState();
    }

    void OnDisable()
    {
        // コンポーネントが取り外された/無効化された時は Animator を有効に戻す
        if (m_animator != null) m_animator.enabled = true;
    }

    private void ResolveAnimator()
    {
        if (m_animator != null) return;
        if (m_smr != null) m_animator = m_smr.GetComponentInParent<Animator>();
    }

    private void ApplyAnimatorState()
    {
        if (m_animator == null) return;
        bool wantDisabled = !Application.isPlaying && m_lockInEditor;
        bool newEnabled = !wantDisabled;
        if (m_animator.enabled != newEnabled) m_animator.enabled = newEnabled;
    }

    private void EnsureTPoseMap()
    {
        if (m_tposeBoneMap != null) return;
        if (m_tPoseReference == null) return;
        m_tposeBoneMap = new Dictionary<string, Transform>();
        foreach (var t in m_tPoseReference.GetComponentsInChildren<Transform>(true))
        {
            if (!m_tposeBoneMap.ContainsKey(t.name)) m_tposeBoneMap[t.name] = t;
        }
    }

    void LateUpdate()
    {
        ApplyAnimatorState();

        if (Application.isPlaying) return;
        if (!m_lockInEditor) return;
        if (m_smr == null) return;
        if (m_tPoseReference == null) return;

        EnsureTPoseMap();
        if (m_tposeBoneMap == null || m_tposeBoneMap.Count == 0) return;

        var bones = m_smr.bones;
        for (int i = 0; i < bones.Length; i++)
        {
            var bone = bones[i];
            if (bone == null) continue;
            if (!m_tposeBoneMap.TryGetValue(bone.name, out var tposeBone)) continue;
            bone.localPosition = tposeBone.localPosition;
            bone.localRotation = tposeBone.localRotation;
            bone.localScale = tposeBone.localScale;
        }
    }
}
