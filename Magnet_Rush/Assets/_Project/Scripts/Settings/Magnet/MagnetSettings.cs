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
    [Label("ペア検索カットオフ距離（m）")]
    [Tooltip("ペア検索のハードカットオフ距離（m）。この距離以上のペアは力を計算しない（パフォーマンス用）")]
    public float magnetRange = 10f;

    [Header("[制限]")]
    [Label("1組の引き寄せ力の上限（0=制限なし）")]
    [Tooltip("異極ペアの引き寄せ力の上限。強すぎる時の頭打ち。0で制限なし")]
    [FormerlySerializedAs("maxForcePerObject")]
    [FormerlySerializedAs("maxForcePerPair")]
    public float attractMaxForcePerPair = 0f;
    [Label("1組の反発力の上限（0=制限なし）")]
    [Tooltip("同極ペアの反発力の上限。反発だけ頭打ちにしたい時に使う。0で制限なし")]
    public float repelMaxForcePerPair = 0f;

    [Label("プレイヤーが磁場の中で鈍くなる強さの基準値")]
    [Tooltip("プレイヤーが磁場の中で受けてる磁力の合計がこの値に達すると、最大限まで鈍くなる。実際にどれくらい鈍くなるかは『磁場内速度低下率』で決まる。0=磁場の中でも鈍くならない")]
    public float influenceNormalizeForce = 300f;

    [Header("[接触]")]
    [Label("FixedJoint発動距離")]
    [Tooltip("Object×Object の FixedJoint 発動距離（Entity絡みは holdEngageDistance を使用）")]
    public float snapDistance = 1.5f;

    [Header("[スナップ]")]
    [Label("FixedJoint破壊力")]
    [Tooltip("FixedJoint の破壊力（同極反発で分離）")]
    public float snapBreakForce = 100f;

    [Header("[PDホールド]")]
    [Label("PDホールド開始距離")]
    [Tooltip("この距離内に入ると PD 保持に切り替わる (Entity絡みペア専用)")]
    public float holdEngageDistance = 1.5f;

    [Label("バネ定数（追従の強さ）")]
    [Tooltip("位置エラーに対するバネ定数。大きいほどガッチリ追従するが振動しやすい")]
    public float holdStiffness = 80f;

    [Label("ダンパ係数（振動抑制）")]
    [Tooltip("速度に対するダンパ係数。大きいほど振動が収まるが追従が重くなる")]
    public float holdDamping = 15f;

    [Label("吸着最大許容距離")]
    [Tooltip("吸着中の最大許容距離。超過で吸着解除")]
    public float holdMaxDistance = 3f;

    [Header("[弾同士]")]
    [Label("弾近接ダメージ範囲")]
    [Tooltip("異極の弾が近接した時にダメージ蓄積が発生する距離")]
    public float bulletProximityRange = 1f;

    [Header("[移動変調]")]
    [LabelRange("プレイヤーが磁場の中で鈍くなる率（0=変化なし, 1=完全停止）", 0f, 1f)]
    [Tooltip("プレイヤーが磁場の中で動く時、どれくらい鈍くなるか。0=磁場の中でも普通に動ける、1=完全停止")]
    public float magnetSpeedDamping = 0.3f;
}
