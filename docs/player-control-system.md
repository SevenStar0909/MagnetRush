# プレイヤー制御システム 設計ドキュメント

## 1. システム概要

### 1.1 全体アーキテクチャ

Magnet Rushのプレイヤー制御システムは、Entity基底クラスを中心とした継承ベースの設計と、ジェネリックなステートマシンの組み合わせで構成される。物理演算はUnityのRigidbodyを使わず、自前のCollide-and-Slideアルゴリズムで実装されている。

```
┌─────────────────────────────────────────────────────┐
│                    Player (Entity)                    │
│  ┌──────────┐ ┌──────────┐ ┌───────────────────┐    │
│  │ Rigidbody│ │ Capsule  │ │  EntityController │    │
│  │(kinematic)│ │ Collider │ │ (Collide & Slide) │    │
│  └──────────┘ └──────────┘ └───────────────────┘    │
│                                                       │
│  ┌─────────────────┐  ┌──────────────────────────┐  │
│  │PlayerInputHandler│  │   PlayerStateManager     │  │
│  │  (Input System)  │  │ ┌─────┐┌────┐┌───┐┌───┐│  │
│  └─────────────────┘  │ │Idle ││Move││Aim││Die││  │
│                        │ └─────┘└────┘└───┘└───┘│  │
│  ┌──────────────┐     └──────────────────────────┘  │
│  │ PlayerEvents │                                    │
│  │ (イベントハブ) │                                    │
│  └──────────────┘                                    │
│                                                       │
│  ┌──────────────────┐ ┌──────────────────┐          │
│  │ShootingController│ │  AimController   │          │
│  │  (射撃・セルフ)   │ │(スロー・カメラ寄り)│          │
│  └──────────────────┘ └──────────────────┘          │
│                                                       │
│  ┌──────────────────┐ ┌──────────────────────────┐  │
│  │PoleController  │ │ CameraSettingsApplier    │  │
│  │  (S/N切替)       │ │ (Cinemachine連携)         │  │
│  └──────────────────┘ └──────────────────────────┘  │
│                                                       │
│  ┌──────────────────┐                                │
│  │  PlayerSettings  │ ← ScriptableObject             │
│  │ (全パラメータ)    │                                │
│  └──────────────────┘                                │
└─────────────────────────────────────────────────────┘
```

### 1.2 毎フレームの実行フロー

`Player.Update()` が毎フレーム以下の順序で処理を実行する:

```
Player.Update()
  │
  ├─ 1. UpdateMagneticInfluence()     ← 磁力の影響度からspeed/drag multiplierを計算
  │
  ├─ 2. states.Step(dt)               ← 現在ステートのStep()を実行
  │     ├─ IdlePlayerState.Step()     → SlowDown + Move入力判定
  │     ├─ MovePlayerState.Step()     → MoveWithInput (加速+回転)
  │     ├─ AimPlayerState.Step()      → MoveWithInputStrafe (ストレイフ移動)
  │     └─ DiePlayerState.Step()      → リスポーンタイマー
  │
  ├─ 3. UpdateGround()                ← 接地判定・斜面情報更新
  │
  ├─ 4. UpdateMagneticOrientation(dt) ← 空中で磁力方向に回転
  │
  ├─ 5. ApplyGravity(...)             ← 重力適用 / 接地時スナップ
  │
  └─ 6. ApplyMovement(dt)             ← 全速度成分を位置に反映
        └─ EntityController.Move()    ← Collide-and-Slideで壁衝突処理
```

### 1.3 並行して動作するコンポーネント（独自Update）

以下のコンポーネントはPlayer.Update()とは独立して自身のUpdate()で動作する:

- **AimController.Update()** -- LT入力を監視し、エイムモードの開始/終了を制御
- **ShootingController.Update()** -- RT/X/A入力を監視し、射撃/リロード/セルフファイアを実行
- **PoleController.Update()** -- Y入力を監視し、磁極をS/Nで切り替え

### 1.4 データフロー図

```
InputActionAsset (Input System)
      │
      ▼
PlayerInputHandler ──読み取り──┬── AimController
  MoveInput (Vector2)          │     IsAiming, StartAim(), StopAim()
  AimHeld (bool)               │
  ConsumeFire()                ├── ShootingController
  ConsumeSwitchPole()          │     Fire(), SelfFire()
  ConsumeReload()              │
  ConsumeSelfFire()            ├── PoleController
                               │     CurrentPole (S/N)
                               │
                               ├── Player / States
                               │     AccelerateToInputDirection()
                               │     MoveWithInputStrafe()
                               │
                               ▼
                          PlayerEvents ──通知──→ 外部システム
                            OnShoot, OnSelfShoot
                            OnPoleSwitch, OnReload
```

---

## 2. Entity基底クラス

**ファイル:** `Scripts/Entity/Entity.cs`

### 2.1 クラス定義

```csharp
public abstract class Entity : MonoBehaviour, IMagnetTarget
```

- **抽象クラス**: 直接インスタンス化不可。`Player` や敵クラスが継承する
- **IMagnetTarget実装**: 磁力システムから `ApplyMagnetForce(Vector3)` で外部力を受け取る

### 2.2 フィールド一覧

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `rb` | `Rigidbody` | protected | 物理ボディ（kinematicモードで使用） |
| `capsuleCollider` | `CapsuleCollider` | protected | カプセル衝突判定 |
| `controller` | `EntityController` | protected | Collide-and-Slide衝突制御 |
| `health` | `Health` | public (HideInInspector) | HPコンポーネント |
| `cachedCameraTransform` | `Transform` | protected | メインカメラのTransformキャッシュ |
| `velocity` | `Vector3` | public | ワールド空間の速度ベクトル |
| `externalVelocity` | `Vector3` | public | 外部力（磁力等）。毎フレームリセット |
| `slopingGroundAngle` | `float` (readonly) | protected | 斜面判定の閾値角度（20度） |
| `m_pullOrientationThreshold` | `float` | protected | 磁力回転の開始閾値 |
| `m_pullOrientationSpeed` | `float` | protected | 磁力方向への回転速度 |

### 2.3 プロパティ一覧

| プロパティ | 型 | 説明 |
|---|---|---|
| `localVelocity` | `Vector3` (get/set) | ローカル空間の速度。`transform.up` と `Vector3.up` の間の回転で変換する。斜面上でも正しく動作する仕組み |
| `lateralVelocity` | `Vector3` (get/set) | ローカル空間のXZ成分（横移動）。0.01未満はゼロに丸める。setは既存のY成分を保持してXZのみ書き換える |
| `verticalVelocity` | `float` (get/set) | ローカル空間のY成分（垂直方向）。getは `localVelocity.y`、setはY成分だけ書き換える |
| `IsGrounded` | `bool` (get/protected set) | 接地フラグ |
| `groundHit` | `RaycastHit` | 地面のRaycastHit情報 |
| `groundAngle` | `float` | 地面の傾斜角度 |
| `groundNormal` | `Vector3` | 地面の法線（デフォルト `Vector3.up`） |
| `localSlopeDirection` | `Vector3` | 斜面の水平方向成分 |
| `magnetField` | `IMagnetField` | 現在支配的な磁力場。MagnetManagerが毎フレーム設定 |
| `topSpeedMultiplier` | `float` | 最高速度の倍率（磁力影響等）。デフォルト1.0 |
| `turningDragMultiplier` | `float` | 旋回ドラッグの倍率。デフォルト1.0 |
| `decelerationMultiplier` | `float` | 減速の倍率。デフォルト1.0 |

### 2.4 メソッド詳細

#### `Awake()` (virtual)
```csharp
protected virtual void Awake()
```

初期化処理:
1. `GetComponent` で `Rigidbody`, `CapsuleCollider`, `EntityController`, `Health` を取得
2. Rigidbodyを `isKinematic = true`, `useGravity = false`, `interpolation = None` に設定（物理演算は自前で行うため）
3. `Camera.main` のTransformをキャッシュ

#### `ApplyMovement(float dt)` (virtual)
```csharp
protected virtual void ApplyMovement(float dt)
```

全速度成分を位置に反映する:
1. `velocity + externalVelocity` に `dt` を掛けて `motion` を算出
2. `EntityController` があれば `controller.Move()` でCollide-and-Slideを通して位置を更新
3. なければ単純な `transform.position += motion`
4. `externalVelocity` をゼロにリセット（毎フレーム蓄積→適用→クリアの方式）

#### `ApplyGravity(float gravity, float snapForce, float dt)`
```csharp
protected void ApplyGravity(float gravity, float snapForce, float dt)
```

重力を適用する:
- **接地中** かつ `verticalVelocity < 0`: `verticalVelocity = -snapForce` で地面にスナップ（浮かないように下方向の力を維持）
- **空中**: `verticalVelocity += gravity * dt` で重力加速

#### `Accelerate(Vector3 direction, float turningDrag, float acceleration, float topSpeed, float dt)`
```csharp
protected void Accelerate(Vector3 direction, float turningDrag, float acceleration, float topSpeed, float dt)
```

PLAYER TWO Platformer Projectの加速パターンを採用した横移動加速:
1. 入力方向を正規化
2. `topSpeed * topSpeedMultiplier` で有効最高速度を算出
3. `turningDrag * turningDragMultiplier` で有効旋回ドラッグを算出
4. 現在の `lateralVelocity` を「進行方向成分 (`speed`)」と「横方向成分 (`turningVelocity`)」に分解:
   - `speed = Vector3.Dot(direction, lateralVelocity)` -- 入力方向への射影
   - `turningVelocity = lateralVelocity - direction * speed` -- 残りの横方向
5. 横方向成分を `MoveTowards(zero)` で減衰させる（滑らかな方向転換）
6. 進行方向に `acceleration * dt` で加速（最高速度を超えていない場合のみ。`speed < 0` の場合は逆走中なので常に加速許可）
7. 最終的な `lateralVelocity = direction * speed + turningVelocity`

#### `Decelerate(float deceleration, float dt)`
```csharp
protected void Decelerate(float deceleration, float dt)
```

横移動速度をゼロに向けて減速する:
- `deceleration * decelerationMultiplier * dt` の量だけ `lateralVelocity` を `Vector3.zero` に近づける

#### `UpdateGround()`
```csharp
protected void UpdateGround()
```

地面情報を更新する:
1. カプセルの高さから接地判定距離を算出（`height * 0.5 + 0.3`）
2. `transform.position` から `-transform.up` 方向にRaycastを実行
3. ヒットした場合:
   - 足元からの距離（`hit.distance - height * 0.5`）が `0.1` 未満なら接地と判定
   - 接地時: `groundHit`, `groundNormal`, `groundAngle`, `localSlopeDirection` を更新
4. ヒットしない場合: `IsGrounded = false`、法線をデフォルト `Vector3.up` にリセット

#### `OnSlopingGround()` (virtual)
```csharp
public virtual bool OnSlopingGround()
```
接地中かつ地面の角度が `slopingGroundAngle`（20度）を超えるかどうかを返す。

#### `SlopeFactor(float upwardForce, float downwardForce, float dt)`
```csharp
protected void SlopeFactor(float upwardForce, float downwardForce, float dt)
```

斜面での加減速:
1. 接地中かつ斜面上でなければ何もしない
2. `Vector3.Dot(Vector3.up, groundNormal)` で法線の垂直成分を取得
3. `Vector3.Dot(localSlopeDirection, lateralVelocity) > 0` で下り坂方向に移動中か判定
4. 下り坂なら `downwardForce`、上り坂なら `upwardForce` を適用
5. `lateralVelocity += localSlopeDirection * delta` で斜面方向に力を加える

#### `FaceDirection(Vector3 direction, float rotationSpeed, float dt, bool adjustUp = true)`
```csharp
protected void FaceDirection(Vector3 direction, float rotationSpeed, float dt, bool adjustUp = true)
```

指定方向にSlerpで滑らかに回転:
1. `adjustUp = true` の場合、ローカル空間の方向を `transform.up` に合わせて変換
2. `Quaternion.LookRotation(direction, transform.up)` で目標回転を算出
3. `Quaternion.Slerp` で現在の回転から目標回転に `rotationSpeed * dt` の速度で補間

#### `UpdateMagneticOrientation(float dt)` (virtual)
```csharp
protected virtual void UpdateMagneticOrientation(float dt)
```

空中で強い磁力を受けているとき、磁力方向にプレイヤーを向ける:
- 接地中は何もしない（通常の移動方向回転を優先）
- `externalVelocity` の大きさが `m_pullOrientationThreshold` 未満なら何もしない
- 閾値以上なら `FaceDirection(externalVelocity.normalized, ...)` で回転

#### `ApplyMagnetForce(Vector3 force)` -- IMagnetTarget実装
```csharp
public void ApplyMagnetForce(Vector3 force)
```
`externalVelocity += force` で外部力を蓄積する。磁力システムが毎フレーム呼び出す。

#### `GetCameraRelativeDirection(Vector2 input)` / `GetCameraRelativeDirection(Vector2 input, out float magnitude, bool localSpace = true)`
```csharp
protected Vector3 GetCameraRelativeDirection(Vector2 input)
protected Vector3 GetCameraRelativeDirection(Vector2 input, out float magnitude, bool localSpace = true)
```

2D入力をカメラ相対の3D方向に変換する（PLAYER TWO Platformer Projectのパターン）:
1. 2D入力 `(x, y)` を `Vector3(x, 0, y)` に変換
2. カメラのupをエンティティのupに合わせる回転を算出
3. その回転 × カメラの回転を入力方向に適用
4. `localSpace = true` の場合:
   - エンティティの接地面（`transform.up`）に投影
   - ローカル空間に変換
5. `magnitude` に正規化前の大きさを出力（アナログスティックの傾き度合い）
6. 正規化された方向ベクトルを返す

---

## 3. EntityController（衝突制御）

**ファイル:** `Scripts/Entity/EntityController.cs`

### 3.1 クラス定義

```csharp
[DefaultExecutionOrder(-100)]
public class EntityController : MonoBehaviour
```

`[DefaultExecutionOrder(-100)]`によりEntity.Awakeより先に実行される。これにより既存CapsuleColliderの無効化とトリガーCollider生成がEntity.Awakeの`GetComponent`より先に完了する。

Collide-and-Slideベースの自前衝突制御。UnityのCharacterControllerの代わりに、トリガーCapsuleColliderとCapsuleCastで衝突判定を行い、衝突面に沿ってスライドする。PLAYER TWO Platformer ProjectのEntityControllerを参考にしている。

### 3.2 フィールド一覧

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `slopeLimit` | `float` | 45 | 歩行可能な最大傾斜角。これより急な面は壁として扱う |
| `stepOffset` | `float` | 0.3 | 段差を登れる高さ |
| `skinWidth` | `float` | 0.01 | 壁との間に保つスキン幅。ジッター防止用 |
| `center` | `Vector3` | - | 衝突判定カプセルの中心 |
| `m_radius` | `float` (private) | 0.5 | 衝突判定カプセルの半径 |
| `m_height` | `float` (private) | 2.0 | 衝突判定カプセルの高さ |
| `collisionLayer` | `LayerMask` | -5 | 衝突判定のレイヤーマスク |
| `k_MaxCollisionSteps` | `int` (const) | 3 | Collide-and-Slideの最大反復回数 |
| `m_rigidbody` | `Rigidbody` (private) | - | kinematicリジッドボディ |
| `m_collider` | `CapsuleCollider` (private) | - | トリガーカプセルコライダー |
| `m_overlaps` | `Collider[128]` (private) | - | OverlapCapsule用バッファ |
| `m_ignoredColliders` | `HashSet<Collider>` (private) | - | 一時的に無視するコライダーのセット（O(1)検索） |
| `m_pushActive` | `List<PushInfo>` (private) | - | 現在押しているオブジェクトの情報 |

### 3.3 内部構造体

```csharp
private struct PushInfo
{
    public Rigidbody rb;
    public Collider col;
}
```

押しているオブジェクトのRigidbodyとColliderを保持する。次フレームでdynamicに戻し、無視リストから除去するために使用。

### 3.4 プロパティ

| プロパティ | 型 | 説明 |
|---|---|---|
| `radius` | `float` | 最低値 `skinWidth` を保証する半径 |
| `height` | `float` | 最低値 `radius * 2` を保証する高さ |
| `collider` | `CapsuleCollider` | 内部のトリガーコライダー |
| `capsuleOffset` | `Vector3` (private) | カプセルの上端/下端のオフセット（`transform.up * (height * 0.5 - radius)`） |

### 3.5 メソッド詳細

#### `Awake()`

初期化処理（4段階）:
1. **DisableExistingCollider()**: 既存のCapsuleColliderがあればそのパラメータ（半径、高さ、中心）を取得し、そのコライダーを無効化
2. **InitializeCollider()**: 新しいCapsuleColliderを追加し、`isTrigger = true` に設定
3. **InitializeRigidbody()**: Rigidbodyが無ければ追加、`isKinematic = true` に設定
4. **RefreshCollider()**: コライダーのサイズを `radius - skinWidth`, `height - skinWidth` に設定（skinWidth分だけ小さくすることで隙間を確保）

#### `Move(Vector3 currentPosition, Vector3 motion)` -- メインAPI
```csharp
public Vector3 Move(Vector3 currentPosition, Vector3 motion)
```

毎フレーム `Entity.ApplyMovement()` から呼ばれる:
1. **ReleasePushedObjects()**: 前フレームの押し状態をリセット（kinematicを元に戻し、無視リストから除去）
2. motionをローカル空間に変換し、水平成分と垂直成分に分離
3. **MoveAndSlide(lateralMotion, false, motion)**: 水平移動。`fullMotion`引数ありで動的オブジェクトの押し処理を含む
4. **MoveAndSlide(verticalMotion, true)**: 垂直移動。`fullMotion`なしで通常の壁衝突処理のみ
5. **HandlePenetration()**: めり込み解決。`Physics.ComputePenetration` で押し出し
6. 最終的な位置を返す

#### `MoveAndSlide(position, motion, verticalPass, fullMotion)` -- 統合Collide-and-Slide

1メソッドで水平・垂直両方を処理。`fullMotion`引数の有無で押し処理を切替える:
- `fullMotion`あり（`sqrMagnitude > 0`）→ 動的オブジェクトの押し処理を含む（水平移動用）
- `fullMotion`なし（default=zero）→ 壁衝突のみ（垂直移動用）

最大 `k_MaxCollisionSteps`（3回）まで反復:
1. motionの方向と距離を算出
2. カプセルの起点を計算。水平移動時は `stepOffset` 分だけ下端を持ち上げる（段差対応）
3. `SweepTest()` でCapsuleCast実行
4. 衝突した場合:
   - **動的Rigidbody（pushEnabled時のみ）**: 押せるか判定
     - `GetMaxPushDistance()` で壁チェック（押す先に壁がないか確認）
     - 押せる場合: kinematicに切り替え、位置を移動、無視リストに追加してcontinue（通過）
     - 押せない場合: 壁として扱う
   - **壁として扱う場合**:
     - `skinWidth + radius` 分手前で停止
     - 残りのmotionを壁の法線で投影してスライド方向を算出
     - 傾斜が `slopeLimit` 以上の場合、垂直成分を除去（壁を登らせない）
5. 衝突しなかった場合: そのまま移動して終了

#### `HandlePenetration(Vector3 position)` -- めり込み解決

1. `OverlapCapsuleNonAlloc` で現在位置のカプセル内にあるコライダーを検出
2. 各コライダーに対して `Physics.ComputePenetration` で押し出し方向と距離を計算
3. 位置を修正して返す

#### `GetMaxPushDistance(Collider objCollider, Vector3 direction, float desiredDistance)` -- 押し距離判定

押されるオブジェクトが壁にぶつからずに移動できる最大距離を返す:
1. オブジェクトのBoundsを取得
2. BoxCastで押す方向に壁がないかチェック
3. 壁がある場合: 壁までの距離 - skinWidth を返す
4. 壁がない場合: desiredDistance をそのまま返す

#### `SweepTest(...)` -- CapsuleCast + Raycastのフォールバック

1. まず `Physics.CapsuleCast` で衝突判定
2. ヒットしなければ `Physics.Raycast` でフォールバック（薄いオブジェクトへの対応）

#### `IgnoreCollider(Collider col, bool ignore = true)`

一時的にコライダーを無視リストに追加/除去する。

#### `Resize(float newHeight)`

カプセルの高さを変更する。中心位置を差分の半分だけ調整。

---

## 4. EntityState / EntityStateManager（ステートマシン）

### 4.1 EntityState<T>

**ファイル:** `Scripts/Entity/EntityState.cs`

```csharp
public abstract class EntityState<T> where T : Entity
```

ステートの基底クラス。MonoBehaviourではない純粋なC#クラス。

#### フィールド

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `entity` | `T` | protected | このステートが所属するEntity |
| `manager` | `EntityStateManager<T>` | protected | ステートマネージャーへの参照 |
| `timeSinceEntered` | `float` (property) | public | ステートに入ってからの経過時間 |

#### メソッド

| メソッド | 説明 |
|---|---|
| `Enter(T entity, EntityStateManager<T> manager)` | ステート開始時に呼ばれる。entity/managerの参照を設定し、timeSinceEnteredをリセット |
| `Exit()` | ステート終了時に呼ばれる。デフォルトは空実装 |
| `Step(float dt)` | 毎フレーム呼ばれる。デフォルトは空実装 |
| `OnContact(Collider other)` | 他のColliderと接触した時に呼ばれる。デフォルトは空実装 |

### 4.2 EntityStateManager<T>

**ファイル:** `Scripts/Entity/EntityStateManager.cs`

```csharp
public class EntityStateManager<T> : MonoBehaviour where T : Entity
```

ジェネリックなステートマシン。ステートの登録・遷移・更新を管理する。

#### フィールド

| フィールド | 型 | 説明 |
|---|---|---|
| `m_states` | `Dictionary<Type, EntityState<T>>` | 型をキーにしたステート辞書 |
| `m_entity` | `T` | 管理対象のEntity |

#### プロパティ

| プロパティ | 型 | 説明 |
|---|---|---|
| `current` | `EntityState<T>` | 現在のステート |
| `last` | `EntityState<T>` | 直前のステート |

#### イベント

| イベント | 引数 | 説明 |
|---|---|---|
| `OnStateEnter` | `Type` | 新ステートに入った時に発火。引数は新ステートのType |
| `OnStateExit` | `Type` | ステートから出た時に発火。引数は旧ステートのType |
| `OnStateChanged` | なし | ステートが変更された時に発火 |

#### メソッド

##### `Initialize(T entity)`
```csharp
public void Initialize(T entity)
```
Entityの参照を保持する。サブクラスのAwake()からステート登録後に呼び出す。

##### `RegisterState(EntityState<T> state)`
```csharp
public void RegisterState(EntityState<T> state)
```
ステートのインスタンスをType型をキーに辞書に登録する。

##### `Change<TState>()`
```csharp
public void Change<TState>() where TState : EntityState<T>
```

ステートを遷移させる:
1. 型から辞書でステートを検索（未登録ならエラーログ）
2. 現在ステートがあれば `Exit()` を呼び、`OnStateExit` イベント発火、`last` に保存
3. 新ステートの `Enter()` を呼び、`OnStateEnter` イベント発火、`OnStateChanged` イベント発火

##### `Step(float dt)`
```csharp
public void Step(float dt)
```
現在ステートの `Step(dt)` を実行し、`timeSinceEntered += dt` で経過時間を更新。`Player.Update()` から呼ばれる。

##### `OnContact(Collider other)`
```csharp
public void OnContact(Collider other)
```
現在ステートに衝突通知を転送する。

##### `IsCurrentOfType<TState>()`
```csharp
public bool IsCurrentOfType<TState>() where TState : EntityState<T>
```
現在のステートが指定した型かどうかを返す。

---

## 5. Player（プレイヤー本体）

**ファイル:** `Scripts/Player/Player.cs`

### 5.1 クラス定義

```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public class Player : Entity
```

`Entity` を継承したプレイヤーエンティティ。入力・ステート・磁力の統合制御を行う。`RequireComponent` 属性で必須コンポーネントを保証する。

### 5.2 フィールド一覧

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `m_settings` | `PlayerSettings` | SerializeField (private) | プレイヤー設定SO。旧名 `settings` |

### 5.3 プロパティ一覧

| プロパティ | 型 | 説明 |
|---|---|---|
| `input` | `PlayerInputHandler` | 入力ハンドラー |
| `events` | `PlayerEvents` | イベントハブ |
| `states` | `PlayerStateManager` | ステートマシン |
| `Settings` | `PlayerSettings` | 設定SOへのpublicアクセス |
| `magnetizable` | `Magnetizable` | 磁力影響コンポーネント |

### 5.4 メソッド詳細

#### `Awake()` (override)

1. `base.Awake()` で Entity の初期化を実行
2. `GetComponent` で `PlayerInputHandler`, `PlayerEvents`, `PlayerStateManager`, `Magnetizable` を取得
3. `PlayerSettings` の磁力回転パラメータを Entity基底フィールドに反映:
   - `m_pullOrientationThreshold = m_settings.pullOrientationThreshold`
   - `m_pullOrientationSpeed = m_settings.pullOrientationSpeed`
4. `health.OnDie += OnDie` で死亡イベントを購読

#### `OnDestroy()`

`health.OnDie -= OnDie` でイベント購読を解除。メモリリーク防止。

#### `OnDie()` (private)

`states.Change<DiePlayerState>()` を呼び出して死亡ステートに遷移する。

#### `Update()`

毎フレームの更新ループ。処理順序は極めて重要:

```csharp
void Update()
{
    float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
    UpdateMagneticInfluence();    // 1. 磁力影響度の更新
    states.Step(dt);              // 2. ステートの更新（移動処理）
    UpdateGround();               // 3. 接地判定
    UpdateMagneticOrientation(dt);// 4. 空中での磁力方向回転
    ApplyGravity(...);            // 5. 重力
    ApplyMovement(dt);            // 6. 位置反映
}
```

`dt` は `Time.deltaTime` と `Time.fixedDeltaTime * 3` の小さい方を採用。これによりフレームスパイク時に大きなdtでの移動を防止する。

#### `UpdateMagneticInfluence()` (private)

磁力場の影響度に応じて移動パラメータを変調する:
1. `Magnetizable` または `MagnetManager` がnullなら倍率を1.0にリセット
2. `magnetizable.GetInfluence(maxForcePerObject)` で0-1の影響度を取得
3. `topSpeedMultiplier = 1 - influence * damping` -- 強い磁力ほど最高速度が下がる
4. `turningDragMultiplier = 1 + influence * damping` -- 強い磁力ほど旋回が鈍くなる

#### `AccelerateToInputDirection(float dt)`

カメラ相対の入力方向に加速し、進行方向を向く:
1. `GetCameraRelativeDirection(input.MoveInput)` でカメラ相対の方向を取得
2. 方向が十分な大きさなら:
   - `Accelerate()` で加速（Entity基底メソッド）
   - `FaceDirection()` で進行方向に回転

#### `MoveWithInput(float dt)`

通常移動。内部で `AccelerateToInputDirection(dt)` を呼ぶだけのラッパー。

#### `MoveWithInputStrafe(float dt)`

エイム中のストレイフ移動:
1. `GetCameraRelativeDirection` で入力方向を取得
2. `aimMoveSpeedMultiplier` を適用した速度で `Accelerate()`（通常より遅い）
3. **キャラクターの向きは入力方向ではなくカメラの前方方向に固定**
4. 回転速度は通常の2倍（素早くカメラ方向を向く）
5. `adjustUp = false` で回転計算（カメラ前方はワールド空間で直接使用）

#### `SlowDown(float dt)`

`Decelerate(m_settings.deceleration, dt)` で減速する。

#### `RegularSlopeFactor(float dt)`

`SlopeFactor(m_settings.slopeUpwardForce, m_settings.slopeDownwardForce, dt)` で斜面加減速を適用する。

---

## 6. PlayerInputHandler（入力）

**ファイル:** `Scripts/Player/PlayerInputHandler.cs`

### 6.1 クラス定義

```csharp
public class PlayerInputHandler : MonoBehaviour
```

Unity Input Systemの入力をポーリング方式で読み取る。InputActionAssetを直接参照する方式を採用しており、`PlayerInput` コンポーネントのコールバック方式は使わない。

### 6.2 フィールド

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `m_actions` | `InputActionAsset` | SerializeField (private) | Input System の InputActionAsset。旧名 `actions` |
| `m_move` | `InputAction` | private | 移動アクション |
| `m_attack` | `InputAction` | private | 攻撃アクション（RT） |
| `m_aim` | `InputAction` | private | エイムアクション（LT） |
| `m_switchPole` | `InputAction` | private | 磁極切替アクション（Y） |
| `m_reload` | `InputAction` | private | リロードアクション（X） |
| `m_selfFire` | `InputAction` | private | セルフファイアアクション（A/F） |

### 6.3 プロパティ / メソッド

| 名前 | 型 | 説明 |
|---|---|---|
| `MoveInput` | `Vector2` (property) | 移動入力ベクトル（左スティック）。毎フレーム `ReadValue<Vector2>()` |
| `AimHeld` | `bool` (property) | LTが0.5以上押されているか |
| `ConsumeFire()` | `bool` | RTが今フレーム押されたか。`WasPressedThisFrame()` |
| `ConsumeSwitchPole()` | `bool` | Yが今フレーム押されたか |
| `ConsumeReload()` | `bool` | Xが今フレーム押されたか |
| `ConsumeSelfFire()` | `bool` | A/Fが今フレーム押されたか |

### 6.4 ライフサイクル

#### `Awake()`

1. `m_actions` がnullの場合、`PlayerInput` コンポーネントからフォールバック取得
2. アクション名文字列 `"Move"`, `"Attack"`, `"Aim"`, `"SwitchPole"`, `"Reload"`, `"SelfFire"` で各 `InputAction` を取得

#### `OnEnable()` / `OnDisable()`

`m_actions?.Enable()` / `m_actions?.Disable()` で入力アクション全体の有効/無効を切り替え。`DiePlayerState` がこのコンポーネントを `enabled = false` にすることで死亡中の入力を遮断する。

### 6.5 入力方式の設計思想

- **ポーリング方式**: 各コンポーネント（ShootingController, AimController等）が自身のUpdate()で入力を読み取る
- **Consume命名規則**: `ConsumeFire()`, `ConsumeReload()` 等はフレーム内1回限りの入力。`WasPressedThisFrame()` を使用しているため、複数のコンポーネントが同一フレームで呼んでも全てtrueを返す（Input Systemの仕様）
- **AimHeld**: トリガーの閾値0.5を設定。アナログ入力の半分以上を「押下」と判定

---

## 7. PlayerEvents（イベント）

**ファイル:** `Scripts/Player/PlayerEvents.cs`

### 7.1 クラス定義

```csharp
public class PlayerEvents : MonoBehaviour
```

プレイヤーのアクションに関するイベントハブ。各コントローラーがFire系メソッドを呼び、外部システム（UI、エフェクト、サウンド等）がイベントを購読する。

### 7.2 イベント一覧

| イベント | 型 | 発火元 | 説明 |
|---|---|---|---|
| `OnShoot` | `Action` | `ShootingController.Fire()` | 通常射撃時 |
| `OnSelfShoot` | `Action` | `ShootingController.SelfFire()` | セルフファイア時 |
| `OnPoleSwitch` | `Action<MagneticPole>` | `PoleController.Update()` | 磁極切替時。新しい極が引数 |
| `OnReload` | `Action` | `ShootingController.Update()` | リロード時 |

### 7.3 発火メソッド

| メソッド | 説明 |
|---|---|
| `FireShoot()` | `OnShoot?.Invoke()` |
| `FireSelfShoot()` | `OnSelfShoot?.Invoke()` |
| `FirePoleSwitch(MagneticPole pole)` | `OnPoleSwitch?.Invoke(pole)` |
| `FireReload()` | `OnReload?.Invoke()` |

null条件演算子 (`?.`) により、購読者がいない場合は安全にスキップする。

---

## 8. ShootingController（射撃）

**ファイル:** `Scripts/Player/ShootingController.cs`

### 8.1 クラス定義

```csharp
public class ShootingController : MonoBehaviour
```

RT入力で磁力弾を画面中央方向に発射し、A/F入力で自身に磁力を付与する。

### 8.2 フィールド

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `m_bulletSettings` | `BulletSettings` | SerializeField | 弾のパラメータSO（プレハブ、レイキャスト距離等） |
| `m_playerSettings` | `PlayerSettings` | SerializeField | プレイヤー設定SO（firePointHeight等） |
| `m_firePoint` | `Transform` | SerializeField | 発射位置のTransform |
| `m_selfFireHeightOffset` | `float` | SerializeField | セルフファイアの高さオフセット（デフォルト1.0） |
| `m_input` | `PlayerInputHandler` | private | 入力ハンドラー |
| `m_poleController` | `PoleController` | private | 現在の磁極取得用 |
| `m_aimController` | `AimController` | private | エイム解除コールバック用 |
| `m_events` | `PlayerEvents` | private | イベント発火用 |
| `m_mainCamera` | `Camera` | private | メインカメラ参照 |

### 8.3 定数

| 定数 | 型 | 値 | 説明 |
|---|---|---|---|
| `k_ForwardDotThreshold` | `float` | 0.1 | 前方判定の内積閾値 |

### 8.4 Update()の処理フロー

毎フレーム以下の順序で入力をチェック:

1. **リロード（X）**: `ConsumeReload()` がtrueなら `BulletManager.Instance.ClearAll()` で全弾消去、`FireReload()` イベント発火
2. **射撃（RT）**: `ConsumeFire()` がtrueなら:
   - `bulletSettings` と `bulletPrefab` のnullチェック
   - `BulletManager.CanShoot()` で残弾チェック
   - `Fire()` を呼び出し
3. **セルフファイア（A/F）**: `ConsumeSelfFire()` がtrueなら:
   - 同様のnull・残弾チェック
   - `SelfFire()` を呼び出し

### 8.5 Fire() -- 通常射撃

画面中央方向に磁力弾を発射する:

1. **発射位置の確定**: `m_firePoint` があればその位置、なければ `transform.position + Vector3.up * firePointHeight`
2. **画面中央からカメラレイ取得**: `Screen.width * 0.5, Screen.height * 0.5` からスクリーンレイ生成
3. **ターゲット座標の算出**: `CalculateTargetPoint()` を呼び出し
4. **発射方向の算出**: `(targetPoint - spawnPos).normalized`
5. **弾の生成**: `Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(direction))`
6. **弾の初期化**: `bullet.Initialize(pole, direction)` -- 現在の磁極と方向を渡す
7. **イベント発火**: `m_events.FireShoot()`
8. **着弾コールバック**: `bullet.OnImpact += () => aim.StopAim()` -- 弾が着弾したらエイムモードを解除

### 8.6 CalculateTargetPoint() -- ターゲット座標算出

3段階のフォールバックでターゲット座標を決定する:

1. **レイキャストhit**: カメラ中央からレイキャストし、ヒットが発射位置より前方（`Dot(camForward, hit.point - spawnPos) > 0`）なら `hit.point` を返す
2. **平面交差法**: ヒットなし or 後方の場合、発射位置の高さの水平面 (`Plane(Vector3.up, spawnPos)`) とカメラレイの交点を求める。交点が前方かつ `Dot > k_ForwardDotThreshold` なら採用
3. **フォールバック**: 真下向き等で交点が見つからない場合、`spawnPos + camForward * maxDist` を返す

### 8.7 SelfFire() -- セルフファイア

自分の中心付近に弾を生成し、自身を磁化する:
1. 発射位置: `transform.position + Vector3.up * m_selfFireHeightOffset`
2. 方向: `Vector3.down`（下向き）
3. 弾を生成し `bullet.Initialize(pole, direction, isSelfFire: true)` で初期化
4. `m_events.FireSelfShoot()` イベント発火

---

## 9. AimController（エイム）

**ファイル:** `Scripts/Player/AimController.cs`

### 9.1 クラス定義

```csharp
public class AimController : MonoBehaviour
```

LT入力でエイムモード（スロー + カメラ寄り + FOV変更）を制御する。

### 9.2 フィールド

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `m_settings` | `PlayerSettings` | SerializeField | プレイヤー設定SO |
| `m_cameraSettings` | `CameraSettingsApplier` | SerializeField | カメラ設定適用コンポーネント |
| `m_input` | `PlayerInputHandler` | private | 入力ハンドラー |
| `m_states` | `PlayerStateManager` | private | ステートマネージャー |
| `m_aimReleaseGrace` | `float` | private | LT離しのジッター防止用タイマー |

### 9.3 プロパティ

| プロパティ | 型 | 説明 |
|---|---|---|
| `IsAiming` | `bool` | エイム中かどうか |

### 9.4 Update()の処理フロー

```
LT押下中:
  m_aimReleaseGrace = aimReleaseGraceTime (0.15秒)
  IsAiming == false → StartAim()

LT離し中:
  IsAiming == true の場合:
    m_aimReleaseGrace -= Time.unscaledDeltaTime
    m_aimReleaseGrace <= 0 → StopAim()
```

**ジッター防止**: RTを押す瞬間にLTが一瞬離れることがある。`aimReleaseGraceTime`（0.15秒）の猶予時間を設けて、短時間のLT離しではエイムを解除しない。`Time.unscaledDeltaTime` を使用しているため、スロー状態でも正確にタイマーが動作する。

### 9.5 StartAim()

エイムモードを開始する:
1. `IsAiming = true`
2. `Time.timeScale = aimTimeScale`（デフォルト0.3 = 30%速度）
3. `m_cameraSettings.SetAimMode(true)` でカメラをエイム設定に変更
4. `m_states.Change<AimPlayerState>()` でエイムステートに遷移

### 9.6 StopAim()

エイムモードを終了する:
1. `IsAiming = false`
2. `Time.timeScale = 1f` で時間を通常に戻す
3. `m_cameraSettings.SetAimMode(false)` でカメラを通常設定に戻す
4. ステート遷移:
   - 移動入力があれば `MovePlayerState` に遷移
   - なければ `IdlePlayerState` に遷移

### 9.7 OnDisable()

シーン遷移・オブジェクト破棄時に `Time.timeScale = 1f` で強制的にスロー状態を解除する。エイム中にシーンが変わった場合のスロー残り防止。

---

## 10. PoleController（磁極切替）

**ファイル:** `Scripts/Player/PoleController.cs`

### 10.1 クラス定義

```csharp
public class PoleController : MonoBehaviour
```

Y入力で弾の磁極（S/N）を切り替える。

### 10.2 フィールド

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `m_input` | `PlayerInputHandler` | private | 入力ハンドラー |
| `m_events` | `PlayerEvents` | private | イベント発火用 |

### 10.3 プロパティ / イベント

| 名前 | 型 | 説明 |
|---|---|---|
| `CurrentPole` | `MagneticPole` (property) | 現在の磁極。デフォルト `MagneticPole.S` |
| `OnPoleChanged` | `event Action<MagneticPole>` | 磁極変更時に発火。UIの色更新等に使用可能 |

### 10.4 Update()の処理フロー

1. `m_input.ConsumeSwitchPole()` がfalseなら何もしない
2. `CurrentPole` をトグル（S → N → S → ...）
3. `OnPoleChanged?.Invoke(CurrentPole)` で自身のイベント発火
4. `m_events?.FirePoleSwitch(CurrentPole)` でPlayerEventsのイベント発火

イベントが2系統ある理由:
- `OnPoleChanged`: PoleControllerに直接依存するコンポーネント用（UI等）
- `PlayerEvents.OnPoleSwitch`: PlayerEventsを集約ハブとして使うシステム用

### 10.5 MagneticPole列挙型

```csharp
public enum MagneticPole
{
    None,
    S,
    N
}
```

---

## 11. CameraSettingsApplier（カメラ）

**ファイル:** `Scripts/Player/CameraSettingsApplier.cs`

### 11.1 クラス定義

```csharp
public class CameraSettingsApplier : MonoBehaviour
```

Cinemachineカメラのパラメータをエイムモードに応じて切り替える。

### 11.2 フィールド

| フィールド | 型 | アクセス | 説明 |
|---|---|---|---|
| `m_settings` | `PlayerSettings` | SerializeField | プレイヤー設定SO |
| `m_cinemachineCamera` | `CinemachineCamera` | SerializeField | Cinemachineカメラ参照 |
| `m_orbitalFollow` | `CinemachineOrbitalFollow` | private | 軌道追従コンポーネント |
| `m_defaultFOV` | `float` | private | デフォルトの視野角 |
| `m_defaultOrbits` | `Cinemachine3OrbitRig.Settings` | private | デフォルトの軌道設定 |

### 11.3 Start()

1. **プレハブ間参照の自動設定**: `CinemachineCamera.Follow` / `LookAt` がnullの場合、`FindWithTag(GameTags.Player)` でプレイヤーを検索して自動設定。プレハブ間の参照が切れる問題への対策
2. `CinemachineOrbitalFollow` コンポーネントを取得
3. デフォルトのFOVと軌道設定を保存
4. **カーソルロック**: `Cursor.lockState = CursorLockMode.Locked`, `Cursor.visible = false`

### 11.4 SetAimMode(bool aiming)

エイムモードの切り替えを行う:

**aiming = true の場合:**
1. `aimCameraDistance / cameraDistance` でスケール比を算出
2. Top/Center/Bottom の3軌道のRadiusにスケール比を掛ける（Heightは維持）
3. FOVを `aimFOV`（40度）に変更

**aiming = false の場合:**
1. 軌道設定をデフォルトに復元
2. FOVをデフォルトに復元

カメラが寄ることで以下の効果を得る:
- ショルダービュー風の視点になり照準がしやすくなる
- FOVが狭くなることでズーム効果が得られる

---

## 12. 各プレイヤーステート

### 12.1 IdlePlayerState（待機）

**ファイル:** `Scripts/Player/States/IdlePlayerState.cs`

```csharp
public class IdlePlayerState : EntityState<Player>
```

プレイヤーの待機ステート。入力がないときの状態。

#### Step(float dt)

1. `entity.SlowDown(dt)` で横移動速度を減速（完全停止までの慣性）
2. `entity.input.MoveInput.sqrMagnitude > 0.01f` で移動入力判定
3. 入力があれば `manager.Change<MovePlayerState>()` で移動ステートに遷移

#### 遷移図

```
IdlePlayerState
  ├─ 移動入力あり → MovePlayerState
  ├─ LT押下 → AimPlayerState (AimController経由)
  └─ HP == 0 → DiePlayerState (Player.OnDie経由)
```

### 12.2 MovePlayerState（移動）

**ファイル:** `Scripts/Player/States/MovePlayerState.cs`

```csharp
public class MovePlayerState : EntityState<Player>
```

プレイヤーの移動ステート。入力方向に加速し、進行方向を向く。

#### Step(float dt)

1. `entity.MoveWithInput(dt)` で入力方向に加速 + 回転
2. `entity.input.MoveInput.sqrMagnitude < 0.01f` で入力消失判定
3. 入力がなくなれば `manager.Change<IdlePlayerState>()` で待機ステートに遷移

#### 遷移図

```
MovePlayerState
  ├─ 移動入力なし → IdlePlayerState
  ├─ LT押下 → AimPlayerState (AimController経由)
  └─ HP == 0 → DiePlayerState (Player.OnDie経由)
```

### 12.3 AimPlayerState（エイム）

**ファイル:** `Scripts/Player/States/AimPlayerState.cs`

```csharp
public class AimPlayerState : EntityState<Player>
```

エイム状態。カメラ方向を向きながらストレイフ移動する。

#### Step(float dt)

1. `entity.MoveWithInputStrafe(dt)` でストレイフ移動（カメラ方向を向いたまま横移動、速度半減）
2. 移動入力がない場合は `entity.SlowDown(dt)` で減速

#### 遷移図

```
AimPlayerState
  ├─ LT離し → IdlePlayerState or MovePlayerState (AimController.StopAim経由)
  ├─ 弾着弾 → IdlePlayerState or MovePlayerState (bullet.OnImpact経由)
  └─ HP == 0 → DiePlayerState (Player.OnDie経由)
```

注意: AimPlayerStateは自分自身では遷移を発生させない。遷移は常にAimControllerまたは外部イベントが制御する。

### 12.4 DiePlayerState（死亡）

**ファイル:** `Scripts/Player/States/DiePlayerState.cs`

```csharp
public class DiePlayerState : EntityState<Player>
```

プレイヤーの死亡ステート。一定時間後にリスポーンする。

#### Enter(Player entity, EntityStateManager<Player> manager) (override)

1. `base.Enter()` でEntity/manager参照を設定
2. `entity.lateralVelocity = Vector3.zero` で横移動停止
3. `entity.input.enabled = false` で入力を無効化（PlayerInputHandlerのOnDisable → InputAction.Disable）
4. コライダーを無効化（当たり判定を消す）
5. `m_respawnTimer = entity.Settings.respawnDelay`（デフォルト3秒）

#### Step(float dt)

1. `m_respawnTimer -= dt` でタイマー減算
2. タイマーが0以下になったら `Respawn()` 呼び出し

#### Exit() (override)

1. `entity.input.enabled = true` で入力を再有効化
2. コライダーを再有効化

#### Respawn() (private)

リスポーン処理:
1. `GameManager.Instance.GetSpawnPosition()` でスポーン地点を取得
2. `entity.transform.position` をスポーン地点に設定
3. `entity.health.ResetHealth()` でHP全回復
4. 全速度成分（lateral, vertical, external）をゼロに初期化
5. `manager.Change<IdlePlayerState>()` で待機ステートに遷移（これにより `Exit()` が呼ばれ、入力とコライダーが復活する）

#### 遷移図

```
DiePlayerState
  └─ リスポーンタイマー終了 → IdlePlayerState
```

---

## 13. PlayerSettings（パラメータ）

**ファイル:** `Scripts/Data/PlayerSettings.cs`

### 13.1 クラス定義

```csharp
[CreateAssetMenu(fileName = "PlayerSettings", menuName = "MagnetRush/PlayerSettings")]
public class PlayerSettings : ScriptableObject
```

プレイヤーの全パラメータを外出ししたScriptableObject。Unityエディタの「Create > MagnetRush > PlayerSettings」メニューから生成可能。

### 13.2 パラメータ一覧

#### Movement（移動）

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `topSpeed` | `float` | 6.0 | 最高移動速度。旧名 `moveSpeed` |
| `acceleration` | `float` | 30.0 | 加速度。1秒で速度30増加 |
| `deceleration` | `float` | 25.0 | 減速度。1秒で速度25減少 |
| `turningDrag` | `float` | 20.0 | 旋回ドラッグ。方向転換時の横方向減衰速度 |
| `rotationSpeed` | `float` | 15.0 | キャラクターの回転速度（Slerp補間係数） |

#### Gravity（重力）

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `gravity` | `float` | -20.0 | 重力加速度（下方向=負値）。Unityデフォルト(-9.81)より強い |
| `snapForce` | `float` | 2.0 | 接地時の地面スナップ力。0にすると段差で浮く |
| `groundCheckDistance` | `float` | 0.3 | 接地判定の距離（現在未使用 -- Entity.UpdateGround()はcapsuleCollider.heightから算出） |
| `groundLayer` | `LayerMask` | - | 地面判定レイヤー（現在未使用 -- Entity.UpdateGround()はDefaultRaycastLayersを使用） |

#### Camera（カメラ）

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `cameraSensitivityX` | `float` | 200.0 | カメラ水平感度 |
| `cameraSensitivityY` | `float` | 200.0 | カメラ垂直感度 |
| `cameraDistance` | `float` | 5.0 | 通常時のカメラ距離 |
| `shoulderOffset` | `Vector3` | - | 肩越しカメラのオフセット |

#### 射撃

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `firePointHeight` | `float` | 1.2 | 発射位置のデフォルト高さ（m_firePointが未設定時のフォールバック） |

#### Aim（エイム）

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `aimReleaseGraceTime` | `float` | 0.15 | LT離しのジッター防止猶予時間（秒） |
| `aimTimeScale` | `float` | 0.3 | エイム時のタイムスケール（0.3 = 30%速度） |
| `aimFOV` | `float` | 40.0 | エイム時の視野角（度） |
| `aimCameraDistance` | `float` | 3.0 | エイム時のカメラ距離 |
| `aimMoveSpeedMultiplier` | `float` | 0.5 | エイム時の移動速度倍率（50%） |

#### 死亡・リスポーン

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `respawnDelay` | `float` | 3.0 | 死亡からリスポーンまでの時間（秒） |

#### Slope（斜面）

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `slopeUpwardForce` | `float` | 15.0 | 上り坂の減速力 |
| `slopeDownwardForce` | `float` | 25.0 | 下り坂の加速力 |

#### Magnet（磁力）

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `magnetResistance` | `float` | 0.5 | 磁力への耐性値 |

#### 磁力回転

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `pullOrientationThreshold` | `float` | 5.0 | この外部力（magnitude）以上で空中回転開始 |
| `pullOrientationSpeed` | `float` | 8.0 | 磁力方向への回転速度 |

---

## 付録: 関連インターフェース

### IMagnetTarget

```csharp
public interface IMagnetTarget
{
    void ApplyMagnetForce(Vector3 force);
}
```
磁力システムから外部力を受けるインターフェース。`Entity` が実装し、`externalVelocity` に力を蓄積する。

### IMagnetField

```csharp
public interface IMagnetField
{
    MagneticPole Pole { get; }
    Vector3 GetFieldDirection(Vector3 point);
    float GetStrengthAt(Vector3 point);
    int Priority { get; }
}
```
磁力場の情報を提供するインターフェース。`Entity.magnetField` に設定される。

### ステート遷移全体図

```
                    ┌──────────────────┐
                    │  IdlePlayerState │ ← 初期ステート (Start)
                    └──────┬───────────┘
                           │
           移動入力あり     │      移動入力なし
                    ┌──────▼───────────┐
                    │  MovePlayerState │
                    └──────┬───────────┘
                           │
                    LT押下 │ LT離し
          (AimController)  │ (AimController)
                    ┌──────▼───────────┐
                    │  AimPlayerState  │
                    └──────────────────┘
                           │ 弾着弾 (OnImpact)

        ※ 全ステートから HP==0 で遷移可能:
                    ┌──────────────────┐
                    │  DiePlayerState  │
                    └──────┬───────────┘
                           │ リスポーンタイマー終了
                    ┌──────▼───────────┐
                    │  IdlePlayerState │
                    └──────────────────┘
```
