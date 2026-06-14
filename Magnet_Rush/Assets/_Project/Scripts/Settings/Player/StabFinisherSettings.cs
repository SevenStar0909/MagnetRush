using UnityEngine;

/// <summary>
/// ボススタブ・フィニッシャー演出の調整値。ボスの崩れポーズごとに1プロファイル持つ。
/// 0=Stagger(よろけ)ポーズ用、1=Stun(スタン)ポーズ用。IStabReceiver.StabChoreographyIndex で選ぶ。
/// </summary>
[CreateAssetMenu(fileName = "StabFinisherSettings", menuName = "MagnetRush/StabFinisherSettings")]
[ClassLabelSO("スタブ演出設定")]
public class StabFinisherSettings : ScriptableObject
{
    [System.Serializable]
    public class Profile
    {
        [Label("接近して立つ位置（ボス中心からの水平オフセットm）")]
        [Tooltip("跳び乗る前に立つ位置。ボスの正面側にどれだけ離れて構えるか")]
        public Vector3 approachStandOffset = new Vector3(0f, 0f, 2f);

        [Label("跳び乗りの弧の高さ（m）")]
        [Tooltip("跳び上がる頂点の高さ。大きいほど大きく跳ぶ")]
        public float arcApexHeight = 2.5f;

        [Label("突き刺し方向のひねり（度）")]
        [Tooltip("頭に刺すときの体の向き微調整。寝てる/しゃがみで刺す角度を変える")]
        public float plungeYawOffset = 0f;

        [Label("間合い詰めの時間（秒）")]
        public float approachDuration = 0.35f;

        [Label("跳び上がりの時間（秒）")]
        public float leapDuration = 0.3f;

        [Label("突き下ろしの時間（秒）")]
        public float plungeDuration = 0.2f;

        [Label("離脱の時間（秒）")]
        public float retreatDuration = 0.4f;

    }

    [Label("Staggerポーズ用プロファイル")]
    public Profile stagger = new Profile();

    [Label("Stunポーズ用プロファイル")]
    public Profile stun = new Profile();

    /// <summary>崩れ種別インデックス（0=Stagger / 1=Stun）に対応するプロファイルを返す。範囲外は stagger。</summary>
    public Profile GetProfile(int choreographyIndex)
    {
        return choreographyIndex == 1 ? stun : stagger;
    }
}
