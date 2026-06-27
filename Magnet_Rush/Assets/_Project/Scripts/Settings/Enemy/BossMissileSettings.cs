using UnityEngine;

/// <summary>
/// ボスのミサイル発射パラメータ（角度・弧・自爆防止）。BossMissileLauncher が参照する。
/// 発射点(Transform)とミサイルPrefabはプレハブ階層依存なので、このSOではなく BossMissileLauncher 側に持つ。
/// </summary>
[CreateAssetMenu(fileName = "BossMissileSettings", menuName = "MagnetRush/BossMissileSettings")]
[ClassLabelSO("ボスミサイル設定")]
public class BossMissileSettings : ScriptableObject
{
    [Label("ロブ撃ちを使う")]
    [Tooltip("ONでミサイルを 上2発→左上/右上1発ずつ の順に撃つ。OFFなら前方へ撃つ")]
    public bool fireLobMissiles = true;

    [LabelRange("上ミサイルの角度（度／90で真上）", 0f, 90f)]
    [Tooltip("分岐後の上ミサイル角度(度)。前方から上へ傾ける。90で真上")]
    public float lobAngle = 45f;

    [LabelMin("分岐し始めるまでの時間（秒）", 0f)]
    [Tooltip("発射口から控えめな角度で出てから、上/左右上の形に分岐し始めるまでの時間(秒)")]
    public float lobRiseTime = 0.45f;

    [LabelRange("左右上の開き角度（度）", 0f, 90f)]
    [Tooltip("左右上ミサイルの横方向への開き角度(度)。0なら真上、90なら真横")]
    public float sideLobAngle = 35f;

    [LabelRange("発射直後の上向き角度（度）", 0f, 89f)]
    [Tooltip("発射口から出る瞬間だけ使う控えめな上向き角度(度)。分岐前の見た目用")]
    public float launchAngle = 12f;

    [LabelMin("弧の頂点の高さ", 0f)]
    [Tooltip("弧の頂点の高さ。大きいほどミサイル同士が空中で離れやすい")]
    public float arcHeight = 8f;

    [LabelMin("分岐方向へ膨らませる距離", 0f)]
    [Tooltip("分岐方向へ膨らませる距離。大きいほど横/前方に広い弧を描く")]
    public float arcSpreadDistance = 8f;

    [LabelMin("上2発を左右にずらす距離", 0f)]
    [Tooltip("上2発の弧を左右にずらす距離。0で同じライン")]
    public float arcLaneSpacing = 3f;

    [LabelMin("発射直後の自爆防止時間（秒）", 0f)]
    [Tooltip("発射してからこの秒数はミサイルがボス本体に当たらない(発射直後の自爆防止)。経過後はボスにも当たる=磁力で撃ち返せる。0で即当たる")]
    public float collisionGrace = 3f;
}
