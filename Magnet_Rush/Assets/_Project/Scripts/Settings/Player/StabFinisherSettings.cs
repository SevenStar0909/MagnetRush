using UnityEngine;
using UnityEngine.Playables;

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

        [Label("瞬間移動先＝ボス左斜めのオフセット（右x/上y/前z・ボス基準m）")]
        [Tooltip("スタブ開始時にプレイヤーをここへワープ。ボスの左斜め＝左(xを負)と前(z)に")]
        public Vector3 runStartOffset = new Vector3(-4f, 0f, 4f);

        [Label("ジャンプ踏み切り位置のオフセット（ボス基準m）")]
        [Tooltip("左斜めから切り込んでここでジャンプ。ボスに近め＆やや内側に")]
        public Vector3 jumpOffOffset = new Vector3(-1f, 0f, 1.5f);

        [Label("跳び乗りの弧の高さ（m）")]
        [Tooltip("跳び上がる頂点の高さ。大きいほど大きく跳ぶ")]
        public float arcApexHeight = 2.5f;

        [Label("跳び降りの弧の高さ（m）")]
        [Tooltip("突き終わりに頭から跳び降りるときの蹴り上げの高さ。0だとほぼ滑り落ち、大きいほど一度ぴょんと跳んでから着地する")]
        public float retreatArcHeight = 1.5f;

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

        [Header("[突き調整]")]
        [Label("突き時に頭側へ寄せる量（0=足場のまま / 1=頭の位置）")]
        [Tooltip("突きの瞬間、足場の上からボス頭(StabAnchor)へどれだけ寄せるか。頭に当てたいなら上げる")]
        [Range(0f, 1f)]
        public float plungeHeadPull = 0.3f;

        [Label("突き時に上半身を頭側へ傾ける角度（度）")]
        [Tooltip("突きの瞬間、頭の方へ上体を傾ける近似（全身が中心まわりに傾く・足が少し浮く場合あり）。0で傾けない")]
        public float plungeLeanDegrees = 20f;

        [Label("後ろへ振り向く時間（秒）")]
        [Tooltip("突き開始で後ろ(ボス前)へ振り向くのにかける時間。カメラの回り込みもこの間まで続く。0で即時スナップ")]
        public float turnDuration = 0.5f;
    }

    [Label("Staggerポーズ用プロファイル")]
    public Profile stagger = new Profile();

    [Label("Stunポーズ用プロファイル")]
    public Profile stun = new Profile();

    [Label("演出カメラの Timeline（このボス専用・未設定なら共通デフォルト）")]
    [Tooltip("FinisherCamera の寄り/回り込みをアニメートする Timeline。ボスごとに差し替えられる")]
    public PlayableAsset cameraTimeline;

    /// <summary>崩れ種別インデックス（0=Stagger / 1=Stun）に対応するプロファイルを返す。範囲外は stagger。</summary>
    public Profile GetProfile(int choreographyIndex)
    {
        return choreographyIndex == 1 ? stun : stagger;
    }
}
