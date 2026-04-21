# プレイヤーアニメーション実装タスク

担当者: メンバー / 最終更新: 2026-04-20

---

## このタスクのゴール

既存の `Player` プレハブ（`Assets/_Project/Prefabs/Player/_Player.prefab`）に、キャラクターモデルとアニメーションを組み込む。

**既存の C# スクリプト（`Player.cs` / `PlayerEvents.cs` / 各 State 等）は触らない**。アニメーション接続は `PlayerAnimator` コンポーネントを Inspector で設定するだけで完結する。**新規スクリプト作成は不要**。

「既存コードを変えないと繋がらない」と感じたら作業を止めて相談してほしい。全部 Inspector から繋げる。

---

## 作業は全部 Unity Editor の中で完結する

- ファイルの作成・削除・移動は全て Unity Editor の `Project` ウィンドウ上で行う（右クリック → Create / Delete / Rename、ドラッグ＆ドロップ）
- エクスプローラー（Windows のファイラ）から `Assets/` 以下を直接触らない。`.meta` ファイルが壊れてチーム全員が事故る
- 作業区切り・Play 前・Git 操作前に `Ctrl+S`（シーン保存）+ `Project` 右クリック → **Save Project** を忘れない

---

## 1. 作業の全体像（チェックリスト）

上から順に消化する。各項目は §番号のセクションに詳細。

- [ ] **Step 1** モデル FBX を `Assets/_Project/Asset/Models/Player/` にインポート、Rig: **Humanoid** に設定 （§3）
- [ ] **Step 2** `Assets/_Project/Asset/Animations/Player/` に `PlayerAnimator.controller` を作成 （§4.1）
- [ ] **Step 3** 同フォルダに `UpperBody.mask` を作成 （§4.3）
- [ ] **Step 4** Animation クリップを `Animations/Player/Clips/` に配置（Mixamo 等から持ってきた分） （§4.1）
- [ ] **Step 5** `_Player.prefab` の既存 `Model` 子に、本番 FBX と Animator を組み込む （§5.2）
- [ ] **Step 6** Animator パラメータと遷移を §4.1 / §4.2 の仕様通りに設定する
- [ ] **Step 7** `Model` に `PlayerAnimator` コンポーネントを追加して Inspector 設定（§5.3）
- [ ] **Step 8** Animation Event は演出系（足音・エフェクト）のみ埋め込む。ゲームロジック禁止 （§6）
- [ ] **Step 9** §9 の動作確認チェックリストを全通過

---

## 2. プレイヤーの状態と既存イベント

Animator を組むときに使うフックはこの3種類だけ。これ以外は触らない。

### 2.1 プレイヤーステート（4種）

`PlayerStateManager` が管理。登録順が `State` パラメータの Int 値に対応する。

| ステートクラス | State index | 意味 | アニメーション例 |
|---|---|---|---|
| `IdlePlayerState` | 0 | 立ち止まっている | Idle ループ |
| `MovePlayerState` | 1 | 入力ありで移動中 | Run / Walk（Blend Tree で速度補間） |
| `DiePlayerState` | 2 | HP0で死亡中 | Die（ワンショット）→ リスポーンで戻る |
| `AimPlayerState` | 3 | エイム中（カメラ方向固定・ストレイフ） | Aim 8方向ストレイフ（2D Simple Directional）|

ソース参考: `Assets/_Project/Scripts/Core/Player/States/*.cs`

### 2.2 PlayerEvents（C# Action）

`PlayerEvents.cs` で定義されている `event Action`。`PlayerAnimator` が Animator Trigger に橋渡しする。

| イベント | 発火タイミング |
|---|---|
| `OnShoot` | 通常射撃（RT）時 |
| `OnSelfShoot` | セルフファイア（A / F）時 |
| `OnPolaritySwitch(MagneticPole)` | 磁極切替（Y）時。N / S が渡される |
| `OnReload` | リロード実行時 |

### 2.3 EntityEvents（UnityEvent）

`_Player` ルートの Inspector で直接繋げる。接地/離地は `PlayerAnimator` が `Entity.IsGrounded` を毎フレームポーリングするため、**UnityEvent 接続は不要**。

| イベント | 発火タイミング |
|---|---|
| `onGroundEnter` | 着地した瞬間 |
| `onGroundExit` | 地面から離れた瞬間 |

---

## 3. モデル Import 設定（Step 1）

1. FBX を `Assets/_Project/Asset/Models/Player/` にドラッグ＆ドロップ
2. 選択して Inspector → `Rig` タブ
3. **Animation Type: Humanoid**, **Avatar Definition: Create From This Model** → `Apply`
4. Avatar 横のチェックマークが緑になることを確認（失敗するなら骨の命名が合っていない → 相談）
5. `Model` タブで `Scale Factor` を調整してプレハブのカプセル（高さ 2m）に収まるサイズにする

Generic しかない（犬型など）場合は相談してほしい。本ガイドは Humanoid 前提で書いている。

---

## 4. Animator Controller の作り方（Step 2-4）

### 4.1 Animator パラメータ（確定）

`PlayerAnimator.controller` を開いて `Parameters` タブに以下を登録。**名前は `PlayerAnimator.cs` 側と `StringToHash` でハッシュ一致させているので絶対に変えない**。

| パラメータ | 型 | 用途 |
|---|---|---|
| `State` | **Int** | 現在ステート index（0=Idle, 1=Move, 2=Die, 3=Aim。PlayerStateManager の登録順） |
| `LastState` | **Int** | 前回ステート index |
| `OnStateChanged` | **Trigger** | ステート変化時 1 回発火。Any State → 各ステートの遷移に使う |
| `MoveSpeed` | **Float** | 横速度の m/s 実値（0〜6）。Blend Tree 閾値用 |
| `MoveInputX` | **Float** | -1〜1。AimStrafe 2D Blend Tree 横軸 |
| `MoveInputZ` | **Float** | -1〜1。AimStrafe 2D Blend Tree 縦軸 |
| `IsAiming` | **Bool** | エイム中 |
| `IsGrounded` | **Bool** | 接地中 |
| `Shoot` | **Trigger** | 通常射撃 |
| `SelfShoot` | **Trigger** | セルフファイア |
| `Reload` | **Trigger** | リロード |

> 登録順に注意: `PlayerStateManager.cs` の `RegisterState()` 呼び出し順に対応。
> Idle=0, Move=1, Die=2, Aim=3 で固定。登録順を変えると Animator 側の遷移条件と不整合になる。

`MoveSpeed` は **m/s の実値** を使う。`PlayerSettings.topSpeed = 6 m/s`、エイム中は `aimMoveSpeedMultiplier = 0.5` で 3 m/s 上限。Blend Tree の閾値は Unity 公式の `Compute Thresholds` ボタンで各クリップのルートモーション速度から自動設定できる。

### 4.2 Layer 構成

#### Base Layer（Weight=1, Mask=None, Blending=Override）

```
├── Locomotion (1D Blend Tree: MoveSpeed)
│   ├── Idle (MoveSpeed = 0)
│   ├── Walk (MoveSpeed = 1.8)    ← 歩きクリップのルートモーション速度に合わせる
│   └── Run  (MoveSpeed = 6.0)    ← topSpeed
│
├── AimStrafe (2D Simple Directional: MoveInputX / MoveInputZ)
│   ├── AimIdle    ( 0,  0)
│   ├── AimFwd     ( 0,  1)
│   ├── AimBack    ( 0, -1)
│   ├── AimLeft    (-1,  0)
│   ├── AimRight   ( 1,  0)
│   ├── AimFwdL    (-0.7, 0.7)
│   ├── AimFwdR    ( 0.7, 0.7)
│   ├── AimBackL   (-0.7,-0.7)
│   └── AimBackR   ( 0.7,-0.7)
│
└── Die (ワンショット)
```

**遷移方式:** Any State から各ステートへ `OnStateChanged` Trigger + `State == N` 複合条件で遷移

| 遷移先 | 条件 | 備考 |
|---|---|---|
| Locomotion (Idle/Walk/Run Blend Tree) | `OnStateChanged` かつ `State == 0` または `State == 1` | Transition Duration: 0.15s |
| AimStrafe (2D Blend Tree) | `OnStateChanged` かつ `State == 3` | Transition Duration: 0.15s |
| Die (ワンショット) | `OnStateChanged` かつ `State == 2` | Has Exit Time OFF |

**共通設定:**
- Has Exit Time: すべて OFF（即座に遷移）
- Can Transition To Self: OFF（自ステート再突入を防ぐ）
- リスポーン: Die ステートから Idle に戻る時、PlayerStateManager が IdlePlayerState に遷移 → `OnStateChanged` が発火 → `State==0` で Locomotion に戻る

#### Upper Body Layer（Weight=**1**, Mask=**UpperBody.mask**, Blending=**Override**）

上半身だけに射撃/リロードを流すことで、走りながら撃つ/リロードが成立する。

```
├── Empty (空クリップ / 何もしない)
├── Shoot      (Trigger: Shoot)
├── SelfShoot  (Trigger: SelfShoot)
└── Reload     (Trigger: Reload)
```

全てから Empty への戻り遷移を `Has Exit Time = true, Exit Time = 0.9` で設定しておく。

### 4.3 Avatar Mask（`UpperBody.mask`）の作り方（Step 3）

1. `Project` → `Assets/_Project/Asset/Animations/Player/` で右クリック → `Create > Avatar Mask`
2. 名前: `UpperBody.mask`
3. 選択して Inspector → `Humanoid` タブで **Head / LeftArm / RightArm / Upper Body** を緑（ON）、それ以外を赤（OFF）
4. Animator の Upper Body Layer の歯車 → `Mask` 欄にこの `.mask` をドロップ

参考: [Unity Manual: Animation Layers](https://docs.unity3d.com/Manual/AnimationLayers.html)

---

## 5. プレハブへの組み込み（Step 5-8）

### 5.1 `_Player.prefab` の現状階層（2026-04-20 時点、触っていいのは `Model` の中身だけ）

```
_Player  [Tag=Player, Layer=8, position (0, 1.26, -0.048)]
  Components:
    - Transform
    - Rigidbody (isKinematic=true, useGravity=false, Constraints=FreezeRotation 全軸)
    - EntityController (slopeLimit=45, skinWidth=0.01, radius=0.5, height=2)
    - Player, PlayerStateManager, PlayerEvents, PlayerInputHandler
    - ShootingController, AimController, PolarityController
    - Health (maxHealth=50, damageCooldown=1), Magnetizable
  ※ 親に CapsuleCollider / Animator は無い（追加しない）
  │
  ├─ Model  [Layer=8]  ← ここに本番モデルと Animator を入れる
  │     現在は Unity 組み込み Capsule Mesh のプレースホルダー
  │
  ├─ Hitbox  [Layer=0]  ← 被弾判定コンテナ。触らない
  │     ├─ Hurtbox [Layer=8, Tag=Player]  CapsuleCollider isTrigger=true (r=0.5, h=2)
  │     └─ Pushbox  [Layer=15]            CapsuleCollider isTrigger=false (r=0.5, h=2)
  │
  └─ FirePoint  [Layer=0, localPos (0, 0.812, -0.5)]
        弾の発射起点。銃口に合わせたければ武器ボーンの子に入れても OK
```

### 5.2 モデル組み込み手順（Step 5）

プレハブ編集は必ず `Prefab Mode` に入って行う（`_Player.prefab` をダブルクリック）。

1. **既存 `Model` 子のメッシュ成分だけ削除**
   - `Model` の `MeshFilter` と `MeshRenderer` を Inspector の歯車 → `Remove Component` で削除
   - GameObject `Model` 自体は **残す**（Animator / PlayerAnimator はここに付ける）
2. **FBX を `Model` の子として配置**
   - `Project` の FBX を `Model` にドラッグしてシーンに入れる
   - 入ったキャラの GameObject（FBX ルート）が `Model` の子になっていることを Hierarchy で確認
3. **`Model` に Animator コンポーネントを追加**
   - `Model` 選択 → `Add Component > Animator`
   - `Controller` = `PlayerAnimator.controller`
   - `Avatar` = FBX から生成された Humanoid Avatar
   - **`Apply Root Motion` = OFF**（`EntityController` が位置を制御するので二重移動になる）
4. **サイズ確認**
   - `EntityController` のカプセル（r=0.5, h=2）と、`Hurtbox`/`Pushbox` の CapsuleCollider が同サイズで揃っている
   - 本番モデルのスケールをこれに合わせる。**Collider の数値側は絶対に変更しない**
5. **FirePoint の親子付け（任意）**
   - 現状は `_Player` 直下 `(0, 0.812, -0.5)`。銃口から撃ちたければ武器ボーン/右手ボーンの子に移動しても OK
   - `ShootingController` の参照は Transform なので、親子を変えても自動追従する

**禁止事項**:
- `Model` GameObject 自体を削除・リネーム
- `Hitbox` / `Hurtbox` / `Pushbox` / `FirePoint` の階層・Layer・Tag・Collider サイズ変更
- 本番モデルを `_Player` ルート直下に置く（必ず `Model` の子）
- `Apply Root Motion` を ON にする

### 5.3 `PlayerAnimator` コンポーネントを `Model` に追加する（Step 7）

**C# スクリプト作成は不要**。既に `PlayerAnimator.cs` がプロジェクトに存在する（`Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs`）。

1. `_Player.prefab` を Prefab Mode で開く
2. `Model` 子を選択
3. Inspector → `Add Component` → `Player Animator` を検索して追加
4. `Animator` 欄に同じ `Model` の Animator コンポーネントをドラッグ（未設定なら Awake で自動取得されるが明示推奨）
5. `Player Events` / `Player Input Handler` / `Player State Manager` / `Entity` / `Aim Controller` 欄は **空のままで OK**（親から自動取得される）
6. `Animator Parameter Names` セクションは **絶対に触らない**。§4.1 の名前と完全一致している前提

これで Animator 更新はすべて `PlayerAnimator` が担当する:

- ステート変化 → `State` / `LastState` (Int) + `OnStateChanged` (Trigger)
- 毎フレーム → `MoveSpeed` / `MoveInputX` / `MoveInputZ` / `IsAiming` / `IsGrounded`
- 射撃系イベント → `Shoot` / `SelfShoot` / `Reload` (Trigger)

別途 Bridge スクリプトを書いたり、`EntityStateManagerListener` を配置する必要はない。

---

## 6. Animation Event の扱い（Step 8）

クリップに埋め込む Animation Event は **演出系だけ** に限定する:

- 足音再生
- 残像・粒子エフェクト発火
- マズルフラッシュの演出（弾生成ではない）

**禁止**:

- 弾を生成する
- ダメージ判定を有効化する
- ステート遷移を強制する

理由: 既存の射撃 / ダメージロジックは `ShootingController` 等が時間駆動で動いているため、アニメから呼ぶと判定がアニメ速度に引きずられてゲームバランスが崩れる。

演出系の受け口は `Model` 配下に別スクリプト（例: `PlayerFootstep`）を置いて AnimationEvent で呼ぶ想定。このタスクの対象外なので、必要になったら相談して仕様を切る。

---

## 7. 命名規則

`.claude/rules/naming-conventions.md` 準拠:

- ファイル: `PascalCase.cs`, `PlayerAnimator.controller`, `PlayerIdle.anim`
- フォルダ: `PascalCase/`
- Animator パラメータ: **§4.1 の名前を厳守**。`PlayerAnimator.cs` 側で `Animator.StringToHash` ハッシュ比較しているため、大文字小文字含め一致しないと動かない

---

## 8. 保存ルール

作業区切り・Play 前・Git 操作前に必ず:

1. `Ctrl+S`（開いているシーン保存）
2. `File > Save Project`（プレハブ / SO / Animator 等の保存）

保存忘れた状態で Play に入ると、Inspector 上の変更が破棄されて「動かない、でも設定はしたはず」という地獄になる。

---

## 9. 動作確認チェックリスト（完了基準）

- [ ] 停止中に Idle が再生される
- [ ] 走り出すと `MoveSpeed` が 0 → 6 付近まで上がり、Blend Tree が Idle → Walk → Run に補間される
- [ ] LT を押すと `IsAiming=true` になり AimStrafe に遷移、離すと Locomotion に戻る
- [ ] エイム中に前後左右入力で 2D Blend Tree が 8 方向に補間される
- [ ] RT（射撃）で上半身だけ Shoot が再生、下半身の走りは止まらない
- [ ] リロードボタンで Reload が再生、下半身は継続する
- [ ] ジャンプ / 落下中に `IsGrounded == false` になる（Animator ウィンドウの Parameters タブで確認）
- [ ] HP0 → `State == 2`（Die）に遷移 → ワンショット再生 → リスポーン位置にテレポート → `State == 0`（Idle）に戻る
- [ ] 死亡直後に撃っていた Shoot Trigger が Respawn 後に誤発火しない（PlayerAnimator 内の ResetTriggers が効いている）
- [ ] `Apply Root Motion` が OFF になっている（位置がアニメで動かない）
- [ ] Console に `PlayerAnimator` 関連の警告が出ていない
- [ ] Inspector に `Missing` 表示のコンポーネント参照が無い

---

## 10. やってはいけないことまとめ

1. 既存スクリプト（`Player.cs` / `PlayerEvents.cs` / 各 State 等）の編集 → **相談必須**
2. `Apply Root Motion` を ON
3. Animation Event からゲームロジック呼び出し（弾生成・ダメージ判定）
4. `Model` GameObject を削除・リネーム・作り直す
5. `Hitbox` / `Hurtbox` / `Pushbox` / `FirePoint` の階層・Layer・Tag・Collider サイズ変更
6. `Assets/` 以下をエクスプローラーから直接操作（`.meta` ファイルが壊れる）
7. Animator パラメータ名を勝手に変える（`PlayerAnimator` の StringToHash と不一致で動かなくなる）
8. `PlayerAnimator` コンポーネントを **複数付けない**（1 Model に 1 個まで）
9. `Animator Parameter Names` セクションの文字列を **変更しない**（PlayerAnimator の StringToHash キャッシュと不一致になり動かなくなる）

---

## 11. 参照

### プロジェクト内

- `docs/player-control-system.md` — プレイヤー全体設計
- `Assets/_Project/Scripts/Core/Player/` — 既存スクリプト
- `Assets/_Project/Scripts/Core/Player/PlayerAnimator.cs` — アニメーション制御スクリプト
- `.claude/rules/naming-conventions.md`
- `.claude/rules/unity-design-principles.md`

### Unity 公式

- [Animation Layers（Avatar Mask 含む）](https://docs.unity3d.com/Manual/AnimationLayers.html)
- [Blend Trees](https://docs.unity3d.com/Manual/class-BlendTree.html)
- [1D Blending](https://docs.unity3d.com/Manual/BlendTree-1DBlending.html)
- [Animator.ResetTrigger](https://docs.unity3d.com/ScriptReference/Animator.ResetTrigger.html)
- [Avatar Mask window](https://docs.unity3d.com/2022.3/Documentation/Manual/class-AvatarMask.html)

---

## 12. 困ったとき

- 既存コードを編集しないと繋がらないように見える → **止めて相談**（ほぼ設計的な抜けか、本ガイドの説明不足）
- Humanoid Avatar 作成で骨が赤くなる → 相談（Mixamo 以外のモデルは命名が違うことがある）
- Play してもアニメが全く動かない → Animator の `Controller` / `Avatar` 未設定、`Apply Root Motion` ON、パラメータ名 typo、のどれかが定番
- Shoot はするが Reload しない → Upper Body Layer の Weight が 0 になっていることが多い（1 にする）

---

## 13. 拡張（プログラマー向け）

このプロジェクトは **Platformer Project 形式**。`Player.cs` が「プレイヤーの全能力の辞書」、State クラスが「その能力をどう組み合わせるか」を担当する。

### 13.1 新しい State を追加する

例: ジャンプ専用ステート `JumpPlayerState`。

**手順:**

1. `Magnet_Rush/Assets/_Project/Scripts/Core/Player/States/JumpPlayerState.cs` を新規作成:

    ```csharp
    /// <summary>
    /// ジャンプ中のステート。空中挙動と着地判定を扱う。
    /// </summary>
    public class JumpPlayerState : EntityState<Player>
    {
        public override void Enter(Player entity, EntityStateManager<Player> manager)
        {
            base.Enter(entity, manager);
            // ジャンプ開始時の処理（効果音、ジャンプ初速付与等）
        }

        public override void UpdateState(float dt)
        {
            // 空中でも利用可能なアクションを列挙
            m_entity.SwitchPole();
            m_entity.HandleAimInput();
            m_entity.Fire();
            m_entity.SelfFire();
            m_entity.Reload();

            // 着地したら Idle に戻る
            if (m_entity.IsGrounded)
                m_manager.Change<IdlePlayerState>();
        }

        public override void Exit() { }
    }
    ```

2. `_Player.prefab` を開く（Unity Editor）
3. `PlayerStateManager` コンポーネントの `States` 配列に要素追加
4. ドロップダウンから `Jump Player State` を選ぶ（`ClassTypeName` Drawer が自動で一覧を出す）
5. 登録順が `State` Int index になるので、Animator Controller 側で `State == N`（N = 新要素の位置）の遷移を追加

**注意**:
- **既存の登録順は変更しない**（0=Idle, 1=Move, 2=Die, 3=Aim）。新要素は末尾に追加する（index 4 以降）
- Animator 側に対応する遷移を追加しないとアニメは切り替わらない（State Int は更新されるが遷移条件がないので）

### 13.2 Player に新しい能力メソッドを追加する

例: ダッシュ能力 `Dash()`。

**手順:**

1. `Player.cs` の `// --- 射撃 ---` セクションの下あたりに、新セクションを追加:

    ```csharp
    // --- ダッシュ ---

    /// <summary>ダッシュ入力があれば実行。毎フレーム呼ぶ。</summary>
    public void Dash()
    {
        if (!input.ConsumeDash()) return;

        // ダッシュ力を外部速度として適用
        externalVelocity += transform.forward * m_settings.dashForce;

        events?.FireDash();   // 既存パターン（UnityEvent）で SE/VFX が接続可能
    }
    ```

2. 必要なら `PlayerInputHandler.cs` に `ConsumeDash()` を追加（InputAction も定義）
3. `PlayerEvents.cs` に `public UnityEvent OnDash;` と `public void FireDash() => OnDash?.Invoke();` を追加（Inspector 接続用）
4. `PlayerSettings.cs` （ScriptableObject）に `public float dashForce = 10f;` を追加
5. 使う State の `UpdateState` に `m_entity.Dash();` を追加
6. アニメーション連動させたいなら `PlayerAnimator.cs` に `Dash` Trigger パラメータを追加

### 13.3 やってはいけない

- **`EntityStateManager` / `Entity` base の改修は相談必須**（全 Entity / 敵 AI にも波及する）
- `Player.cs` の既存メソッドのシグネチャを変えない（State が壊れる）
- `PlayerStateManager.states` の登録順を変えない（Animator の State Int 条件がずれる）
- `Player.cs` の `[Header]` セクションに他のコンポーネント機能を混ぜない（SRP を保つ）
- State クラスで直接 `Animator.SetTrigger` を呼ばない（必ず `PlayerAnimator` 経由）

### 13.4 参考: 既存パターン

- `Player.SwitchPole()` / `Fire()` / `StartAim()` が能力メソッドの実例
- `IdlePlayerState.UpdateState()` が State の実例（能力メソッドを列挙するだけ）
- `DiePlayerState.UpdateState()` は **何も呼ばない例**（死亡中は全操作不能を表現）

### 13.5 責務の分離

| 層 | ファイル | 責任 |
|---|---|---|
| **基盤** | `EntityStateManager.cs` / `Entity.cs` | ステート管理の基礎 / 物理・移動の基礎 |
| **能力辞書** | `Player.cs` | プレイヤーができる全アクション |
| **振る舞い** | `States/*PlayerState.cs` | ステートごとに能力の組み合わせを定義 |
| **入力** | `PlayerInputHandler.cs` | 入力ポーリング API |
| **イベント** | `PlayerEvents.cs` | UnityEvent 発火（SE/VFX 接続点） |
| **アニメ** | `PlayerAnimator.cs` | Animator パラメータ駆動（唯一の Animator クライアント） |

拡張する時はこの対応表を意識する。
