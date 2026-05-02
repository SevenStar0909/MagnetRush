using UnityEngine;

public enum FieldShape { Sphere, Box, Cylinder }

[CreateAssetMenu(fileName = "MagnetFieldSettings", menuName = "MagnetRush/MagnetFieldSettings")]
public class MagnetFieldSettings : ScriptableObject
{
    [Header("[形状]")]
    [Label("形状（Sphere/Box/Cylinder）")]
    [Tooltip("磁力場の形状。Sphere=球、Box=直方体、Cylinder=円柱")]
    public FieldShape shape = FieldShape.Sphere;

    [Header("[範囲]")]
    [Label("内側半径（フルパワー範囲）")]
    [Tooltip("この半径内では磁力がフルパワーで効く。描画もこの範囲。")]
    public float innerRadius = 3f;
    [Label("外側半径（ゼロ減衰・0で自動）")]
    [Tooltip("この半径で磁力がゼロに減衰する。innerRadiusより少し大きくすると自然な手触りになる（目安: innerの+30%）。0ならinnerRadius×1.3を自動使用。")]
    public float outerRadius = 8f;

    [Header("[ライフタイム]")]
    [Label("生存時間（秒・0で永続）")]
    [Tooltip("磁力場が消えるまでの秒数。0にすると永続。")]
    public float lifetime = 12f;

    [Header("[Box形状の設定]")]
    [Label("Boxサイズ (幅/高/奥)")]
    [Tooltip("Box形状の幅・高さ・奥行き（ローカル単位）")]
    public Vector3 size = new Vector3(2f, 2f, 2f);

    [Header("[Cylinder形状の設定]")]
    [Label("円柱の高さ")]
    [Tooltip("円柱の高さ（ローカル単位）")]
    public float cylinderHeight = 4f;
    [Label("円柱の半径")]
    [Tooltip("円柱の半径（ローカル単位）")]
    public float cylinderRadius = 1f;

    [Header("[ダメージ蓄積]")]
    [Label("ダメージ蓄積する")]
    [Tooltip("異極の弾が当たった時にダメージを蓄積するか")]
    public bool accumulateDamage = true;
    [Label("蓄積ダメージ上限")]
    [Tooltip("蓄積ダメージの上限。フィールド消滅時に範囲内の敵に放出される。")]
    public float maxStoredDamage = 200f;

    /// <summary>outerRadiusが0以下ならinnerRadius×1.3を自動使用。</summary>
    public float EffectiveOuterRadius => outerRadius > 0f ? outerRadius : innerRadius * 1.3f;
}