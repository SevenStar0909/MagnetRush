using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全物理エンティティ（プレイヤー、敵）の基底クラス。
/// 速度（横移動・垂直・外部）、重力、移動を管理する。
/// </summary>
public abstract class Entity : MonoBehaviour, IMagnetTarget
{
    [Header("Entity Events")]
    public EntityEvents entityEvents;

    protected Rigidbody m_rb;
    protected CapsuleCollider m_capsuleCollider;
    protected EntityController m_controller;
    [HideInInspector] public Health m_health;
    protected Transform m_cachedCameraTransform;

    /// <summary>ワールド空間の速度ベクトル。</summary>
    public Vector3 velocity;

    /// <summary>外部からの力（磁力等）。蓄積型: m_externalDragで毎フレーム指数減衰する。</summary>
    public Vector3 externalVelocity;

    /// <summary>
    /// PDホルダーから受け取る速度変化。減衰しない。
    /// MagnetManager が毎 FixedUpdate 冒頭で全磁化 Entity をゼロリセットし、ProcessHold が再計算する。
    /// 非保持時は構造的に Vector3.zero になるため幽霊運動が発生しない。
    /// </summary>
    public Vector3 holdVelocity;

    /// <summary>
    /// ローカル空間の速度（transform.upとVector3.upの間で自動変換）。
    /// 斜面上でも正しく動作する。
    /// </summary>
    public Vector3 localVelocity
    {
        get => Quaternion.FromToRotation(transform.up, Vector3.up) * velocity;
        set => velocity = Quaternion.FromToRotation(Vector3.up, transform.up) * value;
    }

    /// <summary>ローカル空間のXZ速度（横移動成分）。</summary>
    public Vector3 lateralVelocity
    {
        get
        {
            var local = localVelocity;
            var value = new Vector3(local.x, 0f, local.z);
            return value.sqrMagnitude < 0.0001f ? Vector3.zero : value;
        }
        set
        {
            var local = localVelocity;
            localVelocity = new Vector3(value.x, local.y, value.z);
        }
    }

    /// <summary>ローカル空間のY速度（垂直成分）。</summary>
    public float verticalVelocity
    {
        get => localVelocity.y;
        set
        {
            var local = localVelocity;
            local.y = value;
            localVelocity = local;
        }
    }

    public bool IsGrounded { get; protected set; }

    // --- 地面情報（斜面対応） ---
    public RaycastHit groundHit { get; protected set; }
    public float groundAngle { get; protected set; }
    public Vector3 groundNormal { get; protected set; } = Vector3.up;
    public Vector3 localSlopeDirection { get; protected set; }

    protected readonly float m_slopingGroundAngle = 20f;

    // --- 磁力場 ---
    /// <summary>現在支配的な磁力場。MagnetManagerが毎フレーム設定する。</summary>
    public IMagnetField magnetField { get; set; }

    // --- 外部変調用マルチプライヤー（磁力場・エリア効果等） ---
    public float topSpeedMultiplier { get; set; } = 1f;
    public float turningDragMultiplier { get; set; } = 1f;
    public float decelerationMultiplier { get; set; } = 1f;

    // --- 重力・移動・接地設定 ---
    // サブクラスでSO値を返すように override する。デフォルト値はフォールバック用。

    /// <summary>重力加速度 (m/s²)。負の値で下向き。</summary>
    protected virtual float Gravity => -20f;

    /// <summary>地面への押し付け力。接地時に verticalVelocity に適用される。</summary>
    protected virtual float SnapForce => 2f;

    /// <summary>外部力の指数減衰率。Rigidbody.dragに相当。大きいほど早く減速する。</summary>
    protected virtual float ExternalDrag => 3f;

    /// <summary>接地判定対象レイヤー。</summary>
    protected virtual LayerMask GroundLayer => -1;

    /// <summary>接地判定の追加レイ距離。</summary>
    protected virtual float GroundCheckDistance => 0.3f;

    /// <summary>磁力で引かれているとき向き直す速度の閾値。</summary>
    protected virtual float PullOrientationThreshold => 5f;

    /// <summary>磁力で引かれているときの向き直し速度。</summary>
    protected virtual float PullOrientationSpeed => 8f;

    // --- 汎用プロパティ ---

    /// <summary>Entityのカプセル高さ。m_controller > m_capsuleCollider > 2f の優先順で取得。</summary>
    public float height => m_controller != null ? m_controller.height
        : (m_capsuleCollider != null ? m_capsuleCollider.height : 2f);

    /// <summary>Entityのカプセル半径。m_controller > m_capsuleCollider > 0.5f の優先順で取得。</summary>
    public float radius => m_controller != null ? m_controller.radius
        : (m_capsuleCollider != null ? m_capsuleCollider.radius : 0.5f);

    /// <summary>transform.upを基準にしたローカル空間の前方向。斜面/磁力回転時も正しい方向を返す。</summary>
    public virtual Vector3 localForward =>
        Quaternion.FromToRotation(transform.up, Vector3.up) * transform.forward;

    /// <summary>transform.upを基準にしたローカル空間の右方向。</summary>
    public virtual Vector3 localRight =>
        Quaternion.FromToRotation(transform.up, Vector3.up) * transform.right;

    /// <summary>
    /// Collider中心のワールド座標。足元ではなくカプセルの中心。
    /// Platformer Projectでは`position`だが、transform.positionと紛らわしいので`centerPosition`。
    /// </summary>
    public Vector3 centerPosition
    {
        get
        {
            Vector3 c = m_controller != null ? m_controller.center
                      : (m_capsuleCollider != null ? m_capsuleCollider.center : Vector3.zero);
            return transform.position + transform.rotation * c;
        }
        set
        {
            Vector3 c = m_controller != null ? m_controller.center
                      : (m_capsuleCollider != null ? m_capsuleCollider.center : Vector3.zero);
            transform.position = value - transform.rotation * c;
        }
    }

    // --- 汎用Physicsクエリ ---

    protected Collider[] m_contactBuffer = new Collider[10];
    private readonly List<IEntityContact> m_listenerCache = new();

    /// <summary>Entity形状でCapsuleCast。hitが不要な場合用。</summary>
    public virtual bool CapsuleCast(Vector3 direction, float distance,
        int layer = 0, QueryTriggerInteraction trigger = QueryTriggerInteraction.Ignore)
    {
        return CapsuleCast(direction, distance, out _, layer, trigger);
    }

    /// <summary>Entity形状でCapsuleCast。layer=0は未指定（PhysicsLayers.MaskEntityCollisionを使用）。</summary>
    public virtual bool CapsuleCast(Vector3 direction, float distance, out RaycastHit hit,
        int layer = 0, QueryTriggerInteraction trigger = QueryTriggerInteraction.Ignore)
    {
        if (layer == 0) layer = PhysicsLayers.MaskEntityCollision;
        var origin = centerPosition - direction * radius;
        var offset = transform.up * (height * 0.5f - radius);
        return Physics.CapsuleCast(origin + offset, origin - offset, radius,
            direction, out hit, distance + radius, layer, trigger);
    }

    /// <summary>Entity位置でOverlapCapsule。接触判定に使用。</summary>
    public virtual int OverlapEntity(Collider[] result, float skinOffset = 0)
    {
        var overlapRadius = radius + skinOffset;
        var offset = (height + skinOffset) * 0.5f - radius;
        var top = centerPosition + transform.up * offset;
        var bottom = centerPosition - transform.up * offset;
        return Physics.OverlapCapsuleNonAlloc(top, bottom, overlapRadius,
            result, PhysicsLayers.MaskEntityCollision, QueryTriggerInteraction.Ignore);
    }

    protected virtual void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_capsuleCollider = GetComponent<CapsuleCollider>();
        m_controller = GetComponent<EntityController>();
        m_health = GetComponent<Health>();

        // EntityControllerがない場合のみフォールバック（敵のNavMesh等）
        if (m_controller == null && m_rb != null)
        {
            m_rb.isKinematic = true;
            m_rb.useGravity = false;
        }

        var mainCam = Camera.main;
        if (mainCam != null)
            m_cachedCameraTransform = mainCam.transform;
    }

    /// <summary>
    /// 共通物理ステップ: 接地判定 → 重力 → 移動。サブクラスのUpdate()から呼ぶ。
    /// </summary>
    protected void UpdateEntity(float dt)
    {
        UpdateGround();
        HandleCeiling();
        ApplyGravity(dt);
        UpdateMagneticOrientation(dt);
        ApplyMovement(dt);
        HandleContacts();
    }

    /// <summary>
    /// 接触中のColliderからIEntityContactを取得し、OnEntityContactを発火する。
    /// Entity本体を変更せずにギミック反応を定義できる。
    /// </summary>
    protected void HandleContacts()
    {
        float skinOffset = m_controller != null ? m_controller.skinWidth + Physics.defaultContactOffset : 0.05f;
        int overlaps = OverlapEntity(m_contactBuffer, skinOffset);

        for (int i = 0; i < overlaps; i++)
        {
            if (m_contactBuffer[i].transform == transform) continue;

            m_contactBuffer[i].GetComponents(m_listenerCache);
            foreach (var listener in m_listenerCache)
                listener.OnEntityContact(this);
        }
    }

    /// <summary>
    /// 上昇中に天井にぶつかったら垂直速度をゼロにする。
    /// EntityControllerがある場合はMoveAndSlideが処理するため不要。
    /// </summary>
    protected void HandleCeiling()
    {
        if (m_controller != null) { ChannelLogger.LogGuardReturn("Entity", "EntityController有り(MoveAndSlideが処理)"); return; }
        if (verticalVelocity <= 0f) { ChannelLogger.LogGuardReturn("Entity", "上昇していない"); return; }

        float maxCeilingDist = height * 0.5f + 0.1f;
        if (CapsuleCast(transform.up, maxCeilingDist, out _, PhysicsLayers.MaskGroundCheck))
        {
            verticalVelocity = 0f;
        }
    }

    /// <summary>
    /// 全速度成分を位置に適用する。EntityControllerがあればCollide-and-Slideで壁衝突を処理する。
    /// </summary>
    protected virtual void ApplyMovement(float dt)
    {
        Vector3 motion = (velocity + externalVelocity + holdVelocity) * dt;

        if (m_controller != null)
        {
            transform.position = m_controller.Move(transform.position, motion);
        }
        else
        {
            transform.position += motion;
        }

        // 指数減衰（フレームレート非依存。Rigidbody.dragと同等の自然な減速曲線）
        if (externalVelocity.sqrMagnitude > 0.01f)
            externalVelocity *= Mathf.Exp(-ExternalDrag * dt);
        else
            externalVelocity = Vector3.zero;
    }

    /// <summary>
    /// 重力を適用する。接地時は地面にスナップする。
    /// </summary>
    protected void ApplyGravity(float dt)
    {
        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -SnapForce;
        }
        else
        {
            verticalVelocity += Gravity * dt;
        }
    }

    /// <summary>
    /// 指定方向への横移動速度を加速する（turningDragで滑らかな方向転換）。
    /// Platformer ProjectのAccelerateパターンを採用。
    /// </summary>
    protected void Accelerate(Vector3 direction, float turningDrag, float acceleration, float topSpeed, float dt)
    {
        if (direction.sqrMagnitude < 0.01f) { ChannelLogger.LogGuardReturn("Entity", "入力方向ゼロ"); return; }

        direction.Normalize();

        var effectiveTopSpeed = topSpeed * topSpeedMultiplier;
        var effectiveTurningDrag = turningDrag * turningDragMultiplier;

        // 速度を「進行方向成分」と「横方向成分」に分解
        float speed = Vector3.Dot(direction, lateralVelocity);
        Vector3 turningVelocity = lateralVelocity - direction * speed;

        // 横方向成分を減衰（滑らかな方向転換）
        float turningDelta = effectiveTurningDrag * dt;
        turningVelocity = Vector3.MoveTowards(turningVelocity, Vector3.zero, turningDelta);

        // 進行方向に加速
        if (lateralVelocity.magnitude < effectiveTopSpeed || speed < 0f)
        {
            speed += acceleration * dt;
            speed = Mathf.Clamp(speed, -effectiveTopSpeed, effectiveTopSpeed);
        }

        lateralVelocity = direction * speed + turningVelocity;
    }

    /// <summary>
    /// 横移動速度をゼロに向けて減速する。
    /// </summary>
    protected void Decelerate(float deceleration, float dt)
    {
        float delta = deceleration * decelerationMultiplier * dt;
        lateralVelocity = Vector3.MoveTowards(lateralVelocity, Vector3.zero, delta);
    }

    /// <summary>
    /// 地面情報（法線、角度、斜面方向）を更新する。
    /// </summary>
    protected void UpdateGround()
    {
        bool wasGrounded = IsGrounded;
        float groundCheckDist = height * 0.5f + GroundCheckDistance;

        if (Physics.Raycast(transform.position, -transform.up, out var hit, groundCheckDist,
            GroundLayer, QueryTriggerInteraction.Ignore))
        {
            float footDist = hit.distance - height * 0.5f;
            IsGrounded = footDist < 0.1f;

            if (IsGrounded)
            {
                groundHit = hit;
                groundNormal = hit.normal;
                groundAngle = Vector3.Angle(Vector3.up, hit.normal);
                localSlopeDirection = new Vector3(groundNormal.x, 0f, groundNormal.z).normalized;
            }
        }
        else
        {
            IsGrounded = false;
        }

        if (!IsGrounded)
        {
            groundAngle = 0f;
            groundNormal = Vector3.up;
            localSlopeDirection = Vector3.zero;
        }

        // 接地遷移イベント
        if (IsGrounded && !wasGrounded) entityEvents?.onGroundEnter?.Invoke();
        if (!IsGrounded && wasGrounded) entityEvents?.onGroundExit?.Invoke();
    }

    /// <summary>
    /// 現在の地面が斜面かどうかを返す。
    /// </summary>
    protected virtual bool OnSlopingGround()
    {
        return IsGrounded && groundAngle > m_slopingGroundAngle;
    }

    /// <summary>
    /// 斜面での加減速を適用する。上り坂は減速、下り坂は加速。
    /// </summary>
    protected void SlopeFactor(float upwardForce, float downwardForce, float dt)
    {
        if (!IsGrounded || !OnSlopingGround()) { ChannelLogger.LogGuardReturn("Entity", "非接地 or 斜面でない"); return; }

        var factor = Vector3.Dot(Vector3.up, groundNormal);
        var downwards = Vector3.Dot(localSlopeDirection, lateralVelocity) > 0;
        var multiplier = downwards ? downwardForce : upwardForce;
        var delta = factor * multiplier * dt;
        lateralVelocity += localSlopeDirection * delta;
    }

    /// <summary>
    /// Slerpを使用してエンティティを指定方向に回転させる。
    /// adjustUp=trueの場合、ローカル空間の方向をtransform.upに合わせて変換する。
    /// </summary>
    protected void FaceDirection(Vector3 direction, float rotationSpeed, float dt, bool adjustUp = true)
    {
        if (direction.sqrMagnitude < 0.01f) { ChannelLogger.LogGuardReturn("Entity", "回転方向ゼロ"); return; }

        if (adjustUp)
            direction = Quaternion.FromToRotation(Vector3.up, transform.up) * direction;

        Quaternion targetRotation = Quaternion.LookRotation(direction, transform.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * dt
        );
    }

    /// <summary>
    /// 強い磁力を受けて空中にいるとき、引力/斥力方向にプレイヤーの正面を向ける。
    /// 接地中は通常の移動方向回転を維持する。
    /// </summary>
    protected virtual void UpdateMagneticOrientation(float dt)
    {
        if (IsGrounded) { ChannelLogger.LogGuardReturn("Entity", "接地中は通常回転を維持"); return; }
        if (externalVelocity.sqrMagnitude < PullOrientationThreshold * PullOrientationThreshold) { ChannelLogger.LogGuardReturn("Entity", "磁力が閾値未満"); return; }

        // 水平方向のみ回転（Y軸回転のみ）。上下に傾けるとtransform.upが狂い重力計算が破綻する
        Vector3 horizontalDir = new Vector3(externalVelocity.x, 0f, externalVelocity.z);
        if (horizontalDir.sqrMagnitude > 0.01f)
            FaceDirection(horizontalDir.normalized, PullOrientationSpeed, dt);
    }

    /// <summary>
    /// IMagnetTarget���装。磁力を加速度として蓄積する（AddForce相当）。
    /// 実際の減衰はApplyMovement内のm_externalDragが処理する。
    /// </summary>
    public void ApplyMagnetForce(Vector3 force)
    {
        externalVelocity += force * Time.deltaTime;
    }

    /// <summary>
    /// 2D入力からカメラ相対の移動方向を取得する。
    /// </summary>
    protected Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        return GetCameraRelativeDirection(input, out _);
    }

    /// <summary>
    /// 2D入力からカメラ相対の移動方向を取得する（ProjectOnPlane + localSpace変換）。
    /// magnitudeには正規化前の大きさが出力される（アナログ入力の強度）。
    /// Platformer ProjectのGetMovementCameraDirectionパターンを採用。
    /// </summary>
    protected Vector3 GetCameraRelativeDirection(Vector2 input, out float magnitude, bool localSpace = true)
    {
        magnitude = 0f;
        if (m_cachedCameraTransform == null || input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        // 2D入力を3D方向に変換
        Vector3 direction = new Vector3(input.x, 0f, input.y);

        // カメラのupをエンティティのupに合わせてから回転適用
        var rotation = Quaternion.FromToRotation(m_cachedCameraTransform.up, transform.up);
        direction = rotation * m_cachedCameraTransform.rotation * direction;

        if (localSpace)
        {
            // エンティティの接地面に投影し、ローカル空間に変換
            direction = Vector3.ProjectOnPlane(direction, transform.up);
            direction = Quaternion.FromToRotation(transform.up, Vector3.up) * direction;
        }

        magnitude = direction.magnitude;

        if (magnitude > 0.001f)
            direction /= magnitude;
        else
            return Vector3.zero;

        return direction;
    }
}
