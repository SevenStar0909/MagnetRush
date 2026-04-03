using UnityEngine;

[CreateAssetMenu(fileName = "BulletSettings", menuName = "MagnetRush/BulletSettings")]
public class BulletSettings : ScriptableObject
{
    [Header("弾")]
    [Tooltip("弾の飛行速度（m/s）")]
    public float bulletSpeed = 30f;
    [Tooltip("弾の生存時間（秒）。この時間が経つと自動消滅")]
    public float lifetime = 5f;
    [Tooltip("同時に存在できる弾の最大数。リロード（X）で全弾消去")]
    public int maxBullets = 4;
    [Tooltip("弾のPrefab")]
    public GameObject bulletPrefab;

    [Header("射撃")]
    [Tooltip("射撃レイキャストの最大距離")]
    public float raycastDistance = 200f;
    [Tooltip("磁力場のデフォルト範囲（未使用）")]
    public float defaultMagnetRange = 5f;

    [Header("フォールバック")]
    [Tooltip("ONの場合、敵に当たっても弾を消さず壁と同じくくっつく")]
    public bool useFallbackMode = false;

    [Header("磁力場")]
    [Tooltip("弾着弾時に生成するMagnetFieldの設定")]
    public MagnetFieldSettings bulletFieldSettings;
    [Tooltip("フィールドによる飛行中弾道の曲げ強度")]
    public float fieldAttractionFactor = 5f;
    [Tooltip("フィールドへの蓄積ダメージ値")]
    public float bulletDamage = 10f;

    [Header("弾マテリアル")]
    [Tooltip("S極弾のマテリアル（赤）")]
    public Material sMaterial;
    [Tooltip("N極弾のマテリアル（青）")]
    public Material nMaterial;

    [Header("発射時エフェクト")]
    [Tooltip("N極 発射時エフェクトPrefab")]
    public GameObject fireEffect_N;
    [Tooltip("S極 発射時エフェクトPrefab")]
    public GameObject fireEffect_S;
    [Tooltip("発射時エフェクトの大きさの倍率")]
    public float fireEffectScale = 1.3f;

    [Header("着弾時エフェクト")]
    [Tooltip("N極 着弾時エフェクトPrefab")]
    public GameObject impactEffect_N;
    [Tooltip("S極 着弾時エフェクトPrefab")]
    public GameObject impactEffect_S;
    [Tooltip("着弾時エフェクトの大きさの倍率")]
    public float impactEffectScale = 1.3f;
}
