# Player Architecture Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Platformer 移植後に残った 4 つの構造的脆弱性をアルファ初期のうちに潰す。Animator パラメータ欠落の検出、State 型参照の堅牢化、State Int index の Inspector 依存解消、Player.cs の God Object 化予防。

**Architecture:** Animator 参照検証は Start で AnimatorControllerParameter[] と照合。State 型保存は AssemblyQualifiedName 完全形に変更。State Int index は `PlayerStateIndex` enum で固定化、PlayerAnimator が enum 経由で Type→Int マッピング。Player.cs は partial class で機能別ファイル（Shooting / Aim / Polarity / Movement）に分割。

**Tech Stack:** Unity 6 / C# 9+ partial class / Animator API / Reflection Type.GetType / 既存 PlayerAnimator / EntityStateManager

---

## File Structure

**Modify:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs` ── Start() に `ValidateAnimatorParameters()` と `ValidateStateOrder()` 追加、`HandleStateChange` で enum 経由 index を使用
- `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` ── `PlayerStateManager.states` を完全 AssemblyQualifiedName 形式に更新

**Create:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateIndex.cs` ── enum 定義
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Movement.cs` ── partial、移動系メソッド
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Shooting.cs` ── partial、射撃系
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Aim.cs` ── partial、エイム系
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Polarity.cs` ── partial、磁極系

**Modify (partial split ですが src は変更):**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs` ── partial class 宣言に変更、各機能を他ファイルに移動後に残るのはコアのみ（~150 行）

---

## Verification Model

Unity xUnit 基盤なし。各タスク末尾で:

1. **Compile 検証** ── UniCLI `AssetDatabase.Refresh()` + `Console.GetLog --logType Error`
2. **実行時検証**（Task 1, 3 のみ） ── PlayMode で Animator なし状態/パラメータ欠落状態を再現し LogError が出るか
3. **Diff 確認** ── `git diff --stat`

---

## Task 1: Animator パラメータ存在検証

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs`

**目的:** `m_animator.SetFloat("MoveSpeed", ...)` 等が silent failure するのを防ぐ。Animator Controller 側でパラメータ名を変えたら Start() で即 LogError する。

- [ ] **Step 1: PlayerAnimator.cs を Read**

```
Read: Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs
```

現状 Start() の構造を確認:
```csharp
void Start()
{
    m_hState = Animator.StringToHash(m_stateName);
    // ... 全 11 個の hash キャッシュ
    
    if (m_states != null)
    {
        m_states.OnStateChanged += HandleStateChange;
    }
}
```

- [ ] **Step 2: `ValidateAnimatorParameters()` メソッドを追加**

Start() の次（`HandleStateChange` 等のメソッド定義より前）に追加:

```csharp
    /// <summary>
    /// Animator Controller に必要なパラメータ名が全て定義されているか検証する。
    /// 欠落していれば LogError でガード（silent SetFloat/SetBool を防ぐ）。
    /// Controller 未割当時はスキップ（メンバーが後からアサインする前提）。
    /// </summary>
    private void ValidateAnimatorParameters()
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null) return;

        var expected = new (string name, string purpose)[]
        {
            (m_stateName,           "State (Int)"),
            (m_lastStateName,       "LastState (Int)"),
            (m_onStateChangedName,  "OnStateChanged (Trigger)"),
            (m_moveSpeedName,       "MoveSpeed (Float)"),
            (m_moveInputXName,      "MoveInputX (Float)"),
            (m_moveInputZName,      "MoveInputZ (Float)"),
            (m_isAimingName,        "IsAiming (Bool)"),
            (m_isGroundedName,      "IsGrounded (Bool)"),
            (m_shootName,           "Shoot (Trigger)"),
            (m_selfShootName,       "SelfShoot (Trigger)"),
            (m_reloadName,          "Reload (Trigger)"),
        };

        var existing = new System.Collections.Generic.HashSet<string>();
        foreach (var p in m_animator.parameters)
            existing.Add(p.name);

        foreach (var (name, purpose) in expected)
        {
            if (!existing.Contains(name))
                Debug.LogError(
                    $"[PlayerAnimator] Animator パラメータ '{name}' ({purpose}) が Controller に定義されていません。" +
                    "Inspector の Animator Parameter Names 欄か Animator Controller の Parameters タブを確認してください。",
                    this);
        }
    }
```

- [ ] **Step 3: Start() の末尾で `ValidateAnimatorParameters()` を呼ぶ**

既存の Start() の末尾（`m_states.OnStateChanged += HandleStateChange;` の次）に追加:

```csharp
    void Start()
    {
        m_hState = Animator.StringToHash(m_stateName);
        // ... 既存の hash キャッシュ 11 個 ...

        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }

        ValidateAnimatorParameters();
    }
```

- [ ] **Step 4: UniCLI で compile 確認**

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLog --logType Error
```

Expected: エラー 0。

- [ ] **Step 5: 実動作の確認（Animator Controller 未割当状態）**

現時点で `_Player.prefab` の `Model` 子の Animator には Controller 未割当（メンバーがまだアニメ実装していないため）。これで PlayMode に入っても `LogError` が出ないことを確認:

```bash
unicli exec PlayMode.Enter
unicli exec Console.GetLog --logType Error --count 20
unicli exec PlayMode.Exit
```

Expected: `ValidateAnimatorParameters` 由来の Error なし（`m_animator.runtimeAnimatorController == null` で early return するため）。

- [ ] **Step 6: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs
git commit -m "feat(anim): PlayerAnimator に Animator パラメータ存在検証を追加"
```

---

## Task 2: State AssemblyQualifiedName を完全形に更新

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab`

**目的:** 現状 `_Player.prefab` の `PlayerStateManager.states` は短形式 `"IdlePlayerState, MagnetRush.Player"`。これは `Type.GetType` で解決可能だが、asmdef 名変更時に壊れる上、Unity の Inspector 選択では **完全形式** が保存されるので不整合。完全形式に一度揃える。

- [ ] **Step 1: UniCLI で各 State の完全 AssemblyQualifiedName を取得**

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
unicli exec Eval --code "UnityEngine.Debug.Log(typeof(IdlePlayerState).AssemblyQualifiedName);"
unicli exec Eval --code "UnityEngine.Debug.Log(typeof(MovePlayerState).AssemblyQualifiedName);"
unicli exec Eval --code "UnityEngine.Debug.Log(typeof(DiePlayerState).AssemblyQualifiedName);"
unicli exec Eval --code "UnityEngine.Debug.Log(typeof(AimPlayerState).AssemblyQualifiedName);"
unicli exec Console.GetLog --logType Log --count 8
```

Expected 出力例（実環境で取得した値を使う）:
```
IdlePlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
MovePlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
DiePlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
AimPlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
```

これらの文字列を次のステップで使う。Version / Culture / PublicKeyToken の値は Unity のビルド設定により異なる可能性があるので **実際の取得値**を使うこと。

- [ ] **Step 2: `_Player.prefab` の states 配列を置換**

Read `_Player.prefab`、`PlayerStateManager` の MonoBehaviour ブロックで現状:

```yaml
  states:
  - IdlePlayerState, MagnetRush.Player
  - MovePlayerState, MagnetRush.Player
  - DiePlayerState, MagnetRush.Player
  - AimPlayerState, MagnetRush.Player
```

これを Step 1 で取得した完全形式に置換:

```yaml
  states:
  - IdlePlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
  - MovePlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
  - DiePlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
  - AimPlayerState, MagnetRush.Player, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
```

YAML のインデント（スペース2）と引用符（無し、Unity prefab 慣習）を維持する。Edit tool で1要素ずつ置換（`replace_all=false`）推奨。

- [ ] **Step 3: Compile 確認 + Type.GetType 動作確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLog --logType Error
```

さらに PlayMode に入って state machine 初期化が成功するか:

```bash
unicli exec PlayMode.Enter
unicli exec Console.GetLog --logType Error --count 20
unicli exec PlayMode.Exit
```

Expected: `Type 'xxx' not found` 系のエラーなし。Idle ステートに自動遷移（index=0）。

- [ ] **Step 4: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
git commit -m "refactor(state): PlayerStateManager.states を完全 AssemblyQualifiedName に"
```

---

## Task 3: PlayerStateIndex enum 導入 + order 検証

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateIndex.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs`

**目的:** Animator に送る `State` Int 値を Inspector の配列順ではなく enum で固定する。誰かが Inspector で states 順序を入れ替えても、Animator 側の条件（`State == 2` 等）が破綻しない。

- [ ] **Step 1: `PlayerStateIndex.cs` を新規作成**

Write tool で作成（新規作成なので OK）:

```csharp
/// <summary>
/// Animator の State Int パラメータに渡す index の固定定義。
/// Inspector の states 配列順ではなくこの enum が単一の真実。
/// 新 State 追加時はこの enum に追加 + PlayerAnimator.s_stateTypeToIndex に登録。
/// </summary>
public enum PlayerStateIndex
{
    Idle = 0,
    Move = 1,
    Die  = 2,
    Aim  = 3,
}
```

パス: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateIndex.cs`

- [ ] **Step 2: PlayerAnimator.cs に Type → Index マッピングを追加**

Read `PlayerAnimator.cs`。クラス冒頭の static readonly 領域（ハッシュ定数の近く）に追加:

```csharp
    /// <summary>
    /// State 型 → Animator の State Int 値への固定マッピング。
    /// 新 State を Animator と連動させたい場合はここに追加し、PlayerStateIndex enum にも対応値を定義。
    /// 未登録型は -1 を返す（Animator 側では遷移条件に合致せず無視される）。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<System.Type, int> s_stateTypeToIndex
        = new System.Collections.Generic.Dictionary<System.Type, int>
    {
        { typeof(IdlePlayerState), (int)PlayerStateIndex.Idle },
        { typeof(MovePlayerState), (int)PlayerStateIndex.Move },
        { typeof(DiePlayerState),  (int)PlayerStateIndex.Die  },
        { typeof(AimPlayerState),  (int)PlayerStateIndex.Aim  },
    };

    private static int GetStateIndex(System.Type type)
    {
        if (type == null) return -1;
        return s_stateTypeToIndex.TryGetValue(type, out var idx) ? idx : -1;
    }
```

- [ ] **Step 3: `HandleStateChange()` を enum 経由に書き換え**

既存:
```csharp
    private void HandleStateChange()
    {
        if (m_animator == null || m_states == null) return;
        m_animator.SetInteger(m_hState, m_states.index);
        m_animator.SetInteger(m_hLastState, m_states.lastIndex);
        ResetTriggersExceptStateChange();
        m_animator.SetTrigger(m_hOnStateChanged);
    }
```

変更後（`m_states.index` / `m_states.lastIndex` を使わず enum マッピングを使う）:
```csharp
    private void HandleStateChange()
    {
        if (m_animator == null || m_states == null) return;

        int currentIdx = GetStateIndex(m_states.current?.GetType());
        int lastIdx    = GetStateIndex(m_states.last?.GetType());

        m_animator.SetInteger(m_hState, currentIdx);
        m_animator.SetInteger(m_hLastState, lastIdx);
        ResetTriggersExceptStateChange();
        m_animator.SetTrigger(m_hOnStateChanged);
    }
```

これで Inspector の states 配列を並び替えても Animator への Int 値は enum で固定される。

- [ ] **Step 4: `ValidateStateOrder()` 検証メソッドを追加**

Start() の `ValidateAnimatorParameters()` の次で呼ぶ形で検証追加:

```csharp
    /// <summary>
    /// PlayerStateManager.states に登録されている State が s_stateTypeToIndex のエントリを
    /// 全て持っているか検証する。登録漏れがあれば LogError。
    /// （Inspector 順と enum 値の一致までは検証しない。それは設計として意図的に乖離させている）
    /// </summary>
    private void ValidateStateOrder()
    {
        if (m_states == null) return;

        foreach (var kv in s_stateTypeToIndex)
        {
            if (!m_states.ContainsStateOfType(kv.Key))
                Debug.LogError(
                    $"[PlayerAnimator] Type '{kv.Key.Name}' (expected Int = {kv.Value}) が " +
                    "PlayerStateManager.states に登録されていません。Inspector で追加するか、" +
                    "s_stateTypeToIndex から該当エントリを削除してください。",
                    this);
        }
    }
```

**前提**: `EntityStateManager` には `ContainsStateOfType(Type)` メソッドが必要。現状存在しない場合は `EntityStateManager.cs` に以下を追加:

```csharp
    /// <summary>指定型のステートが登録されているか返す。</summary>
    public bool ContainsStateOfType(Type type)
    {
        return type != null && m_states.ContainsKey(type);
    }
```

Read: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs`

既存の `IsCurrentOfType<TState>()` の近く（パブリックメソッド群）に追加。

- [ ] **Step 5: Start() で ValidateStateOrder() を呼ぶ**

既存:
```csharp
    void Start()
    {
        m_hState = Animator.StringToHash(m_stateName);
        // ... hash cache ...

        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }

        ValidateAnimatorParameters();
    }
```

変更後:
```csharp
    void Start()
    {
        m_hState = Animator.StringToHash(m_stateName);
        // ... hash cache ...

        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }

        ValidateAnimatorParameters();
        ValidateStateOrder();
    }
```

- [ ] **Step 6: Compile 確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLog --logType Error
```

- [ ] **Step 7: PlayMode 動作確認**

現状 `_Player.prefab` の states には 4 State 登録済み、s_stateTypeToIndex も 4 Type で整合する想定。

```bash
unicli exec PlayMode.Enter
unicli exec Console.GetLog --logType Error --count 20
unicli exec PlayMode.Exit
```

Expected: `ValidateStateOrder` のエラーなし。

- [ ] **Step 8: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateIndex.cs \
        Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs \
        Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs
git commit -m "feat(anim): PlayerStateIndex enum を導入、State→Int を Inspector 順から切り離し"
```

---

## Task 4: Player.cs を partial class で機能別分割

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`（partial 宣言化、機能メソッド移動）
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Movement.cs`
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Shooting.cs`
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Aim.cs`
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.Polarity.cs`

**目的:** 現状 388 行の Player.cs を機能別 partial class に分割。God Object 化を早期に防ぐ。Unity の partial class サポートで動作に影響なし（MonoBehaviour は単一クラスとして扱われる、Inspector 表示も従来通り）。

**分割方針:**

- `Player.cs`（~130 行）: class 宣言、Settings SerializeField、static members、Entity overrides、プロパティ (input/events/states/magnetizable)、ライフサイクル (Awake/Start/OnDestroy/OnDisable/Update/OnDie)、UpdateMagneticInfluence
- `Player.Movement.cs`（~50 行）: AccelerateToInputDirection、MoveWithInputStrafe、SlowDown
- `Player.Shooting.cs`（~165 行）: Shooting SerializeFields、m_mainCamera、k_ForwardDotThreshold、Fire、SelfFire、Reload、CalculateTargetPoint
- `Player.Aim.cs`（~55 行）: IsAiming、OnAimChanged、m_aimReleaseGrace、HandleAimInput、StartAim、StopAim
- `Player.Polarity.cs`（~25 行）: CurrentPole、OnPolarityChanged、SwitchPole

**前提**: `Start()` は 1 箇所にしか書けない（partial でも同名メソッド重複不可）。`m_mainCamera = Camera.main;` は `Player.Shooting.cs` で field 宣言するが、Start() は `Player.cs` で一元管理する。

- [ ] **Step 1: 現状の Player.cs を Read して全セクション把握**

```
Read: Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs
```

各機能の行番号（現状）:
- 磁極制御: ~73-89
- エイム制御: ~92-137
- ライフサイクル: ~139-201
- 移動系: ~224-258
- 射撃: ~261-388

- [ ] **Step 2: `Player.Polarity.cs` を新規作成**

Write tool で作成:

```csharp
using System;

/// <summary>
/// Player の磁極制御部分（partial）。
/// Y 入力で S/N を切り替え、UI がイベント購読する。
/// </summary>
public partial class Player
{
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
}
```

- [ ] **Step 3: `Player.Aim.cs` を新規作成**

Write tool で作成:

```csharp
using System;
using UnityEngine;

/// <summary>
/// Player のエイム制御部分（partial）。
/// LT 入力でエイムモードに入りスロー + カメラ固定ストレイフに遷移する。
/// </summary>
public partial class Player
{
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
}
```

- [ ] **Step 4: `Player.Movement.cs` を新規作成**

Write tool で作成:

```csharp
using UnityEngine;

/// <summary>
/// Player の移動系メソッド（partial）。
/// 入力方向への加速、ストレイフ、減速を扱う。
/// </summary>
public partial class Player
{
    /// <summary>
    /// カメラ相対の入力方向に加速し、進行方向を向く。
    /// </summary>
    public void AccelerateToInputDirection(float dt)
    {
        var direction = GetCameraRelativeDirection(input.MoveInput);
        if (direction.sqrMagnitude > 0.01f)
        {
            Accelerate(direction, m_settings.turningDrag, m_settings.acceleration, m_settings.topSpeed, dt);
            FaceDirection(direction, m_settings.rotationSpeed, dt);
        }
    }

    /// <summary>
    /// エイム中のストレイフ移動。カメラ方向を向いたまま横移動する。
    /// </summary>
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

    /// <summary>
    /// 横移動速度を減速する。
    /// </summary>
    public void SlowDown(float dt)
    {
        Decelerate(m_settings.deceleration, dt);
    }
}
```

- [ ] **Step 5: `Player.Shooting.cs` を新規作成**

Write tool で作成:

```csharp
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Player の射撃系（partial）。RT で通常射撃、A/F でセルフファイア、X でリロード。
/// SerializeField は Inspector で _Player.prefab に設定済み。
/// </summary>
public partial class Player
{
    [Header("Shooting")]
    [FormerlySerializedAs("bulletSettings")]
    [SerializeField] private BulletSettings m_bulletSettings;

    [FormerlySerializedAs("firePoint")]
    [SerializeField] private Transform m_firePoint;

    [SerializeField] private float m_selfFireHeightOffset = 1.0f;

    private Camera m_mainCamera;

    private const float k_ForwardDotThreshold = 0.1f;

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

        float height = m_settings != null ? m_settings.firePointHeight : 1.2f;
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
            bullet.Initialize(CurrentPole, direction);
            BulletManager.Instance.Register(bullet);
            bullet.OnImpact += StopAim;
        }

        events?.FireShoot();
    }

    /// <summary>A / F 入力があればセルフファイア（自己磁化）。毎フレーム呼ぶ。</summary>
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

    /// <summary>X 入力があればリロード（全弾クリア）。毎フレーム呼ぶ。</summary>
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
}
```

- [ ] **Step 6: `Player.cs` を partial 宣言 + コアのみに縮小**

Player.cs を全面書き直し。**Step 2-5 で他ファイルに移した内容を全部削除**、class 宣言を `public partial class Player : Entity` に変更、コア残留部のみ保持:

```csharp
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// プレイヤーエンティティ。入力・ステート・磁力の統合制御を行う。
/// 機能別に partial class で分割（Player.Shooting / Player.Aim / Player.Polarity / Player.Movement）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerEvents))]
public partial class Player : Entity
{
    [FormerlySerializedAs("settings")]
    [SerializeField] private PlayerSettings m_settings;

    /// <summary>プレイヤー設定SO。サブコンポーネントから参照される唯一の保持者。</summary>
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

    void Start()
    {
        m_mainCamera = Camera.main;
    }

    protected override void Awake()
    {
        base.Awake();
        input = GetComponent<PlayerInputHandler>();
        events = GetComponent<PlayerEvents>();
        states = GetComponent<PlayerStateManager>();
        magnetizable = GetComponent<Magnetizable>();

        if (m_settings.groundLayer == 0)
            Debug.LogWarning("[Player] PlayerSettings.groundLayerが未設定。PhysicsLayers.MaskGroundCheckを使用。");

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
        if (IsAiming)
        {
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, Time.fixedDeltaTime * 3f);
        UpdateMagneticInfluence();
        states.UpdateState(dt);

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
}
```

**重要な変更点**:
- `public class Player : Entity` → `public partial class Player : Entity`
- 磁極 / エイム / 射撃 / 移動セクションを **全て削除**（他 partial ファイルに移動済み）
- 残留: Settings SerializeField、static members、Entity overrides、プロパティ、ライフサイクル、UpdateMagneticInfluence
- `m_mainCamera` field は `Player.Shooting.cs` で宣言、`Start()` で設定（同じクラスなのでアクセス可能）

- [ ] **Step 7: UniCLI で compile 確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLog --logType Error
```

Expected: エラー 0。partial class はコンパイル時に1クラスに結合されるので MonoBehaviour 的にも問題なし。

**よくあるエラー**:
- `'Player' does not contain a definition for 'Fire'` → partial キーワードが付いていない、または using 不足
- `duplicate method` → 移動が不完全（元ファイルに残ってる）

エラー出たら Step 2-6 のどのファイルに問題があるか確認し修正。

- [ ] **Step 8: PlayMode 動作確認**

```bash
unicli exec PlayMode.Enter
unicli exec Console.GetLog --logType Error --count 20
unicli exec PlayMode.Exit
```

Expected: 既存と同じく Idle に遷移、例外なし。

- [ ] **Step 9: Inspector 表示確認**

`_Player.prefab` を Unity Editor で開き、Player コンポーネントの Inspector に以下が表示されるか確認:

- `Settings`（PlayerSettings SO フィールド）
- `Shooting` [Header] 配下に `Bullet Settings` / `Fire Point` / `Self Fire Height Offset` が表示される

partial class でも Unity Inspector は全ファイルの `[SerializeField]` を結合表示する。Header 順序が変わる可能性はあるが、表示されること自体は保証される。

- [ ] **Step 10: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/
git commit -m "refactor(player): Player.cs を機能別 partial class に分割"
```

---

## Task 5: 最終スモーク + PR 準備

**目的:** Task 1-4 全体の regression 検証 + PR 作成準備。

- [ ] **Step 1: Compile 最終確認**

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLog --logType Error
unicli exec Console.GetLog --logType Warning
```

Expected: Error 0、Warning も無関係なものだけ。

- [ ] **Step 2: PlayMode 全機能確認**

```bash
unicli exec PlayMode.Enter
```

手動で Unity ウィンドウで確認:
- [ ] 起動時に `Validate*` 由来の LogError が出ない（Controller 未割当のため）
- [ ] 左スティック移動
- [ ] LT エイム → Time.timeScale 低下 → 離すと戻る
- [ ] RT 射撃
- [ ] A/F セルフファイア
- [ ] Y 磁極切替
- [ ] X リロード
- [ ] HP 0 で Die → リスポーン

```bash
unicli exec PlayMode.Exit
```

- [ ] **Step 3: 全体 diff 確認**

```bash
git log --oneline origin/develop..HEAD
git diff origin/develop..HEAD --stat
```

Expected: Task 1-4 分のコミット（5件）含めて計 13-14 commits。

- [ ] **Step 4: push + PR 作成**

```bash
git push -u origin feature/player-anim-refactor
```

PR 作成:
```bash
gh pr create --base develop --title "Player アーキテクチャ移植 + アニメ基盤" --body "$(cat <<'EOF'
## 概要

Platformer Project 形式への Player 完全移植 + アニメ基盤 + 構造的脆弱性対策。

## 内容

### Phase 1: アニメ基盤（既存 commits）
- EntityStateManager に index/lastIndex + UnityEvent onStateChanged
- PlayerEvents を UnityEvent 化
- PlayerAnimator.cs 新設（Animator 更新 1 クラス集約）
- docs/player-animation-guide.md 書き直し

### Phase 2: Platformer 完全移植
- PolarityController / AimController / ShootingController を Player.cs に吸収
- State クラスを Player メソッド列挙形式に
- ClassTypeName 属性 + PropertyDrawer 追加
- PlayerStateManager を Inspector 駆動化
- 拡張者向け章追加

### Phase 3: 構造的脆弱性対策
- Animator パラメータ存在検証
- State AssemblyQualifiedName 完全形化
- PlayerStateIndex enum 導入（State Int が Inspector 順非依存に）
- Player.cs を機能別 partial class 分割

## テスト

- Compile: Error 0 / Warning 0
- PlayMode スモーク: 移動/射撃/エイム/磁極/リロード/死亡リスポーン すべて regression なし

## 次のステップ

- メンバーに `docs/player-animation-guide.md` 共有してアニメ実装タスク開始
EOF
)"
```

- [ ] **Step 5: PR URL を報告**

出力された PR URL を控えて完了報告。

---

## Rollback 戦略

各 Task が atomic commit なので問題起きたら該当 commit を revert:

```bash
git revert <commit_sha>
```

Task 4 (partial 分割) で compile エラーが多発する場合は、それだけまるごと戻して他 3 Task を維持:

```bash
git revert <task4_commit_sha>
```

---

## Self-Review 結果

**Spec coverage:**

| 要件 | 対応タスク |
|---|---|
| (1) Animator パラメータ存在検証 | Task 1 |
| (2) AssemblyQualifiedName 完全形化 | Task 2 |
| (3) State index enum 導入 | Task 3 |
| (4) Player.cs partial class 分割 | Task 4 |
| 全体 regression 検証 + PR | Task 5 |

全要件カバー ✅

**Placeholder scan:** 「TBD / TODO」等なし ✅

**Type consistency:**
- `PlayerStateIndex` enum 値 (Task 3) が `_Player.prefab` の states 配列順 (Task 2) と整合していることは **意図的に結合しない**（Task 3 で Dict で分離）→ これは設計意図
- `ContainsStateOfType` 追加 (Task 3 Step 4) → `EntityStateManager.cs` にメソッドが存在するか確認、なければこの Task で追加
- `m_mainCamera` は Task 4 Step 5 で Player.Shooting.cs に移動、Start() は Player.cs で参照 → 同じ partial class なのでアクセス OK

整合性 OK。

---

## Change History

- 2026-04-21 初版（西川駿太 + Claude Opus 4.7 によるアルファ段階脆弱性対策計画）
