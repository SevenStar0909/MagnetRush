# イベント設計

**スコープ**: プロジェクト内で使われる3種類のイベント機構（`event Action` / `UnityEvent` / `static event`）の使い分け

**関連ファイル**: `Core/Entity/StateMachine/EntityEvents.cs`, `Core/Player/PlayerEvents.cs`, `Core/Entity/Health.cs`, `Core/Entity/StateMachine/EntityStateManager.cs`, `Core/Player/AimController.cs`, `Core/Player/Player.cs`, `Core/Magnet/Magnetizable.cs` 他

---

## なぜ3種類使い分けるのか

| 機構 | 接続元 | 接続先 | 永続化 | 検索性（コード→購読者） |
|---|---|---|---|---|
| `event Action<T>` | コードで `+= handler` | コード | しない | Grep で全購読箇所が出る |
| `UnityEvent` | Inspector上でドラッグ&ドロップ | UnityEvent.Invoke 受け取り側 | する（シリアライズ） | Inspector を開かないと辿れない |
| `static event Action<T>` | コードで `+= handler` | コード（型単位） | しない | Grep で全購読箇所が出る |

要約すると：
- **コードで完結する責務 → `event Action`**（リファクタしやすい、IDE で追跡可能）
- **デザイナーが Inspector で接続する演出 → `UnityEvent`**（プログラマ介入なしで音/エフェクト追加可能）
- **インスタンス参照が取れない / 取りたくないグローバル通知 → `static event`**

---

## `event Action<T>`（コード購読）

### 用途
クラス内の状態変化をコードから購読する。型安全、リファクタ追跡可能。

### プロジェクト内の使用例

| 発火元 | イベント | 購読側 | 用途 |
|---|---|---|---|
| `Health` | `OnDamage(int)` | UI / VFX | ダメージ表示 |
| `Health` | `OnDie()` | DiePlayerState, EnemyBase | 死亡処理 |
| `Health` | `OnHeal(int)` | UI | 回復表示 |
| `BulletManager` | `OnBulletCountChanged(int)` | AmmoUI | 弾数表示更新 |
| `MagnetBullet` | `OnImpact()` | VFX 等 | 着弾通知 |
| `MagnetField` | `OnFieldExpired()` | MagnetManager | 磁場消滅時の Joint/PDホルダー連動解除 |
| `EntityStateManager` | `OnStateEnter(Type)`, `OnStateExit(Type)`, `OnStateChanged()` | `EntityStateManagerListener` 等 | ステート遷移通知 |
| `Magnetizable` | `OnPoleChanged(MagneticPole)` | UI（極性表示） | 極性変化 |
| `Magnetizable` | `OnMagnetContact(Magnetizable)` | エフェクト等 | 接触瞬間 |
| `PoleController` | `OnPoleChanged(MagneticPole)` | UI / Player.SwitchPole | プレイヤー極性変化 |
| `PlayerEvents` | `OnShoot`, `OnSelfShoot`, `OnPoleSwitch(MagneticPole)`, `OnReload` | UI / VFX / SE | プレイヤーの行動通知 |

### 命名規則
- **発火元視点で `On〜`**（例: `OnDie`, `OnImpact`）
- 過去形ではなく**動詞または状態名**を使う
- ペイロード型は `Action<T>` の `T` で表す

### 購読パターン
```csharp
// 購読側
void OnEnable()  { health.OnDie += HandleDie; }
void OnDisable() { health.OnDie -= HandleDie; }   // 解除を忘れない
```

`OnEnable`/`OnDisable` で対称にする。`Awake`/`OnDestroy` でも可だが、無効化中も購読が残るとシーン遷移時に NullRef を吐く。

---

## `UnityEvent`（Inspector接続）

### 用途
**プログラマ以外（デザイナー、QA、企画）が Inspector 上で SE/VFX/UI 演出を接続できるよう開放する**フック。

### 設計原則

| 適切な用途 | 不適切な用途 |
|---|---|
| 接地音、攻撃ヒット音、被弾フラッシュ等の演出 | コアロジックのフロー制御 |
| ステート遷移時の汎用フック | ステート間の状態同期 |
| Inspector で頻繁に差し替えたい接続 | 1対1の固定接続（直接参照で十分） |

### プロジェクト内の使用例

| 発火元 | イベント | 用途 |
|---|---|---|
| `Entity` (`EntityEvents`) | `onGroundEnter`, `onGroundExit` | 接地音、着地エフェクト等のデザイナー演出 |
| `EntityStateManagerListener` | `onEnter`, `onExit` | 指定ステート遷移時のSE/VFX |

### `EntityEvents` の構造
```csharp
[Serializable]
public class EntityEvents
{
    [Tooltip("接地した瞬間に発火")]
    public UnityEvent onGroundEnter;

    [Tooltip("地面から離れた瞬間に発火")]
    public UnityEvent onGroundExit;
}
```

`MonoBehaviour` ではなく `[Serializable]` クラス。Entity.cs の `[SerializeField]` として保持され、Inspector から接続される。

### 命名規則
- **`camelCase` の `on〜`**（C# `event` の `OnPascalCase` と区別）
- フィールド名のため、PascalCase の event とは別の命名

### `EntityStateManagerListener` パターン
特定のステート遷移時のみ UnityEvent を発火させたい場合は、Listener コンポーネントに `EntityStateManager.OnStateEnter` を購読させ、フィルタしてから `UnityEvent.Invoke` する。これにより：
- コアロジック側の `event Action` は型情報を持つ
- Inspector 接続は Listener が橋渡しする

---

## `static event Action<T>`（グローバル通知）

### 用途
**インスタンス参照を取らずに購読したい**場合。サブシステム間の疎結合通知。

### プロジェクト内の使用例

| 発火元 | イベント | 用途 |
|---|---|---|
| `Player.OnPlayerReady(Player)` | static | Player 初期化完了通知（UI / カメラ / EnemyAI が購読、Player参照を受け取れる） |
| `AimController.OnAimChanged(bool)` | static | エイム状態変化通知（Reticle UI、感度調整等） |

### 設計判断
- **Singleton.Instance** でも代用可能だが、static event は「初期化完了タイミングに依存しない」「Instance 参照が要らない」のが利点
- ただし**購読解除を忘れるとシーン遷移後も購読が残り** メモリリークやNullRef の原因になる
- 使用時は必ず `OnEnable`/`OnDisable` で対称的に管理する

### 注意
シーン跨ぎで `static` フィールドは初期化されない（`Domain Reload` 設定次第で残る）。`Player.OnPlayerReady` のように、シーンロードのたびに新しい Player が登録される設計の場合は問題にならないが、**シーン遷移時に購読リストをクリアするケアが必要**な場合がある。

---

## 使い分け早見表

```
このイベントは…
├─ Inspector で SE/VFX を差し替えたい？
│  ├─ Yes → UnityEvent
│  └─ No  → 次へ
│
├─ インスタンス参照が取れる？
│  ├─ Yes → event Action<T>
│  └─ No  → static event Action<T>
```

---

## 命名一覧

| 種類 | 命名 | 例 |
|---|---|---|
| `event Action` | `OnPascalCase` | `OnDie`, `OnImpact`, `OnPoleChanged` |
| `UnityEvent` | `oncamelCase` | `onGroundEnter`, `onEnter` |
| `static event` | `OnPascalCase`（同上） | `OnPlayerReady`, `OnAimChanged` |
| 発火メソッド（プライベート） | `Fire〜` | `FireShoot()`, `FireReload()` |
| 入力消費メソッド | `Consume〜` | `ConsumeFire()`, `ConsumeReload()` |

`naming-conventions.md` 準拠。

---

## アンチパターン

### コード購読の責務を UnityEvent に持たせる
```csharp
// ❌ NG: ステート間遷移を UnityEvent で繋ぐ
[SerializeField] UnityEvent onEnemyDied;  // → EnemyManager.UnregisterEnemy を Inspector で接続
```
→ Inspector 接続が切れると EnemyManager に登録残留。リファクタで気付きにくい。

```csharp
// ✓ OK: コアロジックは event Action でコード結合、UnityEvent は演出用に開放
public event Action OnDie;   // → EnemyManager が購読
[SerializeField] UnityEvent onDieEffect;  // → 死亡SE/VFX
```

### static event の購読解除忘れ
```csharp
// ❌ NG: 解除なし → シーン遷移後に NullRef
void Awake() { Player.OnPlayerReady += HandleReady; }

// ✓ OK
void OnEnable()  { Player.OnPlayerReady += HandleReady; }
void OnDisable() { Player.OnPlayerReady -= HandleReady; }
```

### `Action` のチェーン購読でnullチェック忘れ
```csharp
// ❌ NG
event Action OnDie;
OnDie();   // 誰も購読していないとNullRef

// ✓ OK
OnDie?.Invoke();
```

---

## 関連ドキュメント

- [Core/Player](../Core/Player.md) — `PlayerEvents` / `Player.OnPlayerReady` / `AimController.OnAimChanged` / `PoleController.OnPoleChanged`
- [Core/Entity/StateMachine](../Core/Entity/StateMachine.md) — `EntityStateManager` / `EntityStateManagerListener`
- [Core/Entity](../Core/Entity.md) — `Entity.entityEvents`（UnityEvent）, `Health` の `event Action`
- [Core/Magnet](../Core/Magnet.md) — `Magnetizable.OnPoleChanged` / `OnMagnetContact`
- [Core/Magnet/Field](../Core/Magnet/Field.md) — `MagnetField.OnFieldExpired`
