# Player Platformer-Style Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MagnetRush の Player 構造を Platformer Project 形式に完全移植する。独立 MonoBehaviour の `ShootingController` / `AimController` / `PolarityController` を廃止し、全能力を `Player.cs` のメソッドに集約。State クラスから `player.Fire()` / `player.StartAim()` / `player.SwitchPole()` で呼ぶ形にする。

**Architecture:** Platformer Project の Player 集約パターンに準拠。`Player.cs` が「プレイヤーが持つ全能力」のインターフェースとして振る舞い、各 State クラスが OnStep でその能力を列挙する。状態遷移はメソッド内で `states.Change<XxxState>()` を呼ぶ。`ClassTypeName` PropertyDrawer で `PlayerStateManager.states` を Inspector 駆動の文字列配列化し、State の追加/削除を Inspector から可能にする。

**Tech Stack:** Unity 6 / C# 9+ / Reflection (Activator) / Custom PropertyDrawer / 既存 `EntityStateManager<T>` / `PlayerAnimator` / Input System

---

## File Structure

**Modify:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs` ── PolarityController/AimController/ShootingController の全ロジックを吸収。168 → ~500 行
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs` ── `m_aim` フィールド削除、`m_player.IsAiming` 参照に置換
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateManager.cs` ── 固定 new 登録 → `string[] states` Inspector 駆動化
- `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs` ── `GetStateList()` abstract method 追加
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/IdlePlayerState.cs` ── `player.SwitchPole()` / `.Fire()` / `.SelfFire()` / `.Reload()` / `.StartAim()` 呼び出し追加
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/MovePlayerState.cs` ── 同上
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/AimPlayerState.cs` ── `player.Fire()` / `.SelfFire()` / `.StopAim()` / `.SwitchPole()` 追加
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/DiePlayerState.cs` ── 変更最小限（死亡中は何もしない）
- `Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs` ── `m_polarityController` → `m_player` に置換
- `Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs` ── 同上、`AimController` 参照も `Player` に
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/CameraSettingsApplier.cs` ── `AimController.OnAimChanged` → `Player.OnAimChanged` に
- `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` ── `ShootingController` / `AimController` / `PolarityController` コンポーネント削除、Player 側に SerializeField 再 drag
- `docs/player-animation-guide.md` ── 「拡張者向け: 新 State 追加パターン」章追加

**Create:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/ClassTypeNameAttribute.cs` ── Platformer から移植（namespace なしに調整）
- `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/Editor/ClassTypeNameDrawer.cs` ── 同上、エディタ拡張
- `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/Editor/MagnetRush.StateMachine.Editor.asmdef` ── エディタ用 asmdef（既存の asmdef 確認後必要なら）

**Delete:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs` + `.meta`
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs` + `.meta`
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs` + `.meta`

---

## Verification Model

Unity プロジェクトに xUnit 基盤はないため:

1. **Compile 検証** ── UnityMCP `read_console` でエラー 0 確認
2. **PlayMode スモーク** ── 移動 / 射撃 / エイム / 磁極切替 / リロード / 死亡リスポーンが regression なし
3. **Diff 確認** ── 各タスク末尾で `git diff` 目視

各タスク末尾で compile 確認。タスク単位では PlayMode までは回さない（最終 Task 10 でまとめて）。

---

## Migration Strategy

1. **依存が少ない順に移行**: PolarityController → AimController → ShootingController
2. **各 controller 移行は 1 コミット**（ロジック移動 + 参照更新 + 古いファイル削除 + prefab 更新を atomic に）
3. **State クラスと ClassTypeName は controllers 移行後**
4. **prefab 更新は UnityMCP `manage_components` 経由**（CLAUDE.md ルール: `.prefab` 直編集禁止）

---

## Task 1: `PolarityController` を Player.cs に吸収

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs`
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs` + `.meta`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` (via MCP)

**現状把握:**
- `PolarityController` は 36 行、`MonoBehaviour` + 独立 Update()
- フィールド: `CurrentPole` (MagneticPole, get public/set private)
- イベント: `event Action<MagneticPole> OnPolarityChanged` (subscribers: AmmoUI, ReticleUI)
- メソッド: Update() で Y 入力 → 極切替 → OnPolarityChanged 発火 + `m_events.FirePolaritySwitch()`

**移行後の Player.cs への追加:**

- [ ] **Step 1: Player.cs に PolarityController 相当フィールド・プロパティ・イベント・メソッドを追加**

Player.cs の using に追加（既存に無ければ）:

```csharp
using System;
```

Player.cs のクラス内、`public Magnetizable magnetizable { get; private set; }` の直下に追加:

```csharp
// --- 磁極制御 ---

/// <summary>現在の磁極（S または N）。</summary>
public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

/// <summary>磁極切替時に発火。UI 等が購読。</summary>
public event Action<MagneticPole> OnPolarityChanged;

/// <summary>
/// Y 入力があれば磁極を切り替える。毎フレーム呼ぶ前提。
/// </summary>
public void SwitchPole()
{
    if (!input.ConsumeSwitchPole()) return;
    CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
    OnPolarityChanged?.Invoke(CurrentPole);
    events?.FirePolaritySwitch();
}
```

注意: 既存 `PolarityController.Update` の `ChannelLogger.LogGuardReturn` は削除（`SwitchPole()` は毎フレーム呼ばれる設計で「入力なし = スキップ」は正常動作、ログ不要）。

- [ ] **Step 2: Player.cs の Update() で SwitchPole() を呼ぶ（一時的、将来 State 側に移す）**

現状 `void Update()` に `UpdateMagneticInfluence();` の次行に追加:

```csharp
void Update()
{
    float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
    UpdateMagneticInfluence();
    SwitchPole();                        // 一時的にここで呼ぶ（Task 5 で State 側に移す）
    states.UpdateState(dt);

    if (!states.IsCurrentOfType<DiePlayerState>())
        UpdateEntity(dt);
}
```

この時点では `SwitchPole` の呼び出し場所は Player.Update。State に移すのは Task 5。

- [ ] **Step 3: AmmoUI.cs を書き換えて Player 参照に変更**

`Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs` を Read で読んで確認。`m_polarityController` フィールドを `m_player` に置換:

置換パターン:
- `private PolarityController m_polarityController;` → `private Player m_player;`
- `m_polarityController = player.GetComponent<PolarityController>();` → `m_player = player;`
- `m_polarityController.OnPolarityChanged` → `m_player.OnPolarityChanged`
- `m_polarityController.CurrentPole` → `m_player.CurrentPole`

- [ ] **Step 4: ReticleUI.cs を同様に書き換え**

`m_polarityController` 関連のみ置換。`AimController` 関連は Task 2 で扱う（ここでは触らない）。

- [ ] **Step 5: PolarityController.cs と .meta を削除**

UnityMCP 経由で削除（CLAUDE.md ルール遵守）:

```
manage_asset(action="delete", path="Assets/_Project/Scripts/Core/Player/PolarityController.cs")
```

`.meta` は UnityMCP が自動処理する。

- [ ] **Step 6: `_Player.prefab` から PolarityController コンポーネントを削除**

```
manage_components(
    action="remove",
    target="Assets/_Project/Prefabs/Player/_Player.prefab",
    component_name="PolarityController"
)
```

- [ ] **Step 7: UniCLI / MCP で compile 確認**

```
refresh_unity()
read_console(types=["error"], count=50)
```

Expected: エラー 0。警告の `PolarityController` 参照残存があれば Step 3-4 の置換漏れ。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor(player): PolarityController を Player.cs に吸収"
```

---

## Task 2: `AimController` を Player.cs に吸収

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/CameraSettingsApplier.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs` (m_aim フィールド削除)
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs` (m_aimController 参照を一時的に GetComponent<Player> に。Task 3 で再度修正)
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs` + `.meta`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` (via MCP)

**現状把握:**
- `AimController` は 90 行、`MonoBehaviour` + 独立 Update()
- フィールド: `IsAiming` (bool, get public/set private), `m_aimReleaseGrace` (float timer)
- 静的イベント: `static event Action<bool> OnAimChanged` (subscribers: CameraSettingsApplier)
- メソッド: `StartAim()` / `StopAim()` (どちらも `IsAiming` 切替 + `Time.timeScale` 変更 + state 遷移)
- Update(): LT 入力検知 → aimReleaseGrace タイマー管理 → StartAim/StopAim 呼び分け

- [ ] **Step 1: Player.cs に AimController 相当のフィールド・イベント・メソッドを追加**

`CurrentPole` / `OnPolarityChanged` の直下に追加:

```csharp
// --- エイム制御 ---

/// <summary>エイム中かどうか。</summary>
public bool IsAiming { get; private set; }

/// <summary>エイム状態変化時に発火。CameraSettingsApplier 等が購読。</summary>
public static event Action<bool> OnAimChanged;

private float m_aimReleaseGrace;

/// <summary>LT 入力に応じてエイムモードを開始/維持する。毎フレーム呼ぶ。</summary>
public void HandleAimInput()
{
    if (input.AimHeld)
    {
        m_aimReleaseGrace = m_settings.aimReleaseGraceTime;
        if (!IsAiming) StartAim();
    }
    else if (IsAiming)
    {
        m_aimReleaseGrace -= Time.unscaledDeltaTime;
        if (m_aimReleaseGrace <= 0f) StopAim();
    }
}

/// <summary>エイムモード開始。スロー + ステート遷移。</summary>
public void StartAim()
{
    IsAiming = true;
    Time.timeScale = m_settings.aimTimeScale;
    OnAimChanged?.Invoke(true);
    states.Change<AimPlayerState>();
}

/// <summary>エイムモード終了。入力があれば Move、なければ Idle に戻る。</summary>
public void StopAim()
{
    IsAiming = false;
    Time.timeScale = 1f;
    OnAimChanged?.Invoke(false);

    if (input != null && input.MoveInput.sqrMagnitude > 0.01f)
        states.Change<MovePlayerState>();
    else
        states.Change<IdlePlayerState>();
}
```

注意: 元の `OnAimChanged` は `static` なので `Player.OnAimChanged` で参照する。移植後も `static` のまま（理由: シーン跨ぎシングルトン的用途でそう書かれていた）。将来インスタンスイベントに変えたい場合は別タスク。

- [ ] **Step 2: Player.cs の Update() に `HandleAimInput()` 呼び出しを追加**

既存:
```csharp
void Update()
{
    float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
    UpdateMagneticInfluence();
    SwitchPole();
    states.UpdateState(dt);
    ...
}
```

変更後（`SwitchPole()` の次に追加）:
```csharp
void Update()
{
    float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
    UpdateMagneticInfluence();
    SwitchPole();
    HandleAimInput();
    states.UpdateState(dt);
    ...
}
```

- [ ] **Step 3: Player.cs の OnDisable で Time.timeScale を戻す（元の AimController.OnDisable にあった挙動を保持）**

Player.cs に追加:

```csharp
void OnDisable()
{
    // シーン遷移・オブジェクト破棄時にスロー状態を強制解除
    if (IsAiming)
    {
        Time.timeScale = 1f;
    }
}
```

既存に `void OnDisable()` があれば統合。無ければ新規。

- [ ] **Step 4: CameraSettingsApplier.cs を書き換え**

`AimController.OnAimChanged` → `Player.OnAimChanged` に置換:

```csharp
// Before
AimController.OnAimChanged += SetAimMode;
AimController.OnAimChanged -= SetAimMode;

// After
Player.OnAimChanged += SetAimMode;
Player.OnAimChanged -= SetAimMode;
```

- [ ] **Step 5: ReticleUI.cs から AimController 参照を Player に変更**

`m_aimController` フィールド / 参照を `m_player` に置換。`IsAiming` などの参照は `m_player.IsAiming` に。

既存:
```csharp
private AimController m_aimController;
m_aimController = player.GetComponent<AimController>();
```

変更後:
```csharp
private Player m_player;
m_player = player;  // 既に PlayerEvents 経由で取得済みなら流用
```

ファイル全体を Read して、AimController 参照箇所を全て更新。

- [ ] **Step 6: PlayerAnimator.cs から `m_aim` フィールドを削除、`m_player.IsAiming` 参照に変更**

既存の `[SerializeField] private AimController m_aim;` と Awake の `if (m_aim == null) ...` を削除。

代わりに:
```csharp
[Tooltip("Player 本体。未設定なら親から自動取得")]
[SerializeField] private Player m_player;

// Awake 内
if (m_player == null) m_player = GetComponentInParent<Player>();

// LateUpdate 内: m_aim.IsAiming → m_player.IsAiming
if (m_player != null)
{
    m_animator.SetBool(m_hIsAiming, m_player.IsAiming);
}
```

`m_entity` は既に `Entity` 型なので残す。`m_player` を追加。

- [ ] **Step 7: ShootingController.cs の `m_aimController` 参照を一時的に Player に置換**

Task 3 で ShootingController 自体を消すので、一時しのぎ。既存:
```csharp
private AimController m_aimController;
m_aimController = GetComponent<AimController>();
// later: m_aimController.StopAim()
```

変更後（コンパイルを通すため）:
```csharp
private Player m_player;
m_player = GetComponent<Player>();
// later: m_player.StopAim()
```

- [ ] **Step 8: AimController.cs と .meta を削除**

```
manage_asset(action="delete", path="Assets/_Project/Scripts/Core/Player/AimController.cs")
```

- [ ] **Step 9: `_Player.prefab` から AimController コンポーネント削除**

```
manage_components(
    action="remove",
    target="Assets/_Project/Prefabs/Player/_Player.prefab",
    component_name="AimController"
)
```

- [ ] **Step 10: Compile 確認**

```
refresh_unity()
read_console(types=["error"], count=50)
```

Expected: エラー 0。

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "refactor(player): AimController を Player.cs に吸収"
```

---

## Task 3: `ShootingController` を Player.cs に吸収

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs` + `.meta`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` (via MCP)

**現状把握:**
- `ShootingController` は 198 行、`MonoBehaviour` + 独立 Update()
- SerializeField: `m_bulletSettings` (BulletSettings SO), `m_firePoint` (Transform), `m_selfFireHeightOffset` (float=1.0)
- private 参照: `m_playerSettings`, `m_input`, `m_polarityController`, `m_aimController` (→ Task 2 で `m_player` に変更済み), `m_events`, `m_mainCamera`
- const: `k_ForwardDotThreshold = 0.1f`
- メソッド: `Update()` (input consumption), `Fire()`, `SelfFire()`, `CalculateTargetPoint()`

**注意: Inspector 設定の移行**
- ShootingController の SerializeField (m_bulletSettings / m_firePoint / m_selfFireHeightOffset) は `_Player.prefab` に設定済み
- Player に移すと **Inspector で再 drag 必要**（SerializeField は component 単位、自動マイグレーションなし）
- 手順として: Step 1 で Player に field 追加 → Step 2 でコンポーネント削除前に値を記録 → Step 3 で Player 側に再 set → Step 4 で ShootingController 削除
- より安全: MCP で `manage_prefabs` でPlayer コンポーネントに直接フィールド値を set する

- [ ] **Step 1: Player.cs に ShootingController 相当の SerializeField を追加**

Player.cs の既存 `[SerializeField] private PlayerSettings m_settings;` の次に追加:

```csharp
[Header("Shooting")]
[FormerlySerializedAs("bulletSettings")]
[SerializeField] private BulletSettings m_bulletSettings;

[FormerlySerializedAs("firePoint")]
[SerializeField] private Transform m_firePoint;

[SerializeField] private float m_selfFireHeightOffset = 1.0f;

private Camera m_mainCamera;

private const float k_ForwardDotThreshold = 0.1f;
```

`using UnityEngine.Serialization;` は既に Player.cs に含まれている（既存の `FormerlySerializedAs` 使用のため）。

- [ ] **Step 2: Player.cs の Start() で MainCamera を取得**

既存 Player.cs に `void Start()` が無ければ追加:

```csharp
void Start()
{
    m_mainCamera = Camera.main;
}
```

- [ ] **Step 3: Player.cs に Fire() / SelfFire() / CalculateTargetPoint() メソッドを追加**

これは ShootingController.cs から移植。`m_polarityController.CurrentPole` → `CurrentPole`、`m_aimController.StopAim()` → `StopAim()`、`m_events.FireShoot()` → `events.FireShoot()` に置換。

Player.cs 末尾に追加:

```csharp
// --- 射撃 ---

/// <summary>RT 入力があれば通常射撃。毎フレーム呼ぶ。</summary>
public void Fire()
{
    if (!input.ConsumeFire()) return;
    if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
    { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定"); return; }
    if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
    { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可"); return; }
    if (m_mainCamera == null)
    { ChannelLogger.LogGuardReturn("Player", "MainCameraなし"); return; }

    // 発射位置を先に確定
    float height = m_settings != null ? m_settings.firePointHeight : 1.2f;
    Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;

    // 画面中央からカメラレイ取得
    Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
    Ray ray = m_mainCamera.ScreenPointToRay(screenCenter);
    Vector3 camForward = m_mainCamera.transform.forward;

    int layerMask = PhysicsLayers.MaskShootingRaycast;
    float maxDist = m_bulletSettings.raycastDistance;

    Vector3 targetPoint = CalculateTargetPoint(ray, camForward, spawnPos, layerMask, maxDist);

    float debugDuration = 3.0f;
    Debug.DrawLine(ray.origin, targetPoint, Color.cyan, debugDuration);
    Debug.DrawLine(spawnPos, targetPoint, Color.yellow, debugDuration);

    Vector3 direction = (targetPoint - spawnPos).normalized;

    GameObject bulletObj = Instantiate(m_bulletSettings.bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
    var bullet = bulletObj.GetComponent<MagnetBullet>();
    if (bullet != null)
    {
        bullet.Initialize(CurrentPole, direction);
        BulletManager.Instance.Register(bullet);
        bullet.OnImpact += StopAim;
    }

    events?.FireShoot();
}

/// <summary>A / F 入力があればセルフファイア。毎フレーム呼ぶ。</summary>
public void SelfFire()
{
    if (!input.ConsumeSelfFire()) return;
    if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
    { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定(SelfFire)"); return; }
    if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
    { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可(SelfFire)"); return; }

    if (magnetizable != null)
        magnetizable.SetPole(CurrentPole);

    var fieldSettings = m_bulletSettings.bulletFieldSettings;
    if (fieldSettings != null)
    {
        var existing = GetComponent<MagnetField>();
        if (existing == null)
        {
            var field = gameObject.AddComponent<MagnetField>();
            field.Initialize(CurrentPole, fieldSettings);

            if (MagnetManager.Instance != null)
                MagnetManager.Instance.RegisterField(field);

            var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
            visualizer.Show(CurrentPole, fieldSettings);

            GameObject effectPrefab = CurrentPole == MagneticPole.S
                ? m_bulletSettings.impactEffect_S
                : m_bulletSettings.impactEffect_N;
            GameObject effectInstance = null;
            if (effectPrefab != null)
            {
                effectInstance = Instantiate(effectPrefab, transform);
                effectInstance.transform.localPosition = Vector3.zero;
            }

            field.OnFieldExpired += () =>
            {
                if (magnetizable != null) magnetizable.Deactivate();
                if (visualizer != null) Destroy(visualizer);
                if (effectInstance != null) Destroy(effectInstance);
            };
        }
    }

    if (BulletManager.Instance != null)
        BulletManager.Instance.IncrementShotCount();

    events?.FireSelfShoot();
}

/// <summary>射撃時のリロード（X 入力）。毎フレーム呼ぶ。</summary>
public void Reload()
{
    if (!input.ConsumeReload()) return;
    if (BulletManager.Instance == null) return;
    BulletManager.Instance.ClearAll();
    events?.FireReload();
}

/// <summary>弾道計算。カメラレイ交差 → 平面交差 → 前方フォールバック。</summary>
private Vector3 CalculateTargetPoint(Ray ray, Vector3 camForward, Vector3 spawnPos, int layerMask, float maxDist)
{
    if (Physics.Raycast(ray, out RaycastHit hit, maxDist, layerMask))
    {
        if (Vector3.Dot(camForward, hit.point - spawnPos) > 0f)
            return hit.point;
    }

    Plane firePlane = new Plane(Vector3.up, spawnPos);
    if (firePlane.Raycast(ray, out float enter) && enter > 0f)
    {
        Vector3 intersection = ray.GetPoint(enter);
        Vector3 toIntersection = (intersection - spawnPos).normalized;
        if (Vector3.Dot(camForward, toIntersection) > k_ForwardDotThreshold)
            return intersection;
    }

    return spawnPos + camForward * maxDist;
}
```

- [ ] **Step 4: Player.cs の Update() に Fire / SelfFire / Reload 呼び出しを追加（一時的、Task 5 で State 側に移す）**

既存:
```csharp
void Update()
{
    float dt = ...;
    UpdateMagneticInfluence();
    SwitchPole();
    HandleAimInput();
    states.UpdateState(dt);
    ...
}
```

変更後:
```csharp
void Update()
{
    float dt = ...;
    UpdateMagneticInfluence();
    SwitchPole();
    HandleAimInput();
    Fire();
    SelfFire();
    Reload();
    states.UpdateState(dt);
    ...
}
```

- [ ] **Step 5: ShootingController.cs と .meta を削除**

```
manage_asset(action="delete", path="Assets/_Project/Scripts/Core/Player/ShootingController.cs")
```

- [ ] **Step 6: `_Player.prefab` から ShootingController コンポーネント削除**

```
manage_components(
    action="remove",
    target="Assets/_Project/Prefabs/Player/_Player.prefab",
    component_name="ShootingController"
)
```

- [ ] **Step 7: Player component の SerializeField (bulletSettings / firePoint / selfFireHeightOffset) を _Player.prefab に再設定**

削除前の ShootingController の設定値を Inspector 上で控えておく必要あり。`git show HEAD~1:Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` で旧 ShootingController フィールド値を確認。

MCP 経由で Player コンポーネントのフィールドに set:

```
manage_components(
    action="set",
    target="Assets/_Project/Prefabs/Player/_Player.prefab",
    component_name="Player",
    properties={
        "m_bulletSettings": "<旧設定の BulletSettings アセット参照>",
        "m_firePoint": "<旧設定の Transform 参照、例: _Player/FirePoint>",
        "m_selfFireHeightOffset": 1.0
    }
)
```

`FormerlySerializedAs("bulletSettings")` を付けているので、**同じ prefab 内で `bulletSettings` フィールド名だった場合は自動マイグレーションが効く可能性**あり。試してみて効かなかったら手動 set。

- [ ] **Step 8: Compile 確認**

```
refresh_unity()
read_console(types=["error"], count=50)
```

Expected: エラー 0。

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "refactor(player): ShootingController を Player.cs に吸収"
```

---

## Task 4: State クラスに Player メソッド呼び出しを並べる

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/IdlePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/MovePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/AimPlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/DiePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs` (Update() から Task 1-3 で入れた一時呼び出しを削除)

**目的:** Platformer Project の State パターンに合わせて、各 State の `UpdateState` が Player メソッドを列挙する形にする。Player.Update は State 駆動のみに戻す。

- [ ] **Step 1: IdlePlayerState.cs を書き換え**

既存:
```csharp
public class IdlePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.SlowDown(dt);

        if (m_entity.input.MoveInput.sqrMagnitude > 0.01f)
            m_manager.Change<MovePlayerState>();
    }
}
```

変更後:
```csharp
public class IdlePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.SlowDown(dt);
        m_entity.SwitchPole();
        m_entity.HandleAimInput();
        m_entity.Fire();
        m_entity.SelfFire();
        m_entity.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude > 0.01f)
            m_manager.Change<MovePlayerState>();
    }
}
```

- [ ] **Step 2: MovePlayerState.cs を書き換え**

既存:
```csharp
public override void UpdateState(float dt)
{
    m_entity.AccelerateToInputDirection(dt);

    if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        m_manager.Change<IdlePlayerState>();
}
```

変更後:
```csharp
public override void UpdateState(float dt)
{
    m_entity.AccelerateToInputDirection(dt);
    m_entity.SwitchPole();
    m_entity.HandleAimInput();
    m_entity.Fire();
    m_entity.SelfFire();
    m_entity.Reload();

    if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        m_manager.Change<IdlePlayerState>();
}
```

- [ ] **Step 3: AimPlayerState.cs を書き換え**

既存:
```csharp
public override void UpdateState(float dt)
{
    m_entity.MoveWithInputStrafe(dt);

    if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        m_entity.SlowDown(dt);
}
```

変更後:
```csharp
public override void UpdateState(float dt)
{
    m_entity.MoveWithInputStrafe(dt);
    m_entity.SwitchPole();
    m_entity.HandleAimInput();   // 内部で aimReleaseGrace 減少 → StopAim で状態遷移
    m_entity.Fire();
    m_entity.SelfFire();
    m_entity.Reload();

    if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        m_entity.SlowDown(dt);
}
```

- [ ] **Step 4: DiePlayerState.cs は変更なし**

死亡中は何もしない（既存通り）。Fire / SwitchPole 等を呼ばないことで「死亡中は操作不能」を表現（意図的）。

- [ ] **Step 5: Player.cs の Update() から一時的な直接呼び出しを削除**

既存（Task 3 時点）:
```csharp
void Update()
{
    float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
    UpdateMagneticInfluence();
    SwitchPole();
    HandleAimInput();
    Fire();
    SelfFire();
    Reload();
    states.UpdateState(dt);
    ...
}
```

変更後:
```csharp
void Update()
{
    float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
    UpdateMagneticInfluence();
    states.UpdateState(dt);   // State 側で SwitchPole/Fire/etc を呼ぶ

    if (!states.IsCurrentOfType<DiePlayerState>())
        UpdateEntity(dt);
}
```

- [ ] **Step 6: Compile 確認**

```
refresh_unity()
read_console(types=["error"], count=30)
```

Expected: エラー 0。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor(player): State クラスが Player メソッドを列挙する Platformer 形式に"
```

---

## Task 5: `ClassTypeName` 属性と PropertyDrawer を移植

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/ClassTypeNameAttribute.cs`
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/Editor/ClassTypeNameDrawer.cs`
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/Editor/MagnetRush.StateMachine.Editor.asmdef` (既存 asmdef 構成次第で不要かも)

**目的:** Inspector で State クラスをドロップダウン選択できるようにする。Platformer Project の `ClassTypeName` を namespace 無しで移植。

- [ ] **Step 1: 既存 asmdef 構成を確認**

```bash
find Magnet_Rush/Assets/_Project/Scripts/Core/Entity -name "*.asmdef" -o -name "Editor"
```

既存で `StateMachine/Editor/` フォルダが無ければ作成、asmdef が Core 側で Editor を含めない構成なら新規 Editor 用 asmdef が必要。

- [ ] **Step 2: `ClassTypeNameAttribute.cs` を新規作成**

MCP `create_script` or UniCLI 経由で空ファイル作成後、以下を書き込み:

```csharp
using System;
using UnityEngine;

/// <summary>
/// 指定型のサブクラス一覧を Inspector でドロップダウン選択させる属性。
/// 対象フィールドは string 型で、選択されたクラスの AssemblyQualifiedName が保存される。
/// </summary>
public class ClassTypeName : PropertyAttribute
{
    public Type type;

    public ClassTypeName(Type type)
    {
        this.type = type;
    }
}
```

- [ ] **Step 3: `Editor/ClassTypeNameDrawer.cs` を新規作成**

同様に MCP 経由でファイル作成:

```csharp
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ClassTypeName 属性のカスタム描画。指定型のサブクラスをドロップダウン表示する。
/// </summary>
[CustomPropertyDrawer(typeof(ClassTypeName))]
public class ClassTypeNameDrawer : PropertyDrawer
{
    private ClassTypeName m_classTypeName;
    private List<string> m_names;
    private List<string> m_formatedNames;
    private bool m_initialized = false;

    private void Initialize()
    {
        m_classTypeName = (ClassTypeName)attribute;

        var classes = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(m_classTypeName.type) && !t.IsAbstract)
            .ToList();

        m_names = classes.Select(t => t.AssemblyQualifiedName).ToList();
        m_formatedNames = classes
            .Select(t => t.Name)
            .Select(n => Regex.Replace(n, "(\\B[A-Z])", " $1"))
            .ToList();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!m_initialized)
        {
            m_initialized = true;
            Initialize();
        }

        if (m_names.Count == 0)
        {
            EditorGUI.LabelField(position, label.text, "(no subclass found)");
            return;
        }

        if (string.IsNullOrEmpty(property.stringValue))
            property.stringValue = m_names[0];

        if (!m_names.Contains(property.stringValue))
        {
            EditorGUI.LabelField(position, label.text, $"(missing: {property.stringValue})");
            return;
        }

        var current = m_names.IndexOf(property.stringValue);
        position = EditorGUI.PrefixLabel(position, label);
        var selected = EditorGUI.Popup(position, current, m_formatedNames.ToArray());
        property.stringValue = m_names[selected];
    }
}
```

注意: `AssemblyQualifiedName` を使う（Platformer は `ToString()` を使っているが、asmdef 境界越えで `Type.GetType()` が失敗するリスクがあるため）。

- [ ] **Step 4: Editor 用 asmdef を作成（必要なら）**

既存の `Magnet_Rush/Assets/_Project/Scripts/Core/Entity` 配下の asmdef が `includePlatforms` 未設定なら、Editor だけのフォルダに対して別 asmdef が必要。

Step 1 の調査結果で判断。必要なら:

```json
{
    "name": "MagnetRush.StateMachine.Editor",
    "references": [
        "<既存 Core Entity の asmdef 名>"
    ],
    "includePlatforms": ["Editor"],
    "autoReferenced": true
}
```

- [ ] **Step 5: Compile 確認**

```
refresh_unity()
read_console(types=["error"], count=30)
```

Expected: エラー 0。Drawer はまだどこからも使われていないのでコンパイル通るだけ。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(state): ClassTypeName 属性と Editor 拡張を追加"
```

---

## Task 6: `EntityStateManager` に `GetStateList()` abstract method と Reflection 生成対応

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs`

**目的:** Platformer 準拠のサブクラス駆動 state 生成を可能にする。現状 MagnetRush は `Initialize(T entity)` + `RegisterState()` で明示登録だが、これを `GetStateList()` abstract method ベースに切り替える。Backward compat のため既存の `RegisterState()` は残す。

- [ ] **Step 1: `EntityStateManager.cs` に `GetStateList()` abstract method を追加**

既存:
```csharp
public class EntityStateManager<T> : EntityStateManagerBase where T : Entity
{
    private readonly Dictionary<Type, EntityState<T>> m_states = new();
    private readonly List<EntityState<T>> m_list = new();
    private T m_entity;

    public EntityState<T> current { get; private set; }
    public EntityState<T> last { get; private set; }

    public int index => ...;
    public int lastIndex => ...;

    public void Initialize(T entity) { m_entity = entity; }

    public void RegisterState(EntityState<T> state) { ... }

    public void Change<TState>() { ... }

    public void Step(float dt) { ... }
    public void OnContact(Collider other) { ... }
    public bool IsCurrentOfType<TState>() { ... }
}
```

変更後（`abstract` に変更 + `GetStateList()` + `Start()` で自動初期化）:

```csharp
public abstract class EntityStateManager<T> : EntityStateManagerBase where T : Entity
{
    private readonly Dictionary<Type, EntityState<T>> m_states = new();
    private readonly List<EntityState<T>> m_list = new();
    private T m_entity;

    public EntityState<T> current { get; private set; }
    public EntityState<T> last { get; private set; }

    public int index => current != null ? m_list.IndexOf(current) : -1;
    public int lastIndex => last != null ? m_list.IndexOf(last) : -1;

    /// <summary>
    /// サブクラスが返すステートリスト。Inspector 駆動ならここで string[] から Reflection 生成する。
    /// 固定 new 登録のサブクラスはここで new List<...> を返す。
    /// </summary>
    protected abstract List<EntityState<T>> GetStateList();

    protected virtual void Start()
    {
        m_entity = GetComponent<T>();
        var list = GetStateList();
        foreach (var state in list)
        {
            RegisterState(state);
        }
        if (m_list.Count > 0)
        {
            Change(m_list[0].GetType());
        }
    }

    public void RegisterState(EntityState<T> state)
    {
        var type = state.GetType();
        if (m_states.ContainsKey(type)) return;
        m_states[type] = state;
        m_list.Add(state);
    }

    public void Change<TState>() where TState : EntityState<T>
    {
        Change(typeof(TState));
    }

    public void Change(Type type)
    {
        if (!m_states.TryGetValue(type, out var next))
        {
            Debug.LogError($"State {type.Name} not registered.");
            return;
        }

        if (current != null)
        {
            current.Exit();
            InvokeStateExit(current.GetType());
            last = current;
        }

        current = next;
        current.Enter(m_entity, this);
        InvokeStateEnter(current.GetType());
        InvokeStateChanged();
    }

    public void UpdateState(float dt)
    {
        if (current != null)
        {
            current.UpdateState(dt);
            current.timeSinceEntered += dt;
        }
    }

    public void OnContact(Collider other)
    {
        if (current != null) current.OnContact(other);
    }

    public bool IsCurrentOfType<TState>() where TState : EntityState<T>
    {
        return current != null && current.GetType() == typeof(TState);
    }

    /// <summary>
    /// 文字列配列（クラス名 AssemblyQualifiedName）から State リストを生成するヘルパー。
    /// Inspector 駆動のサブクラスで GetStateList から呼ぶ。
    /// </summary>
    protected static List<EntityState<T>> CreateListFromStringArray(string[] array)
    {
        var list = new List<EntityState<T>>();
        foreach (var typeName in array)
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            var type = Type.GetType(typeName);
            if (type == null) { Debug.LogError($"Type {typeName} not found."); continue; }
            var instance = Activator.CreateInstance(type) as EntityState<T>;
            if (instance == null) { Debug.LogError($"Failed to create {typeName}."); continue; }
            list.Add(instance);
        }
        return list;
    }
}
```

重要変更点:
- クラスが `abstract` になった（`GetStateList` が abstract のため）
- `Initialize(T entity)` 廃止 → `Start()` で自動的に `GetComponent<T>()` + `GetStateList()`
- `Step(float dt)` → `UpdateState(float dt)` にリネーム（State 側の UpdateState と統一）
- `CreateListFromStringArray` 追加

- [ ] **Step 2: Player.cs の Update() で `states.Step(dt)` → `states.UpdateState(dt)` にリネーム**

既存:
```csharp
states.UpdateState(dt);
```

これは既に `UpdateState` の場合は変更不要。確認だけ。

実際の現状 Player.cs を Read して確認（line 99 付近）。もし `states.Step(dt)` や `states.StepState(dt)` になっていたら `UpdateState(dt)` に統一。

- [ ] **Step 3: Compile 確認**

```
refresh_unity()
read_console(types=["error"], count=50)
```

Expected: **エラーが出る**。`PlayerStateManager` が `abstract GetStateList()` を override していないため。次の Task 7 で解決。

- [ ] **Step 4: この時点では commit しない**

Task 7 まで進んでから両方まとめて commit。

---

## Task 7: `PlayerStateManager` を Inspector 駆動化

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateManager.cs`

**目的:** `PlayerStateManager.Awake()` の固定 `RegisterState(new IdlePlayerState())` を廃止し、Inspector の `string[] states` フィールドから動的に生成する。

- [ ] **Step 1: PlayerStateManager.cs を書き換え**

既存:
```csharp
public class PlayerStateManager : EntityStateManager<Player>
{
    void Awake()
    {
        var player = GetComponent<Player>();
        RegisterState(new IdlePlayerState());
        RegisterState(new MovePlayerState());
        RegisterState(new DiePlayerState());
        RegisterState(new AimPlayerState());
        Initialize(player);
    }

    void Start()
    {
        Change<IdlePlayerState>();
    }
}
```

変更後:
```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤー用のステートマシン。Inspector の states 配列から動的にステートを生成する。
/// 新 State を追加したい場合は Scripts/Core/Player/States/ に新クラスを置いて Inspector で選ぶ。
/// </summary>
public class PlayerStateManager : EntityStateManager<Player>
{
    [ClassTypeName(typeof(EntityState<Player>))]
    [Tooltip("使用するステートの一覧。Inspector のドロップダウンで選択。登録順が State Int index になる")]
    public string[] states;

    protected override List<EntityState<Player>> GetStateList()
    {
        return CreateListFromStringArray(states);
    }
}
```

注意:
- `Awake` / `Start` を削除 → 基底クラスの `Start()` が自動的に初期化する
- `IdlePlayerState` への初期遷移は基底の `Start` 内で `m_list[0]` 遷移に置き換わる（Task 6 参照）
- `[ClassTypeName(typeof(EntityState<Player>))]` によって Inspector でドロップダウン表示

- [ ] **Step 2: `_Player.prefab` の PlayerStateManager コンポーネントに states 配列を設定**

MCP 経由で配列を初期化:

```
manage_components(
    action="set",
    target="Assets/_Project/Prefabs/Player/_Player.prefab",
    component_name="PlayerStateManager",
    properties={
        "states": [
            "<AssemblyQualifiedName of IdlePlayerState>",
            "<AssemblyQualifiedName of MovePlayerState>",
            "<AssemblyQualifiedName of DiePlayerState>",
            "<AssemblyQualifiedName of AimPlayerState>"
        ]
    }
)
```

AssemblyQualifiedName はそれぞれ `IdlePlayerState, <asmdef名>, Version=..., ...` の形。実際の値は Inspector で一度ドロップダウン操作して `.prefab` ファイルを `cat` で確認するのが確実。

**代替手順（手動設定）:**
1. Unity Editor で `_Player.prefab` を開く
2. PlayerStateManager の `States` に 4 要素追加
3. 各要素のドロップダウンで IdlePlayerState / MovePlayerState / DiePlayerState / AimPlayerState を選択
4. prefab 保存

**登録順**（`State` Int index に影響）:
- 0: Idle
- 1: Move
- 2: Die
- 3: Aim

これは **Task 7 以降の Animator 設定と整合性維持**のため絶対に守る。

- [ ] **Step 3: Compile 確認 + prefab 整合性確認**

```
refresh_unity()
read_console(types=["error"], count=30)
```

`_Player.prefab` の内容も確認:

```bash
grep -A 5 "PlayerStateManager" Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab | head -20
```

states 配列に 4 要素入っていれば OK。

- [ ] **Step 4: Commit（Task 6 と合わせて 1 commit）**

```bash
git add -A
git commit -m "feat(state): EntityStateManager を Inspector 駆動化、PlayerStateManager を string[] 配列に"
```

---

## Task 8: ドキュメント更新 ── 拡張者向け章を追加

**Files:**
- Modify: `docs/player-animation-guide.md`

**目的:** メンバー（プログラマー）が新 State を追加する手順を明記。Platformer Project 形式で書く。

- [ ] **Step 1: 既存ガイドを読む**

```bash
wc -l docs/player-animation-guide.md
```

最新（Task 7 commit 時点）で約 350 行のはず。

- [ ] **Step 2: § 12 の次に新章 § 13「拡張（プログラマー向け）」を追加**

末尾に以下を追加（既存内容を壊さない）:

```markdown
---

## 13. 拡張（プログラマー向け）

このプロジェクトは Platformer Project 形式。**拡張は C# で State 追加 + Player.cs にメソッド追加**。

### 13.1 新しい State を追加する

例: ジャンプ専用ステート `JumpPlayerState`

1. `Scripts/Core/Player/States/JumpPlayerState.cs` を新規作成:

    ```csharp
    public class JumpPlayerState : EntityState<Player>
    {
        public override void Enter(Player entity, EntityStateManager<Player> manager)
        {
            base.Enter(entity, manager);
            // ジャンプ開始時の処理（効果音、ジャンプ初速付与等）
        }

        public override void UpdateState(float dt)
        {
            m_entity.HandleAimInput();
            m_entity.Fire();
            m_entity.SwitchPole();
            // ジャンプ中の処理

            if (m_entity.IsGrounded)
                m_manager.Change<IdlePlayerState>();
        }

        public override void Exit() { }
    }
    ```

2. `_Player.prefab` の `PlayerStateManager` コンポーネントを開く
3. `States` 配列に要素追加 → ドロップダウンから `Jump Player State` を選択
4. 登録順が index になるので、Animator の `State == N` 条件を合わせる（index 確認: Inspector で配列の位置）
5. Animator Controller 側に対応する遷移を追加

### 13.2 Player に新しい能力メソッドを追加する

例: ダッシュ能力 `Dash()`

1. `Scripts/Core/Player/Player.cs` に追加:

    ```csharp
    // --- ダッシュ ---

    /// <summary>ダッシュ入力があれば実行。毎フレーム呼ぶ。</summary>
    public void Dash()
    {
        if (!input.ConsumeDash()) return;
        // ダッシュ力適用
        externalVelocity += transform.forward * m_settings.dashForce;
        events?.FireDash();   // 既存パターンに従って UnityEvent も発火
    }
    ```

2. 使う State の `UpdateState` に `m_entity.Dash();` を追加
3. 必要なら `PlayerEvents.cs` に `UnityEvent OnDash` を追加（Inspector で SE/VFX 接続用）
4. `PlayerAnimator.cs` に `Dash` Trigger パラメータを追加（アニメを再生したいなら）

### 13.3 やってはいけない

- `EntityStateManager` / `Entity` base の改修は**相談必須**（全 Entity / 敵にも波及する）
- `Player.cs` の既存メソッドのシグネチャ変更 → State 側が全部壊れる、相談必須
- `PlayerStateManager` の states 配列の順序変更 → Animator の State Int 条件がずれる

### 13.4 既存パターンの参考

- `Player.SwitchPole()` / `Fire()` / `StartAim()` が実例。新メソッドは同じスタイルで書く
- `IdlePlayerState.UpdateState()` が実例。新 State は同じ列挙スタイルで書く
```

- [ ] **Step 3: Compile 確認（doc のみなので不要）**

スキップ。

- [ ] **Step 4: Commit**

```bash
git add docs/player-animation-guide.md
git commit -m "docs(anim): 拡張者向け章を追加（新 State / 新 Player メソッドの書き方）"
```

---

## Task 9: フル PlayMode スモーク

**目的:** Task 1-8 完了後、全機能が regression なく動くことを実機確認。

- [ ] **Step 1: Compile 最終確認**

```
refresh_unity()
read_console(types=["error", "warning"], count=50)
```

Expected: error 0、warning も 0 か既存の無関係なもののみ。

- [ ] **Step 2: `_Player.prefab` の健全性確認**

Unity Editor で `_Player.prefab` を開き、以下を目視:

- [ ] `Player` コンポーネントの `m_bulletSettings` / `m_firePoint` / `m_selfFireHeightOffset` が設定済み
- [ ] `PlayerStateManager` の `states` に 4 要素が入っている
- [ ] `PlayerAnimator` の `m_player` 参照が Player 本体に繋がっている
- [ ] `ShootingController` / `AimController` / `PolarityController` コンポーネントが **存在しない**（削除済み）
- [ ] コンポーネント Missing 表示がない

- [ ] **Step 3: PlayMode 動作確認**

```
manage_editor(action="enter_play_mode")
```

手動チェックリスト:

- [ ] 左スティックで移動できる
- [ ] ジャンプ可能（もし既存機能にあれば）
- [ ] LT でエイム → AimPlayerState 遷移 → Time.timeScale 低下 → 離すと解除 (release grace 込み)
- [ ] RT で射撃 → 弾生成、Console 例外なし
- [ ] A / F でセルフファイア
- [ ] Y で磁極切替 → Reticle / Ammo UI の色変化
- [ ] X でリロード → 弾数カウンタ復帰
- [ ] HP 0 → DiePlayerState 遷移 → Collider 無効 → スポーン地点復帰 → HP 回復 → IdlePlayerState 復帰
- [ ] 死亡直後に新しい弾を撃ってもアニメ Trigger 残留バグなし

```
manage_editor(action="exit_play_mode")
```

- [ ] **Step 4: 動作確認結果を報告**

全項目通過なら Task 完了。失敗項目があれば修正して再実行。

- [ ] **Step 5: 保存とコミット（Unity が prefab/meta 更新してれば）**

```
refresh_unity()
```

```bash
git status
git diff
```

変更があれば:

```bash
git add -A
git commit -m "chore(player): PlayMode 検証で Unity が更新した .meta / .prefab を保存"
```

無ければスキップ。

- [ ] **Step 6: PR 作成の前段として branch 状況確認**

```bash
git log --oneline origin/develop..HEAD
git diff origin/develop..HEAD --stat
```

Expected: 8-9 commits（Task 1-8 各 1 + 必要なら chore）。diff stats で想定通り変更されていること確認。

---

## Rollback 戦略

各 Task が atomic commit なので、問題起きたら該当コミットを revert するだけ:

```bash
# 特定タスクだけ戻す
git revert <commit_sha>

# Task N から後を全部戻す（注意: force push 必要）
git reset --hard <commit_before_Task_N>
```

Prefab 破壊など深刻なら、リファクタ開始前の状態に戻す:

```bash
git reset --hard 9f4533d  # 計画ファイル commit 直後
```

---

## Self-Review 結果

**Spec coverage:**

| 要件 | 対応タスク |
|---|---|
| 完全移植（Platformer 形式） | Task 1-4 |
| ClassTypeName 移植 | Task 5-7 |
| メンバーの拡張パターン明記 | Task 8 |
| アルファ相当の保守性 | 全タスクで SRP 維持 |
| regression なし | Task 9 スモーク |

全要件カバー ✅

**Placeholder scan:** 「TBD / TODO / appropriate error handling」等なし ✅

**Type consistency:**
- `Player.cs` が追加する `CurrentPole` / `OnPolarityChanged` / `IsAiming` / `StartAim` / `StopAim` / `HandleAimInput` / `OnAimChanged` / `Fire` / `SelfFire` / `Reload` / `SwitchPole` / `CalculateTargetPoint` のシグネチャが Task 1-3 内で一貫 ✅
- `EntityStateManager<T>` の `Step` → `UpdateState` 改名が Task 6 で行われ、既存 `Player.cs` の `states.UpdateState(dt)` と一致（Task 6 Step 2 で確認）✅
- `ClassTypeName` 属性が `AssemblyQualifiedName` を使う（Task 5 Step 3）、`CreateListFromStringArray` が同じ `Type.GetType(typeName)` で復元（Task 6 Step 1）✅

整合性 OK。

---

## Change History

- 2026-04-20 初版（西川駿太 + Claude Opus 4.7 による Platformer Project 完全移植計画）
