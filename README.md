# Magnet Rush

## フォルダ構成

```
Assets/
├── _Project/              … ゲーム本体のアセット（チーム共有）
│   ├── Asset/             … アセット素材
│   │   ├── Animations/        アニメーションクリップ・コントローラ
│   │   ├── Audio/             サウンド素材
│   │   │   ├── BGM/           BGM
│   │   │   ├── SE/            効果音
│   │   │   └── Voice/         ボイス
│   │   ├── Effect/            エフェクト素材
│   │   ├── Fonts/             フォント
│   │   ├── Materials/         マテリアル
│   │   ├── Models/            3Dモデル (FBX等)
│   │   ├── Sprites/           2Dスプライト
│   │   └── Textures/          テクスチャ
│   ├── Prefabs/           … 再利用可能なプレハブ
│   │   ├── _Common/           汎用・共通プレハブ
│   │   ├── Bullet/            弾プレハブ
│   │   ├── Debug/             デバッグ用
│   │   ├── Effect/            エフェクトプレハブ
│   │   ├── Enemy/             敵プレハブ
│   │   ├── Object/            オブジェクトプレハブ
│   │   ├── Player/            プレイヤープレハブ
│   │   ├── Stage/             ステージ・背景オブジェクト
│   │   └── UI/                UIパーツ
│   ├── Scenes/            … シーンファイル
│   │   ├── GameScene.unity
│   │   ├── MapScene.unity
│   │   ├── TitleScene.unity
│   │   ├── TestScene.unity
│   │   └── Member/            メンバー別作業シーン
│   ├── Scripts/           … C#スクリプト
│   │   ├── Common/            共通ユーティリティ
│   │   ├── Core/              コアシステム
│   │   ├── Debug/             デバッグツール
│   │   ├── Game/              ゲームロジック
│   │   ├── Rendering/         レンダリング関連
│   │   ├── Settings/          設定クラス
│   │   └── UI/                UI関連
│   ├── ScriptableObjects/ … ScriptableObject データアセット
│   ├── Shaders/           … カスタムシェーダー
│   └── ~Sandbox/          … 個人実験用（本番に含めない）
├── _Sandbox/              … 個人実験用（グローバル）
├── Editor/                … エディタ拡張スクリプト
├── Nova/                  … Nova UIフレームワーク
├── Settings/              … URP等のレンダリング設定
├── TextMesh Pro/          … TextMeshProアセット
└── ThirdParty/            … 外部アセット・プラグイン
```
