# Core/Player

**パス**: `Assets/_Project/Scripts/Core/Player/`
**アセンブリ**: `MagnetRush.Player`

## 概要

プレイヤーエンティティと、その周辺コントローラ（入力・エイム・極性切替・射撃・カメラ）を提供するフォルダ。
`Player` が `Entity` を継承し、サブコンポーネント群が `Player` の各側面を分業で制御する。

### アーキテクチャ概観

```
Player (Entity : IMagnetTarget)
  ├─ input     ── PlayerInputHandler        (Input System ポーリング)
  ├─ events    ── PlayerEvents              (コード購読用イベントハブ)
  ├─ states    ── PlayerStateManager        (EntityStateManager<Player>)
  │              ├─ IdlePlayerState
  │              ├─ MovePlayerState
  │              ├─ AimPlayerState
  │              └─ DiePlayerState
  └─ magnetizable ── Magnetizable (Core/Magnet)

  並列で付くサブコンポーネント (全て Player にアタッチ)
    ├─ AimController         (LT → Time.timeScale + AimState)
    ├─ PoleController       (Y → S/N 切替)
    ├─ ShootingController    (RT → 弾発射 / A → SelfFire)
    └─ CameraSettingsApplier (Cinemachine TPS + Yaw/Pitch)

  サブクラスで Entity override:
    Gravity / SnapForce / ExternalDrag / GroundLayer / PullOrientation*
```

## スクリプト一覧（ルート直下）

| スクリプト | 種別 | 役割 |
|---|---|---|
| [Player](#player) | Entity継承 MonoBehaviour | プレイヤー本体。入力・ステート・磁力の統合制御 |
| [PlayerInputHandler](#playerinputhandler) | MonoBehaviour | Input System ポーリングラッパー |
| [PlayerEvents](#playerevents) | MonoBehaviour | プレイヤーアクションのイベントハブ |
| [PlayerStateManager](#playerstatemanager) | EntityStateManager\<Player\> | ステート登録と初期遷移 |
| [AimController](#aimcontroller) | MonoBehaviour | LT入力でエイムモード切替（スロー+カメラ） |
| [PoleController](#polecontroller) | MonoBehaviour | Y入力で弾の磁極S/N切替 |
| [ShootingController](#shootingcontroller) | MonoBehaviour | RT発射 / A自己磁化 / Xリロード |
| [CameraSettingsApplier](#camerasettingsapplier) | MonoBehaviour | Cinemachine TPS + Yaw/Pitch制御 |

### サブフォルダ

- [States/](Player/States.md) — プレイヤーの4ステート（Idle/Move/Aim/Die）

## 他フォルダとの連携

- **Core/Entity** — `Player : Entity`、`PlayerStateManager : EntityStateManager<Player>`
- **Core/Magnet** — `Magnetizable` でPlayerを磁化、`MagnetField / Visualizer` を SelfFire で生成
- **Core/Bullet** — `ShootingController` が `MagnetBullet` を Instantiate し `BulletManager.Register`
- **Settings/Player** — `PlayerSettings` SO（速度、カメラ感度、エイム、FOV等）
- **Settings/Bullet** — `BulletSettings` SO（弾Prefab、着弾エフェクト、MagnetField設定）
- **UI** — `Player.OnPlayerReady` / `PlayerEvents` を購読して弾数・HP・極性を表示
- **Game** — `GameManager.GetSpawnPosition()` をDeathステートで参照

---

## Player

**ファイル**: `Player.cs`
**要件**: `[RequireComponent(Rigidbody, PlayerInputHandler, PlayerEvents)]`
**継承元**: `Entity`

### 役割
プレイヤーエンティティ。入力ハンドラ・ステートマシン・磁化コンポーネントを集約し、毎Update で `EntityUpdate` を駆動。`PlayerSettings` SO を**単一の保持者**として持ち、サブコンポーネントは `Player.Settings` 経由で取得する。

### アタッチ対象
プレイヤーPrefab のルートGameObject。

### 静的API
- `static Player Current` — 現在アクティブなPlayerインスタンス（シングルトン風）
- `static event Action<Player> OnPlayerReady` — `Awake` で発火。シーン参照なしでサブシステムがPlayerを取得するため
- `ResetStatics()` — `[RuntimeInitializeOnLoadMethod]` でドメインリロード時にクリア

### Inspector項目
- `m_settings` — `PlayerSettings` SO

### 公開プロパティ
| プロパティ | 中身 |
|---|---|
| `Settings` | `PlayerSettings` SO |
| `input` | `PlayerInputHandler` |
| `events` | `PlayerEvents` |
| `states` | `PlayerStateManager` |
| `magnetizable` | `Magnetizable` |

### Entity overrides（全て `m_settings` から返す）
`Gravity`, `SnapForce`, `ExternalDrag`, `GroundCheckDistance`, `GroundLayer`, `PullOrientationThreshold`, `PullOrientationSpeed`

### Update フロー
```
Update(dt)
  dt = min(Time.deltaTime, fixedDeltaTime * 3f)   // 大きいフレームドロップを制限
  UpdateMagneticInfluence()                        // 磁力の影響度 → 速度倍率に反映
  states.Step(dt)
  if (current != DiePlayerState) EntityUpdate(dt)  // 死亡中は物理スキップ
```

### 磁力影響（`UpdateMagneticInfluence`）
- `Magnetizable.GetInfluence(maxForcePerObject)` で 0-1 の影響度を取得
- `topSpeedMultiplier = 1 - influence * damping`（強い磁力で鈍くなる）
- `turningDragMultiplier = 1 + influence * damping`

### 移動API（ステートから呼ばれる）
- **`AccelerateToInputDirection(dt)`** — 通常移動。入力方向に加速 + 進行方向回転
- **`MoveWithInputStrafe(dt)`** — エイム中のストレイフ。速度 = `topSpeed * aimMoveSpeedMultiplier`。常にカメラ方向を向く
- **`SlowDown(dt)`** — 横移動減速

### 死亡処理
`Health.OnDie` を購読し `states.Change<DiePlayerState>()`。

---

## PlayerInputHandler

**ファイル**: `PlayerInputHandler.cs`

### 役割
Unity Input System をポーリング方式でラップする。`InputActionAsset` を直接参照し、`Move / Attack / Aim / SwitchPole / Reload / SelfFire` の6アクションを提供。

### Inspector項目
- `m_actions` — `InputActionAsset`（未設定時は `PlayerInput` コンポーネントから取得）

### 公開API
| API | 用途 |
|---|---|
| `MoveInput` | `Vector2` 左スティック値 |
| `AimHeld` | LT押下中かどうか（`> 0.5`） |
| `ConsumeFire()` | RT `WasPressedThisFrame` |
| `ConsumeSwitchPole()` | Y `WasPressedThisFrame` |
| `ConsumeReload()` | X `WasPressedThisFrame` |
| `ConsumeSelfFire()` | A / F `WasPressedThisFrame` |

### 備考
`OnEnable / OnDisable` で `InputActionAsset.Enable() / Disable()`。

---

## PlayerEvents

**ファイル**: `PlayerEvents.cs`

### 役割
プレイヤーアクションのイベントハブ。**コード購読用**の `event Action` を提供（Inspector接続用の `EntityEvents` とは別レイヤー）。

### 公開イベント
| イベント | 発火元 |
|---|---|
| `OnShoot` | `ShootingController.Fire` |
| `OnSelfShoot` | `ShootingController.SelfFire` |
| `OnPoleSwitch(MagneticPole)` | `PoleController.Update` |
| `OnReload` | `ShootingController.Update` (X押下時) |

各 `Fire*` メソッド経由で発火。UIなどが購読する。

---

## PlayerStateManager

**ファイル**: `PlayerStateManager.cs`
**継承**: `EntityStateManager<Player>`

### 役割
プレイヤー用ステートマシン。Awakeで4ステートを登録、Startで `IdlePlayerState` に初期遷移。

### 登録ステート
`IdlePlayerState` / `MovePlayerState` / `DiePlayerState` / `AimPlayerState`
（詳細は [States/](Player/States.md)）

---

## AimController

**ファイル**: `AimController.cs`
**要件**: `[RequireComponent(Player)]`

### 役割
LT入力でエイムモードを制御。`Time.timeScale` によるスロー + カメラ距離/FOV変更 + `AimPlayerState` への遷移。

### 公開API
- `bool IsAiming` — エイム中かどうか
- `static event Action<bool> OnAimChanged` — 状態変化通知。`CameraSettingsApplier` が購読

### 挙動
- LT押下中: `StartAim()` で `IsAiming=true`、`Time.timeScale = aimTimeScale`、`AimPlayerState` へ
- LT離す: **猶予時間** `aimReleaseGraceTime` 内は解除しない（RT押下時のジッター防止）
- 猶予切れで `StopAim()` → `timeScale=1`、入力に応じて `Move/Idle` へ復帰

### 備考
`OnDisable` で `Time.timeScale = 1` に強制リセット（シーン遷移・破棄時の安全策）。

---

## PoleController

**ファイル**: `PoleController.cs`

### 役割
Y入力で弾の磁極（S/N）を切り替える。初期値はS。

### 公開API
- `MagneticPole CurrentPole` — 現在の極性
- `event Action<MagneticPole> OnPoleChanged` — 切替時

### 処理
`ConsumeSwitchPole()` trueで S⇔N トグル → `PlayerEvents.FirePoleSwitch`。

---

## ShootingController

**ファイル**: `ShootingController.cs`
**要件**: `[RequireComponent(Player)]`

### 役割
RT射撃・A自己磁化・Xリロードの制御。画面中央方向への弾発射と、自身への磁力付与（コリジョン経由せず直接）を担う。

### Inspector項目
| フィールド | 用途 |
|---|---|
| `m_bulletSettings` | 弾設定SO |
| `m_firePoint` | 発射位置Transform（未指定時は `position + up * firePointHeight`） |
| `m_selfFireHeightOffset` | （未使用、将来拡張用） |

### 入力処理（Update）
- **X（リロード）** → `BulletManager.ClearAll()` + `PlayerEvents.FireReload`
- **RT（射撃）** → `Fire()`
- **A/F（SelfFire）** → `SelfFire()`

### `Fire()` フロー
1. 画面中央から `Camera.ScreenPointToRay` で `Ray` 取得
2. `CalculateTargetPoint` で標的座標を確定：
   - レイキャストhitが発射位置より前方ならそれを採用
   - 水平平面との交点（前方方向かつ閾値以上の角度）
   - 真下向きなら `camera.forward` フォールバック
3. 発射方向 = `(target - spawnPos).normalized`
4. `Instantiate(bulletPrefab)` → `MagnetBullet.Initialize(pole, dir)` → `BulletManager.Register`
5. `bullet.OnImpact += aim.StopAim`（着弾でエイム解除）

### `SelfFire()` フロー
1. プレイヤー `Magnetizable.SetPole(pole)` で直接磁化
2. `gameObject.AddComponent<MagnetField>()` → `field.Initialize(pole, settings)` → `MagnetManager.RegisterField`
3. `AddComponent<MagnetFieldVisualizer>()` → `Show`
4. 着弾エフェクトをS/N別にInstantiate
5. `field.OnFieldExpired` 購読で Magnetizable/Visualizer/Effect を破棄

### 備考
- `MaskShootingRaycast` を使用（Trigger系・Playerを除外）
- `Debug.DrawLine` でcamera視線・弾道を可視化（3秒間）

---

## CameraSettingsApplier

**ファイル**: `CameraSettingsApplier.cs`
**種別**: `[DefaultExecutionOrder(-200)]`

### 役割
TPS用カメラ制御。右スティック/マウスで Yaw/Pitch 回転、エイム時にFOV/距離切替、`CinemachineThirdPersonFollow` のパラメータ調整。

### Inspector項目
- `m_cinemachineCamera` — 対象の Cinemachine カメラ

### 初期化（`InitializeWithPlayer`）
`Player.OnPlayerReady` または `Player.Current` 経由で Player を取得後：
1. プレイヤーの子に `CameraPivot` GameObject を生成（`localPosition = up * 1.2`、肩越し視点）
2. Cinemachine `Follow` / `LookAt` に Pivot をセット
3. `ThirdPersonFollow` のTPS標準値を強制（ShoulderOffset, CameraDistance=3.5, Damping等）
4. `PlayerSettings` に値があれば上書き
5. カーソル非表示・ロック

### `LateUpdate`
マウス + 右スティック入力を合成して Yaw/Pitch 更新。`pitch` は `-10° 〜 60°` でクランプ（地面貫通防止＋自然な範囲）。

### エイムモード切替（`SetAimMode(bool)`）
`AimController.OnAimChanged` 購読。
- `CameraDistance` を `aimCameraDistance` / デフォルトで切替
- `FOV` を `aimFOV` / デフォルトで切替

### 備考
- `Time.unscaledDeltaTime` を使用（エイム中のスロー影響を受けない）
- スティック入力はフレーム非依存で `* 5f` で増幅
