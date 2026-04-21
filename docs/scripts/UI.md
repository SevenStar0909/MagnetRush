# UI

**パス**: `Assets/_Project/Scripts/UI/`
**アセンブリ**: `MagnetRush.UI`

## 概要

ゲームプレイ中のオンスクリーンUI（残弾・レティクル）と、デバッグ用UI（磁力パラメータ調整・チートメニュー・Graphyトグル）。
プレイヤーのイベント（`PoleController.OnPoleChanged`、`BulletManager.OnBulletCountChanged`、`AimController.OnAimChanged`）を購読してスプライト切替を行う。

### 構成図

```
ゲーム中UI
  ├─ AmmoUI           ← BulletManager.OnBulletCountChanged + PoleController.OnPoleChanged
  │                     極性×残弾(0-4)のスプライト切替
  └─ ReticleUI        ← AimController.IsAiming + PoleController.OnPoleChanged
                        Hipfire/Aim × S/N の4種レティクル

デバッグUI（Editor + DEBUG ビルドのみ）
  ├─ DebugUI          F1: 磁力Force/Range Slider + 弾リスト
  ├─ DebugActionMenu  F2: カテゴリ別チートボタン（HP回復/全弾クリア/全敵撃破等）
  └─ GraphyStagedToggle F2: Graphy 3段階トグル + 日本語化
```

## スクリプト一覧

| スクリプト | 種別 | 役割 |
|---|---|---|
| [AmmoUI](#ammoui) | MonoBehaviour | 残弾数表示（極性+残弾数統合スプライト） |
| [ReticleUI](#reticleui) | MonoBehaviour | レティクル（エイム状態×極性で4種切替） |
| [DebugUI](#debugui) | MonoBehaviour | 磁力パラメータ Slider + 弾リスト（F1） |
| [DebugActionMenu](#debugactionmenu) | MonoBehaviour | チートメニュー（F2） |
| [GraphyStagedToggle](#graphystagedtoggle) | MonoBehaviour | Graphyの3段階表示 + 日本語化（F2） |

## 他フォルダとの連携

- **Core/Player/PoleController** — `OnPoleChanged` を `AmmoUI`/`ReticleUI` が購読
- **Core/Player/AimController** — `IsAiming` を `ReticleUI` が毎フレーム参照
- **Core/Bullet/BulletManager** — `OnBulletCountChanged` を `AmmoUI` が購読、`ClearAll` を `DebugActionMenu` が呼ぶ
- **Core/Enemy/EnemyBase** — `DebugActionMenu` の「全敵撃破」で参照
- **Settings/Magnet/MagnetSettings** — `DebugUI` のSliderが `magnetForce` / `magnetRange` を動的変更
- **3rd party: Tayx.Graphy** — `GraphyStagedToggle` が使用

---

## AmmoUI

**ファイル**: `AmmoUI.cs`

### 役割
残弾数を単一の `Image` で表現。S極/N極×残弾数(0-4) の統合スプライトをSerializeFieldで持ち、極性または残弾数の変化で切り替える。

### アタッチ対象
HUD用Canvas下のGameObject。

### Inspector項目
| フィールド | 意味 |
|---|---|
| `m_ammoImage` | 表示する `UnityEngine.UI.Image` |
| `m_spritesS[]` | S極 残弾スプライト配列 (0〜4) |
| `m_spritesN[]` | N極 残弾スプライト配列 (0〜4) |

### 購読元
- `PoleController.OnPoleChanged` → `m_currentPole` 更新 → 再描画
- `BulletManager.OnBulletCountChanged(usedCount)` → `m_currentRemaining = max - usedCount` → 再描画

### 備考
- `Start` で `FindWithTag(Player)` → `PoleController` 取得
- `OnDestroy` で両イベント購読解除

---

## ReticleUI

**ファイル**: `ReticleUI.cs`

### 役割
画面中央のレティクル。Hipfire(通常)とAim(エイム中)×S極/N極の4種スプライトを状態に応じて切り替える。

### アタッチ対象
HUD用Canvas下のGameObject。

### Inspector項目
- `m_reticleImage` — 対象 `Image`
- `m_hipfireS / m_hipfireN` — 通常時のスプライト
- `m_aimS / m_aimN` — エイム中のスプライト

### 挙動
- `Start` — Player探索 → `PoleController.OnPoleChanged` を購読、`AimController` 参照取得
- `Update` — `UpdateSprite()` を毎フレーム呼ぶ（`AimController.IsAiming` をポーリング）
- `UpdateSprite` — `aiming` と `currentPole` の2軸で4種から1つを選択

### 備考
- エイム状態もイベント化されているが、現状は `Update` ポーリングで取得

---

## DebugUI

**ファイル**: `DebugUI.cs`

### 役割
磁力パラメータ（`magnetForce` / `magnetRange`）をランタイムでSlider編集 + 弾の状態一覧を表示するデバッグ画面。**F1キー**で表示/非表示切替。

### アタッチ対象
デバッグCanvas。`#if !DEBUG && !UNITY_EDITOR` でリリースビルド時は `Awake` で `SetActive(false)`。

### Inspector項目
- `m_panel` — 表示/非表示対象のパネルGameObject
- `m_forceSlider / m_rangeSlider` — 磁力Force/Rangeのスライダー
- `m_forceLabel / m_rangeLabel` — 現在値表示のTMP
- `m_bulletListText` — 弾数表示のTMP
- `m_magnetSettings` — 編集対象の `MagnetSettings` SO

### スライダーの挙動
- Force: 1〜100 (default=現在値)、`OnForceChanged` で `magnetForce` 直接書き換え
- Range: 1〜50 (default=現在値)、`OnRangeChanged` で `magnetRange` 直接書き換え

### 表示更新（Update）
- F1で `m_panel` の `activeSelf` トグル
- `UpdateBulletList` — `BulletManager.CurrentCount / MaxBullets`
- `UpdateLabels` — `magnetForce / magnetRange` の現在値

### 備考
- **SO を直接書き換える**のでEditor時は永続化される（Play停止後も値が残る）
- `OnDestroy` でSliderリスナー解除

---

## DebugActionMenu

**ファイル**: `DebugActionMenu.cs`

### 役割
IMGUIベースのチートメニュー。**F2キー**で表示/非表示切替。カテゴリ分けされたボタンで各種デバッグアクションを即実行できる。

### アタッチ対象
デバッグ用GameObject。`#if !DEBUG && !UNITY_EDITOR` で `Awake` 時に `Destroy(this)`。

### デフォルト登録アクション
| カテゴリ | ラベル | 処理 |
|---|---|---|
| プレイヤー | HP全回復 | `player.m_health.ResetHealth()` |
| 弾 | 全弾クリア | `BulletManager.ClearAll()` |
| 敵 | 全敵撃破 | `EnemyBase` 全件に `Damage(9999)` |
| システム | TimeScale 0 | `Time.timeScale = 0` |
| システム | TimeScale 1 | `Time.timeScale = 1` |
| システム | TimeScale 0.5 | `Time.timeScale = 0.5` |

### 公開静的API
```csharp
DebugActionMenu.Register("カテゴリ名", "ボタンラベル", () => { /* 処理 */ });
DebugActionMenu.ClearAll();
```
外部スクリプトから `Register` すれば任意のデバッグボタンを動的追加可能。

### OnGUI
- 折り畳み可能な `DebugCategory` 一覧をスクロール表示
- 各ボタン押下でコールバック実行（try-catchで例外ログ）
- 下部に `TimeScale` スライダー (0〜3)
- ドラッグ可能ウィンドウ（`GUI.DragWindow`）

---

## GraphyStagedToggle

**ファイル**: `GraphyStagedToggle.cs`
**要件**: `[RequireComponent(GraphyManager)]` (Tayx.Graphy)

### 役割
サードパーティの Graphy（FPS/RAM等のランタイムプロファイラ）を**3段階**でトグルする。同時にRAMモジュールのラベルを日本語化する（`reserved → 予約`, `allocated → 確保`）。

### Inspector項目
- `m_toggleKey` — トグルキー（デフォルト `F2`）
- `m_japaneseFont` — 日本語表示用フォント

### 3段階トグル
| Stage | 表示 |
|---|---|
| 0 | 全モジュール FULL（FPS/RAM/Audio/Advanced） |
| 1 | Advanced OFF（左下の詳細情報を隠す） |
| 2 | 全モジュール OFF（完全非表示） |

### Start処理
`SafeArea/RAM - Module/` 以下の `reserved_ram_text / allocated_ram_text / mono_ram_text` を探し、テキストとフォントを差し替え。

### 備考
- **`DebugActionMenu` とキーが被る（どちらもF2）**ので、配置によってはどちらか一方だけ有効にする運用が必要
- Keyboard/GraphyManager null時は `LogGuardReturn` で static ログ抑制
