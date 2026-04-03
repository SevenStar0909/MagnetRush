# 物理・磁力システム 技術ドキュメント

> 最終更新: 2026-03-31
> 対象ソースコード: `_Project/Scripts/Entity/`, `_Project/Scripts/Magnet/`, `_Project/Scripts/Bullet/`, `_Project/Scripts/Data/`, `_Project/Scripts/Common/`

---

## 目次

1. [物理移動システム（なぜ壁/床をすり抜けていたか）](#1-物理移動システムなぜ壁床をすり抜けていたか)
2. [重力システム](#2-重力システム)
3. [磁力システム全体像](#3-磁力システム全体像)
4. [弾の仕様（MagnetBullet）](#4-弾の仕様magnetbullet)
5. [磁力場（MagnetField）](#5-磁力場magnetfield)
6. [磁力の力計算（MagnetManager.ProcessPair）](#6-磁力の力計算magnetmanagerprocesspair)
7. [力の適用経路](#7-力の適用経路)
8. [可視化（MagnetFieldVisualizer）](#8-可視化magnetfieldvisualizer)
9. [各設定値の一覧と意味](#9-各設定値の一覧と意味)

---

## 1. 物理移動システム（なぜ壁/床をすり抜けていたか）

### 1.1 以前の問題: transform.position += による直接移動

Unity の物理エンジン（PhysX）は `Rigidbody` の `MovePosition` や力の適用を通じて衝突を検出する。しかし、`transform.position +=` で直接座標を書き換えると、PhysX の衝突解決パイプラインを**完全にバイパス**する。

```
[以前のコード]
transform.position += velocity * dt;   // PhysXを通らない → 壁をすり抜ける
```

この方式では:
- ContinuousCollisionDetection を設定していても機能しない（Rigidbody経由の移動でないため）
- 高速移動時にフレーム間でコライダーが壁を通過する（トンネリング）
- OnCollisionEnter/Stay も発火しない場合がある

### 1.2 現在の解決策: EntityController (Collide-and-Slide)

`EntityController` は PLAYER TWO Platformer Project の `EntityController` を参考にした、自前の Collide-and-Slide 衝突制御コンポーネントである。

**設計思想**: Unity の CharacterController を使わず、CapsuleCast ベースで自前実装する。理由は:
- CharacterController は磁力による外部速度・斜面・段差の細かい挙動制御が困難
- Rigidbody ベースだと磁力の瞬間的な力の適用で振動が起きやすい
- 自前実装なら「押し」処理やペネトレーション解決を完全に制御できる

#### ファイル: `EntityController.cs`
- `[DefaultExecutionOrder(-100)]` — 他のスクリプトより先に初期化される

### 1.3 CapsuleCast で壁を検出する仕組み

移動方向に対して `Physics.CapsuleCast` を実行し、移動パス上の障害物を検出する。

```
SweepTest メソッド:

  CapsuleCast    ──→ 壁検出（カプセル形状の掃引テスト）
       ↓ ヒットなし
  Raycast        ──→ フォールバック（薄い壁対策）
```

```csharp
private bool SweepTest(Vector3 position, Vector3 top, Vector3 bottom,
    Vector3 direction, float distance, out RaycastHit hit)
{
    bool capsuleHit = Physics.CapsuleCast(top, bottom, radius,
        direction, out hit, distance, collisionLayer, QueryTriggerInteraction.Ignore);
    if (capsuleHit) return true;

    return Physics.Raycast(position, direction, out hit,
        distance, collisionLayer, QueryTriggerInteraction.Ignore);
}
```

CapsuleCast の `distance` パラメータは `moveDistance + radius - skinWidth` に設定される。カプセルの原点を `moveDirection * radius` 分だけ後退させてから掃引するため、実質的に「カプセル表面から移動距離+skinWidth分先まで」を検査する。

### 1.4 MoveAndSlide の反復アルゴリズム（最大3回）

```
MoveAndSlide アルゴリズム:

入力: position（現在位置）, motion（移動ベクトル）

反復 1: motion方向にCapsuleCast
  ├─ ヒットなし → position += motion → 終了
  └─ ヒットあり
       ├─ 動的Rigidbody → 押し処理 → continue（再試行）
       └─ 静的壁
            ├─ 安全距離だけ移動: position += moveDir * safeDistance
            ├─ 残りベクトルを壁面に投影: motion = ProjectOnPlane(leftover, normal)
            └─ 急斜面の場合: 垂直成分を除去（上方向スライド防止）

反復 2: 投影された motion で再度CapsuleCast
  └─ （同様の処理）

反復 3: 最終反復
  └─ （同様の処理、ここで打ち切り）
```

```
k_MaxCollisionSteps = 3

なぜ3回か:
- 1回: 直進で壁にぶつかる
- 2回: スライドした先で別の壁にぶつかる（角）
- 3回: さらにスライド → ほぼ全ケースカバー
- 4回以上: 実用上ほぼ不要。計算コスト削減のため打ち切り
```

### 1.5 skinWidth の役割

```csharp
public float skinWidth = 0.01f;
```

skinWidth は壁との間に保つ最小距離（スキン幅）。

```
壁面から skinWidth 分だけ離れた位置で停止する:

     壁
     |
     |  ← skinWidth (0.01)
     | |
     | |  ← カプセル表面
     | |
     |
```

**なぜ必要か**:
- 浮動小数点誤差でカプセルが壁に0距離まで近づくと、次フレームで「すでにめり込んでいる」状態になりジッター（振動）が発生する
- skinWidth を挟むことで、常にわずかな隙間を保ち安定する
- CapsuleCast のヒット距離から `skinWidth + radius` を引いた値が安全移動距離になる:
  ```csharp
  float safeDistance = hit.distance - skinWidth - radius;
  ```
- コライダーサイズも `radius - skinWidth`, `height - skinWidth` で設定し、物理形状よりわずかに小さくする

### 1.6 HandlePenetration（めり込み解決）の仕組み

MoveAndSlide の後、まだめり込みが残っている場合に `HandlePenetration` で押し出す。

```csharp
private Vector3 HandlePenetration(Vector3 position)
{
    // OverlapCapsuleNonAlloc で重なっているコライダーを全検出
    int count = Physics.OverlapCapsuleNonAlloc(point1, point2, radius,
        m_overlaps, collisionLayer, QueryTriggerInteraction.Ignore);

    for (int i = 0; i < count; i++)
    {
        // 無視リスト・自分自身・動的Rigidbody はスキップ
        if (m_ignoredColliders.Contains(m_overlaps[i])) continue;
        if (m_overlaps[i].transform == transform) continue;
        var overlapRb = m_overlaps[i].attachedRigidbody;
        if (overlapRb != null && !overlapRb.isKinematic) continue;

        // ComputePenetration で最小分離ベクトルを計算
        if (Physics.ComputePenetration(m_collider, position, transform.rotation,
            m_overlaps[i], m_overlaps[i].transform.position, m_overlaps[i].transform.rotation,
            out var direction, out var dist))
        {
            position += direction * dist;
        }
    }
    return position;
}
```

**処理フロー**:
1. `OverlapCapsuleNonAlloc` で 128 個までのオーバーラップを検出
2. 無視リスト（押し中のオブジェクト等）をスキップ
3. **動的 Rigidbody は除外** — めり込み解決は静的壁に対してのみ行う（動的オブジェクトは押し処理で対応するため）
4. `Physics.ComputePenetration` で最小分離ベクトルを取得し、その分だけ位置を補正

**なぜ必要か**: CapsuleCast だけでは「すでにめり込んでいる」状態を解決できない。磁力で壁に押し付けられた場合や、移動するプラットフォームにめり込んだ場合のセーフティネット。

### 1.7 Trigger CapsuleCollider と物理 CapsuleCollider の二重構造

```
EntityController の Awake() で行われること:

1. 既存の CapsuleCollider を検出 → サイズを記録 → enabled = false
2. 新しい CapsuleCollider を AddComponent → isTrigger = true
3. Rigidbody を isKinematic = true に設定
```

```
二重構造の理由:

 [元の CapsuleCollider]          [新しい CapsuleCollider]
 ├─ enabled = false              ├─ isTrigger = true
 ├─ 物理衝突に使わない           ├─ OnTrigger イベント発火用
 └─ サイズ情報の元ソース          └─ radius - skinWidth で少し小さい
                                  └─ MoveAndSlide の CapsuleCast と対応

 [Rigidbody]
 ├─ isKinematic = true
 ├─ PhysX の力の影響を受けない
 └─ transform.position の直接制御を EntityController が行う
```

**なぜ isTrigger = true か**:
- EntityController は自前で CapsuleCast を使って衝突を処理するため、PhysX の通常の衝突解決（OnCollision）は不要
- しかし、OnTriggerEnter/Stay/Exit は弾の着弾判定や磁力場のEntity検知に必要
- Trigger コライダーなら他のコライダーと物理的に干渉せず、イベントだけ受け取れる

**なぜ Rigidbody が isKinematic = true か**:
- Entity.Awake() でも `rb.isKinematic = true` を設定している
- kinematic にすることで PhysX の力（重力、衝突応答）を無効化し、位置は EntityController.Move() が完全に制御する
- 磁力の力は `externalVelocity` に加算され、MoveAndSlide 経由で壁衝突を考慮した移動になる

### 1.8 段差対応（stepOffset）

```csharp
public float stepOffset = 0.3f;
```

MoveAndSlide の水平パス（`verticalPass = false`）で、カプセルの下端を `stepOffset` 分だけ持ち上げる:

```csharp
if (!verticalPass && height > radius * 2f)
    point2 += transform.up * stepOffset;
```

```
段差対応の仕組み:

通常時:                          stepOffset 適用時:
┌────┐                          ┌────┐
│    │  ← カプセル上端         │    │
│    │                          │    │
│    │                          │    │
└────┘  ← カプセル下端         └────┘  ← 下端が stepOffset 分上がる
  ___                             ___
 |   | ← 段差 (0.3m)            |   | ← CapsuleCast が段差を通過
 |   |                           |   |
━━━━━━━━━━━━                   ━━━━━━━━━━━━
```

**水平 CapsuleCast のときだけ**下端を持ち上げることで、段差以下の高さの障害物を「壁」として検出しなくなる。垂直パスでは通常のカプセルを使うため、段差の上に着地できる。

### 1.9 オブジェクトを押す仕組み

EntityController は動的 Rigidbody（`isKinematic = false`）を持つオブジェクトを「押す」機能を内蔵している。

#### Move メソッドの全体フロー

```csharp
public Vector3 Move(Vector3 currentPosition, Vector3 motion)
{
    ReleasePushedObjects();                                         // (1)
    currentPosition = MoveAndSlide(currentPosition, lateralMotion, false, motion);  // (2) 水平パス（押し有効）
    currentPosition = MoveAndSlide(currentPosition, verticalMotion, true);          // (3) 垂直パス（押し無効）
    currentPosition = HandlePenetration(currentPosition);           // (4)
    return currentPosition;
}
```

```
Move の処理順序:

(1) ReleasePushedObjects
    前フレームで押していたオブジェクトを解放
    ├─ isKinematic = false に戻す
    └─ 無視リストから除去

(2) MoveAndSlide（水平パス）pushEnabled = true
    ├─ 動的Rigidbodyに当たった場合:
    │   ├─ GetMaxPushDistance で壁チェック
    │   ├─ isKinematic = true に変更（物理挙動を一時停止）
    │   ├─ transform.position += pushDir * maxPush（直接移動）
    │   ├─ 無視リストに追加（以降のCapsuleCastで無視）
    │   └─ m_pushActive に記録
    └─ 静的壁に当たった場合: 通常のスライド処理

(3) MoveAndSlide（垂直パス）pushEnabled = false
    ├─ 動的Rigidbodyに当たった場合:
    │   ├─ 無視リストに追加（通過する）
    │   └─ m_pushActive に記録（次フレームで解放用）
    └─ 静的壁に当たった場合: 通常のスライド処理

(4) HandlePenetration
    └─ 動的Rigibody は除外（overlapRb != null && !overlapRb.isKinematic → continue）
```

#### 動的 Rigidbody の検出

MoveAndSlide 内で CapsuleCast がヒットしたとき、`hit.collider.attachedRigidbody` を取得し、それが `!isKinematic`（動的）であれば押し処理に入る。

```csharp
var hitRb = hit.collider.attachedRigidbody;
if (hitRb != null && !hitRb.isKinematic)
{
    // 押し処理
}
```

#### kinematic 切替 + transform.position 移動 + 無視リスト

押し処理のコア:

```csharp
m_pushActive.Add(new PushInfo { rb = hitRb, col = hit.collider });
hitRb.isKinematic = true;                          // 物理演算を一時停止
hitRb.transform.position += pushDir * maxPush;     // 直接移動
m_ignoredColliders.Add(hit.collider);              // 以降のCast/Penetrationで無視
```

**なぜ isKinematic に切り替えるか**:
- `AddForce` だと「押す力」と「壁からの反発力」が競合し、ジッターやすり抜けが発生する
- kinematic にして直接 position を動かすことで、確実に意図した距離だけ移動させる
- 次フレームの `ReleasePushedObjects` で `isKinematic = false` に戻すため、重力等の物理挙動は維持される

#### BoxCast による壁チェック（GetMaxPushDistance）

押されるオブジェクトが壁にぶつからないかをチェックする:

```csharp
private float GetMaxPushDistance(Collider objCollider, Vector3 direction, float desiredDistance)
{
    Bounds bounds = objCollider.bounds;
    Vector3 halfExtents = bounds.extents;
    Vector3 center = bounds.center;

    if (Physics.BoxCast(center, halfExtents, direction, out var wallHit,
        Quaternion.identity, desiredDistance + skinWidth, collisionLayer, QueryTriggerInteraction.Ignore))
    {
        if (wallHit.collider == objCollider || wallHit.collider.transform == objCollider.transform)
            return desiredDistance;  // 自分自身に当たった場合は無視

        float maxDist = wallHit.distance - skinWidth;
        return Mathf.Max(maxDist, 0f);  // 壁までの距離が最大押し距離
    }

    return desiredDistance;  // 壁がなければ全距離押せる
}
```

```
BoxCast の概念図:

プレイヤー →→→ [箱]  →→→→→→  |壁|
                 ↑               ↑
           押されるオブジェクト   BoxCastが壁を検出
           のBoundsサイズで      → maxDist = 壁までの距離
           BoxCast
```

#### 垂直パスでの動的 Rigidbody 無視

垂直パス（`pushEnabled = false`、つまり `verticalPass = true`）では、動的 Rigidbody に当たっても押さずに**通過**する:

```csharp
if (!pushEnabled)
{
    var checkRb = hit.collider.attachedRigibody;
    if (checkRb != null && !checkRb.isKinematic)
    {
        m_pushActive.Add(new PushInfo { rb = checkRb, col = hit.collider });
        m_ignoredColliders.Add(hit.collider);
        continue;  // 反復を再実行 → このコライダーを無視して移動
    }
}
```

**なぜ垂直パスでは押さないか**:
- プレイヤーが箱の上に乗っているとき、重力による下方移動で箱を地面にめり込ませてしまう
- 水平方向のみ「押す」ことで、自然な物理挙動を実現する

#### HandlePenetration での動的 Rigidbody 除外

```csharp
var overlapRb = m_overlaps[i].attachedRigidbody;
if (overlapRb != null && !overlapRb.isKinematic) continue;
```

めり込み解決でも動的オブジェクトを除外する。プレイヤーが箱と重なっている場合、ComputePenetration で押し出すと箱が飛んでいく代わりにプレイヤーが不自然に弾かれるため。

#### ReleasePushedObjects による次フレームリセット

```csharp
private void ReleasePushedObjects()
{
    foreach (var info in m_pushActive)
    {
        if (info.rb != null)
            info.rb.isKinematic = false;      // 物理演算を再開
        if (info.col != null)
            m_ignoredColliders.Remove(info.col); // 無視リストから除去
    }
    m_pushActive.Clear();
}
```

Move() の先頭で毎フレーム呼ばれる。前フレームで押したオブジェクトの isKinematic を false に戻し、無視リストからも除去する。これにより:
- 押していないフレームでは通常の物理挙動（重力落下等）に戻る
- 次のフレームで再度衝突すれば再び押し処理に入る
- 1フレームの「押し→解放」サイクルで安定した押し挙動を実現

---

## 2. 重力システム

### 2.1 Entity.ApplyGravity: IsGrounded の判定と snapForce

```csharp
protected void ApplyGravity(float dt)
{
    if (IsGrounded && verticalVelocity < 0f)
    {
        verticalVelocity = -m_snapForce;   // 地面にスナップ（小さい下向き速度）
    }
    else
    {
        verticalVelocity += m_gravity * dt; // 自由落下
    }
}
```

**接地中（IsGrounded = true）で下方速度の場合**:
- `verticalVelocity = -m_snapForce`（デフォルト: -2）に固定
- これは「地面に張り付く」小さな下向き速度
- **なぜゼロにしないか**: 斜面を下る際、速度をゼロにするとバウンド（地面から浮く→落ちるの繰り返し）が発生する。常にわずかな下向き力をかけることで、斜面に密着し続ける

```
snapForce の効果:

速度をゼロにした場合:            snapForce を使う場合:
    ↗ 浮く                        → 斜面に密着
   ↙ 落ちる                       → 斜面に密着
  ↗ 浮く                          → 斜面に密着
   (バウンド現象)                    (安定)
```

**空中（IsGrounded = false）の場合**:
- `verticalVelocity += m_gravity * dt` で加速度的に落下（デフォルト: -20 m/s^2）
- Unity標準の重力（-9.81）より大きい値を使っている。ゲーム的な手触り重視

### 2.2 Entity.EntityStep の処理順序と理由

```csharp
protected void EntityStep(float dt)
{
    UpdateGround();                  // (1) 接地判定
    ApplyGravity(dt);                // (2) 重力適用
    UpdateMagneticOrientation(dt);   // (3) 磁力による回転
    ApplyMovement(dt);               // (4) 速度 → 位置変換
}
```

```
処理順序の理由:

(1) UpdateGround
    まず「今フレームで地面にいるか」を判定する。
    これが先でないと ApplyGravity の分岐が前フレームの情報に基づいてしまう。

(2) ApplyGravity
    接地判定の結果に基づき、snapForce か自由落下かを決定。
    ApplyMovement より前に呼ぶことで、このフレームの重力が移動に反映される。

(3) UpdateMagneticOrientation
    強い磁力を受けて空中にいるとき、引力/斥力方向にキャラを回転。
    ApplyMovement の前に行うことで、回転後の transform.up に基づいた移動になる。

(4) ApplyMovement
    velocity + externalVelocity を位置に変換。
    EntityController があれば Collide-and-Slide で壁を考慮。
    最後に externalVelocity をゼロリセット。
```

### 2.3 Entity.UpdateGround: Raycast ベースの接地判定

```csharp
protected void UpdateGround()
{
    float height = capsuleCollider != null ? capsuleCollider.height : 2f;
    float groundCheckDist = height * 0.5f + 0.3f;

    if (Physics.Raycast(transform.position, -transform.up, out var hit, groundCheckDist,
        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
    {
        float footDist = hit.distance - height * 0.5f;
        IsGrounded = footDist < 0.1f;
        // ...法線・角度・斜面方向を記録...
    }
    else
    {
        IsGrounded = false;
    }
}
```

```
Raycast の概念図:

  transform.position (カプセルの中心)
          │
          │ ← height * 0.5（カプセル中心→足元）
          │
  ────────┼──────── 足元
          │ ← footDist（足元から地面まで）
          │         footDist < 0.1 なら接地判定
          ▼
  ━━━━━━━━━━━━━━━━ 地面

  groundCheckDist = height * 0.5 + 0.3
  (足元から 0.3m 下まで検索)
```

**なぜ CharacterController.isGrounded を使わないか**:
- CharacterController を使っていない（EntityController で自前実装）
- Raycast なら `transform.up` 方向を使えるため、磁力で天井に張り付いた場合にも対応可能
- `QueryTriggerInteraction.Ignore` でトリガーコライダーを除外

**地面情報の記録**:
- `groundHit`: RaycastHit 構造体（位置、法線等）
- `groundNormal`: 地面の法線（斜面判定用）
- `groundAngle`: Vector3.up との角度
- `localSlopeDirection`: 斜面の傾斜方向（XZ平面に投影、正規化）

---

## 3. 磁力システム全体像

### 3.1 フロー概要

```
[弾を撃つ]
    │
    ▼
[MagnetBullet が飛行]
    │  ← FixedUpdate で MagnetField の影響を受けて弾道が曲がる
    │
    ▼
[着弾]
    ├─ Magnetizable なし → パターン1: StickToSurface（弾がくっつく）
    │   └─ 弾自身が MagnetField + Magnetizable を持つ磁力源になる
    │
    └─ Magnetizable あり → パターン2: MagnetizeTarget（弾消滅+対象を磁化）
        └─ 対象に MagnetField + Magnetizable が付与される
    │
    ▼
[MagnetManager.FixedUpdate]
    │
    ├─ 全 Magnetizable ペアを総当たり（O(n^2)）
    │   └─ ProcessPair: 距離・極性・質量に基づいて力を計算
    │       ├─ 異極 → 引力（互いに近づく方向）
    │       └─ 同極 → 斥力（互いに離れる方向）
    │
    ├─ snapDistance 以内の異極ペア → MagneticSnapResolver
    │   └─ FixedJoint で物理固定
    │
    └─ AssignFieldsToEntities: 各 Entity に最強のフィールドを割り当て
    │
    ▼
[力の適用]
    Magnetizable.ApplyForce
    ├─ IMagneticResponse （最優先: カスタム応答）
    ├─ IMagnetTarget     （Entity: externalVelocity に加算）
    └─ Rigidbody.AddForce（一般オブジェクト）
    │
    ▼
[Entity.ApplyMovement]
    velocity + externalVelocity → EntityController.Move
    externalVelocity = Vector3.zero（リセット）
```

### 3.2 関連クラスの役割分担

```
┌─────────────────────────────────────────────────────────────────┐
│                        MagnetManager                            │
│  磁力システムの中枢。全 Magnetizable/MagnetField を管理         │
│  FixedUpdate で全ペアの力を計算し適用する                       │
│  ├─ MagneticSnapResolver: 異極吸着の FixedJoint 管理           │
│  └─ MagnetSettings: グローバル設定（力、範囲、スナップ距離）    │
└───┬──────────────────┬──────────────────────────────────────────┘
    │                  │
    ▼                  ▼
┌──────────────┐  ┌──────────────────────────────────────────────┐
│ Magnetizable │  │ MagnetField                                  │
│ 磁化対象     │  │ 磁力場。形状ベースの方向/減衰計算            │
│ 極性/質量    │  │ トリガーで範囲内 Entity を検知               │
│ 力の適用     │  │ ├─ MagnetFieldTriggerBridge: 子GO の橋渡し  │
│ レイヤー管理 │  │ ├─ MagnetFieldVisualizer: ワイヤーフレーム   │
└──────────────┘  │ └─ MagnetFieldSettings: 個別設定             │
                   └──────────────────────────────────────────────┘
    ▲
    │（力の適用先を判別）
    │
┌───┴─────────────────────────────────────────────────────────────┐
│ IMagneticResponse  │  IMagnetTarget      │  Rigidbody          │
│ 最優先             │  次に優先           │  最後のフォールバック│
│ カスタム応答       │  Entity が実装      │  AddForce で適用    │
│ (力+ソース位置)    │  (externalVelocity) │                     │
└────────────────────┴────────────────────┴─────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ MagnetBullet                    │  BulletManager                │
│ 磁力弾。着弾時にパターン分岐   │  残弾管理（shotCount ベース）  │
│ 飛行中は MagnetField で弾道曲げ│  リロード時に全消去            │
│ └─ BulletSettings: 弾設定      │                               │
└─────────────────────────────────┴───────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ 共通型                                                          │
│ ├─ MagneticPole: 極性列挙 (None / S / N)                      │
│ ├─ IMagnetField: 磁力場インターフェース (Common モジュール)     │
│ ├─ IMagnetTarget: 磁力受容インターフェース                     │
│ ├─ IMagneticResponse: カスタム磁力応答インターフェース          │
│ └─ BoundsHelper: 幾何計算ユーティリティ                        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. 弾の仕様（MagnetBullet）

### 4.1 パターン1: StickToSurface（壁にくっつく）

**発動条件**: 着弾対象に `Magnetizable` コンポーネントがない場合（壁、天井など）

```csharp
private void StickToSurface(Collider surface)
{
    IsStuck = true;
    m_rb.linearVelocity = Vector3.zero;
    m_rb.isKinematic = true;
    transform.SetParent(surface.transform);

    var mag = GetComponent<Magnetizable>();
    if (mag != null)
    {
        mag.SetPole(Pole);
        mag.mass = Mathf.Infinity;  // 壁に固定 → 動かない
    }

    // MagnetField を弾自身に付与
    var field = gameObject.AddComponent<MagnetField>();
    field.Initialize(Pole, m_settings.bulletFieldSettings);

    // 可視化
    var visualizer = gameObject.AddComponent<MagnetFieldVisualizer>();
    visualizer.Show(Pole, m_settings.bulletFieldSettings);

    // フィールド期限切れ → 弾ごと消える
    field.OnFieldExpired += () => Destroy(gameObject);
}
```

```
パターン1の結果:

  壁 ━━━━━━━━━━━
       ●  ← 弾（くっついている）
       │     isKinematic = true
       │     mass = Infinity（動かない）
       │
    ╭──╮
    │  │  ← MagnetField（球状/Box状/Cylinder状）
    │  │     innerRadius 内: フルパワー
    │  │     outerRadius まで: 線形減衰
    ╰──╯
```

**mass = Infinity の意味**: MagnetManager.ProcessPair の質量分配で `ratioA = 0` になる。つまり壁にくっついた弾は磁力で動かず、相手側だけが動く。

### 4.2 パターン2: MagnetizeTarget（弾消滅+対象を磁化）

**発動条件**: 着弾対象に `Magnetizable` コンポーネントがある場合（磁化可能オブジェクト）

```csharp
private void MagnetizeTarget(Collider target, Magnetizable targetMag)
{
    targetMag.SetPole(Pole);  // 対象に極性を付与

    // MagnetField を対象に付与
    var field = target.gameObject.AddComponent<MagnetField>();
    field.Initialize(Pole, m_settings.bulletFieldSettings);

    // 可視化
    var visualizer = target.gameObject.AddComponent<MagnetFieldVisualizer>();
    visualizer.Show(Pole, m_settings.bulletFieldSettings);

    // フィールド期限切れ → 対象の磁化解除 + Visualizer除去
    field.OnFieldExpired += () =>
    {
        if (targetMag != null) targetMag.Deactivate();
        if (visualizer != null) Destroy(visualizer);
    };

    Destroy(gameObject);  // 弾は消える
}
```

```
パターン2の結果:

  [対象オブジェクト]
       ↓ 弾が当たる
  [対象オブジェクト + Magnetizable(S極) + MagnetField + Visualizer]
       弾は消滅

  対象自身が磁力源になる。フィールド期限切れで自動的に磁化解除。
```

### 4.3 SelfFire（自分に磁力付与）

`Initialize(pole, direction, isSelfFire: true)` で発射された弾は特殊挙動:

```csharp
// OnTriggerEnter 内の判定:
if (other.isTrigger)
{
    if (!IsSelfFire || !other.CompareTag(GameTags.Player)) return;
    // SelfFire弾のみ、プレイヤーのTriggerコライダーに当たる
}

// 通常弾はプレイヤーを無視:
if (other.CompareTag(GameTags.Player) && !IsSelfFire) return;
```

- 通常弾: プレイヤーのコライダーを無視（自分を撃てない）
- SelfFire弾: プレイヤーの Trigger コライダーにのみ反応する
- プレイヤーには Magnetizable があるため、パターン2（MagnetizeTarget）が発動し、プレイヤー自身が磁化される

### 4.4 弾道曲げ（FixedUpdate で MagnetField の影響）

飛行中の弾は、既存の MagnetField から力を受けて弾道が曲がる:

```csharp
void FixedUpdate()
{
    if (IsStuck || m_rb == null || m_settings == null) return;

    var fields = MagnetManager.Instance.GetActiveFields();
    for (int i = 0; i < fields.Count; i++)
    {
        var field = fields[i];
        float strength = field.GetStrengthAt(transform.position);
        if (strength <= 0f) continue;

        // 極性判定: 異極=吸引、同極=反発
        bool attract = Pole != field.Pole && field.Pole != MagneticPole.None && Pole != MagneticPole.None;
        Vector3 toCenter = (field.Center - transform.position).normalized;
        float pull = strength * m_settings.fieldAttractionFactor;

        m_rb.linearVelocity += (attract ? toCenter : -toCenter) * pull * Time.fixedDeltaTime;
    }
}
```

```
弾道曲げの概念図:

         MagnetField (S極)
              ●
             ╱
    弾 (N極) ╱  ← 異極なので吸引
    ● ──→ ╱
          ╱
         ▼ （弾道がフィールド中心に向かって曲がる）
```

**注意点**:
- `linearVelocity` に直接加算しているため、速度が無限に増加しうる
- `fieldAttractionFactor` で曲げ強度を調整（デフォルト: 5.0）
- `GetStrengthAt` で距離減衰が適用されるため、遠くのフィールドほど影響が小さい
- `Time.unscaledDeltaTime` ではなく `Time.fixedDeltaTime` を使用（FixedUpdate内なので正しい）

### 4.5 残弾管理（BulletManager、shotCount ベース）

```
BulletManager の残弾管理:

  maxBullets = 4（デフォルト）

  発射時:  shotCount++, activeBullets に追加
  着弾時:  弾が Destroy → OnDestroy で activeBullets から除去
           ただし shotCount は減らない!
  リロード: ClearAll() → 全 MagnetField を ForceExpire
            → 全弾を Destroy → shotCount = 0

  つまり:
  - 4発撃ったらリロードするまで撃てない
  - 弾が着弾で消えても残弾は回復しない
  - リロードで全ての磁力効果がリセットされる
```

```csharp
public bool CanShoot()
{
    return m_shotCount < MaxBullets;  // 撃った数で判定（存在数ではない）
}
```

**設計意図**: 「4発を計画的に使い、リロードで全リセット」というゲームプレイサイクル。弾が消えたら撃てるようにすると、連射→着弾→連射のスパムが可能になり、磁力のペア配置を考えるゲーム性が損なわれる。

**ClearAll（リロード）の処理**:
1. `FindObjectsByType<MagnetField>` で全フィールドを検索
2. 各フィールドの `ForceExpire()` を呼ぶ（OnFieldExpired イベント発火 → 磁化解除、Visualizer除去）
3. activeBullets の全弾を Destroy
4. shotCount = 0 にリセット
5. `OnBulletCountChanged` イベント発火（UI更新用）

### 4.6 弾同士の衝突: ダメージ蓄積

弾が MagnetField を持つ弾（壁にくっついた弾）に当たったとき:

```csharp
if (other.CompareTag(GameTags.MagnetBullet))
{
    var otherField = other.GetComponent<MagnetField>();
    if (otherField != null)
    {
        bool isOpposite = Pole != otherField.Pole && ...;
        if (isOpposite)
        {
            otherField.AccumulateDamage(m_settings.bulletDamage);
            Destroy(gameObject);
        }
    }
    return;
}
```

異極の弾を既存のフィールドに当てると、フィールドにダメージが蓄積される。フィールド消滅時に `HandleFieldExplosion` で範囲内の敵にダメージが放出される。

---

## 5. 磁力場（MagnetField）

### 5.1 形状: Sphere / Box / Cylinder

```csharp
public enum FieldShape { Sphere, Box, Cylinder }
```

```
Sphere（球）:           Box（直方体）:         Cylinder（円柱）:

      ╭───╮              ┌─────┐              ┌───┐
    ╱       ╲             │     │              │   │
   │    ●    │            │  ●  │              │ ● │
    ╲       ╱             │     │              │   │
      ╰───╯              └─────┘              └───┘

  Center が中心          Center + Size で定義   Top ─ Bottom + Radius
  方向 = (point-Center)  NearestPointOnBox     NearestPointOnFiniteLine
```

### 5.2 innerRadius / outerRadius / EffectiveOuterRadius

```
磁力強度の距離減衰:

強度
1.0 ├──────────┐
    │          │
    │ フルパワー │
    │   圏     │╲
    │          │  ╲  線形減衰
    │          │    ╲
0.0 ├──────────┴──────┴─────
    0      inner    outer   距離

    innerRadius: この距離以内は強度 = 1.0（フルパワー）
    outerRadius: この距離で強度 = 0.0（ゼロ）
    inner 〜 outer 間: 線形補間で減衰
```

```csharp
// MagnetFieldSettings:
public float EffectiveOuterRadius => outerRadius > 0f ? outerRadius : innerRadius * 1.3f;
```

`outerRadius` が 0 以下の場合、`innerRadius * 1.3` を自動使用する。これにより、innerRadius だけ設定すれば自然な減衰が得られる。

### 5.3 GetStrengthAt: 距離による力の減衰

```csharp
public float GetStrengthAt(Vector3 point)
{
    // 形状に応じた最近接面を取得
    Vector3 nearestSurface = Shape switch
    {
        FieldShape.Box => BoundsHelper.NearestPointOnBox(Center, Size, transform.rotation, point),
        FieldShape.Cylinder => BoundsHelper.NearestPointOnFiniteLine(Top, Bottom, point),
        _ => Center  // Sphere: 中心点そのもの
    };

    float dist = Vector3.Distance(point, nearestSurface);
    if (dist <= m_settings.innerRadius) return 1f;
    if (dist >= m_settings.EffectiveOuterRadius) return 0f;

    return 1f - (dist - m_settings.innerRadius) / (m_settings.EffectiveOuterRadius - m_settings.innerRadius);
}
```

**形状ごとの距離計算の違い**:
- **Sphere**: 中心点からの距離
- **Box**: Box表面上の最近接点からの距離（Box内部にいる場合は距離0→強度1.0）
- **Cylinder**: 軸（Top〜Bottom線分）上の最近接点からの距離

### 5.4 GetFieldDirection: 形状ベースの方向計算

```csharp
public Vector3 GetFieldDirection(Vector3 point)
{
    Vector3 dir = Shape switch
    {
        FieldShape.Box => (point - BoundsHelper.NearestPointOnBox(Center, Size, transform.rotation, point)).normalized,
        FieldShape.Cylinder => (point - BoundsHelper.NearestPointOnFiniteLine(Top, Bottom, point)).normalized,
        _ => (point - Center).normalized
    };
    return dir == Vector3.zero ? Vector3.up : dir;  // ゼロベクトル防止
}
```

**BoundsHelper の幾何計算**:

```csharp
// NearestPointOnFiniteLine: 線分上の最近接点
// Cylinder の軸に使用。Top と Bottom を結ぶ線分にクランプ。
public static Vector3 NearestPointOnFiniteLine(Vector3 start, Vector3 end, Vector3 point)

// NearestPointOnBox: Box表面上の最近接点
// 回転を考慮し、ローカル空間でクランプ後にワールドに戻す。
public static Vector3 NearestPointOnBox(Vector3 center, Vector3 size, Quaternion rotation, Vector3 point)
```

### 5.5 トリガー検知の仕組み（子GO + MagnetFieldTriggerBridge）

```
MagnetField のトリガー構造:

[弾 GameObject]
 ├─ MagnetBullet
 ├─ Magnetizable
 ├─ MagnetField
 ├─ Rigidbody (isKinematic)
 ├─ SphereCollider (弾の衝突用)
 │
 └─ [MagnetFieldTrigger] ← 子 GameObject
      ├─ Layer: "MagnetField"
      ├─ SphereCollider (isTrigger = true, radius = CalcTriggerRadius())
      ├─ Rigidbody (isKinematic = true)  ← トリガーイベント発火に必要
      └─ MagnetFieldTriggerBridge → 親 MagnetField に転送
```

**なぜ子 GO に分離するか**:
1. **レイヤー分離**: 弾は "Bullet" レイヤー、トリガーは "MagnetField" レイヤー。Layer Collision Matrix で弾同士の衝突と磁力場の検知を独立に制御できる
2. **親GOのレイヤーを変えずに済む**: 弾のレイヤーを変えるとBulletとしての衝突判定が壊れる
3. **コライダー競合回避**: 弾の SphereCollider と磁力場の大きな SphereCollider が同一GOにあると、PhysX の挙動が不安定になりうる

**MagnetFieldTriggerBridge**:
子GOのOnTriggerStay/Enter/Exit イベントを親の MagnetField に転送する単純なブリッジクラス:

```csharp
public class MagnetFieldTriggerBridge : MonoBehaviour
{
    private MagnetField m_field;

    public void Initialize(MagnetField field) => m_field = field;

    void OnTriggerStay(Collider other)  => m_field?.HandleTriggerStay(other);
    void OnTriggerEnter(Collider other) => m_field?.HandleTriggerEnter(other);
    void OnTriggerExit(Collider other)  => m_field?.HandleTriggerExit(other);
}
```

**CalcTriggerRadius**: 形状に応じてトリガーの半径を決定:
- Sphere: `EffectiveOuterRadius`
- Box: `size.magnitude * 0.5 + EffectiveOuterRadius`（Box対角線 + 外側減衰距離）
- Cylinder: `max(height/2, radius) + EffectiveOuterRadius`

**Entity 検知の流れ**:
1. Entity が MagnetFieldTrigger の SphereCollider に入る
2. `OnTriggerStay` → `MagnetFieldTriggerBridge` → `MagnetField.HandleTriggerStay`
3. `HandleTriggerStay` 内で `other.GetComponent<Entity>()` を取得しキャッシュ
4. `m_entitiesInRange` リストに追加
5. `MagnetManager.AssignFieldsToEntities` が毎フレーム、各フィールドの `GetEntitiesInRange()` を参照し、最強フィールドを Entity に割り当てる

### 5.6 ライフタイムと ForceExpire

```csharp
void Update()
{
    // lifetime = 0 は永続（タイマー不動）
    if (m_settings.lifetime <= 0f) return;

    m_remainingLifetime -= Time.deltaTime;
    if (m_remainingLifetime <= 0f)
        ForceExpire();
}

public void ForceExpire()
{
    if (m_expired) return;
    m_expired = true;
    OnFieldExpired?.Invoke();  // コールバック発火
    Destroy(this);             // MagnetField コンポーネントのみ破棄
}
```

**OnFieldExpired の購読者**:
- **MagnetManager**: `HandleFieldExplosion`（蓄積ダメージを範囲内Entityに放出）+ `SnapResolver.ReleaseAllForField`（FixedJoint解放）
- **MagnetBullet.StickToSurface**: `Destroy(gameObject)` — 弾ごと消える
- **MagnetBullet.MagnetizeTarget**: `targetMag.Deactivate()` + `Destroy(visualizer)` + `Destroy(effectInstance)` — 磁化解除

**ダメージ蓄積と爆発**:

```csharp
// MagnetField.AccumulateDamage:
public void AccumulateDamage(float amount)
{
    if (!m_settings.accumulateDamage) return;
    m_storedDamage = Mathf.Min(m_storedDamage + amount, m_settings.maxStoredDamage);
}

// MagnetManager.HandleFieldExplosion:
// フィールド消滅時、蓄積ダメージを範囲内Entityに距離減衰で適用
float damageRatio = 1f - dist / radius;
int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * damageRatio));
entity.health.Damage(finalDamage);
```

---

## 6. 磁力の力計算（MagnetManager.ProcessPair）

### 6.1 ペア検索: 全 Magnetizable の総当たり（O(n^2)）

```csharp
void FixedUpdate()
{
    for (int i = 0; i < m_cachedList.Count; i++)
    {
        if (!m_cachedList[i].IsActive) continue;

        for (int j = i + 1; j < m_cachedList.Count; j++)
        {
            if (!m_cachedList[j].IsActive) continue;

            ProcessPair(m_cachedList[i], m_cachedList[j], contactsThisFrame);
        }
    }
}
```

`i + 1` から始めることで、同じペアを2回処理しない。`IsActive` が false（`MagneticPole.None`）のオブジェクトはスキップ。

**計算量**: n 個の Magnetizable に対して `n*(n-1)/2` ペア。弾4発 + 磁化オブジェクト数個の規模なら問題ないが、大量の磁化オブジェクトがある場合はボトルネックになりうる。

**レジストリ管理**: `HashSet<Magnetizable>` で管理し、dirty フラグで `List<Magnetizable>` にキャッシュ。FixedUpdate 内では List をイテレートする（HashSet の直接イテレートよりアロケーションが少ない）。

### 6.2 magnetRange: ハードカットオフ

```csharp
if (distance > m_settings.magnetRange || distance < 0.01f) return;
```

- `magnetRange`（デフォルト: 10m）を超えるペアは即 return。パフォーマンス用
- `distance < 0.01f` は同一位置（ゼロ除算防止）

### 6.3 effectiveOuter / effectiveInner: フィールド個別範囲

```csharp
float effectiveOuter = Mathf.Max(a.FieldOuterRadius, b.FieldOuterRadius);
float effectiveInner = Mathf.Max(a.FieldInnerRadius, b.FieldInnerRadius);

if (effectiveOuter <= 0f) effectiveOuter = m_settings.magnetRange;
if (effectiveInner <= 0f) effectiveInner = effectiveOuter * 0.8f;

if (distance > effectiveOuter) return;
```

**2つの Magnetizable のフィールド範囲の大きい方を採用する**。これにより、大きなフィールドを持つ磁石は遠くのオブジェクトにも力を及ぼせる。フィールドがない場合は `magnetRange` にフォールバック。

### 6.4 inner/outer 線形減衰

```csharp
float strength;
if (distance <= effectiveInner)
{
    strength = 1f;  // inner 以内: フルパワー
}
else
{
    strength = 1f - (distance - effectiveInner) / (effectiveOuter - effectiveInner);
    // inner 〜 outer 間: 1.0 → 0.0 に線形減衰
}

float forceMagnitude = m_settings.magnetForce * strength;

if (m_settings.maxForcePerObject > 0f)
    forceMagnitude = Mathf.Min(forceMagnitude, m_settings.maxForcePerObject);
```

```
forceMagnitude の計算:

magnetForce(15) * strength(0〜1) = 最終力(0〜15)

ただし maxForcePerObject(50) でクランプ
（複数のフィールドから力を受ける場合の暴走防止）
```

### 6.5 質量非対称の力分配（massA / massB）

```csharp
float massA = a.mass;
float massB = b.mass;
float ratioA, ratioB;

if (float.IsInfinity(massA) && float.IsInfinity(massB))
{
    ratioA = 0f; ratioB = 0f;  // 両方固定 → どちらも動かない
}
else if (float.IsInfinity(massA))
{
    ratioA = 0f; ratioB = 1f;  // A固定 → Bだけ動く
}
else if (float.IsInfinity(massB))
{
    ratioA = 1f; ratioB = 0f;  // B固定 → Aだけ動く
}
else
{
    float totalMass = massA + massB;
    ratioA = massB / totalMass;  // 軽い方が多く動く
    ratioB = massA / totalMass;
}
```

```
質量分配の例:

質量 A=1, B=3 の場合:
  ratioA = 3/(1+3) = 0.75  ← 軽い A は力の75%を受ける
  ratioB = 1/(1+3) = 0.25  ← 重い B は力の25%を受ける

質量 A=Infinity (壁の弾), B=1 の場合:
  ratioA = 0    ← A は動かない
  ratioB = 1    ← B は力の100%を受ける
```

**設計意図**: 現実の物理（ニュートンの第3法則）では力は等しいが加速度が質量に反比例する。ここでは「力自体を質量比で分配」することで、同じ効果をより直感的に実装している。

### 6.6 同極反発 / 異極引力

```csharp
bool isOpposite = a.Pole != b.Pole && a.Pole != MagneticPole.None && b.Pole != MagneticPole.None;
bool isSame = a.Pole == b.Pole;

if (isOpposite)  // 異極: 引力
{
    a.ApplyForce( dirAtoB * forceMagnitude * ratioA, b.transform.position);
    b.ApplyForce(-dirAtoB * forceMagnitude * ratioB, a.transform.position);
}
else if (isSame)  // 同極: 斥力
{
    a.ApplyForce(-dirAtoB * forceMagnitude * ratioA, b.transform.position);
    b.ApplyForce( dirAtoB * forceMagnitude * ratioB, a.transform.position);
}
```

```
力の方向:

異極引力 (S ←→ N):            同極反発 (S ←─→ S):

  [S] ──→ dirAtoB ──→ [N]     [S] ←── dirAtoB ──→ [S]
  A に +dirAtoB の力             A に -dirAtoB の力
  B に -dirAtoB の力             B に +dirAtoB の力
  → 互いに近づく                → 互いに離れる
```

**MagneticPole.None との組み合わせ**: `isOpposite` も `isSame` も false になるため、力は適用されない。Pole=None は「磁化されていない」状態。

### 6.7 snapDistance 以下での MagneticSnapResolver

```csharp
if (isOpposite && distance < m_settings.snapDistance)
{
    m_snapResolver?.Resolve(a, b, Time.fixedDeltaTime);

    long pairKey = GetPairKey(a, b);
    contactsThisFrame.Add(pairKey);

    if (m_activeContacts.Add(pairKey))
    {
        a.NotifyContact(b);
        b.NotifyContact(a);
    }
}
```

snapDistance（デフォルト: 1.5m）以内に異極ペアが入ると:
1. `MagneticSnapResolver.Resolve` が呼ばれる
2. 既に固定済みなら何もしない
3. 未固定なら `Snap` で FixedJoint を生成

**MagneticSnapResolver.Snap の詳細**:

```csharp
public void Snap(Magnetizable a, Magnetizable b)
{
    Magnetizable anchor = FindAnchor(a, b);  // 重い方がアンカー
    Magnetizable mover = anchor == a ? b : a; // 軽い方がムーバー

    var joint = mover.gameObject.AddComponent<FixedJoint>();
    joint.connectedBody = anchor.GetComponent<Rigidbody>();
    joint.breakForce = m_settings.snapBreakForce;  // 破壊力を設定

    m_attachedPairs.Add(key);
    m_joints[key] = joint;
}
```

```
FixedJoint の仕組み:

  [壁の弾 (mass=Infinity)]  ←── FixedJoint ──→  [プレイヤー (mass=1)]
       anchor (重い方)                              mover (軽い方)

  Joint は mover 側に AddComponent される。
  breakForce を超える力がかかると Joint が壊れる（同極反発で分離）。
```

**接触 Enter/Exit の追跡**:
- `m_activeContacts` に現在接触中のペアキーを保持
- 新しく追加されたペア（`m_activeContacts.Add` が true を返す）に対して `NotifyContact` を呼ぶ
- フレーム末に `m_activeContacts.IntersectWith(contactsThisFrame)` で、今フレームに接触していないペアを除去

---

## 7. 力の適用経路

### 7.1 MagnetManager.ProcessPair → Magnetizable.ApplyForce

ProcessPair 内で力の大きさと方向が決まると、`Magnetizable.ApplyForce(force, sourcePosition)` が呼ばれる。

### 7.2 ApplyForce 内の優先順位

```csharp
public void ApplyForce(Vector3 force, Vector3 sourcePosition)
{
    m_totalForceThisFrame += force.magnitude;  // 影響度の蓄積

    // 優先順位1: IMagneticResponse（カスタム応答）
    if (m_magneticResponse != null && m_magneticResponse.IsResponseActive)
    {
        m_magneticResponse.OnMagnetForce(force, sourcePosition);
        return;
    }

    // 優先順位2: IMagnetTarget（Entity）
    if (m_magnetTarget != null)
    {
        m_magnetTarget.ApplyMagnetForce(force);
        return;
    }

    // 優先順位3: Rigidbody.AddForce（一般物体）
    if (m_rb != null && !m_rb.isKinematic)
    {
        m_rb.AddForce(force, ForceMode.Acceleration);
        return;
    }
}
```

```
力の適用経路の判別:

Magnetizable.ApplyForce(force, sourcePosition)
     │
     ├─ IMagneticResponse あり & IsResponseActive = true
     │   └─ OnMagnetForce(force, sourcePosition)
     │      カスタム応答。force だけでなく sourcePosition も渡る。
     │      例: 敵がプレイヤーの位置に基づいて回避行動を取る等
     │
     ├─ IMagnetTarget あり
     │   └─ ApplyMagnetForce(force)
     │      Entity が実装。externalVelocity に加算する。
     │
     └─ Rigidbody あり & !isKinematic
         └─ AddForce(force, ForceMode.Acceleration)
            質量を無視した加速度として適用。
            一般的な物理オブジェクト向け。
```

**IMagneticResponse と IMagnetTarget の違い**:
- `IMagnetTarget`: 力（Vector3）のみ受け取る。Entity が実装し、externalVelocity に加算する単純なインターフェース
- `IMagneticResponse`: 力に加えて sourcePosition も受け取る。さらに `OnMagnetContact` コールバックもある。特定のオブジェクト（タレット、シールド等）がカスタムの磁力応答を実装するための拡張点
- `IsResponseActive` により、応答のオン/オフを動的に切り替えられる

### 7.3 Entity.ApplyMagnetForce → externalVelocity 加算

```csharp
// Entity.cs (IMagnetTarget 実装):
public void ApplyMagnetForce(Vector3 force)
{
    externalVelocity += force;
}
```

**externalVelocity は速度（m/s）として扱われる**。MagnetManager の `magnetForce` は実質的に「速度変化量/フレーム」。

### 7.4 externalVelocity の毎フレームリセット

```csharp
protected virtual void ApplyMovement(float dt)
{
    Vector3 motion = (velocity + externalVelocity) * dt;

    if (controller != null)
        transform.position = controller.Move(transform.position, motion);
    else
        transform.position += motion;

    externalVelocity = Vector3.zero;  // ← 毎フレームリセット
}
```

**なぜリセットするか**:
- 磁力は「毎フレーム計算される継続的な力」。前フレームの力が残ると二重加算になる
- MagnetManager.FixedUpdate で毎フレーム新しい力が計算されるため、externalVelocity は「このフレームの外部力の合計」を表す一時変数
- velocity（プレイヤーの入力による速度）は保持し続けるが、externalVelocity はリセットすることで「磁力が消えたら即座に力がなくなる」挙動を実現

```
フレームごとの流れ:

Frame N:
  MagnetManager.FixedUpdate → ApplyForce → externalVelocity = (3, 0, 0)
  Entity.Update → EntityStep → ApplyMovement
    motion = (velocity + externalVelocity) * dt
    position += motion
    externalVelocity = (0, 0, 0)  ← リセット

Frame N+1:
  MagnetManager.FixedUpdate → ApplyForce → externalVelocity = (2.8, 0, 0)
  (距離が変わったので力も変わる)
  ...
```

---

## 8. 可視化（MagnetFieldVisualizer）

### 8.1 LineRenderer ベースのワイヤーフレーム描画

`MagnetFieldVisualizer` は MagnetFieldSettings の形状に基づいて、LineRenderer でワイヤーフレームを描画する。

```csharp
public void Show(MagneticPole pole, MagnetFieldSettings settings)
```

**色の決定**:
- S極: 青 `(0.2, 0.4, 1.0, 0.8)` (inner), 半透明 `alpha=0.3` (outer)
- N極: 赤 `(1.0, 0.2, 0.2, 0.8)` (inner), 半透明 `alpha=0.3` (outer)

**描画マテリアル**: `Sprites/Default` シェーダーを使用。`s_sharedLineMaterial` で全インスタンスが共有（マテリアルの無駄な生成を防ぐ）。

### 8.2 Sphere / Box / Cylinder 対応

#### Sphere の描画

```
緯度リング: -60, -30, 0, 30, 60 度 （5本の水平リング）
経度リング: 0, 45, 90, 135 度     （4本の垂直リング）
各リング: 48 セグメントの円

     ╭──────╮  ← 60度
    ╱────────╲  ← 30度
   │──────────│  ← 0度（赤道）
    ╲────────╱  ← -30度
     ╰──────╯  ← -60度
   + 4本の経線が球を縦に走る
```

#### Box の描画

```
上面矩形 + 下面矩形 + 4本の縦辺 = 12本のライン

    ┌─────┐
   ╱│    ╱│
  ┌─┼───┐ │
  │ └───┼─┘
  │╱    │╱
  └─────┘
```

#### Cylinder の描画

```
上面円 + 下面円 + 4本の縦辺 = 2リング + 4ライン

    ╭───╮
    │   │
    │   │  × 4本
    │   │
    ╰───╯
```

### 8.3 inner + outer 2段階描画

```csharp
switch (settings.shape)
{
    case FieldShape.Sphere:
        DrawSphere(innerColor, settings.innerRadius);          // フルパワー圏
        DrawSphere(outerColor, settings.EffectiveOuterRadius); // 減衰圏
        break;
    // Box, Cylinder も同様に 2段階
}
```

inner は不透明度 0.8、outer は不透明度 0.3 で描画される。プレイヤーはフルパワー圏の範囲を視覚的に把握できる。

### 8.4 回転固定コンテナ（LateUpdate で rotation = identity）

```csharp
void LateUpdate()
{
    if (m_fieldContainer != null)
        m_fieldContainer.rotation = Quaternion.identity;
}
```

**なぜ回転を固定するか**: MagnetFieldVisualizer は弾や磁化オブジェクトの子として生成される。親が回転すると描画もつられて回転し、磁力場の形が歪んで見える。LateUpdate で毎フレーム `rotation = Quaternion.identity` にリセットすることで、ワールド軸に平行な描画を維持する。

```
回転固定しない場合:        回転固定する場合:

  弾が傾いている             弾が傾いていても
     ╱╲                     ╭──╮
    ╱  ╲  ← 楕円に見える    │  │ ← 常に正円
   ╱    ╲                   │  │
  ╱──────╲                  ╰──╯
```

**コンテナ構造**:
- `MagnetFieldContainer` という子GOを生成
- 全リング/ラインはこのコンテナの子
- `useWorldSpace = false` で描画し、コンテナの回転だけ制御

---

## 9. 各設定値の一覧と意味

### 9.1 MagnetSettings

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `magnetForce` | float | 15 | ペア間に適用される基本磁力。strength(0〜1)と掛けて最終力になる。 |
| `magnetRange` | float | 10 | ハードカットオフ距離。この距離を超えるペアは計算自体をスキップ。パフォーマンス用。 |
| `maxForcePerObject` | float | 50 | 1オブジェクトが1ペアから受ける力の上限。0で無制限。複数フィールドの暴走防止。 |
| `snapDistance` | float | 1.5 | この距離以内に異極ペアが入ると MagneticSnapResolver が発動。 |
| `snapBreakForce` | float | 100 | FixedJoint の破壊力。この力を超えるとJointが壊れる（同極反発で分離）。 |
| `magnetSpeedDamping` | float | 0.3 | 磁力場内でのEntity最高速度低下率。0=影響なし、1=完全停止。Entity.topSpeedMultiplier に適用される。 |

### 9.2 MagnetFieldSettings

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `shape` | FieldShape | Sphere | 磁力場の形状。Sphere/Box/Cylinder。 |
| `innerRadius` | float | 3 | この距離以内は磁力強度=1.0（フルパワー）。Visualizerのinner描画範囲。 |
| `outerRadius` | float | 8 | この距離で磁力強度=0.0。0以下なら innerRadius*1.3 を自動使用。 |
| `lifetime` | float | 12 | フィールドの存続秒数。0で永続。 |
| `size` | Vector3 | (2,2,2) | Box形状の幅・高さ・奥行き（ローカル単位）。 |
| `cylinderHeight` | float | 4 | Cylinder形状の高さ（ローカル単位）。 |
| `cylinderRadius` | float | 1 | Cylinder形状の半径（ローカル単位）。 |
| `accumulateDamage` | bool | true | 異極弾の衝突でダメージを蓄積するか。 |
| `maxStoredDamage` | float | 200 | 蓄積ダメージの上限。フィールド消滅時に範囲内Entityに距離減衰で放出。 |

**EffectiveOuterRadius** (読取専用プロパティ):
```csharp
public float EffectiveOuterRadius => outerRadius > 0f ? outerRadius : innerRadius * 1.3f;
```
outerRadius が未設定（0以下）の場合のフォールバック。innerの30%増しで自然な減衰。

### 9.3 BulletSettings

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `bulletSpeed` | float | 30 | 弾の飛行速度 (m/s)。Initialize 時に linearVelocity に設定。 |
| `lifetime` | float | 5 | 弾の存続秒数。unscaledDeltaTime で減算（エイム中のスロー影響を受けない）。 |
| `maxBullets` | int | 4 | リロードまでに撃てる最大弾数。shotCount ベースで管理。 |
| `bulletPrefab` | GameObject | null | 弾のプレハブ参照。 |
| `raycastDistance` | float | 200 | 射撃時のレイキャスト距離（照準判定用）。 |
| `defaultMagnetRange` | float | 5 | デフォルトの磁力範囲。 |
| `useFallbackMode` | bool | false | ONの場合、Magnetizable持ちのオブジェクトにも StickToSurface する（デバッグ用）。 |
| `bulletFieldSettings` | MagnetFieldSettings | null | 弾着弾時に生成するMagnetFieldの設定SO。 |
| `fieldAttractionFactor` | float | 5 | 飛行中の弾道曲げ強度。GetStrengthAt * この値 * dt が速度変化量。 |
| `bulletDamage` | float | 10 | 弾がフィールドに当たったときの蓄積ダメージ値。 |
| `sMaterial` | Material | null | S極弾のマテリアル（赤系）。 |
| `nMaterial` | Material | null | N極弾のマテリアル（青系）。 |
| `fireEffect_N` | GameObject | null | N極発射時エフェクトPrefab。 |
| `fireEffect_S` | GameObject | null | S極発射時エフェクトPrefab。 |
| `fireEffectScale` | float | 1.3 | 発射時エフェクトの大きさ倍率。親のlossyScaleを相殺してワールドサイズを固定。 |
| `impactEffect_N` | GameObject | null | N極着弾時エフェクトPrefab。 |
| `impactEffect_S` | GameObject | null | S極着弾時エフェクトPrefab。 |
| `impactEffectScale` | float | 1.3 | 着弾時エフェクトの大きさ倍率。 |

---

## 付録: 実行順序

```
DefaultExecutionOrder:
  -100  EntityController    (衝突制御の初期化)
  -50   MagnetManager       (磁力システムの初期化)
  -30   BulletManager       (弾管理の初期化)

フレームごとの処理順:

FixedUpdate:
  MagnetManager.FixedUpdate
    ├─ CleanupDestroyedJoints
    ├─ 破棄済み除去
    ├─ ProcessPair (全ペア)
    │   └─ Magnetizable.ApplyForce → externalVelocity に蓄積
    └─ AssignFieldsToEntities

  MagnetBullet.FixedUpdate
    └─ 弾道曲げ (linearVelocity 変更)

Update:
  Entity (サブクラス).Update
    └─ EntityStep
        ├─ UpdateGround
        ├─ ApplyGravity
        ├─ UpdateMagneticOrientation
        └─ ApplyMovement
            ├─ EntityController.Move (Collide-and-Slide)
            └─ externalVelocity = zero

  MagnetField.Update
    └─ ライフタイムカウントダウン → ForceExpire

LateUpdate:
  Magnetizable.LateUpdate
    └─ m_totalForceThisFrame = 0 (影響度リセット)

  MagnetFieldVisualizer.LateUpdate
    └─ container.rotation = identity (回転固定)
```
