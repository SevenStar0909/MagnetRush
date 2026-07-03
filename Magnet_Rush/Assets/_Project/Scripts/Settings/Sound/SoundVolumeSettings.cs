using UnityEngine;

/// <summary>
/// サウンド音量のプランナー用調整シート。全ての音を1音1項目で個別調整する。
/// SoundManager が再生時に「全体音量 × 各音の音量」を適用する。
/// 「Tools/サウンド調整シート」ウィンドウなら各音をその場で試聴できる（Inspectorでも編集可）。
/// </summary>
[CreateAssetMenu(menuName = "MagnetRush/SoundVolumeSettings")]
[ClassLabelSO("サウンド音量調整シート")]
public class SoundVolumeSettings : ScriptableObject
{
    [Header("[全体]")]
    [LabelRange("SE全体の音量", 0f, 1f)]
    [Tooltip("ゲーム中のSE全部にかかる音量。全体的にうるさい時はまずここを下げる")]
    public float seMasterVolume = 0.5f;

    [LabelRange("BGM全体の音量", 0f, 1f)]
    [Tooltip("BGM全部にかかる音量")]
    public float bgmMasterVolume = 0.8f;

    [Header("[プレイヤー]")]
    [LabelRange("磁力弾の射撃音", 0f, 2f)]
    [Tooltip("1で原音、0.5で半分、2で2倍。以下すべて同じ")]
    public float playerShot = 0.1f;

    [LabelRange("セルフショット（自分に撃つ）", 0f, 2f)]
    public float selfShot = 1f;

    [LabelRange("エイム（構え）", 0f, 2f)]
    public float aim = 1f;

    [LabelRange("リロード", 0f, 2f)]
    public float reload = 1f;

    [LabelRange("磁極の切り替え", 0f, 2f)]
    public float magChange = 1f;

    [LabelRange("足音", 0f, 2f)]
    [Tooltip("歩き足音4種（PlayerFoot01〜04）共通の音量")]
    public float playerFoot = 0.1f;

    [LabelRange("スタブ（突き刺し）", 0f, 2f)]
    public float stab = 1f;

    [Header("[磁力]")]
    [LabelRange("磁力場のループ音", 0f, 2f)]
    public float magField = 1f;

    [LabelRange("弾の着弾音", 0f, 2f)]
    public float hitObject = 1f;

    [Header("[敵]")]
    [LabelRange("タレットの砲撃", 0f, 2f)]
    public float turretShot = 1f;

    [LabelRange("タレット弾の着弾", 0f, 2f)]
    public float turretImpact = 1f;

    [LabelRange("ダイソン敵のループ音", 0f, 2f)]
    public float dysonEnemy = 1f;

    [LabelRange("ドローン敵の飛行ループ音", 0f, 2f)]
    public float droneEnemy = 0.5f;

    [LabelRange("車輪敵の走行ループ音", 0f, 2f)]
    public float enemyWheel = 0.5f;

    [Header("[ボス]")]
    [LabelRange("ミサイル発射", 0f, 2f)]
    public float missileShot = 1f;

    [LabelRange("ミサイル着弾", 0f, 2f)]
    public float missileImpact = 1f;

    [LabelRange("ボスの叩きつけ", 0f, 2f)]
    public float bossEnemySlam = 1f;

    [LabelRange("ボスの足音", 0f, 2f)]
    public float bossEnemyFoot = 1f;

    [Header("[UI]")]
    [LabelRange("決定ボタン", 0f, 2f)]
    public float button = 1f;

    [LabelRange("カーソル移動", 0f, 2f)]
    public float select = 1f;

    [LabelRange("ステージ選択", 0f, 2f)]
    public float stageSelect = 1f;

    [Header("[BGM]")]
    [LabelRange("ボス戦BGM", 0f, 2f)]
    public float bossBattleBgm = 1f;

    [LabelRange("タイトルBGM", 0f, 2f)]
    public float titleBgm = 1f;

    [LabelRange("ステージセレクトBGM", 0f, 2f)]
    public float stageSelectBgm = 1f;

    [LabelRange("チュートリアルBGM", 0f, 2f)]
    public float tutorialBgm = 1f;

    [LabelRange("ステージ１BGM", 0f, 2f)]
    public float stage01Bgm = 1f;

    [Header("[3D距離減衰]")]
    [LabelMin("磁力場ループ音：フル音量の距離(m)", 0f)]
    [Tooltip("この距離までは減衰せずフル音量。ここから先は離れるほど小さくなる")]
    public float magFieldMinDistance = 4f;

    [LabelMin("磁力場ループ音：聞こえなくなる距離(m)", 0f)]
    [Tooltip("この距離まで離れるとほぼ聞こえなくなる")]
    public float magFieldMaxDistance = 25f;

    [LabelMin("弾の着弾音：フル音量の距離(m)", 0f)]
    public float hitObjectMinDistance = 4f;

    [LabelMin("弾の着弾音：聞こえなくなる距離(m)", 0f)]
    public float hitObjectMaxDistance = 30f;

    [LabelMin("ボスSE：フル音量の距離(m)", 0f)]
    [Tooltip("ボスの足音・叩きつけ・ミサイル発射に共通の減衰距離")]
    public float bossSeMinDistance = 6f;

    [LabelMin("ボスSE：聞こえなくなる距離(m)", 0f)]
    public float bossSeMaxDistance = 60f;

    /// <summary>SEキューの最終音量（SE全体 × 各音の音量）を返す。未登録キューは全体音量のみ。</summary>
    public float GetSeVolume(string cueName) => seMasterVolume * GetSeCueScale(cueName);

    /// <summary>BGMキューの最終音量（BGM全体 × 各音の音量）を返す。未登録キューは全体音量のみ。</summary>
    public float GetBgmVolume(string cueName) => bgmMasterVolume * GetBgmCueScale(cueName);

    // SoundData に新しいキューを追加したら、フィールドとこの対応表にも1行足すこと
    private float GetSeCueScale(string cueName)
    {
        switch (cueName)
        {
            case SoundData.SE.PlayerShot: return playerShot;
            case SoundData.SE.SelfShot: return selfShot;
            case SoundData.SE.Aim: return aim;
            case SoundData.SE.Reload: return reload;
            case SoundData.SE.MagChange: return magChange;
            case SoundData.SE.Stab: return stab;
            case SoundData.SE.PlayerFoot01:
            case SoundData.SE.PlayerFoot02:
            case SoundData.SE.PlayerFoot03:
            case SoundData.SE.PlayerFoot04: return playerFoot;
            case SoundData.SE.MagField: return magField;
            case SoundData.SE.HitObject: return hitObject;
            case SoundData.SE.TurretShot: return turretShot;
            case SoundData.SE.TurretImpact: return turretImpact;
            case SoundData.SE.DysonEnemy: return dysonEnemy;
            case SoundData.SE.DroneEnemy: return droneEnemy;
            case SoundData.SE.EnemyWheel: return enemyWheel;
            case SoundData.SE.MissileShot: return missileShot;
            case SoundData.SE.MissileImpact: return missileImpact;
            case SoundData.SE.BossEnemySlam: return bossEnemySlam;
            case SoundData.SE.BossEnemyFoot: return bossEnemyFoot;
            case SoundData.SE.Button: return button;
            case SoundData.SE.Select: return select;
            case SoundData.SE.StageSelect: return stageSelect;
            default: return 1f;
        }
    }

    private float GetBgmCueScale(string cueName)
    {
        switch (cueName)
        {
            case SoundData.BGM.BossBattle: return bossBattleBgm;
            case SoundData.BGM.Title: return titleBgm;
            case SoundData.BGM.StageSelect: return stageSelectBgm;
            case SoundData.BGM.Tutorial: return tutorialBgm;
            case SoundData.BGM.Stage01: return stage01Bgm;
            default: return 1f;
        }
    }

    /// <summary>
    /// キューの3D減衰距離 (フル音量距離, 聞こえなくなる距離) を返す。
    /// 未登録キューは既定の (4, 35)。タレット・ドローン・車輪は各コンポーネント側の設定を使うのでここには無い。
    /// </summary>
    public Vector2 GetCueDistance(string cueName)
    {
        switch (cueName)
        {
            case SoundData.SE.MagField: return new Vector2(magFieldMinDistance, magFieldMaxDistance);
            case SoundData.SE.HitObject: return new Vector2(hitObjectMinDistance, hitObjectMaxDistance);
            case SoundData.SE.BossEnemyFoot:
            case SoundData.SE.BossEnemySlam:
            case SoundData.SE.MissileShot: return new Vector2(bossSeMinDistance, bossSeMaxDistance);
            default: return new Vector2(4f, 35f);
        }
    }
}
