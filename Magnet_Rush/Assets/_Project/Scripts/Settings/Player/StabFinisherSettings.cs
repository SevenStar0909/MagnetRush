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
    [Tooltip("FinisherCamera の寄り/回り込み専用 Timeline。ボスごとに差し替えられる。TimeScaleTrack はここではなく finisherCutscene 側へ置く")]
    public PlayableAsset cameraTimeline;

    [Label("プレイヤー演出 Timeline（録画した軌跡＋体）。設定すると Timeline 駆動になる")]
    [Tooltip("StabFinisherCutscene 等。PlayerRoot=移動軌跡 / Player=体アニメ / Stab Slow=演出全体のスロー。Camera トラックは使わない（StabFinisherCamera が担当）")]
    public PlayableAsset finisherCutscene;

    [Label("録画時の刺し点位置（演出をこのボス相対へ合わせる基準）")]
    [Tooltip("Timeline を録画した時の刺し点(StabAnchor・未設定ならボス原点)のワールド座標。実行時はこの基準から実ボスへ位置＋向きで合わせる。下のボタンで自動入力")]
    public Vector3 cutsceneAuthoringBossPosition;

    [Label("録画時の刺し点の向き（度・水平ヨー）")]
    [Tooltip("録画した時に刺し点(StabAnchor)が向いていた水平角度。実行時はこの差分だけ演出を回してボスの向きに合わせる。下のボタンで自動入力")]
    public float cutsceneAuthoringBossYaw;

    [Label("録画時のプレイヤーの向き（度・水平ヨー）")]
    [Tooltip("録画した時にプレイヤーが向いていた水平角度。体アニメはこの向き基準で前進するので、実行時はヨー差分だけ回して同じ向きを再現する。下のボタンで自動入力")]
    public float cutsceneAuthoringPlayerYaw;

    [Label("Timeline 駆動時のヒット発火時間（秒）")]
    [Tooltip("演出開始から何秒でボスにダメージ＋VFX を出すか。突きが刺さる瞬間に合わせる")]
    public float cutsceneHitTime = 2.5f;

    [Label("着弾エフェクト（突きが刺さった瞬間に出す）")]
    [Tooltip("突きがボスに刺さった瞬間に出すエフェクトのプレハブ。StabFinisherCutscene の Stab Explosion トラックが再生を進めるので、スロー中でも止まらず最後まで再生される")]
    public GameObject finisherImpactEffect;

    [Label("Dustエフェクト（Timelineで調整）")]
    [Tooltip("StabFinisherCutscene の Stab Dust トラックで再生するDustプレハブ。位置・開始時間・尺はTimeline上のプレビューオブジェクトとクリップで調整する")]
    public GameObject finisherDustEffect;

    /// <summary>崩れ種別インデックス（0=Stagger / 1=Stun）に対応するプロファイルを返す。範囲外は stagger。</summary>
    public Profile GetProfile(int choreographyIndex)
    {
        return choreographyIndex == 1 ? stun : stagger;
    }
}
