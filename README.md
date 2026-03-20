# Magnet Rush

## フォルダ構成

```
Assets/
├── _Project/          … ゲーム本体のアセット（チーム共有）
│   ├── Asset/         … アセット素材
│   │   ├── Animations/    アニメーションクリップ・コントローラ
│   │   ├── Audio/         サウンド素材
│   │   │   ├── BGM/       BGM
│   │   │   ├── SE/        効果音
│   │   │   └── Voice/     ボイス
│   │   ├── Fonts/         フォント
│   │   ├── Materials/     マテリアル
│   │   ├── Models/        3Dモデル (FBX等)
│   │   ├── Shaders/       カスタムシェーダー
│   │   ├── Sprites/       2Dスプライト
│   │   └── Textures/      テクスチャ
│   ├── Prefabs/       … 再利用可能なプレハブ
│   │   ├── Characters/    キャラクター
│   │   ├── Stage/         ステージ・背景オブジェクト
│   │   ├── UI/            UIパーツ
│   │   └── _Common/       汎用・共通プレハブ
│   ├── Scenes/        … シーンファイル
│   │   └── Member/        メンバー別作業シーン
│   ├── Scripts/       … C#スクリプト
│   ├── ~Data/         … ScriptableObject等のデータアセット
│   └── ~Sandbox/      … 個人実験用（本番に含めない）
├── Editor/            … エディタ拡張スクリプト
├── Settings/          … URP等のレンダリング設定
└── ThirdParty/        … 外部アセット・プラグイン
```
