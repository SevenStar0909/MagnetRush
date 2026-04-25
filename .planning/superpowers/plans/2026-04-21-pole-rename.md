# Polarity → Pole Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Polarity` 系統の識別子を `Pole` に統一リネーム。既存の `MagneticPole` enum / `CurrentPole` プロパティと命名を揃え、クラス名・イベント名・プロパティ名の非対称を解消する。

**Architecture:** 意味保存の純粋リネーム。API / 振る舞い / Inspector 接続は変更なし。各 Task は compile green を維持する atomic commit。クラス `PolarityController` → `PoleController`、C# event `OnPolarityChanged` → `OnPoleChanged`、UnityEvent `OnPolaritySwitch` → `OnPoleSwitch`、Fire method `FirePolaritySwitch` → `FirePoleSwitch`、Player hub プロパティ `polarity` → `pole`、private field `m_polarity` → `m_pole` をコードベース全体で一貫して実施。

**Tech Stack:** Unity 6, C# 9, UnityEvent, UniCLI (for AssetDatabase.MoveAsset + ForceReserializeAssets)

**前提ブランチ:** `main`（前回 PR #37 マージ済）から新規 feature ブランチ `feature/pole-rename` を切る

---

## 共通前提

### 環境変数

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
```

### リネーム対応表（完全版）

| 種別 | 旧 | 新 | 場所 |
|---|---|---|---|
| File | `PolarityController.cs` | `PoleController.cs` | `Magnet_Rush/Assets/_Project/Scripts/Core/Player/` |
| File meta | `PolarityController.cs.meta` | `PoleController.cs.meta` | (guid 保持) |
| Class | `PolarityController` | `PoleController` | 同 file 内 |
| C# event | `public event Action<MagneticPole> OnPolarityChanged` | `public event Action<MagneticPole> OnPoleChanged` | PoleController 内 |
| UnityEvent | `public UnityEvent OnPolaritySwitch` | `public UnityEvent OnPoleSwitch` | `PlayerEvents.cs` |
| UnityEvent Fire method | `public void FirePolaritySwitch()` | `public void FirePoleSwitch()` | `PlayerEvents.cs` |
| Hub property | `public PolarityController polarity` | `public PoleController pole` | `Player.cs` |
| Private field | `private PolarityController m_polarity` | `private PoleController m_pole` | 各 caller |
| UI 購読ハンドラ | `private void OnPolarityChanged(MagneticPole pole)` | `private void OnPoleChanged(MagneticPole pole)` | `AmmoUI.cs` / `ReticleUI.cs` |

### リネームしないもの

- `MagneticPole` enum（既に Pole 採用）
- `CurrentPole` プロパティ（既に Pole 採用）
- `MagnetBullet`, `Magnetizable`, `MagnetField`, `MagnetManager` 等の `Magnet` 接頭辞（磁力ファミリー、別スコープ）
- `BulletManager.SetPole` / `Magnetizable.SetPole` 等のメソッド（既に Pole 採用）

### コンパイル確認コマンド

```bash
unicli exec Compile
```
Expected: `Compilation succeeded (0 errors, 0 warnings)`

---

## Task 1: C# event `OnPolarityChanged` → `OnPoleChanged` に改名

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs`

**目的:** C# event (Unity でなく .NET event) の名前を `OnPolarityChanged` → `OnPoleChanged` にする。公開 API は `MagneticPole` 型に既に揃っているので、event 名だけを一貫させる。

- [ ] **Step 1: `PolarityController.cs` の event 宣言と invoke を更新**

before (L16, L32):
```csharp
    public event Action<MagneticPole> OnPolarityChanged;
```
```csharp
        OnPolarityChanged?.Invoke(CurrentPole);
```

after:
```csharp
    public event Action<MagneticPole> OnPoleChanged;
```
```csharp
        OnPoleChanged?.Invoke(CurrentPole);
```

XML doc も合わせて更新（L14-15）:

before:
```csharp
    /// <summary>磁極切替時に発火。UI 等が購読する。</summary>
    public event Action<MagneticPole> OnPolarityChanged;
```

after:
```csharp
    /// <summary>磁極切替時に発火。UI 等が購読する。</summary>
    public event Action<MagneticPole> OnPoleChanged;
```

- [ ] **Step 2: `AmmoUI.cs` の event subscribe / unsubscribe / ハンドラ名を更新**

3 箇所（L34, L51, L57）の `OnPolarityChanged` をすべて `OnPoleChanged` に置換:

before (抜粋):
```csharp
                m_polarity.OnPolarityChanged += OnPolarityChanged;
```
```csharp
            m_polarity.OnPolarityChanged -= OnPolarityChanged;
```
```csharp
    private void OnPolarityChanged(MagneticPole pole)
```

after:
```csharp
                m_polarity.OnPoleChanged += OnPoleChanged;
```
```csharp
            m_polarity.OnPoleChanged -= OnPoleChanged;
```
```csharp
    private void OnPoleChanged(MagneticPole pole)
```

- [ ] **Step 3: `ReticleUI.cs` の event subscribe / unsubscribe / ハンドラ名を更新**

3 箇所（L40, L51, L59）の `OnPolarityChanged` をすべて `OnPoleChanged` に置換（AmmoUI と同パターン）。

- [ ] **Step 4: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Compile
```
Expected: `Compilation succeeded (0 errors, 0 warnings)`

- [ ] **Step 5: 残存確認**

```bash
# OnPolarityChanged が完全消滅していることを確認
grep -rn "OnPolarityChanged" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets" || echo "OK: no leftover"
```
Expected: `OK: no leftover`

- [ ] **Step 6: コミット**

```bash
git -C "C:/Users/nanat/Desktop/MagnetRush" add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs \
  Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs \
  Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs
git -C "C:/Users/nanat/Desktop/MagnetRush" commit -m "refactor(pole): C# event OnPolarityChanged を OnPoleChanged に改名（MagneticPole enum と命名統一）"
```

---

## Task 2: UnityEvent `OnPolaritySwitch` → `OnPoleSwitch`、`FirePolaritySwitch` → `FirePoleSwitch`

**Files:**
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerEvents.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab`

**目的:** Inspector で繋ぐ UnityEvent の名前を改名。PlayerEvents 内の呼び出しメソッドも合わせて改名。prefab の YAML key は Unity の `ForceReserializeAssets` で自動追従させる。

- [ ] **Step 1: `PlayerEvents.cs` を更新**

`using UnityEngine.Serialization;` を import に追加（`FormerlySerializedAs` を使うため）。
ファイル先頭:

before:
```csharp
using UnityEngine;
using UnityEngine.Events;
```

after:
```csharp
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
```

Comment / field / method を更新:

before（該当箇所）:
```csharp
/// 極性情報は OnPolaritySwitch 発火後に Player.CurrentPole から読む。
```
```csharp
    [Tooltip("磁極切替時に発火。極は Player.CurrentPole から取得")]
    public UnityEvent OnPolaritySwitch;
```
```csharp
    public void FirePolaritySwitch() => OnPolaritySwitch?.Invoke();
```

after:
```csharp
/// 極性情報は OnPoleSwitch 発火後に PoleController.CurrentPole から読む。
```
```csharp
    [Tooltip("磁極切替時に発火。極は PoleController.CurrentPole から取得")]
    [FormerlySerializedAs("OnPolaritySwitch")]
    public UnityEvent OnPoleSwitch;
```
```csharp
    public void FirePoleSwitch() => OnPoleSwitch?.Invoke();
```

`FormerlySerializedAs` は UnityEvent の PersistentCalls 接続が既にワイヤー済みの場合に壊さず引き継ぐための保険。現状 prefab は `m_Calls: []` で空なので実害なしだが、将来ワイヤー済み prefab が出ても安全。

- [ ] **Step 2: `PolarityController.cs` の呼び出し側を更新**

before (L33):
```csharp
        m_events.FirePolaritySwitch();
```

after:
```csharp
        m_events.FirePoleSwitch();
```

- [ ] **Step 3: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Compile
```
Expected: `Compilation succeeded (0 errors, 0 warnings)`

- [ ] **Step 4: prefab YAML の UnityEvent field 名を追従**

prefab には `OnPolaritySwitch:` という YAML key が残っている（L66 付近）。スクリプト側で field 名が変わった後に prefab を再シリアライズすれば Unity が自動で `OnPoleSwitch:` に書き換える。

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.ForceReserializeAssets(new[] { \"Assets/_Project/Prefabs/Player/_Player.prefab\" }); UnityEditor.AssetDatabase.SaveAssets();"
```

検証:
```bash
grep "OnPolaritySwitch\|OnPoleSwitch" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab"
```
Expected: `OnPoleSwitch:` のみ出力、`OnPolaritySwitch` は出ない。

- [ ] **Step 5: 残存確認**

```bash
grep -rn "OnPolaritySwitch\|FirePolaritySwitch" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets" || echo "OK: no leftover"
```
Expected: `OK: no leftover`

- [ ] **Step 6: コミット**

```bash
git -C "C:/Users/nanat/Desktop/MagnetRush" add Magnet_Rush/Assets/_Project/Scripts/Core/Player/PlayerEvents.cs \
  Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs \
  Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
git -C "C:/Users/nanat/Desktop/MagnetRush" commit -m "refactor(pole): UnityEvent OnPolaritySwitch/FirePolaritySwitch を OnPoleSwitch/FirePoleSwitch に改名、prefab YAML key も追従"
```

---

## Task 3: クラス・ファイル・プロパティ名を `PolarityController`/`polarity` → `PoleController`/`pole` に一括改名

**Files:**
- Rename: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs` → `PoleController.cs`
- Rename: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs.meta` → `PoleController.cs.meta`（guid 保持）
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/PoleController.cs`（リネーム後のファイルを編集）
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/Player.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/ShootingController.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/AmmoUI.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/UI/ReticleUI.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/IdlePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/MovePlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/AimPlayerState.cs`
- Modify: `Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab`（`m_EditorClassIdentifier` 更新）

**目的:** クラス名 `PolarityController` → `PoleController` とファイル名を同時リネームし、Player の hub プロパティを `polarity` → `pole`、caller の field `m_polarity` → `m_pole` まで一括で揃える。**このタスクは atomic single commit（間のどの段階でも compile 中間状態になる）**。

**リネーム規則:**

- `PolarityController` → `PoleController`（型名、26 箇所前後）
- `m_polarity` → `m_pole`（private field 名、caller 側）
- `polarity` → `pole`（Player の public プロパティ名、caller 側）

### Step-by-step

- [ ] **Step 1: ファイルを `PoleController.cs` にリネーム（guid 保持）**

```bash
export PATH="$PATH:/c/Users/nanat/AppData/Local/UniCli"
export UNICLI_PROJECT="C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush"
unicli exec Eval --code "UnityEditor.AssetDatabase.MoveAsset(\"Assets/_Project/Scripts/Core/Player/PolarityController.cs\", \"Assets/_Project/Scripts/Core/Player/PoleController.cs\");"
```

Expected: `[null]`（成功）。このコマンドは `.meta` ファイルも同時にリネームし、guid は保持される（prefab の `m_Script` 参照は有効のまま）。

検証:
```bash
ls "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Player/PoleController.cs" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Player/PoleController.cs.meta"
```

- [ ] **Step 2: `PoleController.cs` のクラス名を更新**

before (L10):
```csharp
public class PolarityController : MonoBehaviour
```

after:
```csharp
public class PoleController : MonoBehaviour
```

- [ ] **Step 3: `Player.cs` を更新**

3 箇所改修:

before (L13):
```csharp
[RequireComponent(typeof(PolarityController))]
```
after:
```csharp
[RequireComponent(typeof(PoleController))]
```

before (L63-64):
```csharp
    /// <summary>磁極 Controller。</summary>
    public PolarityController polarity { get; private set; }
```
after:
```csharp
    /// <summary>磁極 Controller。</summary>
    public PoleController pole { get; private set; }
```

before (L75):
```csharp
        polarity = GetComponent<PolarityController>();
```
after:
```csharp
        pole = GetComponent<PoleController>();
```

- [ ] **Step 4: `ShootingController.cs` を更新**

XML doc (L5):
before:
```csharp
/// 依存: PlayerInputHandler, PlayerEvents, Magnetizable, PolarityController, AimController, Player（PlayerSettings 参照用）
```
after:
```csharp
/// 依存: PlayerInputHandler, PlayerEvents, Magnetizable, PoleController, AimController, Player（PlayerSettings 参照用）
```

RequireComponent (L10):
before:
```csharp
[RequireComponent(typeof(PolarityController))]
```
after:
```csharp
[RequireComponent(typeof(PoleController))]
```

Field declaration (L23):
before:
```csharp
    private PolarityController m_polarity;
```
after:
```csharp
    private PoleController m_pole;
```

Awake (L34):
before:
```csharp
        m_polarity = GetComponent<PolarityController>();
```
after:
```csharp
        m_pole = GetComponent<PoleController>();
```

残り 5 箇所の `m_polarity.CurrentPole` を `m_pole.CurrentPole` に置換（L77, L100, L109, L115, L117）。`Edit` ツールの `replace_all: true` で `m_polarity.CurrentPole` → `m_pole.CurrentPole` が安全（`m_polarity` 単体出現はないため）。

- [ ] **Step 5: `AmmoUI.cs` を更新**

XML doc (L7):
before:
```csharp
/// BulletManagerとPolarityControllerのイベントを購読する。
```
after:
```csharp
/// BulletManagerとPoleControllerのイベントを購読する。
```

Field (L22):
before:
```csharp
    private PolarityController m_polarity;
```
after:
```csharp
    private PoleController m_pole;
```

Start (L31):
before:
```csharp
            m_polarity = playerObj.GetComponent<PolarityController>();
```
after:
```csharp
            m_pole = playerObj.GetComponent<PoleController>();
```

残りの `m_polarity` 参照 3 箇所（L32, L34-35, L50-51）をすべて `m_pole` に置換。`Edit` の `replace_all: true` で `m_polarity` → `m_pole` が安全（`m_polarity` プレフィックス一致のみ）。

- [ ] **Step 6: `ReticleUI.cs` を更新**

XML doc (L7):
before:
```csharp
/// PolarityController / AimController を購読する。
```
after:
```csharp
/// PoleController / AimController を購読する。
```

Field (L26):
before:
```csharp
    private PolarityController m_polarity;
```
after:
```csharp
    private PoleController m_pole;
```

Start (L35):
before:
```csharp
            m_polarity = playerObj.GetComponent<PolarityController>();
```
after:
```csharp
            m_pole = playerObj.GetComponent<PoleController>();
```

残りの `m_polarity` 参照 3 箇所（L38, L40-41, L50-51）を `m_pole` に置換（`Edit` の `replace_all: true` で `m_polarity` → `m_pole`）。

- [ ] **Step 7: State クラス 3 個を更新**

IdlePlayerState.cs (L9):
before:
```csharp
        m_entity.polarity.Switch();
```
after:
```csharp
        m_entity.pole.Switch();
```

MovePlayerState.cs (L9): 同様に `m_entity.polarity.Switch()` → `m_entity.pole.Switch()`。

AimPlayerState.cs (L10): 同様。

- [ ] **Step 8: 保存 + コンパイル確認**

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.Refresh(); UnityEditor.AssetDatabase.SaveAssets();"
unicli exec Compile
```
Expected: `Compilation succeeded (0 errors, 0 warnings)`

- [ ] **Step 9: prefab `m_EditorClassIdentifier` を更新**

prefab L217 付近に `m_EditorClassIdentifier: MagnetRush.Player::PolarityController` が残っている。スクリプト再コンパイル後に `ForceReserializeAssets` で `MagnetRush.Player::PoleController` に自動追従する。

```bash
unicli exec Eval --code "UnityEditor.AssetDatabase.ForceReserializeAssets(new[] { \"Assets/_Project/Prefabs/Player/_Player.prefab\" }); UnityEditor.AssetDatabase.SaveAssets();"
```

検証:
```bash
grep "PolarityController\|PoleController" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab"
```
Expected: `PoleController` のみ、`PolarityController` は出ない。

- [ ] **Step 10: 最終残存確認**

```bash
grep -rn "PolarityController\|m_polarity\|\.polarity\b" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets" || echo "OK: no leftover code refs"
```
Expected: `OK: no leftover code refs`

```bash
ls "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Scripts/Core/Player/PolarityController.cs" 2>&1 || echo "OK: old file removed"
```
Expected: `OK: old file removed`

- [ ] **Step 11: コミット**

```bash
git -C "C:/Users/nanat/Desktop/MagnetRush" add -A Magnet_Rush/Assets/_Project/Scripts/Core/Player/ Magnet_Rush/Assets/_Project/Scripts/UI/ Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab
git -C "C:/Users/nanat/Desktop/MagnetRush" commit -m "refactor(pole): PolarityController → PoleController（ファイル・クラス・プロパティ polarity → pole 一括改名、prefab m_EditorClassIdentifier 追従）"
```

---

## Task 4: ドキュメント (`docs/`) の Polarity 参照を更新

**Files:**
- Modify: `docs/player-animation-guide.md`
- Modify: `docs/scripts/Core/Player.md`
- Modify: `docs/scripts/UI.md`
- Modify: `docs/scripts/_Concepts/EventArchitecture.md`
- Modify: `docs/scripts/README.md`
- Modify: `docs/player-control-system.md`
- Modify: `docs/platformer-audit-player.md`
- Modify: `docs/audio-task-brief.md`
- Modify: `docs/audio-spec.md`

**目的:** ドキュメント中の `Polarity`/`polarity` を `Pole`/`pole` に追従させ、チームメンバーが古い用語に混乱しないようにする。Task 3 完了後なので `PolarityController` を探しても実コードには存在しない状態。

**注意:** ドキュメント内容を吟味せず機械的に置換すると文脈が壊れる可能性があるため、ファイルごとに内容を確認してから置換する。

- [ ] **Step 1: 各 doc を Read してコンテキストを確認**

```bash
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/player-animation-guide.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/scripts/Core/Player.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/scripts/UI.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/scripts/_Concepts/EventArchitecture.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/scripts/README.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/player-control-system.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/platformer-audit-player.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/audio-task-brief.md"
grep -n "Polarity\|polarity" "C:/Users/nanat/Desktop/MagnetRush/docs/audio-spec.md"
```

各ファイルの該当行を確認。

- [ ] **Step 2: コード由来の固有名詞のみ Pole に置換**

以下のパターンは **コードに 1:1 対応する固有名詞** なので機械的に置換して良い:

| 旧 | 新 |
|---|---|
| `PolarityController` | `PoleController` |
| `OnPolarityChanged` | `OnPoleChanged` |
| `OnPolaritySwitch` | `OnPoleSwitch` |
| `FirePolaritySwitch` | `FirePoleSwitch` |
| `m_polarity` | `m_pole` |
| `player.polarity` | `player.pole` |
| `.polarity.Switch()` | `.pole.Switch()` |

**以下は文脈によって残す**（日本語自然文の一般用語。機械的に置換しない）:
- 「磁極」「極性」「ポール」といった日本語（そのまま）
- 「polarity system」のような一般的な英語フレーズ（Pole に置換するか判断）

Edit ツールで各ファイルのコード由来パターンのみピンポイントに置換。

- [ ] **Step 3: 変更したドキュメントを確認**

```bash
grep -rn "PolarityController\|OnPolarityChanged\|OnPolaritySwitch\|FirePolaritySwitch\|m_polarity" "C:/Users/nanat/Desktop/MagnetRush/docs" || echo "OK: code-refs fully replaced"
```
Expected: `OK: code-refs fully replaced`

- [ ] **Step 4: コミット**

```bash
git -C "C:/Users/nanat/Desktop/MagnetRush" add docs/
git -C "C:/Users/nanat/Desktop/MagnetRush" commit -m "docs(pole): ドキュメント中のコード固有名詞 Polarity* を Pole* に追従"
```

---

## Task 5: 最終検証

**目的:** コンパイル + Grep + PlayMode で Pole 統一が完遂していることを確認する。

- [ ] **Step 1: 全体コンパイル**

```bash
unicli exec Compile
```
Expected: `Compilation succeeded (0 errors, 0 warnings)`

- [ ] **Step 2: コードベース全体の leftover 検索**

```bash
# コード由来の固有識別子が残っていないか
grep -rn "PolarityController\|OnPolarityChanged\|OnPolaritySwitch\|FirePolaritySwitch\|m_polarity\|\.polarity\b" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets" "C:/Users/nanat/Desktop/MagnetRush/docs" || echo "OK: all clean"
```
Expected: `OK: all clean`

`.planning/` 配下や過去のコミットメッセージに「Polarity」が残るのは履歴なので OK（無視）。

- [ ] **Step 3: prefab 整合性チェック**

```bash
grep "PoleController\|OnPoleSwitch" "C:/Users/nanat/Desktop/MagnetRush/Magnet_Rush/Assets/_Project/Prefabs/Player/_Player.prefab"
```
Expected: `PoleController` と `OnPoleSwitch` が 1 箇所ずつ出現。

- [ ] **Step 4: PlayMode スモーク（ユーザー実機）**

Unity で PlayMode 開始し以下を確認:

| # | 操作 | 期待 |
|---|---|---|
| 1 | PlayMode 開始 | Console エラーなし |
| 2 | Y ボタン | 磁極切替、AmmoUI/ReticleUI のスプライトが S↔N で切替 |
| 3 | RT | 発射（現在の磁極で bullet 生成） |
| 4 | A/F | セルフファイア（現在の磁極で magnetic field 生成） |

- [ ] **Step 5: 成功なら push / PR 作成に進む**

```bash
git -C "C:/Users/nanat/Desktop/MagnetRush" log --oneline main..HEAD
```
Expected: Task 1〜4 の 4 コミットが並ぶ。

```bash
git -C "C:/Users/nanat/Desktop/MagnetRush" push -u origin feature/pole-rename
gh pr create --base main --head feature/pole-rename --title "refactor(pole): Polarity 系識別子を Pole に一括改名（命名統一）"
```

---

## 変更履歴

- 2026-04-21: 初版作成（Polarity → Pole 統一リネーム計画、4 実作業 Task + 検証 Task）
