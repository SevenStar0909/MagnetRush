# Player Composition Restore Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Player の能力系 (Shooting / Aim / Polarity) を partial class 方式から独立 MonoBehaviour の Pure Composition に戻す。State / UI / PlayerAnimator は Controller を直接参照し、Player.cs は薄いハブとして Movement のみ保持する。

**Architecture:** Unity GameObject-Component Pattern に準拠。同一 `_Player` GameObject 上に `Player / ShootingController / AimController / PolarityController` を並列配置。ShootingController は sibling Controller を `GetComponent` で取得、UI / PlayerAnimator も Player 経由ではなく Controller 直接参照。Movement は Entity base の protected メソッド (`Accelerate`, `FaceDirection`, `Decelerate`, `m_cachedCameraTransform`) に依存するため Player.cs 内に残す（Platformer Project 系の "基本移動は本体" パターン）。

**Tech Stack:** Unity 6, C# 9 (no nullable), Unity Input System, Cinemachine, UnityEvent (PlayerEvents), assembly `MagnetRush.Player.asmdef`

**前提ブランチ:** `feature/player-anim-refactor`（13 コミット積み済み、未 push）

**コミット方針:** 各 Task 1 コミット。任意の時点で `git reset` しても compile green。

---

## Scope とファイルの責務

### 新規作成

| File | 責務 |
|---|---|
| `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs` | 磁極切替 (`CurrentPole`, `OnPolarityChanged`, `Switch()`) |
| `Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs` | エイム制御 (`IsAiming`, `OnAimChanged`, `HandleAimInput()`, `StartAim()`, `StopAim()`) |
| `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs` | 射撃 (`Fire()`, `SelfFire()`, `Reload()`) + SerializeField `m_bulletSettings`, `m_firePoint`, `m_selfFireHeightOffset` |

### 修正

| File | 内容 |
|---|---|
| `Player.cs` | 薄いハブ化、Movement 3メソッド吸収、hub プロパティ `shooting/aim/polarity` 追加、能力系 API 削除、`partial` 除去 |
| `Player.Movement.cs` → Player.cs に吸収後削除 | Movement 3メソッドを Player.cs に移動 |
| `Player.Aim.cs` | 削除 |
| `Player.Polarity.cs` | 削除 |
| `Player.Shooting.cs` | 削除 |
| `States/IdlePlayerState.cs` | `m_entity.Fire()` → `m_entity.shooting.Fire()` 等、6箇所 |
| `States/MovePlayerState.cs` | 同上、6箇所 |
| `States/AimPlayerState.cs` | 同上、6箇所 |
| `PlayerAnimator.cs` | `m_player.IsAiming` → `m_aim.IsAiming`、SerializeField `m_aim` 追加 |
| `UI/AmmoUI.cs` | `m_player.OnPolarityChanged/CurrentPole` → `m_polarity.OnPolarityChanged/CurrentPole` |
| `UI/ReticleUI.cs` | 同上 + `m_player.IsAiming` → `m_aim.IsAiming` |
| `CameraSettingsApplier.cs` | `Player.OnAimChanged` → `AimController.OnAimChanged` |
| `_Player.prefab` | 3 Controller MonoBehaviour 追加、`m_bulletSettings/m_firePoint/m_selfFireHeightOffset` を Player→ShootingController に移動、PlayerAnimator の `m_aim` 参照追加、UI 参照更新 |

### 維持

- `PlayerEvents.cs`（UnityEvent、触らない）
- `PlayerInputHandler.cs`
- `PlayerStateManager.cs` / `PlayerStateIndex.cs`
- `States/DiePlayerState.cs`（能力系は呼ばない）
- `EntityStateManager`, `EntityState`, `Entity`, `Magnetizable`
- `UI` で `Player.Current` 経由で player を取得するパターンは維持（`playerObj.GetComponent<Player>()` → さらに `GetComponent<PolarityController>()` で sibling 取得）

---

## 共通前提

### 環境変数（UniCLI 経由の shell command 用）

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
```

### Assets/ 配下の操作ルール（.claude/CLAUDE.md 準拠）

- **ファイル作成**: MCP `create_script` または UniCLI 経由。`Write` ツールで直接作ると `.meta` 不整合
- **ファイル削除**: `unicli exec Eval --code "UnityEditor.AssetDatabase.DeleteAsset(\"Assets/path\");"`
- **ファイル移動**: `unicli exec Eval --code "UnityEditor.AssetDatabase.MoveAsset(\"Assets/old\", \"Assets/new\");"`
- **編集**: `Edit` / `Write` ツールで既存ファイルを更新するのは OK（`.meta` は既存のまま）
- **保存**: 作業区切りで `unicli exec Eval --code "UnityEditor.AssetDatabase.SaveAssets();"`

### コンパイル確認コマンド

```bash
unicli exec Console.ReadLogs
```
Expected: `Compilation errors: 0` / `Warnings: 0`（既存の警告は許容）

---

## Task 1: PolarityController.cs を新規作成

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs`

**目的:** 磁極切替責務を独立 MonoBehaviour として分離。旧 `Player.Polarity.cs` の内容を `Player` 依存を解いて移植する。

- [ ] **Step 1: 新規スクリプト作成**

MCP `create_script` でファイル作成:
- path: `Assets/_Project/Scripts/Core/Player/PolarityController.cs`
- contents:

```csharp
using System;
using UnityEngine;

/// <summary>
/// 磁極制御コンポーネント。Y 入力で S/N を切り替え、UI 等へイベント通知する。
/// 依存: PlayerInputHandler, PlayerEvents（同 GameObject 上）
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public class PolarityController : MonoBehaviour
{
    /// <summary>現在の磁極（S または N）。</summary>
    public MagneticPole CurrentPole { get; private set; } = MagneticPole.S;

    /// <summary>磁極切替時に発火。UI 等が購読する。</summary>
    public event Action<MagneticPole> OnPolarityChanged;

    private PlayerInputHandler m_input;
    private PlayerEvents m_events;

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_events = GetComponent<PlayerEvents>();
    }

    /// <summary>Y 入力があれば磁極を切り替える。毎フレーム呼ぶ前提。</summary>
    public void Switch()
    {
        if (!m_input.ConsumeSwitchPole()) return;
        CurrentPole = CurrentPole == MagneticPole.S ? MagneticPole.N : MagneticPole.S;
        OnPolarityChanged?.Invoke(CurrentPole);
        m_events?.FirePolaritySwitch();
    }
}
```

- [ ] **Step 2: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。`PolarityController` は独立クラスで既存コードと競合しない。

- [ ] **Step 3: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs.meta
git commit -m "feat(player): PolarityController を独立 MonoBehaviour として新規作成"
```

---

## Task 2: AimController.cs を新規作成

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs`

**目的:** エイム制御責務を独立 MonoBehaviour として分離。旧 `Player.Aim.cs` の内容を `PlayerSettings` のみ残して移植する。`OnAimChanged` は static のまま移す（CameraSettingsApplier が subscribe タイミングで `Player.Current` 未生成のパターンに対応するため、旧設計と同じ静的イベント）。

- [ ] **Step 1: 新規スクリプト作成**

MCP `create_script`:
- path: `Assets/_Project/Scripts/Core/Player/AimController.cs`
- contents:

```csharp
using System;
using UnityEngine;

/// <summary>
/// エイム制御コンポーネント。LT 入力でエイムモードに入りスロー + カメラ固定ストレイフに遷移する。
/// 依存: PlayerInputHandler, PlayerStateManager, Player（PlayerSettings 参照用、同 GameObject）
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerStateManager))]
[RequireComponent(typeof(Player))]
public class AimController : MonoBehaviour
{
    /// <summary>エイム中かどうか。</summary>
    public bool IsAiming { get; private set; }

    /// <summary>エイム状態変化時に発火。CameraSettingsApplier 等が購読。静的なのは Player.Current 未生成時点で購読可能にするため。</summary>
    public static event Action<bool> OnAimChanged;

    private PlayerInputHandler m_input;
    private PlayerStateManager m_states;
    private Player m_player;
    private float m_aimReleaseGrace;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnAimChanged = null;
    }

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_states = GetComponent<PlayerStateManager>();
        m_player = GetComponent<Player>();
    }

    /// <summary>LT 入力に応じてエイムモードを開始/維持する。毎フレーム呼ぶ。</summary>
    public void HandleAimInput()
    {
        if (m_input.AimHeld)
        {
            m_aimReleaseGrace = m_player.Settings.aimReleaseGraceTime;
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
        Time.timeScale = m_player.Settings.aimTimeScale;
        OnAimChanged?.Invoke(true);
        m_states.Change<AimPlayerState>();
    }

    /// <summary>エイムモード終了。入力があれば Move、なければ Idle に戻る。</summary>
    public void StopAim()
    {
        IsAiming = false;
        Time.timeScale = 1f;
        OnAimChanged?.Invoke(false);

        if (m_input != null && m_input.MoveInput.sqrMagnitude > 0.01f)
            m_states.Change<MovePlayerState>();
        else
            m_states.Change<IdlePlayerState>();
    }
}
```

- [ ] **Step 2: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。`AimController` は独立クラス。既存 `Player.Aim.cs` の `HandleAimInput/StartAim/StopAim/IsAiming/OnAimChanged` と名前空間が被るが、別クラスに所属するので競合しない。

- [ ] **Step 3: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs.meta
git commit -m "feat(player): AimController を独立 MonoBehaviour として新規作成"
```

---

## Task 3: ShootingController.cs を新規作成

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs`

**目的:** 射撃責務を独立 MonoBehaviour として分離。SerializeField (`m_bulletSettings` / `m_firePoint` / `m_selfFireHeightOffset`) とロジックを Player から移植。`CurrentPole` は sibling `PolarityController` から、`StopAim` bullet コールバックは sibling `AimController` から取得する。

- [ ] **Step 1: 新規スクリプト作成**

MCP `create_script`:
- path: `Assets/_Project/Scripts/Core/Player/ShootingController.cs`
- contents:

```csharp
using UnityEngine;

/// <summary>
/// 射撃コンポーネント。RT で通常射撃、A/F でセルフファイア、X でリロード。
/// 依存: PlayerInputHandler, PlayerEvents, Magnetizable, PolarityController, AimController, Player（PlayerSettings 参照用）
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(Magnetizable))]
[RequireComponent(typeof(PolarityController))]
[RequireComponent(typeof(AimController))]
[RequireComponent(typeof(Player))]
public class ShootingController : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private BulletSettings m_bulletSettings;
    [SerializeField] private Transform m_firePoint;
    [SerializeField] private float m_selfFireHeightOffset = 1.0f;

    private Camera m_mainCamera;
    private PlayerInputHandler m_input;
    private PlayerEvents m_events;
    private Magnetizable m_magnetizable;
    private PolarityController m_polarity;
    private AimController m_aim;
    private Player m_player;

    private const float k_ForwardDotThreshold = 0.1f;

    void Awake()
    {
        m_input = GetComponent<PlayerInputHandler>();
        m_events = GetComponent<PlayerEvents>();
        m_magnetizable = GetComponent<Magnetizable>();
        m_polarity = GetComponent<PolarityController>();
        m_aim = GetComponent<AimController>();
        m_player = GetComponent<Player>();
    }

    void Start()
    {
        m_mainCamera = Camera.main;
    }

    /// <summary>RT 入力があれば通常射撃。毎フレーム呼ぶ。</summary>
    public void Fire()
    {
        if (!m_input.ConsumeFire()) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可"); return; }
        if (m_mainCamera == null)
        { ChannelLogger.LogGuardReturn("Player", "MainCameraなし"); return; }

        float height = m_player.Settings != null ? m_player.Settings.firePointHeight : 1.2f;
        Vector3 spawnPos = m_firePoint != null ? m_firePoint.position : transform.position + Vector3.up * height;

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
            bullet.Initialize(m_polarity.CurrentPole, direction);
            BulletManager.Instance.Register(bullet);
            bullet.OnImpact += m_aim.StopAim;
        }

        m_events?.FireShoot();
    }

    /// <summary>A / F 入力があればセルフファイア（自己磁化）。毎フレーム呼ぶ。</summary>
    public void SelfFire()
    {
        if (!m_input.ConsumeSelfFire()) return;
        if (m_bulletSettings == null || m_bulletSettings.bulletPrefab == null)
        { ChannelLogger.LogGuardReturn("Player", "BulletSettings未設定(SelfFire)"); return; }
        if (BulletManager.Instance == null || !BulletManager.Instance.CanShoot())
        { ChannelLogger.LogGuardReturn("Player", "BulletManager未初期化 or 射撃不可(SelfFire)"); return; }

        if (m_magnetizable != null)
            m_magnetizable.SetPole(m_polarity.CurrentPole);

        var fieldSettings = m_bulletSettings.bulletFieldSettings;
        if (fieldSettings != null)
        {
            var existing = GetComponent<MagnetField>();
            if (existing == null)
            {
                var field = gameObject.AddComponent<MagnetField>();
                field.Initialize(m_polarity.CurrentPole, fieldSettings);

                if (MagnetManager.Instance != null)
                    MagnetManager.Instance.RegisterField(field);

                var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
                visualizer.Show(m_polarity.CurrentPole, fieldSettings);

                GameObject effectPrefab = m_polarity.CurrentPole == MagneticPole.S
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
                    if (m_magnetizable != null) m_magnetizable.Deactivate();
                    if (visualizer != null) Destroy(visualizer);
                    if (effectInstance != null) Destroy(effectInstance);
                };
            }
        }

        if (BulletManager.Instance != null)
            BulletManager.Instance.IncrementShotCount();

        m_events?.FireSelfShoot();
    }

    /// <summary>X 入力があればリロード（全弾クリア）。毎フレーム呼ぶ。</summary>
    public void Reload()
    {
        if (!m_input.ConsumeReload()) return;
        if (BulletManager.Instance == null) return;
        BulletManager.Instance.ClearAll();
        m_events?.FireReload();
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
}
```

- [ ] **Step 2: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。ShootingController は独立クラス。旧 `Player.Shooting.cs` の `Fire/SelfFire/Reload` と同名メソッドがあるが、所属クラスが違うので競合しない。

- [ ] **Step 3: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs.meta
git commit -m "feat(player): ShootingController を独立 MonoBehaviour として新規作成"
```

---

## Task 4: Player.cs に hub プロパティ `shooting/aim/polarity` を追加

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`

**目的:** Player を 3 Controller への参照を持つハブに拡張する。既存の API (`Fire()` / `StartAim()` 等) はまだ残す（後で削除）ので、この段階では Player.cs は「両対応」状態。`[RequireComponent]` で prefab に 3 Controller を自動付加可能にする。

- [ ] **Step 1: Player.cs の先頭属性に RequireComponent 追加**

`Player.cs` の 8-10 行目を次のように置換:

before:
```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public partial class Player : Entity
```

after:
```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(PolarityController))]
[RequireComponent(typeof(AimController))]
[RequireComponent(typeof(ShootingController))]
public partial class Player : Entity
```

- [ ] **Step 2: hub プロパティを追加**

`Player.cs` のプロパティ群（`public PlayerStateManager states { get; private set; }` のブロック、58 行目付近）の直後に以下を挿入:

```csharp
    /// <summary>射撃 Controller。</summary>
    public ShootingController shooting { get; private set; }

    /// <summary>エイム Controller。</summary>
    public AimController aim { get; private set; }

    /// <summary>磁極 Controller。</summary>
    public PolarityController polarity { get; private set; }
```

- [ ] **Step 3: Awake に Controller 取得を追加**

`Player.cs` の `Awake()` メソッド（65 行目付近）、`magnetizable = GetComponent<Magnetizable>();` の直後に以下を挿入:

```csharp
        shooting = GetComponent<ShootingController>();
        aim = GetComponent<AimController>();
        polarity = GetComponent<PolarityController>();
```

- [ ] **Step 4: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。この時点では `player.shooting.Fire()` と `player.Fire()`（旧 partial）の両方が呼べる状態。prefab には Controller がまだ付いていないので実行時 null だが、このコミット時点ではコンパイルさえ通ればよい。

- [ ] **Step 5: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs
git commit -m "feat(player): Player に shooting/aim/polarity hub プロパティと RequireComponent を追加"
```

---

## Task 5: _Player.prefab に 3 Controller 追加 + SerializeField 値を ShootingController に移行

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab`

**目的:** prefab に `ShootingController / AimController / PolarityController` MonoBehaviour を追加し、`m_bulletSettings / m_firePoint / m_selfFireHeightOffset` を Player ブロックから ShootingController ブロックに移行する。この段階では Player ブロックにも旧フィールドは**残す**（Task 10 でまとめて削除）。

### 背景（prefab 構造の既知値）

Player script guid: `e6594f79a3932ea4d8bb7a7e3c6875a9`
PolarityController script guid: 新規（Task 1 で生成、meta ファイルから取得）
AimController script guid: 新規（Task 2 で生成、meta ファイルから取得）
ShootingController script guid: 新規（Task 3 で生成、meta ファイルから取得）
PlayerEvents script guid: `21430a5baa125a846b3890234515ba9a`
PlayerStateManager script guid: `21d975a99f0064f4cbbc4f8ebfc0aaea`

移行する SerializeField 値（現 prefab L81-83）:
- `m_bulletSettings: {fileID: 11400000, guid: 425ee2473071b7a4fa2afd4ccb6a162c, type: 2}`
- `m_firePoint: {fileID: 5721768207443567397}`
- `m_selfFireHeightOffset: 1`

prefab の root GameObject fileID: `1725508640444081474`

- [ ] **Step 1: 新規 Controller script の GUID を取得**

```bash
grep guid Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs.meta \
          Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs.meta \
          Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs.meta
```

取得した 3 つの GUID を後続 step で使用する（仮に `<POLARITY_GUID>` / `<AIM_GUID>` / `<SHOOTING_GUID>` と表記）。

- [ ] **Step 2: prefab 保存済みか確認（Unity 起動中なら SaveOpenScenes）**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();"
```

- [ ] **Step 3: 3 Controller MonoBehaviour を追加**

`Assets/_Project/Prefabs/Player/_Player.prefab` の `Player` MonoBehaviour ブロック（L57 前後 `--- !u!114 &4022685328076152797` から L83）の**直後** に、以下 3 ブロックを挿入:

```yaml
--- !u!114 &7300000000000000001
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1725508640444081474}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <POLARITY_GUID>, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!114 &7300000000000000002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1725508640444081474}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <AIM_GUID>, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!114 &7300000000000000003
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1725508640444081474}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <SHOOTING_GUID>, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_bulletSettings: {fileID: 11400000, guid: 425ee2473071b7a4fa2afd4ccb6a162c, type: 2}
  m_firePoint: {fileID: 5721768207443567397}
  m_selfFireHeightOffset: 1
```

`<POLARITY_GUID>` / `<AIM_GUID>` / `<SHOOTING_GUID>` は Step 1 で取得した値に置換する。

- [ ] **Step 4: GameObject の m_Component リストに 3 Controller の fileID を追加**

prefab の L10-19 `_Player` GameObject の `m_Component` リストに以下 3 行を追記（Rigidbody や PlayerInputHandler の後、Transform 子オブジェクトの前）:

before:
```yaml
  m_Component:
  - component: {fileID: 890088630592913366}
  - component: {fileID: 4866480273526876224}
  - component: {fileID: 4022685328076152797}
  - component: {fileID: 3248401797439555635}
  - component: {fileID: 8415192833665430904}
  - component: {fileID: 7473496859954039961}
  - component: {fileID: 4320032113719753399}
  - component: {fileID: 2310377158301139839}
  - component: {fileID: 4919385699858757655}
```

after:
```yaml
  m_Component:
  - component: {fileID: 890088630592913366}
  - component: {fileID: 4866480273526876224}
  - component: {fileID: 4022685328076152797}
  - component: {fileID: 3248401797439555635}
  - component: {fileID: 8415192833665430904}
  - component: {fileID: 7473496859954039961}
  - component: {fileID: 4320032113719753399}
  - component: {fileID: 2310377158301139839}
  - component: {fileID: 4919385699858757655}
  - component: {fileID: 7300000000000000001}
  - component: {fileID: 7300000000000000002}
  - component: {fileID: 7300000000000000003}
```

- [ ] **Step 5: ForceReserializeAssets + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.ForceReserializeAssets(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。prefab の読み込み時に「Player の m_bulletSettings は ShootingController に重複しているが問題なし」（両方で参照しているだけ）。

- [ ] **Step 6: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
git commit -m "feat(prefab): _Player に PolarityController/AimController/ShootingController を追加、Shooting SerializeField 値を移行"
```

---

## Task 6: State クラスを Controller 経由呼び出しに更新

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/IdlePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/MovePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/AimPlayerState.cs`
- (DiePlayerState は能力系を呼ばないので変更なし)

**目的:** State が Player の facade メソッドではなく Controller を直接呼ぶ形に書き換え。Composition を明示的に表現する。

- [ ] **Step 1: IdlePlayerState.cs を書き換え**

ファイル全文を以下で置換:

```csharp
/// <summary>
/// プレイヤーの待機ステート。移動入力で移動ステートに遷移する。
/// </summary>
public class IdlePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.SlowDown(dt);
        m_entity.polarity.Switch();
        m_entity.aim.HandleAimInput();
        m_entity.shooting.Fire();
        m_entity.shooting.SelfFire();
        m_entity.shooting.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude > 0.01f)
        {
            m_manager.Change<MovePlayerState>();
        }
    }
}
```

- [ ] **Step 2: MovePlayerState.cs を書き換え**

ファイル全文を以下で置換:

```csharp
/// <summary>
/// プレイヤーの移動ステート。入力がなくなると待機ステートに遷移する。
/// </summary>
public class MovePlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        m_entity.AccelerateToInputDirection(dt);
        m_entity.polarity.Switch();
        m_entity.aim.HandleAimInput();
        m_entity.shooting.Fire();
        m_entity.shooting.SelfFire();
        m_entity.shooting.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            m_manager.Change<IdlePlayerState>();
        }
    }
}
```

- [ ] **Step 3: AimPlayerState.cs を書き換え**

ファイル全文を以下で置換:

```csharp
/// <summary>
/// エイム状態。カメラ方向を向きながらストレイフ移動する。
/// </summary>
public class AimPlayerState : EntityState<Player>
{
    public override void UpdateState(float dt)
    {
        // ストレイフ移動：カメラ方向を向き、速度半減
        m_entity.MoveWithInputStrafe(dt);
        m_entity.polarity.Switch();
        m_entity.aim.HandleAimInput();   // aimReleaseGrace が切れると StopAim() → 別ステートに遷移
        m_entity.shooting.Fire();
        m_entity.shooting.SelfFire();
        m_entity.shooting.Reload();

        if (m_entity.input.MoveInput.sqrMagnitude < 0.01f)
        {
            m_entity.SlowDown(dt);
        }
    }
}
```

- [ ] **Step 4: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。

- [ ] **Step 5: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/IdlePlayerState.cs \
        Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/MovePlayerState.cs \
        Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/AimPlayerState.cs
git commit -m "refactor(state): Player の能力呼び出しを Controller 直接呼び出しに変更"
```

---

## Task 7: PlayerAnimator.cs を AimController 参照に更新

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab`

**目的:** PlayerAnimator は `m_player.IsAiming` を読んでいたが、IsAiming は AimController に移動する。`m_aim` SerializeField を追加し、そちらから読む形に変更。

- [ ] **Step 1: PlayerAnimator.cs に m_aim SerializeField を追加**

L29-30 付近、`[SerializeField] private Player m_player;` の直後に以下を挿入:

```csharp
    [Tooltip("エイム Controller。未設定なら親の GetComponentInParent<AimController>()")]
    [SerializeField] private AimController m_aim;
```

- [ ] **Step 2: Awake での取得を追加**

L84 付近、`if (m_player   == null) m_player   = GetComponentInParent<Player>();` の直後に以下を挿入:

```csharp
        if (m_aim      == null) m_aim      = GetComponentInParent<AimController>();
```

- [ ] **Step 3: LateUpdate の IsAiming 読み取りを差し替え**

L207-210 付近を以下のように変更:

before:
```csharp
        if (m_player != null)
        {
            m_animator.SetBool(m_hIsAiming, m_player.IsAiming);
        }
```

after:
```csharp
        if (m_aim != null)
        {
            m_animator.SetBool(m_hIsAiming, m_aim.IsAiming);
        }
```

- [ ] **Step 4: prefab の PlayerAnimator ブロックに m_aim 参照を追加**

PlayerAnimator は prefab の `Model` GameObject 配下にある。PlayerAnimator の MonoBehaviour ブロックを検索:

```bash
grep -n "m_Script.*ce3f3cd8bd3c81847bf48beed2325720" Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
```

そのブロックの `m_player: {fileID: ...}` 行の**直後**に以下を追加:

```yaml
  m_aim: {fileID: 7300000000000000002}
```

（fileID 7300000000000000002 は Task 5 で割り当てた AimController の fileID）

- [ ] **Step 5: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.ForceReserializeAssets(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。

- [ ] **Step 6: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs \
        Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
git commit -m "refactor(anim): PlayerAnimator が IsAiming を AimController から読むように変更"
```

---

## Task 8: AmmoUI.cs と ReticleUI.cs を Controller 参照に更新

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs`

**目的:** UI は `player.OnPolarityChanged / player.CurrentPole / player.IsAiming` を参照していたが、これらは Controller に移動する。UI が Player から `GetComponent<PolarityController>()` / `GetComponent<AimController>()` で Controller を取得する形に変更。

- [ ] **Step 1: AmmoUI.cs を書き換え**

ファイル全文を以下で置換:

```csharp
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 残弾数UIの表示・更新。スプライト切替方式（極性＋残弾数統合画像）。
/// BulletManagerとPolarityControllerのイベントを購読する。
/// </summary>
public class AmmoUI : MonoBehaviour
{
    [FormerlySerializedAs("ammoImage")]
    [SerializeField] private Image m_ammoImage;

    [Header("S極 残弾スプライト (0〜4)")]
    [FormerlySerializedAs("spritesS")]
    [SerializeField] private Sprite[] m_spritesS;

    [Header("N極 残弾スプライト (0〜4)")]
    [FormerlySerializedAs("spritesN")]
    [SerializeField] private Sprite[] m_spritesN;

    private PolarityController m_polarity;
    private MagneticPole m_currentPole = MagneticPole.S;
    private int m_currentRemaining = 4;

    void Start()
    {
        var playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_polarity = playerObj.GetComponent<PolarityController>();
            if (m_polarity != null)
            {
                m_polarity.OnPolarityChanged += OnPolarityChanged;
                m_currentPole = m_polarity.CurrentPole;
            }
        }

        if (BulletManager.Instance != null)
        {
            BulletManager.Instance.OnBulletCountChanged += OnBulletCountChanged;
            OnBulletCountChanged(0);
        }

        UpdateSprite();
    }

    void OnDestroy()
    {
        if (m_polarity != null)
            m_polarity.OnPolarityChanged -= OnPolarityChanged;

        if (BulletManager.Instance != null)
            BulletManager.Instance.OnBulletCountChanged -= OnBulletCountChanged;
    }

    private void OnPolarityChanged(MagneticPole pole)
    {
        m_currentPole = pole;
        UpdateSprite();
    }

    private void OnBulletCountChanged(int usedCount)
    {
        int max = BulletManager.Instance != null ? BulletManager.Instance.MaxBullets : 4;
        m_currentRemaining = Mathf.Clamp(max - usedCount, 0, max);
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (m_ammoImage == null) { ChannelLogger.LogGuardReturn("UI", "残弾Image未設定"); return; }

        Sprite[] sprites = m_currentPole == MagneticPole.S ? m_spritesS : m_spritesN;
        if (sprites != null && m_currentRemaining >= 0 && m_currentRemaining < sprites.Length)
            m_ammoImage.sprite = sprites[m_currentRemaining];
    }
}
```

- [ ] **Step 2: ReticleUI.cs を書き換え**

ファイル全文を以下で置換:

```csharp
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// レティクルUI。エイム状態と極性に応じてスプライトを切り替える。
/// PolarityController / AimController を購読する。
/// </summary>
public class ReticleUI : MonoBehaviour
{
    [FormerlySerializedAs("reticleImage")]
    [SerializeField] private Image m_reticleImage;

    [Header("Hipfire (通常時)")]
    [FormerlySerializedAs("hipfireS")]
    [SerializeField] private Sprite m_hipfireS;
    [FormerlySerializedAs("hipfireN")]
    [SerializeField] private Sprite m_hipfireN;

    [Header("Aim (エイム時)")]
    [FormerlySerializedAs("aimS")]
    [SerializeField] private Sprite m_aimS;
    [FormerlySerializedAs("aimN")]
    [SerializeField] private Sprite m_aimN;

    private PolarityController m_polarity;
    private AimController m_aim;
    private MagneticPole m_currentPole = MagneticPole.S;

    void Start()
    {
        var playerObj = GameObject.FindWithTag(GameTags.Player);
        if (playerObj != null)
        {
            m_polarity = playerObj.GetComponent<PolarityController>();
            m_aim = playerObj.GetComponent<AimController>();

            if (m_polarity != null)
            {
                m_polarity.OnPolarityChanged += OnPolarityChanged;
                m_currentPole = m_polarity.CurrentPole;
            }
        }

        UpdateSprite();
    }

    void OnDestroy()
    {
        if (m_polarity != null)
            m_polarity.OnPolarityChanged -= OnPolarityChanged;
    }

    void Update()
    {
        UpdateSprite();
    }

    private void OnPolarityChanged(MagneticPole pole)
    {
        m_currentPole = pole;
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        if (m_reticleImage == null) { ChannelLogger.LogGuardReturn("UI", "レティクルImage未設定"); return; }

        bool aiming = m_aim != null && m_aim.IsAiming;

        if (aiming)
            m_reticleImage.sprite = m_currentPole == MagneticPole.S ? m_aimS : m_aimN;
        else
            m_reticleImage.sprite = m_currentPole == MagneticPole.S ? m_hipfireS : m_hipfireN;
    }
}
```

- [ ] **Step 3: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。

- [ ] **Step 4: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs
git commit -m "refactor(ui): AmmoUI/ReticleUI が PolarityController/AimController を直接購読するように変更"
```

---

## Task 9: CameraSettingsApplier.cs を AimController.OnAimChanged に更新

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/CameraSettingsApplier.cs`

**目的:** `Player.OnAimChanged` は削除されるので、新設の `AimController.OnAimChanged`（同じ static event）に購読先を切り替える。`Player.OnPlayerReady` と `Player.Current` は引き続き利用。

- [ ] **Step 1: OnEnable / OnDisable の購読先を変更**

L26-37 を次のように書き換え:

before:
```csharp
    void OnEnable()
    {
        Player.OnAimChanged += SetAimMode;
        Player.OnPlayerReady += InitializeWithPlayer;
        if (Player.Current != null) InitializeWithPlayer(Player.Current);
    }

    void OnDisable()
    {
        Player.OnAimChanged -= SetAimMode;
        Player.OnPlayerReady -= InitializeWithPlayer;
    }
```

after:
```csharp
    void OnEnable()
    {
        AimController.OnAimChanged += SetAimMode;
        Player.OnPlayerReady += InitializeWithPlayer;
        if (Player.Current != null) InitializeWithPlayer(Player.Current);
    }

    void OnDisable()
    {
        AimController.OnAimChanged -= SetAimMode;
        Player.OnPlayerReady -= InitializeWithPlayer;
    }
```

- [ ] **Step 2: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Console.ReadLogs
```
Expected: Compile error 0。

- [ ] **Step 3: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/CameraSettingsApplier.cs
git commit -m "refactor(camera): CameraSettingsApplier が AimController.OnAimChanged を購読するように変更"
```

---

## Task 10: Player.cs から旧 API を削除 + Movement 吸収 + partial ファイル削除 + prefab 旧 SerializeField 削除

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Aim.cs`
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Polarity.cs`
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Shooting.cs`
- Delete: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Movement.cs`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab`

**目的:** 旧 partial の責務は全て Controller に移行済みなので、partial ファイル 4 つと Player.cs の旧 API を削除する。Movement は Entity base の protected メソッド依存なので Player.cs 内に吸収。prefab の Player ブロックから `m_bulletSettings / m_firePoint / m_selfFireHeightOffset` を削除する。

- [ ] **Step 1: Player.cs を新しい全文に書き換え**

`Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs` 全文を以下で置換:

```csharp
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーエンティティ。入力・ステート・磁力の統合制御を行うハブ。
/// 能力系（射撃/エイム/磁極）は同 GameObject 上の Controller に分離。
/// Movement は Entity base の protected メソッド依存のため本クラスに保持。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
[RequireComponent(typeof(PolarityController))]
[RequireComponent(typeof(AimController))]
[RequireComponent(typeof(ShootingController))]
public class Player : Entity
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;

    /// <summary>プレイヤー設定SO。Controller から参照される唯一の保持者。</summary>
    public PlayerSettings Settings => m_settings;

    /// <summary>現在アクティブな Player インスタンス。Awakeで設定、OnDestroyでクリア。</summary>
    public static Player Current { get; private set; }

    /// <summary>Player.Awake で発火。シーン参照なしでサブシステムが Player を取得する用。</summary>
    public static event Action<Player> OnPlayerReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
        OnPlayerReady = null;
    }

    protected override float Gravity => m_settings.gravity;
    protected override float SnapForce => m_settings.snapForce;
    protected override float ExternalDrag => m_settings.externalDrag;
    protected override float GroundCheckDistance => m_settings.groundCheckDistance;
    protected override LayerMask GroundLayer => m_settings.groundLayer != 0 ? m_settings.groundLayer : PhysicsLayers.MaskGroundCheck;
    protected override float PullOrientationThreshold => m_settings.pullOrientationThreshold;
    protected override float PullOrientationSpeed => m_settings.pullOrientationSpeed;

    /// <summary>プレイヤーの入力ハンドラー。</summary>
    public PlayerInputHandler input { get; private set; }

    /// <summary>プレイヤーイベントの発火用。</summary>
    public PlayerEvents events { get; private set; }

    /// <summary>プレイヤーのステートマシン。</summary>
    public PlayerStateManager states { get; private set; }

    /// <summary>磁力影響を受けるコンポーネント。</summary>
    public Magnetizable magnetizable { get; private set; }

    /// <summary>射撃 Controller。</summary>
    public ShootingController shooting { get; private set; }

    /// <summary>エイム Controller。</summary>
    public AimController aim { get; private set; }

    /// <summary>磁極 Controller。</summary>
    public PolarityController polarity { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
        states = GetComponent<PlayerStateManager>();
        magnetizable = GetComponent<Magnetizable>();
        shooting = GetComponent<ShootingController>();
        aim = GetComponent<AimController>();
        polarity = GetComponent<PolarityController>();

        if (m_settings.groundLayer == 0)
            Debug.LogWarning("[Player] PlayerSettings.groundLayerが未設定。PhysicsLayers.MaskGroundCheckを使用。");

        // HP=0でDiePlayerStateに遷移
        if (m_health != null)
        {
            m_health.OnDie += OnDie;
        }

        Current = this;
        OnPlayerReady?.Invoke(this);
    }

    void OnDestroy()
    {
        if (m_health != null)
        {
            m_health.OnDie -= OnDie;
        }
        if (Current == this) Current = null;
    }

    private void OnDie()
    {
        states.Change<DiePlayerState>();
    }

    void OnDisable()
    {
        // シーン遷移・オブジェクト破棄時にスロー状態を強制解除
        if (aim != null && aim.IsAiming)
        {
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
        UpdateMagneticInfluence();
        states.UpdateState(dt);   // State 側で Controller を呼ぶ

        // 死亡中は重力・移動処理をスキップ（UpdateEntityがvelocityを上書きして落下するのを防ぐ）
        if (!states.IsCurrentOfType<DiePlayerState>())
            UpdateEntity(dt);
    }

    /// <summary>
    /// 磁力場の影響度に応じてEntity multiplierを更新する。
    /// 強い磁力を受けているほど移動が鈍くなる。
    /// </summary>
    private void UpdateMagneticInfluence()
    {
        if (magnetizable == null || MagnetManager.Instance == null
            || MagnetManager.Instance.Settings == null)
        {
            topSpeedMultiplier = 1f;
            turningDragMultiplier = 1f;
            ChannelLogger.LogGuardReturn("Player", "Magnetizable/MagnetManager未取得");
            return;
        }

        float influence = magnetizable.GetInfluence(MagnetManager.Instance.Settings.maxForcePerObject);
        float damping = MagnetManager.Instance.Settings.magnetSpeedDamping;

        topSpeedMultiplier = 1f - influence * damping;
        turningDragMultiplier = 1f + influence * damping;
    }

    // --- Movement ---（Entity base の protected メソッド依存のため Player に保持）

    /// <summary>カメラ相対の入力方向に加速し、進行方向を向く。</summary>
    public void AccelerateToInputDirection(float dt)
    {
        var direction = GetCameraRelativeDirection(input.MoveInput);
        if (direction.sqrMagnitude > 0.01f)
        {
            Accelerate(direction, m_settings.turningDrag, m_settings.acceleration, m_settings.topSpeed, dt);
            FaceDirection(direction, m_settings.rotationSpeed, dt);
        }
    }

    /// <summary>エイム中のストレイフ移動。カメラ方向を向いたまま横移動する。</summary>
    public void MoveWithInputStrafe(float dt)
    {
        Vector3 dir = GetCameraRelativeDirection(input.MoveInput);
        float aimSpeed = m_settings.topSpeed * m_settings.aimMoveSpeedMultiplier;
        if (dir.sqrMagnitude > 0.01f)
        {
            Accelerate(dir, m_settings.turningDrag, m_settings.acceleration, aimSpeed, dt);
        }
        if (m_cachedCameraTransform != null)
        {
            Vector3 camForward = m_cachedCameraTransform.forward;
            camForward.y = 0f;
            FaceDirection(camForward, m_settings.rotationSpeed * 2f, dt, false);
        }
    }

    /// <summary>横移動速度を減速する。</summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_settings.deceleration, dt);
    }
}
```

**変更ポイント:**
- `public partial class Player` → `public class Player`
- `Start()` / `m_mainCamera` 削除（ShootingController に移動済）
- `OnDisable` 内 `IsAiming` 判定を `aim.IsAiming` 経由に変更
- Movement 3メソッド（`AccelerateToInputDirection` / `MoveWithInputStrafe` / `SlowDown`）を吸収
- 能力 API（`Fire`/`SelfFire`/`Reload`/`HandleAimInput`/`StartAim`/`StopAim`/`SwitchPole`/`IsAiming`/`CurrentPole`/`OnAimChanged`/`OnPolarityChanged`）削除
- SerializeField `m_bulletSettings`/`m_firePoint`/`m_selfFireHeightOffset` は Player.cs 側に宣言無し（ShootingController に移行済）

- [ ] **Step 2: 旧 partial ファイル 4 つを削除**

Assets/ 配下の削除は UniCLI Eval で `.meta` も含めて削除:

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.DeleteAsset(\"Assets/_Project/Scripts/Core/Player/Player.Aim.cs\");"
unicli exec Eval --code "UnityEditor.AssetDatabase.DeleteAsset(\"Assets/_Project/Scripts/Core/Player/Player.Polarity.cs\");"
unicli exec Eval --code "UnityEditor.AssetDatabase.DeleteAsset(\"Assets/_Project/Scripts/Core/Player/Player.Shooting.cs\");"
unicli exec Eval --code "UnityEditor.AssetDatabase.DeleteAsset(\"Assets/_Project/Scripts/Core/Player/Player.Movement.cs\");"
```

- [ ] **Step 3: _Player.prefab の Player ブロックから旧 SerializeField を削除**

prefab の L81-83 の 3 行を削除:

```yaml
  m_bulletSettings: {fileID: 11400000, guid: 425ee2473071b7a4fa2afd4ccb6a162c, type: 2}
  m_firePoint: {fileID: 5721768207443567397}
  m_selfFireHeightOffset: 1
```

（これら 3 行は Task 5 で既に ShootingController ブロックに複製済なので、Player ブロックから削除しても値は失われない）

- [ ] **Step 4: ForceReserializeAssets + 保存**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.ForceReserializeAssets(); UnityEditor.AssetDatabase.SaveAssets();"
```

- [ ] **Step 5: 最終コンパイル + warning 確認**

```bash
unicli exec Console.ReadLogs
```
Expected: Compile error 0, new warning 0。

以下が残っていないことを確認（すべて参照先消滅なのでコンパイルエラー or "missing member" で止まる）:
- `player.Fire()` / `player.SelfFire()` / `player.Reload()`
- `player.HandleAimInput()` / `player.StartAim()` / `player.StopAim()`
- `player.SwitchPole()`
- `player.IsAiming` / `player.CurrentPole`
- `player.OnAimChanged` / `player.OnPolarityChanged`
- `Player.OnAimChanged`
- `Player.Polarity.cs` / `Player.Shooting.cs` / `Player.Aim.cs` / `Player.Movement.cs` が存在しない

```bash
grep -rn "player\.Fire\(\|player\.SelfFire\(\|player\.Reload\(\|player\.HandleAimInput\(\|player\.StartAim\(\|player\.StopAim\(\|player\.SwitchPole\(\|player\.IsAiming\|player\.CurrentPole\|m_player\.IsAiming\|m_player\.CurrentPole\|m_player\.OnPolarityChanged\|Player\.OnAimChanged" Magnet_Rush/Assets/_Project/Scripts/
```
Expected: 0 matches。

```bash
ls Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.*.cs 2>/dev/null || echo "OK: no partial files"
```
Expected: `OK: no partial files`

- [ ] **Step 6: コミット**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs \
        Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
# 削除ファイル
git add -u Magnet_Rush/Assets/_Project/Scripts/Core/Player/
git commit -m "refactor(player): 旧 partial 4ファイル削除、Player.cs をハブ+Movement に絞る、prefab 旧 SerializeField 削除"
```

---

## Task 11: 最終検証（compile + PlayMode smoke）

**目的:** 全体整合性の最終確認。compile error / warning ゼロ、PlayMode で主要操作を確認する。

- [ ] **Step 1: コンパイル・警告ゼロ確認**

```bash
unicli exec Console.Clear
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.ReadLogs
```
Expected: Compile errors: 0, warnings: 0 (既存の想定外 warning なし)

- [ ] **Step 2: prefab の component 配列を目視確認**

```bash
grep -n "m_Component\|component: {fileID" Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab | head -20
```
Expected: `_Player` の `m_Component` に 12 エントリ（元 9 + 追加 3 Controller）。

- [ ] **Step 3: Controller の互いの参照確認**

```bash
grep -n "guid: 425ee2473071b7a4fa2afd4ccb6a162c" Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
```
Expected: **1 箇所のみ**（ShootingController ブロック内。Player ブロックには残っていないこと）。

- [ ] **Step 4: PlayMode スモーク（ユーザー実行）**

Unity Editor を開いて PlayMode に入り、以下を順に確認:

| # | 操作 | 期待結果 |
|---|---|---|
| 1 | PlayMode 開始 | Console エラーなし、`_Player` 配下の PolarityController / AimController / ShootingController が有効 |
| 2 | 左スティック移動 | Idle → Move 遷移、キャラが移動方向を向く |
| 3 | Y ボタン | 磁極切替、AmmoUI / ReticleUI のスプライトが N ⇔ S で切り替わる |
| 4 | LT ホールド | Aim 状態、Time.timeScale 変化、ReticleUI が Aim スプライト、カメラ距離/FOV 変化 |
| 5 | RT | 弾が発射、画面中央方向に飛ぶ、残弾 UI が減る |
| 6 | A または F | セルフファイア、磁場視覚化表示 |
| 7 | X | リロード、残弾 UI が 4 に戻る |
| 8 | LT 離す | エイム終了、Time.timeScale = 1 |
| 9 | ダメージで HP 0 | DiePlayerState 遷移、リスポーン |

- [ ] **Step 5: PlayMode 成功を確認してコミット（既にコミット済なら空コミット不要）**

PlayMode で問題があれば Task 5〜10 の該当箇所を調査・修正。問題なければ PR 作成に進む。

```bash
git log --oneline feature/player-anim-refactor | head -20
```

---

## 変更履歴

- 2026-04-21: 初版作成（Composition 復元計画）

