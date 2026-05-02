using UnityEngine;

[CreateAssetMenu(fileName = "BulletSettings", menuName = "MagnetRush/BulletSettings")]
public class BulletSettings : ScriptableObject
{
    [Header("[弾]")]
    [Label("飛行速度（m/s）")]
    [Tooltip("弾の飛行速度（m/s）")]
    public float bulletSpeed = 30f;
    [Label("生存時間（秒）")]
    [Tooltip("弾の生存時間（秒）。この時間が経つと自動消滅")]
    public float lifetime = 5f;
    [Label("最大同時存在数")]
    [Tooltip("同時に存在できる弾の最大数。リロード（X）で全弾消去")]
    public int maxBullets = 4;
    [Label("弾Prefab")]
    [Tooltip("弾のPrefab")]
    public GameObject bulletPrefab;

    [Header("[射撃]")]
    [Label("レイキャスト最大距離")]
    [Tooltip("射撃レイキャストの最大距離")]
    public float raycastDistance = 200f;
    [Label("磁力場デフォルト範囲（未使用）")]
    [Tooltip("磁力場のデフォルト範囲（未使用）")]
    public float defaultMagnetRange = 5f;

    [Header("[フォールバック]")]
    [Label("敵にくっつくモード")]
    [Tooltip("ONの場合、敵に当たっても弾を消さず壁と同じくくっつく")]
    public bool useFallbackMode = false;

    [Header("[磁力場]")]
    [Label("着弾時磁力場設定")]
    [Tooltip("弾着弾時に生成するMagnetFieldの設定")]
    public MagnetFieldSettings bulletFieldSettings;
    [Label("飛行中の曲げ強度")]
    [Tooltip("フィールドによる飛行中弾道の曲げ強度")]
    public float fieldAttractionFactor = 5f;
    [Label("蓄積ダメージ値")]
    [Tooltip("フィールドへの蓄積ダメージ値")]
    public float bulletDamage = 10f;

    [Header("[弾マテリアル]")]
    [Label("S極マテリアル（赤）")]
    [Tooltip("S極弾のマテリアル（赤）")]
    public Material sMaterial;
    [Label("N極マテリアル（青）")]
    [Tooltip("N極弾のマテリアル（青）")]
    public Material nMaterial;

    [Header("[発射時エフェクト]")]
    [Label("N極 発射エフェクト")]
    [Tooltip("N極 発射時エフェクトPrefab")]
    public GameObject fireEffect_N;
    [Label("S極 発射エフェクト")]
    [Tooltip("S極 発射時エフェクトPrefab")]
    public GameObject fireEffect_S;
    [Label("発射エフェクト倍率")]
    [Tooltip("発射時エフェクトの大きさの倍率")]
    public float fireEffectScale = 1.3f;

    [Header("[着弾時エフェクト]")]
    [Label("N極 着弾エフェクト")]
    [Tooltip("N極 着弾時エフェクトPrefab")]
    public GameObject impactEffect_N;
    [Label("S極 着弾エフェクト")]
    [Tooltip("S極 着弾時エフェクトPrefab")]
    public GameObject impactEffect_S;
    [Label("着弾エフェクト倍率")]
    [Tooltip("着弾時エフェクトの大きさの倍率")]
    public float impactEffectScale = 1.3f;
}
