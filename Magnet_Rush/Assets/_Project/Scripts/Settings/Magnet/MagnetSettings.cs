using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "MagnetSettings", menuName = "MagnetRush/MagnetSettings")]
[ClassLabelSO("磁石設定")]
public class MagnetSettings : ScriptableObject
{
    [Header("[磁力]")]
    [Label("引き寄せ（異極）の強さ")]
    [Tooltip("異極ペアを引き合わせる力。inner範囲内でこの値がフルで適用される")]
    [FormerlySerializedAs("magnetForce")]
    public float attractForce = 15f;
    [Label("反発（同極）の強さ")]
    [Tooltip("同極ペアを反発させる力。inner範囲内でこの値がフルで適用される。引き寄せより弱めにすると暴れにくい")]
    public float repelForce = 8f;
    [Label("これより遠いと磁力を無視する距離（m）")]
    [Tooltip("この距離より離れた相手には磁力を計算しない（処理を軽くするため）")]
    public float magnetRange = 10f;

    [Header("[制限]")]
    [Label("引き寄せる力の上限（0=無制限）")]
    [Tooltip("引き寄せる力が強すぎる時の頭打ち。0なら無制限")]
    [FormerlySerializedAs("maxForcePerObject")]
    [FormerlySerializedAs("maxForcePerPair")]
    public float attractMaxForcePerPair = 0f;
    [Label("反発する力の上限（0=無制限）")]
    [Tooltip("反発する力の頭打ち。反発だけ抑えたい時に使う。0なら無制限")]
    public float repelMaxForcePerPair = 0f;

    [Label("プレイヤーが磁場の中で鈍くなる強さの基準値")]
    [Tooltip("プレイヤーが磁場の中で受けてる磁力の合計がこの値に達すると、最大限まで鈍くなる。実際にどれくらい鈍くなるかは『磁場内速度低下率』で決まる。0=磁場の中でも鈍くならない")]
    public float influenceNormalizeForce = 300f;

    [Header("[接触]")]
    [Label("物体同士がくっつく距離")]
    [Tooltip("物（クレート等）同士がこの距離まで近づくとくっつく。敵が絡む時は『吸い付き始める距離』を使う")]
    public float snapDistance = 1.5f;

    [Label("くっついた瞬間に弾ける")]
    [Tooltip("異極同士が接触したら磁力を解除し、接触方向へバチーンと弾き飛ばす")]
    public bool burstOnAttractContact = true;

    [Label("弾ける速さ（m/s）")]
    [Tooltip("くっついた後に互いを引き離す速度。大きいほど勢いよく弾ける")]
    public float contactBurstVelocity = 12f;

    [Label("弾ける上向き補正")]
    [Tooltip("弾ける方向に足す上向き成分。0なら真横、0.2〜0.4で少し派手に跳ねる")]
    public float contactBurstUpwardBias = 0.25f;

    [Header("[スナップ]")]
    [Label("くっついた物体が離れる力")]
    [Tooltip("くっついた物どうしを引き剥がすのに要る力（同じ極で反発すると外れる）")]
    public float snapBreakForce = 100f;

    [Header("[PDホールド]")]
    [Label("吸い付き始める距離")]
    [Tooltip("敵がこの距離まで近づくと吸い付きモードに入る（敵が絡むペア専用）")]
    public float holdEngageDistance = 1.5f;

    [Label("吸い付く強さ")]
    [Tooltip("大きいほどガッチリ追いかけてくっつく。上げすぎるとプルプル震えやすい")]
    public float holdStiffness = 80f;

    [Label("吸い付いた時のブレの収まりやすさ")]
    [Tooltip("大きいほど揺れずにピタッと止まる。上げすぎると追いかけが重くなる")]
    public float holdDamping = 15f;

    [Label("離れすぎると吸い付きが外れる距離")]
    [Tooltip("くっついた状態でこの距離より離れると吸い付きが外れる")]
    public float holdMaxDistance = 3f;

    [Header("[弾同士]")]
    [Label("弾同士がダメージを出す距離")]
    [Tooltip("違う極の弾どうしがこの距離まで近づくとダメージがたまる")]
    public float bulletProximityRange = 1f;

    [Header("[移動変調]")]
    [LabelRange("プレイヤーが磁場の中で鈍くなる率（0=変化なし, 1=完全停止）", 0f, 1f)]
    [Tooltip("プレイヤーが磁場の中で動く時、どれくらい鈍くなるか。0=磁場の中でも普通に動ける、1=完全停止")]
    public float magnetSpeedDamping = 0.3f;

    [Header("[加算]")]
    [LabelRange("磁場が重なったときの力の加算（0=加算しない/一番強い磁場だけ, 1=全部加算）", 0f, 1f)]
    [Tooltip("磁場を複数重ねたとき、2個目以降の引っ張る力をどれだけ足すか。0=一番強い磁場1個分だけ効く（重ねても強くならない）、1=重ねた分だけ強くなる（従来）、0.5=半分だけ足す。1発の磁力の強さや鈍さには影響しない。")]
    public float overlapForceWeight = 1f;

    [Header("[反発の振動]")]
    [Label("反発時にコントローラーを振動させる")]
    [Tooltip("プレイヤーが同極磁力で反発（弾かれる）した瞬間にコントローラーを1発振動させる")]
    public bool enableRepulsionRumble = true;

    [LabelRange("反発の重い振動の強さ（0〜1）", 0f, 1f)]
    [Tooltip("反発時の重いゴロゴロした振動（低周波モーター）の強さ。0で無効")]
    public float repulsionRumbleLow = 0.6f;

    [LabelRange("反発の軽い振動の強さ（0〜1）", 0f, 1f)]
    [Tooltip("反発時の軽いブルブルした振動（高周波モーター）の強さ。0で無効")]
    public float repulsionRumbleHigh = 0.4f;

    [LabelMin("反発の振動時間（秒）", 0f)]
    [Tooltip("反発時に振動を鳴らす長さ。短いほど鋭い手応えになる")]
    public float repulsionRumbleDuration = 0.15f;

    [LabelMin("次の反発を鳴らすまでの間隔（秒）", 0f)]
    [Tooltip("反発が一旦途切れてからこの時間が経つと、次の反発を新しい1発として鳴らす。連続反発でブルブル鳴り続けるのを防ぐ")]
    public float repulsionRumbleRetriggerGap = 0.15f;

    [LabelMin("これ未満の弱い反発では鳴らさない", 0f)]
    [Tooltip("1フレームにかかる反発力がこの値未満なら振動させない。0なら弱い反発でも鳴る")]
    public float repulsionRumbleMinForce = 0.5f;
}
