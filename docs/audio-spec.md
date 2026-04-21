# MagnetRush サウンド実装仕様書

**対象読者**: プログラマ / サウンドデザイナー
**採用技術**: CRI ADX2 LE（無償版サウンドミドルウェア）
**最終更新**: 2026-04-22

---

## 実装フェーズ

このドキュメントは**最終的な完成形の設計**を記す。実装は段階的に進める。

| フェーズ | 作業 | 着手条件 |
|---|---|---|
| **フェーズ1** | CRI SDK導入 + 基盤セットアップ（プログラマ） | 即着手 |
| **フェーズ2** | 音データ配置 + Inspector配線 + Binder実装 + AISAC/BGM/Snapshot制御 | デザイナーからACB/ACF受領後 |

**フェーズ1の具体手順**: [`audio-task-brief.md`](./audio-task-brief.md)

以下の章は**フェーズ2の実装で参照する設計書**。フェーズ1段階では読み物として目を通すだけでよい。

---

## 0. 採用技術とコア方針

### なぜ CRI ADX2 LE か

MagnetRushのコア体験である**磁力の動的連動**（引力強度→音量・ピッチ、ホールド時間→音響変化）や**適応BGM**を、Unity標準だけで組むとプログラマの負荷が大きい。CRI ADX2 LEはこれらを Atom Craft（デザイナー専用ツール、日本語UI）側で完結させられる。

- **問い合わせ不要**、SDK直ダウンロード可
- **無償利用可**（前年度年商1,000万円以下の会社/団体/個人）
- **日本語UI**
- サポート: Discord CRIWARE User Community

### コア方針

1. **設計は「薄く」組む** — CRIが音の仕組みを内蔵しているので、プログラマは「使うだけ」にする
2. **音の追加・調整はデザイナー完結** — Atom Craftで音を足しても、プログラマのコード変更は不要
3. **プログラマは Core → CRI の呼び出しだけ書く** — ラッパーは最小限（ワンショット再生ヘルパー1個のみ）

---

## 1. 全体アーキテクチャ

```
┌──────────────────────────────────────────────┐
│ Atom Craft（デザイナー専用、Unity外のツール）  │
│                                               │
│  Cue Sheet設計 / Cue作成（音源+パラメータ）    │
│  AISAC設計（動的変化カーブ）                   │
│  Category（SFX/BGM/UI音量グループ）            │
│  DSP Bus Snapshot（シーン別音響切替）          │
│                                               │
│  ビルド → ACF / ACB / AWB ファイル            │
└──────────────────────┬───────────────────────┘
                       │ StreamingAssets に配置
                       ↓
┌──────────────────────────────────────────────┐
│ Unity プロジェクト                             │
│                                               │
│  [Boot シーン]                                 │
│    ├─ CriWareLibraryInitializer（1回のみ）     │
│    └─ CriAtom（CueSheet登録）                  │
│                                               │
│  [ゲームシーン]                                │
│    ├─ CriAtomListener（Mainカメラにアタッチ）  │
│    ├─ CriAtomSource（各GameObjectにアタッチ）  │
│    └─ AudioOneShot（静的ヘルパー、自作1個）    │
│                                               │
│  [Core]                                       │
│    ├─ PlayerEvents.OnShoot（UnityEvent）       │
│    │   → Inspectorで CriAtomSource.Play 直接繋ぎ │
│    └─ event Action（Health.OnDamage等）        │
│        → AudioBinder で購読 → AudioOneShot    │
└──────────────────────────────────────────────┘
```

---

## 2. 責務分担

### サウンドデザイナー（Unity 触らない、Atom Craft専任）

| 作業 | 成果物 |
|---|---|
| Cue Sheet 設計（SE/BGM/UI） | `MagnetRush.acb` / `.awb` |
| プロジェクト設定（Category/DSP Bus/AISAC定義） | `MagnetRush.acf` |
| AISAC作成（引力→音量、HP→BGM強度等の動的カーブ） | Cueに紐付けた制御データ |
| DSP Bus Snapshot作成（`Default`/`LowHp`/`Combat`等） | Snapshot定義 |
| 音源選定・ミックス | `.wav` / `.ogg` |

**Unityに触るのは ACB/ACF/AWB を StreamingAssets に配置する時だけ。**

### プログラマ（メンバー1名）

| 作業 | 成果物 |
|---|---|
| CRI SDK導入（Unity Package Import） | `Assets/Plugins/CriWare/` |
| Bootシーンへの CriWareLibraryInitializer / CriAtom 配置 | シーン更新 |
| Main カメラへの CriAtomListener 配置 | シーン更新 |
| `AudioOneShot.cs` 実装（ワンショット3D再生ヘルパー） | 1ファイル、約100行 |
| PlayerEvents（UnityEvent）への CriAtomSource 接続設定 | Inspector設定、コード不要 |
| `event Action` 系への購読（Binderクラス） | 数ファイル、各20-50行 |
| 磁力強度等のAISAC毎フレーム更新 | MagnetManager等のUpdateに1-2行 |
| BGM/Category/Snapshot切替（GameManager等から） | 既存マネージャーに1-2行 |

### 西川（レビュー・統合のみ）

- 仕様書メンテ、実装レビュー、統合動作確認

---

## 3. Atom Craft 側（デザイナー作業範囲）

### Cue Sheet 構成（推奨）

```
MagnetRush.acb
├── Player/
│   ├── Shoot          （ポリフォニックキュー、5種ランダム）
│   ├── SelfShoot
│   ├── PoleSwitch
│   ├── Reload
│   └── Hurt
├── MagnetBullet/
│   ├── Launch         （発射音）
│   ├── HitStatic      （壁/地面ヒット）
│   ├── HitEnemy       （敵ヒット）
│   └── Deflect        （反発）
├── Enemy/
│   ├── Spawn
│   ├── Attack
│   ├── Hurt
│   └── Die
├── Magnet/
│   ├── FieldLoop      （ループ、磁力場の寿命で音量減衰）
│   ├── AttractLoop    （ループ、AISAC_Force で音量・ピッチ連動）
│   ├── HoldStart
│   ├── HoldLoop
│   └── HoldRelease
├── UI/
│   ├── Select
│   ├── Confirm
│   ├── Cancel
│   └── Pause
└── BGM/
    ├── Title
    ├── Stage_Main     （適応BGM、AISAC_CombatIntensityで強度変化）
    └── Boss
```

### Category 構成

```
Master
├── SFX
│   ├── Player
│   ├── Enemy
│   ├── Magnet
│   └── Environment
├── BGM
├── UI
└── Ambience
```

各 Category に Volume が設定可能。OptionsUI から `CriAtom.SetCategoryVolume("BGM", 0.7f)` で制御。

### AISAC 設計（動的連動の要）

| AISAC名 | 入力元（Unity側） | 連動する音パラメータ | 用途 |
|---|---|---|---|
| `AISAC_MagnetForce` | `MagnetManager.ForceNormalized` (0-1) | AttractLoop の音量・ピッチ | 引力強度→音響 |
| `AISAC_HoldDuration` | ホールド継続時間 (0-1) | HoldLoopの音量・エフェクト | 長押しの重厚化 |
| `AISAC_PlayerHp` | `Health.HpRatio` (0-1) | BGMフィルタ、SFXローパス | ピンチ演出 |
| `AISAC_CombatIntensity` | 近接敵数など (0-1) | BGMレイヤーミックス | 戦闘強度 |

**デザイナーがAtom Craftで定義 → プログラマは Unity側で数値を `SetAisacControl` で渡すだけ。**

### DSP Bus Snapshot

| Snapshot名 | トリガ | 音響変化 |
|---|---|---|
| `Default` | 通常時 | プリセット |
| `LowHp` | HP < 30% | BGMにLPF、SFX減衰 |
| `Pause` | ポーズ中 | 全体音量-20dB |
| `Combat_Boss` | ボス戦 | BGM強調、Reverb追加 |

切替コード: `CriAtom.AttachDspBusSetting("LowHp")`

---

## 4. Unity 側（プログラマ作業範囲）

### 4.1 シーンセットアップ

**Boot シーン**（ゲーム起動時1回だけ通る）:
```
CriWareLibraryInitializer（GameObjectにアタッチ、Inspector設定のみ）
CriAtom
  └── Cue Sheet: MagnetRush.acb（Inspector で登録）
```

**Main カメラ**:
```
CriAtomListener（AddComponent、設定不要）
```

### 4.2 音の鳴らし方（3パターン）

#### パターンA: PlayerEvents（UnityEvent）→ Inspector直接繋ぎ

既存 `PlayerEvents.cs` は `UnityEvent` 化済み。音を鳴らすだけなら**コード不要、Inspector のみ**。

```
Player GameObject:
  ├── PlayerEvents
  │     OnShoot (UnityEvent)
  │       → PlayerShootAudio (CriAtomSource).Play()  ← Inspector で繋ぐだけ
  │     OnReload (UnityEvent)
  │       → PlayerReloadAudio (CriAtomSource).Play()
  ├── PlayerShootAudio (CriAtomSource, cueName="Player/Shoot")
  └── PlayerReloadAudio (CriAtomSource, cueName="Player/Reload")
```

**追加の音**: デザイナーがCueを増やして、プログラマが追加の `CriAtomSource` を貼って `OnShoot` に繋ぐだけ。コード変更ゼロ。

#### パターンB: event Action → Binder で購読

`Health.OnDamage`, `MagnetBullet.OnImpact` など `event Action` ベースは、薄い購読クラス（Binder）で受ける。

```csharp
/// <summary>
/// Health の event Action を CRI 再生に繋ぐ購読クラス。
/// </summary>
[RequireComponent(typeof(Health))]
public class HealthAudioBinder : MonoBehaviour
{
    [SerializeField] private CriAtomSource m_hurtSource; // "Player/Hurt" 等を設定
    private Health m_health;

    private void OnEnable()
    {
        m_health = GetComponent<Health>();
        m_health.OnDamage += HandleDamage;
        m_health.OnDie += HandleDie;
    }

    private void OnDisable()
    {
        m_health.OnDamage -= HandleDamage;
        m_health.OnDie -= HandleDie;
    }

    private void HandleDamage(int amount) => m_hurtSource?.Play();
    private void HandleDie() => AudioOneShot.PlayAt("Enemy", "Die", transform.position);
}
```

#### パターンC: AudioOneShot（位置指定ワンショット）

誰にもアタッチしない音（着弾、爆発など）。自作ヘルパー `AudioOneShot.PlayAt(cueSheet, cue, position)` を使う。実装は `audio-task-brief.md` 参照。

### 4.3 AISAC 動的連動

**毎フレ連動が必要な音**のみ、Update で AISAC 値を更新する。

```csharp
// MagnetManager.Update などに追加（1-2行）
CriAtomEx.SetGlobalAisacControl("AISAC_MagnetForce", m_forceNormalized);
```

グローバル更新なら全 CriAtomSource に自動反映される。

### 4.4 BGM / Category / Snapshot

```csharp
// BGMは専用の CriAtomSource を使い回す
m_bgmSource.cueName = "BGM/Stage_Main";
m_bgmSource.Play();

// Category音量（OptionsUIスライダーから）
CriAtom.SetCategoryVolume("BGM", volume01);

// Snapshot切替（シーン遷移、戦闘突入時など）
CriAtom.AttachDspBusSetting("Combat_Boss");
```

---

## 5. データフロー例

### 例1: プレイヤーが射撃したら音が鳴る

```
[1] RT押下 → ShootingController.Update() で射撃処理
[2] m_events.FireShoot() 呼び出し
[3] PlayerEvents.OnShoot (UnityEvent) が発火
[4] Inspector で繋がれた PlayerShootAudio.Play() が自動実行
[5] CriAtomSource が "Player/Shoot" Cue を再生
[6] CRIがポリフォニック設定で5種からランダム1種選んで発音
    （音量・ピッチもAtom Craftで設定したレンジ内でランダム）
```

**プログラマが書くコード: ゼロ**（PlayerEvents.FireShoot() は既存）

### 例2: 弾が壁に当たったら着弾音

```
[1] MagnetBullet が壁と衝突 → OnTriggerEnter等で着弾処理
[2] OnImpact?.Invoke() （event Action 発火）
[3] MagnetBulletAudioBinder が購読済み、HandleImpact() 実行
[4] AudioOneShot.PlayAt("MagnetBullet", "HitStatic", transform.position)
[5] 内部の共有 CriAtomExPlayer が 3D位置指定で発音
```

**プログラマが書くコード: Binder 1個（20行程度）**

### 例3: 引き寄せ中に磁力が強くなるにつれて音が高くなる

```
[1] 引き寄せ開始時: m_magnetLoopSource.cueName = "Magnet/AttractLoop"; Play();
[2] 毎フレーム: MagnetManager.Update() で
    CriAtomEx.SetGlobalAisacControl("AISAC_MagnetForce", forceNormalized);
[3] CRIが AISAC カーブに従って音量・ピッチをリアルタイム変化
[4] 引き寄せ終了時: m_magnetLoopSource.Stop();
```

**プログラマが書くコード: 3行**（Start, Update, Stop）

---

## 6. 拡張シナリオ

### シナリオA: 「敵が見つけた瞬間のSE追加したい」

1. **デザイナー**: Atom Craft で `Enemy/Noticed` Cue 作成 → ACB ビルド
2. **プログラマ**: Enemy Prefab に `CriAtomSource` 追加（cueName=`Enemy/Noticed`）。既存 event（例えば `OnNoticed`）に Binder で繋ぐ、またはUnityEvent化してInspectorから繋ぐ
3. **完了**: 15-30分

### シナリオB: 「射撃音のバリエを3種から5種に増やしたい」

1. **デザイナー**: Atom Craft で `Player/Shoot` のポリフォニックキューに2本追加 → ACB ビルド
2. **完了**: 5分、**コード変更ゼロ**

### シナリオC: 「ピンチ演出の閾値を25%→30%に」

1. **プログラマ**: `LowHpBinder` の `m_threshold` を Inspector で 0.3 に変更
2. **完了**: 10秒

### シナリオD: 「戦闘強度でBGMを動的ミックス」

1. **デザイナー**: Atom Craft で `BGM/Stage_Main` に `AISAC_CombatIntensity` を設定（敵が多いほど打楽器レイヤーを強める等）
2. **プログラマ**: `CombatIntensityTracker` を作って毎フレーム `CriAtomEx.SetGlobalAisacControl("AISAC_CombatIntensity", value)` 呼ぶ
3. **完了**: デザイナー1時間、プログラマ30分

---

## 7. 設計の強み

| 要件 | 実現手段 |
|---|---|
| バグがでにくい | CRIの成熟したAPI、Cue Sheet由来の型安全なCueID、UnityEvent直結でコード量最小 |
| debugしやすい | Atom Craft上で単体試聴可能、UnityでのAISAC/Cueは Inspector 可視 |
| 問題をすぐ特定可能 | CRIのConsoleに再生ログ、CriAtomSource の状態が Inspector から見える |
| わかりやすい | ラッパー最小、CRIのAPIをそのまま使う（学習が1つに集約） |
| 修正が簡単 | 音の追加・調整は Atom Craft 単独で完結、コード変更不要 |
| 一貫性がある | Core は既存パターン（UnityEvent / event Action）を維持、音は別レイヤー |
| 開発がしやすい | デザイナー/プログラマが互いにブロックしない |

---

## 8. 参考リンク

- [CRI ADX LE 製品ページ](https://game.criware.jp/products/adx-le/)
- [CRIWARE Unity Plugin Manual](https://game.criware.jp/manual/unity_plugin/latest/index.html)
- [CRI ADX2 Unity 入門編](https://game.criware.jp/learn/tutorial/unity/)
- [CRIWARE Portal（日本語Tips）](https://criware.info/)
- プログラマメンバー向けTODO: [`audio-task-brief.md`](./audio-task-brief.md)
