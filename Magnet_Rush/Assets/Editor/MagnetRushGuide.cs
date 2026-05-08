using UnityEditor;
using UnityEngine;

/// <summary>
/// MagnetRushプロジェクトガイド。MagnetRush > ガイド で開く。
/// チームメンバー向けのシステム説明・操作方法リファレンス。
/// </summary>
public class MagnetRushGuide : EditorWindow
{
    private Vector2 m_scrollPos;
    private int m_selectedTab;
    private static readonly string[] k_TabNames =
    {
        "概要", "操作", "磁力システム", "弾・射撃", "Entity", "レベル設計", "デバッグ"
    };

    [MenuItem("MagnetRush/ガイド")]
    static void Open()
    {
        var window = GetWindow<MagnetRushGuide>("MagnetRush ガイド");
        window.minSize = new Vector2(380, 420);
    }

    void OnGUI()
    {
        m_selectedTab = GUILayout.Toolbar(m_selectedTab, k_TabNames);
        m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);

        switch (m_selectedTab)
        {
            case 0: DrawOverview(); break;
            case 1: DrawControls(); break;
            case 2: DrawMagnetSystem(); break;
            case 3: DrawBullet(); break;
            case 4: DrawEntity(); break;
            case 5: DrawLevelDesign(); break;
            case 6: DrawDebug(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawOverview()
    {
        Section("MagnetRush とは");
        Bullet("TPS（三人称視点）3Dアクションゲーム");
        Bullet("磁力弾を撃ってオブジェクトや自分に磁力を付与");
        Bullet("S極・N極の引力/斥力でステージを攻略");
        Bullet("4人チーム制作のプロトタイプ");

        Section("ゲームの流れ");
        Bullet("① プレイヤーが磁力弾を壁やオブジェクトに撃つ");
        Bullet("② 着弾点にMagnetField（磁場）が発生");
        Bullet("③ 範囲内の磁化オブジェクトに引力/斥力が働く");
        Bullet("④ 自分にも磁力を付与できる（SelfFire）");
        Bullet("⑤ 磁力を使ってパズルを解いたり敵を倒す");

        Section("主要コンポーネント");
        Row("Player", "プレイヤー操作（移動・射撃・極性切替）");
        Row("MagnetBullet", "磁力弾。壁に貼り付いて磁場を展開");
        Row("MagnetField", "磁場。inner/outerRadiusで力が線形減衰");
        Row("MagnetManager", "全磁化ペアの力計算を一括管理");
        Row("Magnetizable", "磁力の影響を受けるコンポーネント");
        Row("Entity", "全キャラクター/オブジェクトの基底クラス");
        Row("EntityController", "Collide-and-Slide式の衝突処理");
        Row("BulletManager", "弾数管理・リロード");

        Section("フォルダ構造");
        Row("Scripts/Player/", "プレイヤー関連（入力・移動・射撃・カメラ）");
        Row("Scripts/Bullet/", "弾・弾管理");
        Row("Scripts/Magnet/", "磁力システム（Manager・Field・Magnetizable）");
        Row("Scripts/Entity/", "Entity基底・Controller・状態管理");
        Row("Scripts/Data/", "ScriptableObject（設定値全般）");
        Row("Scripts/UI/", "UI（残弾・デバッグ）");
        Row("Scripts/Game/", "GameManager（リスタート・リスポーン）");
    }

    void DrawControls()
    {
        Section("プレイヤー操作");
        Row("左スティック / WASD", "移動");
        Row("右スティック / マウス", "カメラ操作");
        Row("RT / 左クリック", "射撃（磁力弾を発射）");
        Row("LT / 右クリック", "エイム（スロー＋ズーム）");
        Row("A / F", "SelfFire（自分に磁力付与）");
        Row("Y / Q", "極性切替（S極 ⇔ N極）");
        Row("X / R", "リロード（全弾回収）");

        Section("デバッグ操作");
        Row("F1", "デバッグUI表示/非表示");
        Row("F5", "シーンリスタート");
        Row("1", "スポーン地点にテレポート");

        Section("極性の仕組み");
        Bullet("S極（赤）とN極（青）の2種類");
        Bullet("同極 = 斥力（反発）、異極 = 引力（引き合う）");
        Bullet("Yボタン / Qキーで切替");
        Bullet("弾は現在の極性で発射される");
    }

    void DrawMagnetSystem()
    {
        Section("磁力の仕組み");
        Bullet("弾が壁に着弾 → MagnetField（磁場）が展開される");
        Bullet("磁場にはinnerRadius（内径）とouterRadius（外径）がある");
        Bullet("内径以内 = 最大の力、外径まで線形減衰");

        Section("磁場の可視化（開発中のスクショ参照）");
        Bullet("青いワイヤーフレーム円 = N極の磁場範囲");
        Bullet("赤いワイヤーフレーム円 = S極の磁場範囲");
        Bullet("内側の円 = innerRadius（最大力）");
        Bullet("外側の円 = outerRadius（力の到達限界）");

        Section("力の適用順");
        Bullet("① IMagneticResponse（カスタム応答）");
        Bullet("② IMagnetTarget（Entity基底が実装。ApplyMagnetForce）");
        Bullet("③ Rigidbody.AddForceAtPosition（物理オブジェクト）");
        Bullet("③の場合、表面最近点に力を適用 → トルクで回転する");

        Section("MagnetManager");
        Bullet("全Magnetizableを登録し、ペアごとの力を計算");
        Bullet("FixedUpdateで毎フレーム処理");
        Bullet("接触距離のペアにはOnMagnetContactコールバック");

        Section("Magnetizable コンポーネント");
        Bullet("磁力の影響を受けるオブジェクトに付ける");
        Bullet("SetPole(S/N) で磁化 → アウトラインが表示される");
        Bullet("Deactivate() で消磁 → セトリングフェーズ（直立復帰）");
        Bullet("mass: 質量。壁に固定された弾はInfinity");

        Section("設定値（ScriptableObject）");
        Row("MagnetSettings", "magnetForce / magnetRange");
        Row("BulletMagnetFieldSettings", "innerRadius / outerRadius / lifetime");
    }

    void DrawBullet()
    {
        Section("弾の仕組み");
        Bullet("ShootingAbility が画面中央方向に弾を生成");
        Bullet("平面交差法で正確なターゲット座標を算出（TPS補正）");
        Bullet("弾はMagnetBulletコンポーネントで管理");

        Section("弾のライフサイクル");
        Bullet("① 生成: Instantiate + Initialize(pole, direction)");
        Bullet("② 飛行中: linearVelocity で直進。磁場の影響を受ける");
        Bullet("③ 着弾パターン1: 壁に貼り付き → MagnetField展開");
        Bullet("④ 着弾パターン2: 磁化対象に当たる → 対象を磁化して消滅");

        Section("SelfFire（自分を磁化）");
        Bullet("Aボタン / Fキー で発動");
        Bullet("プレイヤーの中心付近に下向きの弾を生成");
        Bullet("プレイヤー自身にMagnetField＋磁化が付与される");
        Bullet("壁のS極に向かって自分をN極で引き寄せる等の戦術");

        Section("弾数管理");
        Row("最大弾数", "BulletSettings.maxBullets（デフォルト4）");
        Row("残弾UI", "AmmoUI で表示（S極=赤、N極=青のスプライト）");
        Row("リロード", "Xボタン / Rキー → 全弾回収 + 磁場消滅");

        Section("弾のメッシュ");
        Bullet("Grenade_launcher/FBX/Bullet.fbx を使用");
        Bullet("プレハブ: Prefabs/Bullet/MagnetBullet.prefab");
        Bullet("ルートにRigidbody+SphereCollider+スクリプト");
        Bullet("子Modelにメッシュ（X軸90度回転で向き補正）");

        Section("設定値（ScriptableObject）");
        Row("BulletSettings", "speed / lifetime / maxBullets / damage");
        Row("", "sMaterial / nMaterial（S極赤・N極青）");
        Row("", "fireEffect / impactEffect（エフェクト）");
    }

    void DrawEntity()
    {
        Section("Entity（基底クラス）");
        Bullet("全キャラクター・オブジェクトの共通基盤");
        Bullet("接地判定、重力、磁力向き、移動を統合管理");
        Bullet("EntityStep() で1フレームの処理を実行");

        Section("EntityStep の処理順");
        Bullet("① UpdateGround — 接地判定");
        Bullet("② HandleCeiling — 天井衝突");
        Bullet("③ ApplyGravity — 重力適用");
        Bullet("④ UpdateMagneticOrientation — 磁力方向に向く（水平のみ）");
        Bullet("⑤ ApplyMovement — 最終移動");
        Bullet("⑥ HandleContacts — 接触コールバック");

        Section("EntityController（衝突処理）");
        Bullet("Kinematic Rigidbody + CapsuleCast によるCollide-and-Slide");
        Bullet("壁/床/天井の衝突を自前で処理（物理エンジンに頼らない）");
        Bullet("オブジェクトを押す機能あり（Push）");

        Section("Push（押し出し）");
        Bullet("プレイヤーがオブジェクトに触れると押す");
        Bullet("hit.normalベースの角度判定（dot < 0.7 ≒ 45度）");
        Bullet("押し中はオブジェクトをkinematicに切替 → transform.position移動");
        Bullet("壁チェック（BoxCast）で壁にめり込まない");

        Section("EntityEvents");
        Bullet("OnGroundEnter / OnGroundExit（UnityEvent）");
        Bullet("Inspectorからイベント接続可能");

        Section("IEntityContact");
        Bullet("Entity接触時の疎結合コールバック");
        Bullet("OnEntityContact(Entity entity) を実装");
    }

    void DrawLevelDesign()
    {
        Section("レベル設計のポイント（磁力ゲーム向け）");

        Section("壁面");
        Bullet("磁力弾が壁に貼り付く → 壁面積は広い方が戦略的");
        Bullet("壁の厚みがありすぎると磁力が反対側に届かない");
        Bullet("角度のある壁 → 磁力の方向が斜めになり戦術幅が広がる");

        Section("カバーオブジェクト");
        Row("ハーフウォール", "1.0〜1.2m（しゃがみ）、壁面に弾を撃てる高さ");
        Row("スタンドカバー", "1.5〜1.8m、立って撃つ用");
        Row("厚みの目安", "0.3〜0.5m。薄すぎると磁力が貫通感あり");

        Section("高低差");
        Bullet("上から壁に弾を撃ち下ろす → 磁力で下のオブジェクトを引き上げ");
        Bullet("SelfFireで自分を磁化 → 高所の壁に引き寄せて登る");
        Bullet("高低差は3m程度。2〜3段まで");

        Section("可動オブジェクト");
        Bullet("Magnetizable + Rigidbody のオブジェクトを配置");
        Bullet("磁力で動かしてパズルを解く/道を塞ぐ/敵にぶつける");
        Bullet("EntityControllerのPushでも動かせる");
        Bullet("質量（mass）で動かしやすさを調整");

        Section("磁場範囲を意識した空間");
        Bullet("MagnetFieldのouterRadius = 磁力の最大到達距離");
        Bullet("部屋が狭すぎると全域が磁場の影響下 → 大味になる");
        Bullet("適度な広さ（outerRadius×2〜3の部屋幅）が戦術的");
        Bullet("柱や仕切り壁で磁場を遮る構造も有効");

        Section("テストのポイント");
        Bullet("壁に弾を撃って磁場の広がりを確認（青/赤ワイヤーフレーム）");
        Bullet("SelfFireで自分を引き寄せて移動できるか");
        Bullet("可動オブジェクトが磁力で動くか");
        Bullet("カバー越しの磁力引き寄せが機能するか");
    }

    void DrawDebug()
    {
        Section("デバッグUI（F1で表示）");
        Bullet("磁力（magnetForce）のスライダー: 1〜100");
        Bullet("範囲（magnetRange）のスライダー: 1〜50");
        Bullet("現在の弾数表示");

        Section("デバッグキー");
        Row("F1", "デバッグUI表示切替");
        Row("F5", "シーンリスタート");
        Row("1", "スポーン地点にテレポート");

        Section("コンソールログ");
        Bullet("[Bullet] → Pattern1/2: 弾の着弾パターン");
        Bullet("[MaterialAutoConverter]: マテリアル自動変換");
        Bullet("[Magnetizable] Rendererがありません: アウトライン設定失敗");

        Section("よくある問題");
        Row("弾が止まる", "SphereColliderのradiusがscaleで巨大化");
        Row("プレイヤーが傾く", "UpdateMagneticOrientationは水平方向のみ");
        Row("オブジェクト押しが粘る", "hit.normalベースの角度判定を確認");
        Row("残弾が回復する", "m_shotCountベースで管理（activeBulletsではない）");
        Row("マゼンタ表示", "Built-in StandardシェーダーをURP Litに変換");

        Section("設定ファイル（ScriptableObject）");
        Row("PlayerSettings", "移動速度・ジャンプ・カメラ");
        Row("BulletSettings", "弾速・弾数・ダメージ・マテリアル");
        Row("MagnetSettings", "磁力・範囲");
        Row("BulletMagnetFieldSettings", "磁場の内径・外径・寿命");

        Section("レイヤー構成（PhysicsLayers.cs）");
        Row("Player (8)", "プレイヤー");
        Row("Ground (6)", "地面");
        Row("Wall (9)", "壁");
        Row("Bullet (10)", "弾");
        Row("MagnetField (11)", "磁場トリガー");
        Row("Magnetized (12)", "磁化オブジェクト");
    }

    static void Section(string title)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
    }

    static void Row(string key, string desc)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(key, EditorStyles.miniLabel, GUILayout.Width(150));
        EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndHorizontal();
    }

    static void Bullet(string text)
    {
        EditorGUILayout.LabelField("  • " + text, EditorStyles.wordWrappedLabel);
    }
}
