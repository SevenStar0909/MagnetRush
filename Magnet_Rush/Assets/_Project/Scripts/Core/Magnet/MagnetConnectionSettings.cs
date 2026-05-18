using UnityEngine;

// Scripts/Settings/Magnet/MagneticConnectionSettings.cs
[CreateAssetMenu(menuName = "Settings/Magnet/ConnectionSettings")]
public class MagneticConnectionSettings : ScriptableObject
{

    [SerializeField] private float m_duration = 5.0f;                // 寿命・制限時間（待機中も進行）
    [SerializeField] private float m_maxDistance = 15.0f;            // 最大維持距離
    [SerializeField] private float m_pullForce = 20.0f;              // 軽い側に毎フレーム適用する引力の強さ
    [SerializeField] private float m_connectionBulletSpeed;         // 接続型本体の発射速度（プランナー確認待ち）
    [SerializeField] private float m_chargeThresholdSeconds;         // RT長押し閾値（仮置き）
    [SerializeField] private LayerMask m_occluderMask;               // 遮蔽物判定用マスク（Wall / Ground）
    [SerializeField] private Color m_sColor, m_nColor;               // 両端極性色（青／赤）
    [SerializeField] private GameObject m_visualizerParticlePrefab;  // 本実装（VFX Graph）で値設定するプレハブ

    // 各フィールドの公開プロパティ（ゲッター）
    public float Duration => m_duration;
    public float MaxDistance => m_maxDistance;
    public float PullForce => m_pullForce;
    public float ConnectionBulletSpeed => m_connectionBulletSpeed;
    public float ChargeThresholdSeconds => m_chargeThresholdSeconds;
    public LayerMask OccluderMask => m_occluderMask;
    public Color SColor => m_sColor;
    public Color NColor => m_nColor;
    public GameObject VisualizerParticlePrefab => m_visualizerParticlePrefab;
}