using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボス本体の SkinnedMeshRenderer を共有する子 SMR を生成し、
/// ボーン別の N/S 磁極をシェーダーに渡してアウトライン描画する。
/// 既存マテリアルは触らず、Inverted Hull の追加 SMR として併存する。
///
/// 状態は「base (持続)」と「flash (時限)」の2層を持ち、flash 優先で合成する。
/// 手 → 磁極状態は base 経路、被弾点滅は flash 経路で使い分ける想定。
/// </summary>
[DefaultExecutionOrder(-50)]
public class BossPerBoneOutline : MonoBehaviour
{
    public enum BonePoleState { None = 0, N = 1, S = 2 }

    [Header("[参照]")]
    [SerializeField] private SkinnedMeshRenderer m_sourceSMR;
    [SerializeField] private Material m_outlineMaterial;

    [Header("[デバッグ用]")]
    [Tooltip("Awake時に強制で特定ボーンに N/S を割り当てて見た目を確認するためのテストエントリ")]
    [SerializeField] private DebugBonePole[] m_debugInitialPoles;

    [System.Serializable]
    public struct DebugBonePole
    {
        public string boneName;
        public BonePoleState pole;
    }

    private struct BoneState
    {
        public BonePoleState basePole;
        public BonePoleState flashPole;
        public float flashEndTime;
    }

    private SkinnedMeshRenderer m_outlineSMR;
    private MaterialPropertyBlock m_mpb;
    private readonly Dictionary<string, int> m_boneIndexMap = new();
    private BoneState[] m_state;
    private float[] m_isN;
    private float[] m_isS;
    private bool m_anyFlashActive;

    private const int k_MaxBones = 64;

    private static readonly int s_isNProperty = Shader.PropertyToID("_IsNPerBone");
    private static readonly int s_isSProperty = Shader.PropertyToID("_IsSPerBone");

    void Awake()
    {
        if (m_sourceSMR == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "sourceSMR未設定"); return; }
        if (m_outlineMaterial == null) { ChannelLogger.LogGuardReturn("EnemyBossA", "outlineMaterial未設定"); return; }

        BuildBoneIndexMap();
        CreateOutlineRenderer();
        ApplyDebugInitialPoles();
    }

    private void BuildBoneIndexMap()
    {
        var bones = m_sourceSMR.bones;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            m_boneIndexMap[bones[i].name] = i;
        }
    }

    private void CreateOutlineRenderer()
    {
        var go = new GameObject("BossOutline_SMR");
        go.transform.SetParent(m_sourceSMR.transform.parent, false);
        go.transform.localPosition = m_sourceSMR.transform.localPosition;
        go.transform.localRotation = m_sourceSMR.transform.localRotation;
        go.transform.localScale = m_sourceSMR.transform.localScale;

        m_outlineSMR = go.AddComponent<SkinnedMeshRenderer>();
        m_outlineSMR.sharedMesh = m_sourceSMR.sharedMesh;
        m_outlineSMR.bones = m_sourceSMR.bones;
        m_outlineSMR.rootBone = m_sourceSMR.rootBone;

        int subCount = m_sourceSMR.sharedMesh.subMeshCount;
        var mats = new Material[subCount];
        for (int i = 0; i < subCount; i++) mats[i] = m_outlineMaterial;
        m_outlineSMR.sharedMaterials = mats;
        m_outlineSMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_outlineSMR.receiveShadows = false;

        m_state = new BoneState[k_MaxBones];
        m_isN = new float[k_MaxBones];
        m_isS = new float[k_MaxBones];
        m_mpb = new MaterialPropertyBlock();
        WriteToShader();
    }

    private void ApplyDebugInitialPoles()
    {
        if (m_debugInitialPoles == null) return;
        foreach (var entry in m_debugInitialPoles)
        {
            if (string.IsNullOrEmpty(entry.boneName)) continue;
            SetBonePole(entry.boneName, entry.pole);
        }
    }

    /// <summary>
    /// 指定ボーンに磁極を持続適用する。flash 中の場合は flash 解除後にこの値に戻る。
    /// </summary>
    public void SetBonePole(string boneName, BonePoleState pole)
    {
        if (!TryResolveBoneIndex(boneName, out int idx)) return;
        m_state[idx].basePole = pole;
        RecomputeAndWrite();
    }

    /// <summary>
    /// 指定ボーンを duration 秒だけ pole 色でフラッシュさせる。被弾点滅に使う。
    /// flash 期間中に再度呼ぶと終了時刻を上書きする (リトリガ)。
    /// </summary>
    public void FlashBonePole(string boneName, BonePoleState pole, float duration)
    {
        if (!TryResolveBoneIndex(boneName, out int idx)) return;
        if (duration <= 0f) { ChannelLogger.LogGuardReturn("EnemyBossA", "duration<=0"); return; }
        m_state[idx].flashPole = pole;
        m_state[idx].flashEndTime = Time.time + duration;
        m_anyFlashActive = true;
        RecomputeAndWrite();
    }

    void Update()
    {
        if (!m_anyFlashActive) return;

        float now = Time.time;
        bool anyStill = false;
        bool dirty = false;
        for (int i = 0; i < m_state.Length; i++)
        {
            if (m_state[i].flashPole == BonePoleState.None) continue;
            if (now >= m_state[i].flashEndTime)
            {
                m_state[i].flashPole = BonePoleState.None;
                m_state[i].flashEndTime = 0f;
                dirty = true;
            }
            else
            {
                anyStill = true;
            }
        }
        if (dirty) RecomputeAndWrite();
        m_anyFlashActive = anyStill;
    }

    private bool TryResolveBoneIndex(string boneName, out int idx)
    {
        if (!m_boneIndexMap.TryGetValue(boneName, out idx))
        {
            Debug.LogWarning($"[BossPerBoneOutline] 未知のボーン名: {boneName}");
            idx = -1;
            return false;
        }
        if (idx < 0 || idx >= k_MaxBones) { ChannelLogger.LogGuardReturn("EnemyBossA", $"ボーンindex超過: {idx}"); idx = -1; return false; }
        return true;
    }

    private void RecomputeAndWrite()
    {
        for (int i = 0; i < m_state.Length; i++)
        {
            // flash が None でなければそれを採用、それ以外は base
            BonePoleState effective = m_state[i].flashPole != BonePoleState.None
                ? m_state[i].flashPole
                : m_state[i].basePole;
            m_isN[i] = effective == BonePoleState.N ? 1f : 0f;
            m_isS[i] = effective == BonePoleState.S ? 1f : 0f;
        }
        WriteToShader();
    }

    private void WriteToShader()
    {
        if (m_outlineSMR == null) return;
        m_outlineSMR.GetPropertyBlock(m_mpb);
        m_mpb.SetFloatArray(s_isNProperty, m_isN);
        m_mpb.SetFloatArray(s_isSProperty, m_isS);
        m_outlineSMR.SetPropertyBlock(m_mpb);
    }
}
