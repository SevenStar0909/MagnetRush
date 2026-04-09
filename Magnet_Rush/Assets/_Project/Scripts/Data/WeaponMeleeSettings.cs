using UnityEngine;

[CreateAssetMenu(fileName = "WeaponMeleeSettings", menuName = "MagnetRush/WeaponMeleeSettings")]
public class WeaponMeleeSettings : ScriptableObject
{
    [Header("Attack")]
    //武器の基本ダメージ量
    public int damage = 10;

    //次の攻撃を行えるようになるまでの待機時間
    public float attackCooldown = 0.5f;

    //攻撃判定のコライダーを有効にしておく時間
    public float attackActiveTime = 0.15f;

    [Header("Pickup")]
    //磁化中は拾えないようにするかどうか
    public bool disablePickupWhileMagnetized = true;

    //地面に落ちた直後、再び拾えるようになるまでの待機時間
    public float repickupCooldown = 1.0f;

    [Header("Magnet")]
    //所持中に磁力の影響を受けた場合、武器を強制的に落とすかどうか
    public bool dropWhenMagnetAffectedWhileOwned = true;

    //所持中の武器が落下する磁力影響度のしきい値
    public float forcedDropInfluenceThreshold = 0.1f;

    [Header("Physics")]
    //地面に落ちている状態での質量
    public float groundMass = 1f;

    //地面に落ちている状態での移動減衰
    public float drag = 0f;

    //地面に落ちている状態での回転減衰
    public float angularDrag = 2f;
}
