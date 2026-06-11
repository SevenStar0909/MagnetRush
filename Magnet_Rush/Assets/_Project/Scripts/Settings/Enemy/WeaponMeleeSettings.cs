using UnityEngine;

[CreateAssetMenu(fileName = "WeaponMeleeSettings", menuName = "MagnetRush/WeaponMeleeSettings")]
[ClassLabelSO("近接武器設定")]
public class WeaponMeleeSettings : ScriptableObject
{
    [Header("[拾得]")]
    [Label("磁化中は拾得不可")]
    [Tooltip("磁化中は拾えないようにするかどうか")]
    public bool disablePickupWhileMagnetized = true;

    [Label("再拾得クールダウン（秒）")]
    [Tooltip("地面に落ちた直後、再び拾えるようになるまでの待機時間")]
    public float repickupCooldown = 1.0f;

    [Header("[磁力]")]
    [Label("磁力影響で落下する")]
    [Tooltip("所持中に磁力の影響を受けた場合、武器を強制的に落とすかどうか")]
    public bool dropWhenMagnetAffectedWhileOwned = true;

    [Label("強制ドロップ磁力しきい値")]
    [Tooltip("所持中の武器が落下する磁力影響度のしきい値")]
    public float forcedDropInfluenceThreshold = 0.1f;

    [Header("[物理]")]
    [Label("地面落下時の質量")]
    [Tooltip("地面に落ちている状態での質量")]
    public float groundMass = 1f;

    [Label("地面落下時の移動減衰")]
    [Tooltip("地面に落ちている状態での移動減衰")]
    public float drag = 0f;

    [Label("地面落下時の回転減衰")]
    [Tooltip("地面に落ちている状態での回転減衰")]
    public float angularDrag = 2f;
}
