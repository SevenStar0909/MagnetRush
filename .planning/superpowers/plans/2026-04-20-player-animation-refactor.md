# Player Animation Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** プレイヤーアニメーション実装の基盤を Platformer Project 形式に寄せてリファクタし、メンバーが C# を書かずに Unity Editor だけでアニメを組める状態にする。

**Architecture:** ハイブリッドイベント設計 ── Inspector 接続用は `UnityEvent`、コード内部通知は `event Action<T>`。Animator 更新は `PlayerAnimator.cs` 1クラスに集約（Bridge / Listener 経由の分散を廃止）。ステート遷移は Int index + 単一 `OnStateChanged` Trigger で表現し、Animator の Trigger を 5 個から 1 個に削減。

**Tech Stack:** Unity 6 / C# 9+ / Input System / ScriptableObject / `UnityEvent` / `event Action` / 既存 `EntityStateManager<T>` / 既存 `PlayerStateManager`

---

## File Structure

**Modify:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs` ── `List<EntityState<T>>` 追加、`index`/`lastIndex` プロパティ追加、`OnStateChanged` を `event Action` 併用の UnityEvent 化
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerEvents.cs` ── `event Action` を `UnityEvent` に変換（`OnPolaritySwitch` は引数なし UnityEvent + `PolarityController.CurrentPole` 参照方式）
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs` ── 購読を `.AddListener` に変更、`CurrentPole` public プロパティ追加
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs` ── 購読を `.AddListener` / `.RemoveListener` に変更
- `docs/player-animation-guide.md` ── 全面書き直し（Bridge 章削除、`PlayerAnimator` 設定手順に置換）

**Create:**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs` ── Animator 更新ロジック 1 本化。ステート index / 連続値供給 / イベント購読すべて
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs.meta` ── Unity が生成（手動作成禁止）

**Delete:**
- （なし。既存コードの削除はなし。将来削除候補: `EntityStateManagerListener` の Animator 直結用途は自然と消えるが、SE/VFX 用に残す）

**Verify-only (読むだけ):**
- `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/Entity.cs` ── `EntityEvents` / `lateralVelocity` / `IsGrounded`
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerStateManager.cs` ── 登録順序が `index` に影響
- `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/*.cs` ── 変更なし
- `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab` ── 実行前に Unity 側で `PlayerAnimator` を `Model` 子に付ける（Task 7 で手順を書く）

---

## Verification Model

Unity プロジェクトには xUnit 的なユニットテスト基盤がないため、各タスクの検証は以下の 3 層:

1. **Compile 検証** ── `unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"` の後 Console にエラーが無い
2. **PlayMode スモーク** ── Unity を Play して、主要挙動（移動・射撃・リロード・磁極切替・死亡→リスポーン）が以前と変わらず動く
3. **Diff 確認** ── `git diff` を読んで、意図通りの変更のみであることを目視

各タスク末尾で上記を実行。失敗したらロールバックして原因特定。

---

## Task 1: `EntityStateManager` に `List` と index プロパティを追加

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs:28-107`

**目的:** `Animator` の `State` (Int) パラメータに渡す登録順 index を取得可能にする。Dictionary だけでは順序が保証されないため、並行する `List` を導入する。

- [ ] **Step 1: `EntityStateManager.cs` を開いて現状の Dictionary を把握**

Read: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs`
確認事項: `m_states` (Dictionary) の使用箇所、`RegisterState` で登録時に `m_states[state.GetType()] = state` としている。

- [ ] **Step 2: `List<EntityState<T>> m_list` フィールドを追加**

`m_states` の直下に追加:

```csharp
private readonly List<EntityState<T>> m_list = new();
```

- [ ] **Step 3: `RegisterState` に `m_list.Add` を追加**

既存:
```csharp
public void RegisterState(EntityState<T> state)
{
    m_states[state.GetType()] = state;
}
```

変更後:
```csharp
public void RegisterState(EntityState<T> state)
{
    var type = state.GetType();
    if (m_states.ContainsKey(type)) return;
    m_states[type] = state;
    m_list.Add(state);
}
```

重複登録をガードして、`m_list` と `m_states` の整合を保つ。

- [ ] **Step 4: `index` / `lastIndex` プロパティを追加**

`current` プロパティの直下に追加:

```csharp
/// <summary>現在ステートの登録順 index。未登録 or 未初期化時は -1。</summary>
public int index => current != null ? m_list.IndexOf(current) : -1;

/// <summary>前回ステートの登録順 index。未初期化時は -1。</summary>
public int lastIndex => last != null ? m_list.IndexOf(last) : -1;
```

- [ ] **Step 5: UniCLI で compile 確認**

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLogs --count 30
```

Expected: Console に `error` を含む行がない。`warning` は OK。

- [ ] **Step 6: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs
git commit -m "feat(state): EntityStateManager に index / lastIndex プロパティ追加"
```

---

## Task 2: `EntityStateManager` に UnityEvent 版 `onStateChanged` を追加

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs:1-23`

**目的:** Inspector から VFX / SE / アニメ等を「ステート変化時」に直結できるようにする。引数なしの UnityEvent（Inspector serializable）。既存 `event Action OnStateChanged` は内部通知用としてそのまま残す（ハイブリッド設計）。

- [ ] **Step 1: `EntityStateManagerBase` クラスに `UnityEvent onStateChanged` フィールドを追加**

ファイル冒頭の `using` に追加:

```csharp
using UnityEngine.Events;
```

`EntityStateManagerBase` の `event Action OnStateChanged` の直下に追加:

```csharp
[Tooltip("Inspectorから接続可能なステート変化イベント。コード購読はOnStateChangedを使う")]
public UnityEvent onStateChanged;
```

- [ ] **Step 2: `InvokeStateChanged()` で UnityEvent 側も発火するよう変更**

既存:
```csharp
protected void InvokeStateChanged() => OnStateChanged?.Invoke();
```

変更後:
```csharp
protected void InvokeStateChanged()
{
    OnStateChanged?.Invoke();
    onStateChanged?.Invoke();
}
```

`onStateChanged` は `[SerializeField]` ではなく `public` フィールドなので、Inspector 側で未初期化なら `null` の可能性がある（Unity 6 では通常自動初期化される）。`?.Invoke()` で保護。

- [ ] **Step 3: UniCLI で compile 確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLogs --count 30
```

Expected: エラーなし。

- [ ] **Step 4: `_Player.prefab` で Inspector に `onStateChanged` が表示されるか確認**

```bash
unicli exec Hierarchy.Select --path "_Player" 2>/dev/null || echo "Play Mode 不要、Inspector のみ"
```

手動確認: Unity Editor で `_Player.prefab` を Prefab Mode で開き、`PlayerStateManager` の Inspector に `On State Changed` UnityEvent 欄が出ているか。

- [ ] **Step 5: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/EntityStateManager.cs
git commit -m "feat(state): EntityStateManager に UnityEvent 版 onStateChanged を追加"
```

---

## Task 3: `PlayerEvents` を UnityEvent ベースに書き換え

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerEvents.cs:1-18`

**目的:** 意味的イベント（`OnShoot` / `OnSelfShoot` / `OnReload` / `OnPolaritySwitch`）を Inspector 接続可能にする。`OnPolaritySwitch` は引数なしに変更し、極情報は `PolarityController.CurrentPole`（Task 4 で追加）から取得する方式にする。

- [ ] **Step 1: 既存購読箇所を grep で洗い出し**

```bash
grep -rn "m_events\.On\|events\.On\|PlayerEvents" Magnet_Rush/Assets/_Project/Scripts/ --include="*.cs"
```

Expected 出力（すでに確認済み）:
- `PolarityController.cs` が `OnPolaritySwitch` を Fire
- `ShootingController.cs` が `OnShoot` / `OnSelfShoot` / `OnReload` を Fire
- 購読側は現時点で無い（これから `PlayerAnimator` が購読する）

- [ ] **Step 2: `PlayerEvents.cs` 全体を書き換え**

置換後の全文:

```csharp
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// プレイヤーアクションのイベントハブ。
/// VFX/SE/アニメ等がInspectorから繋げるようUnityEvent化。
/// 極性情報は OnPolaritySwitch 発火後に PolarityController.CurrentPole から読む。
/// </summary>
public class PlayerEvents : MonoBehaviour
{
    [Tooltip("通常射撃時に発火")]
    public UnityEvent OnShoot;

    [Tooltip("セルフファイア時に発火")]
    public UnityEvent OnSelfShoot;

    [Tooltip("磁極切替時に発火。極は PolarityController.CurrentPole から取得")]
    public UnityEvent OnPolaritySwitch;

    [Tooltip("リロード時に発火")]
    public UnityEvent OnReload;

    public void FireShoot() => OnShoot?.Invoke();
    public void FireSelfShoot() => OnSelfShoot?.Invoke();
    public void FirePolaritySwitch() => OnPolaritySwitch?.Invoke();
    public void FireReload() => OnReload?.Invoke();
}
```

注意:
- `MonoBehaviour` 継承は維持（`_Player.prefab` 上でコンポーネントとして必要）
- `event Action` 削除 → UnityEvent フィールド
- `FirePolaritySwitch` の引数を削除
- 全 `OnXxx` は `public` のまま（UnityEvent は Inspector 公開のため `[SerializeField]` 不要）

- [ ] **Step 3: UniCLI で compile 確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLogs --count 50
```

Expected: **`PolarityController.cs` の `FirePolaritySwitch(pole)` 呼び出しで引数エラーが出る**（Task 4 で修正）。他のコンパイルエラーはないこと。

- [ ] **Step 4: この時点では commit しない**

Task 4 が終わるまで build 赤のまま。Task 4 でまとめて commit する。

---

## Task 4: `PolarityController` を UnityEvent 対応 + `CurrentPole` プロパティ追加

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs`

**目的:** `PlayerEvents.FirePolaritySwitch()` の引数削除に合わせる + `CurrentPole` を外部参照可能にする（アニメ側で極情報が必要な時に `PolarityController.CurrentPole` で取れる）。

- [ ] **Step 1: 現状の `PolarityController.cs` を読む**

Read: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs`

確認ポイント:
- 現状 `m_events.FirePolaritySwitch(pole)` を呼んでいる箇所
- 現状の極保持フィールド名（`m_currentPole` など）

- [ ] **Step 2: `CurrentPole` public プロパティを追加**

現状の private 極フィールド（仮: `m_currentPole`）の直下に:

```csharp
/// <summary>現在の磁極。OnPolaritySwitch 発火後に購読側から参照される。</summary>
public MagneticPole CurrentPole => m_currentPole;
```

フィールド名が `m_currentPole` でない場合は実際のフィールド名に合わせる。

- [ ] **Step 3: `FirePolaritySwitch` 呼び出しを引数なしに変更**

既存（推定）:
```csharp
m_events.FirePolaritySwitch(m_currentPole);
```

変更後:
```csharp
m_events.FirePolaritySwitch();
```

複数箇所ある場合は全部置換。

- [ ] **Step 4: UniCLI で compile 確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLogs --count 30
```

Expected: エラーなし。警告もなければ理想。

- [ ] **Step 5: PlayMode スモークテスト**

```bash
unicli exec PlayMode.Enter
```

シーンで Y を押して磁極切替 → Console に例外が出ないこと。確認後:

```bash
unicli exec PlayMode.Exit
```

- [ ] **Step 6: Commit（Task 3 と合わせて1コミット）**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerEvents.cs \
        Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs
git commit -m "refactor(events): PlayerEvents を UnityEvent 化、PolarityController に CurrentPole プロパティ"
```

---

## Task 5: `ShootingController` は変更不要の確認

**Files:**
- Verify-only: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs`

**目的:** `ShootingController` は `PlayerEvents.FireShoot()` 等を呼ぶだけで、自身が購読はしていない可能性が高い。その場合 Task 3 の変更で破綻しない。変更不要なら Skip。

- [ ] **Step 1: `ShootingController.cs` を読んで購読の有無を確認**

Read: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs`

確認事項:
- `m_events.OnShoot +=` のような購読がないか
- `m_events.FireShoot()` のみの呼び出しか

- [ ] **Step 2: 購読がない場合 → このタスクは No-op**

引数なしの `FireShoot()` / `FireSelfShoot()` / `FireReload()` は Task 3 でも引数なしのまま維持しているので、シグネチャ不整合は発生しない。

- [ ] **Step 3: 購読がある場合（予想外）**

`+= メソッド名` → `.AddListener(メソッド名)`、`-= メソッド名` → `.RemoveListener(メソッド名)` に置換する。UniCLI で compile 確認 → commit:

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs
git commit -m "refactor(events): ShootingController を UnityEvent 購読に変更"
```

- [ ] **Step 4: 変更があっても無くても、次のタスクへ**

---

## Task 6: `PlayerAnimator.cs` 新規作成

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs`

**目的:** Animator の更新ロジックを 1 ファイルに集約する。Platformer Project の `PlayerAnimator.cs` を MagnetRush 要件に合わせて書き換えたもの。

**アーキテクチャ要点:**
- パラメータ名は SerializeField (デフォルト値付き) で1箇所管理
- Start で `Animator.StringToHash` キャッシュ
- ステート変化時に `State` (Int) と `LastState` (Int) + `OnStateChanged` (Trigger) を更新
- LateUpdate で `MoveSpeed` (Float) / `MoveInputX,Z` (Float) / `IsAiming` (Bool) / `IsGrounded` (Bool) を毎フレーム流す
- `PlayerEvents` を購読して `Shoot` / `SelfShoot` / `Reload` Trigger を発火
- Die / Respawn は `ResetAllTriggers` してから SetTrigger（Trigger 残留バグ回避）

- [ ] **Step 1: MCP か UniCLI でファイル新規作成**

MCP 可能なら:
```
create_script に相当する MCP tool で空ファイル生成
```

UniCLI でも可:
```bash
unicli exec Eval --code "System.IO.File.WriteAllText(\"Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs\", \"\"); UnityEditor.AssetDatabase.Refresh();"
```

重要: **`Write` ツールで直接作らない**（`.meta` 整合性のため CLAUDE.md ルール）。

- [ ] **Step 2: ファイル全文を書き込む**

`PlayerAnimator.cs` に以下を書き込む:

```csharp
using UnityEngine;

/// <summary>
/// プレイヤーの Animator を駆動する専用コンポーネント。
/// PlayerEvents を購読して射撃系 Trigger を、LateUpdate で連続値を、
/// ステート変化で State(Int)+OnStateChanged(Trigger) を更新する。
/// Animator の直接操作はこのクラスのみに集約し、他からは触らない。
/// 依存: Animator, PlayerEvents, PlayerInputHandler, PlayerStateManager, Entity
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("このプレイヤーの Animator。未設定なら自身の GetComponent<Animator>()")]
    [SerializeField] private Animator m_animator;

    [Tooltip("イベントハブ。未設定なら親の GetComponentInParent<PlayerEvents>()")]
    [SerializeField] private PlayerEvents m_events;

    [Tooltip("入力ハンドラ。未設定なら親の GetComponentInParent<PlayerInputHandler>()")]
    [SerializeField] private PlayerInputHandler m_input;

    [Tooltip("ステートマネージャ。未設定なら親の GetComponentInParent<PlayerStateManager>()")]
    [SerializeField] private PlayerStateManager m_states;

    [Tooltip("Entity。未設定なら親の GetComponentInParent<Entity>()")]
    [SerializeField] private Entity m_entity;

    [Tooltip("AimController。IsAiming 判定用。")]
    [SerializeField] private AimController m_aim;

    [Header("Animator Parameter Names (Inspector 単一箇所管理)")]
    [SerializeField] private string m_stateName = "State";
    [SerializeField] private string m_lastStateName = "LastState";
    [SerializeField] private string m_onStateChangedName = "OnStateChanged";
    [SerializeField] private string m_moveSpeedName = "MoveSpeed";
    [SerializeField] private string m_moveInputXName = "MoveInputX";
    [SerializeField] private string m_moveInputZName = "MoveInputZ";
    [SerializeField] private string m_isAimingName = "IsAiming";
    [SerializeField] private string m_isGroundedName = "IsGrounded";
    [SerializeField] private string m_shootName = "Shoot";
    [SerializeField] private string m_selfShootName = "SelfShoot";
    [SerializeField] private string m_reloadName = "Reload";

    // --- Hash cache ---
    private int m_hState;
    private int m_hLastState;
    private int m_hOnStateChanged;
    private int m_hMoveSpeed;
    private int m_hMoveInputX;
    private int m_hMoveInputZ;
    private int m_hIsAiming;
    private int m_hIsGrounded;
    private int m_hShoot;
    private int m_hSelfShoot;
    private int m_hReload;

    private int[] m_allTriggers;

    void Awake()
    {
        if (m_animator == null) m_animator = GetComponent<Animator>();
        if (m_events   == null) m_events   = GetComponentInParent<PlayerEvents>();
        if (m_input    == null) m_input    = GetComponentInParent<PlayerInputHandler>();
        if (m_states   == null) m_states   = GetComponentInParent<PlayerStateManager>();
        if (m_entity   == null) m_entity   = GetComponentInParent<Entity>();
        if (m_aim      == null) m_aim      = GetComponentInParent<AimController>();
    }

    void Start()
    {
        m_hState           = Animator.StringToHash(m_stateName);
        m_hLastState       = Animator.StringToHash(m_lastStateName);
        m_hOnStateChanged  = Animator.StringToHash(m_onStateChangedName);
        m_hMoveSpeed       = Animator.StringToHash(m_moveSpeedName);
        m_hMoveInputX      = Animator.StringToHash(m_moveInputXName);
        m_hMoveInputZ      = Animator.StringToHash(m_moveInputZName);
        m_hIsAiming        = Animator.StringToHash(m_isAimingName);
        m_hIsGrounded      = Animator.StringToHash(m_isGroundedName);
        m_hShoot           = Animator.StringToHash(m_shootName);
        m_hSelfShoot       = Animator.StringToHash(m_selfShootName);
        m_hReload          = Animator.StringToHash(m_reloadName);

        m_allTriggers = new[] { m_hOnStateChanged, m_hShoot, m_hSelfShoot, m_hReload };

        if (m_states != null)
        {
            m_states.OnStateChanged += HandleStateChange;
        }
    }

    void OnEnable()
    {
        if (m_events == null) return;
        m_events.OnShoot.AddListener(HandleShoot);
        m_events.OnSelfShoot.AddListener(HandleSelfShoot);
        m_events.OnReload.AddListener(HandleReload);
    }

    void OnDisable()
    {
        if (m_events == null) return;
        m_events.OnShoot.RemoveListener(HandleShoot);
        m_events.OnSelfShoot.RemoveListener(HandleSelfShoot);
        m_events.OnReload.RemoveListener(HandleReload);
    }

    void OnDestroy()
    {
        if (m_states != null)
            m_states.OnStateChanged -= HandleStateChange;
    }

    void LateUpdate()
    {
        if (m_animator == null) return;

        if (m_entity != null)
        {
            m_animator.SetFloat(m_hMoveSpeed, m_entity.lateralVelocity.magnitude);
            m_animator.SetBool(m_hIsGrounded, m_entity.IsGrounded);
        }

        if (m_input != null)
        {
            var mv = m_input.MoveInput;
            m_animator.SetFloat(m_hMoveInputX, mv.x);
            m_animator.SetFloat(m_hMoveInputZ, mv.y);
        }

        if (m_aim != null)
        {
            m_animator.SetBool(m_hIsAiming, m_aim.IsAiming);
        }
    }

    private void HandleStateChange()
    {
        if (m_animator == null || m_states == null) return;
        m_animator.SetInteger(m_hState, m_states.index);
        m_animator.SetInteger(m_hLastState, m_states.lastIndex);
        ResetTriggersExceptStateChange();
        m_animator.SetTrigger(m_hOnStateChanged);
    }

    private void HandleShoot()     { if (m_animator != null) m_animator.SetTrigger(m_hShoot); }
    private void HandleSelfShoot() { if (m_animator != null) m_animator.SetTrigger(m_hSelfShoot); }
    private void HandleReload()    { if (m_animator != null) m_animator.SetTrigger(m_hReload); }

    private void ResetTriggersExceptStateChange()
    {
        if (m_animator == null) return;
        m_animator.ResetTrigger(m_hShoot);
        m_animator.ResetTrigger(m_hSelfShoot);
        m_animator.ResetTrigger(m_hReload);
    }
}
```

重要な設計判断:
- **`AimController.IsAiming`** を参照する前提（既存プロパティ、なければ Task 6.5 で追加）。要確認。
- **`m_states.OnStateChanged`**（`event Action`）を購読。UnityEvent 側（Task 2 で追加した `onStateChanged`）はあえて使わない ── PlayerAnimator は内部コンポーネントなので Action で十分、UnityEvent は外部からの Inspector 接続用に温存。
- **`ResetTriggersExceptStateChange`** で Shoot/SelfShoot/Reload の残留を状態遷移時にクリア。Die / Respawn 用のロジックは State Int 値で遷移判定する前提にしたので Trigger を Die 専用にする必要はない。

- [ ] **Step 3: `AimController.IsAiming` プロパティがあるか確認**

```bash
grep -n "IsAiming\|bool.*Aim" Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs
```

- プロパティがあれば Step 5 へ
- なければ Step 4 で追加

- [ ] **Step 4: `AimController.IsAiming` プロパティを追加（必要な場合のみ）**

既存の内部エイム判定フィールドを public プロパティで公開:

```csharp
/// <summary>エイム中かどうか。PlayerAnimator 等が参照。</summary>
public bool IsAiming => /* 既存の判定ロジック */;
```

実体フィールドが `m_isAiming` (bool) なら `public bool IsAiming => m_isAiming;` でOK。

- [ ] **Step 5: UniCLI で compile 確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLogs --count 50
```

Expected: エラーなし。`PlayerAnimator` が成功してコンパイルされる。

- [ ] **Step 6: Commit**

```bash
git add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs \
        Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs.meta \
        Magnet_Rush/Assets/_Project/Scripts/Core/Player/AimController.cs 2>/dev/null
git commit -m "feat(anim): PlayerAnimator を新設、Animator 更新を 1 クラスに集約"
```

（`AimController.cs` は Step 4 で変更した場合のみ、無ければ除外）

---

## Task 7: `docs/player-animation-guide.md` を全面書き直し

**Files:**
- Modify: `docs/player-animation-guide.md`（442 行 → おおよそ 350 行に短縮）

**目的:** メンバータスクを「Animator Controller 作成 + Inspector 接続 + 動作確認」のみに限定。Bridge スクリプト作成章（§4.3-4.5）を削除し、代わりに `PlayerAnimator` コンポーネントを `Model` に付けて Inspector 設定する手順にする。

- [ ] **Step 1: 既存ガイドの削除対象章を特定**

- 削除: § 4.3 (EntityStateManagerListener で Animator 直結), § 4.4 (PlayerAnimationBridge 新規作成), § 4.5 (接地離地 UnityEvent 接続)
- 保持: § 0-3（設計思想、全体像、モデル Import、Animator パラメータ定義）、§ 4.1（プレハブ階層）、§ 4.2（モデル組み込み）、§ 5-11（既存のまま）

- [ ] **Step 2: Animator パラメータ表を更新**

旧 § 4.1 のパラメータ表を以下で置換:

```
| パラメータ | 型 | 用途 |
|---|---|---|
| State | Int | 現在ステート index（0=Idle, 1=Move, 2=Aim, 3=Die。PlayerStateManager の登録順） |
| LastState | Int | 前回ステート index |
| OnStateChanged | Trigger | ステート変化時 1 回発火。Any State → 各ステートの遷移に使う |
| MoveSpeed | Float | 横速度の m/s 実値。Blend Tree 閾値用 |
| MoveInputX | Float | -1〜1。AimStrafe 2D Blend Tree 横軸 |
| MoveInputZ | Float | -1〜1。AimStrafe 2D Blend Tree 縦軸 |
| IsAiming | Bool | エイム中 |
| IsGrounded | Bool | 接地中 |
| Shoot | Trigger | 通常射撃 |
| SelfShoot | Trigger | セルフファイア |
| Reload | Trigger | リロード |
```

Trigger は **5 個 → 4 個**（Die / Respawn は不要、State Int で判定）。

- [ ] **Step 3: Animator 遷移ロジックを更新**

旧 § 4.2 の「遷移」表を以下で置換:

```
**遷移方式**: Any State から各ステートへ `OnStateChanged` Trigger + `State == N` で遷移

| 遷移先 | 条件 |
|---|---|
| Locomotion (Idle/Walk/Run Blend Tree) | OnStateChanged Trigger かつ State == 0 or 1 |
| AimStrafe (2D Blend Tree) | OnStateChanged Trigger かつ State == 2 |
| Die (ワンショット) | OnStateChanged Trigger かつ State == 3 |

**Has Exit Time**: すべて OFF（即座に遷移）
**Transition Duration**: 0.15s
**Can Transition To Self**: OFF
```

- [ ] **Step 4: 旧 § 4.3-4.5（Bridge 系）を新しい § 4.3 に置換**

新しい § 4.3 の内容:

```markdown
### 4.3 PlayerAnimator コンポーネントを Model に追加する

**C# スクリプト作成は不要**。既に `PlayerAnimator.cs` が存在する。

1. `_Player.prefab` を Prefab Mode で開く
2. `Model` 子を選択
3. Inspector → `Add Component` → `Player Animator` を検索して追加
4. `Animator` 欄に同じ `Model` の Animator をドラッグ（未設定なら Awake で自動取得されるが明示推奨）
5. `Player Events` / `Player Input Handler` / `Player State Manager` / `Entity` / `Aim Controller` 欄は空のままで OK（親から自動取得される）
6. `Animator Parameter Names` セクションは **触らない**。Animator Controller 側と名前が一致している前提

これで Animator 更新はすべて `PlayerAnimator` が担当する。別途 Bridge / Listener を書く必要はない。
```

- [ ] **Step 5: § 5 の動作確認チェックリストを更新**

`Die` Trigger / `Respawn` Trigger 関連の項目を `State == 3` の表現に置き換える。

- [ ] **Step 6: § 10「禁止事項」から「Bridge 関連」を削除、追加で「PlayerAnimator を複数付けない」「Animator Parameter Names 欄を変えない」を追記**

- [ ] **Step 7: Markdown として構造確認**

```bash
grep -n "^##\|^###" docs/player-animation-guide.md
```

章番号に穴や重複がないこと。

- [ ] **Step 8: Commit**

```bash
git add docs/player-animation-guide.md
git commit -m "docs(anim): player-animation-guide.md を PlayerAnimator 形式に書き直し"
```

---

## Task 8: フルスモーク ── 全体を PlayMode で触って regression なし確認

**目的:** Task 1-7 完了後、メンバーに渡す前に全機能が壊れていないことを確認する。

- [ ] **Step 1: Unity を起動して develop ブランチ最新をロード**

既に起動中なら:

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh();"
unicli exec Console.GetLogs --count 30
```

Expected: compile エラー 0。

- [ ] **Step 2: PlayMode でシーンを再生**

```bash
unicli exec PlayMode.Enter
```

Expected: シーンがエラーなく起動し、Player が操作可能な状態。

- [ ] **Step 3: 動作チェックリスト**

各項目を手動で実行し、結果を控える:

- [ ] 左スティックで移動できる
- [ ] ジャンプできる
- [ ] LT でエイム（AimPlayerState 遷移）、離すと戻る
- [ ] RT で射撃 → 弾が出る、Console に例外なし
- [ ] A / F でセルフファイア
- [ ] Y で磁極切替 → 視覚表示切替、例外なし
- [ ] リロード → 弾数回復、例外なし
- [ ] HP を 0 に落として死亡 → DiePlayerState に遷移、リスポーン

アニメは未実装なので動かなくて OK。**例外 / null ref 系の regression が出ないこと**が合格基準。

- [ ] **Step 4: PlayMode 終了**

```bash
unicli exec PlayMode.Exit
```

- [ ] **Step 5: 保存とコミット（もし Unity が .prefab / .meta を書き換えていたら）**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();"
git status
```

変更があれば:

```bash
git add .
git commit -m "chore(anim): PlayMode 検証で Unity が更新した .meta / .prefab を保存"
```

変更が無ければ何もしない。

- [ ] **Step 6: ブランチ状況を確認し、PR 作成の準備**

```bash
git log --oneline -10
git diff origin/develop..HEAD --stat
```

push して PR を作るかはユーザーの判断。計画としてはここで終了。

---

## Task 9 (optional): `ClassTypeName` PropertyDrawer 移植

**優先度:** 低。Task 1-8 完了後、余力があれば実施。

**目的:** `EntityStateManagerListener.states` の文字列入力を、PropertyDrawer でドロップダウン選択化する（Platformer の `ClassTypeName` 属性）。リネーム検出は effetti limitaré だが、typo 防止になる。

**Files:**
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/ClassTypeNameAttribute.cs`
- Create: `Magnet_Rush/Assets/_Project/Scripts/Core/Entity/StateMachine/Editor/ClassTypeNameDrawer.cs`
- Modify: `EntityStateManagerListener.cs` の `states` フィールドに `[ClassTypeName(typeof(EntityState))]` 付与

手順は Platformer Project の該当ファイルを直接参考にする（`docs/platformer-project-patterns.md` § 5.1 にサマリあり）。

**スキップする場合:** Task 9 を消して計画終了。将来やりたくなったら別計画として切り出す。

---

## Rollback 戦略

各タスクの commit で安全に戻れる。問題が起きたら:

```bash
# 直前の commit を取り消す（変更内容は残す）
git reset --soft HEAD^

# 完全に戻す（変更内容も破棄）
git reset --hard HEAD^
```

Task 3-4 は 2 タスクで 1 commit なので、戻す時はまとめて戻る。

---

## Self-Review 結果

**Spec coverage:**

| 要件 | 対応タスク |
|---|---|
| バグが出にくい | Task 6（Trigger 1 個化 + ResetTriggers） |
| Debug しやすい | Task 6（1 クラス集約）、Task 2（onStateChanged で外部 Inspector hook） |
| 問題すぐ特定 | Task 6（PlayerAnimator.cs 見るだけ）、Task 1（index で状態を Int で確認可能） |
| わかりやすい | Task 7（ガイドから Bridge 削除で手順簡素化） |
| 修正が簡単 | Task 3（UnityEvent 化で Inspector 差し替え可）、Task 6（パラメータ名 1 箇所） |
| 一貫性 | Task 2-3（Inspector 用 UnityEvent / 内部 Action のハイブリッド規則を明文化） |
| 開発しやすい | Task 7（メンバーは C# 不要） |

全 7 要件カバー ✅

**Placeholder scan:** 「TBD」「TODO」「implement later」「Add appropriate error handling」等の placeholder なし ✅

**Type consistency:**
- `PlayerAnimator.cs` が参照する API: `Entity.lateralVelocity` / `Entity.IsGrounded` / `PlayerInputHandler.MoveInput` / `AimController.IsAiming` / `PlayerStateManager.OnStateChanged` / `PlayerStateManager.index` / `PlayerStateManager.lastIndex`
- うち `AimController.IsAiming` は未確認 → Task 6.3-6.4 で確認 / 追加を明記 ✅
- `PlayerStateManager.index` / `lastIndex` は Task 1 で追加 ✅
- `PlayerEvents.OnShoot` / `OnSelfShoot` / `OnReload` の UnityEvent 化は Task 3 で実施 ✅

整合性 OK。

---

## Change History

- 2026-04-20 初版（西川駿太 + Claude Opus 4.7 による会話ログからの計画抽出）
