using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ボス右手専用の被弾判定（振り下ろしカウンター）。
/// 振り下ろし攻撃中（AttackStance/AttackMotion）に手へ当たった時だけ Stun を発火する（スタブ可）。
/// それ以外は通常の Health ダメージのみ。
/// IHittable を実装し、GetComponentInParent&lt;IHittable&gt; で右手子コライダー側から最近祖として捕捉される。
/// スタンゲージ（よろけ）の蓄積はボス本体ヒット側（EnemyBossAI.OnBodyHit）が担当する。
/// 依存: Health, EnemyBossBaseA_Animator
/// </summary>
public sealed class ArmStunHitbox : MonoBehaviour, IHittable
{
    private const int RingSegments = 48;

    [SerializeField] private Health m_health;
    [SerializeField] private EnemyBossBaseA_Animator m_animator;
    [SerializeField] private EnemyBossAI m_bossAI;

    [SerializeField]
    [Tooltip("ヒット解決上の所属グループ。ボスなので通常 Enemy")]
    private HitGroup m_hitGroup = HitGroup.Enemy;

    [Header("Counter Cue")]
    [SerializeField]
    [Tooltip("振り下ろし攻撃中、スタン可能な右手先端に黄色い目印を表示する")]
    private bool m_showCounterCue = true;

    [SerializeField]
    [Tooltip("目印対象のTransform。未設定ならこのHitbox配下のCollider形状を使う")]
    private Transform m_counterCueTarget;

    [SerializeField]
    [Tooltip("目印の色")]
    private Color m_counterCueColor = new Color(1f, 0.86f, 0.05f, 0.95f);

    [SerializeField, FormerlySerializedAs("m_counterCueRadius"), Min(0.01f)]
    [Tooltip("Collider形状に対する表示倍率。1でHitboxと同じ大きさ")]
    private float m_counterCueShapeScale = 1.03f;

    [SerializeField, Min(0.001f)]
    [Tooltip("目印の線の太さ")]
    private float m_counterCueLineWidth = 0.055f;

    [SerializeField, Min(0f)]
    [Tooltip("目印の点滅速度。0で点滅なし")]
    private float m_counterCuePulseSpeed = 6f;

    [SerializeField, Range(0f, 0.5f)]
    [Tooltip("目印のサイズ点滅量")]
    private float m_counterCuePulseAmount = 0.08f;

    [SerializeField]
    [Tooltip("目印位置のローカルオフセット")]
    private Vector3 m_counterCueLocalOffset = Vector3.zero;

    /// <summary>所属グループ。攻撃側との比較で自傷・同士討ちを弾く。</summary>
    public HitGroup HitGroup => m_hitGroup;

    public event Action<HitData> OnHitEvent;

    private readonly List<CounterCueShape> m_counterCueShapes = new();
    private GameObject m_counterCueRoot;
    private static Material s_counterCueLineMaterial;

    private sealed class CounterCueShape
    {
        public Collider Collider;
        public GameObject Root;
        public readonly List<LineRenderer> Lines = new();
    }

    void Awake()
    {
        if (m_health == null)
            m_health = GetComponentInParent<Health>();

        if (m_animator == null)
            m_animator = GetComponentInParent<EnemyBossBaseA_Animator>();

        if (m_bossAI == null)
            m_bossAI = GetComponentInParent<EnemyBossAI>();

        if (m_health == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: Health 未取得");
        if (m_animator == null)
            ChannelLogger.LogError("EnemyBossA", $"[ArmStunHitbox] {name}: EnemyBossBaseA_Animator 未取得");

        BuildCounterCue();
        SetCounterCueVisible(false);
    }

    private void OnDisable()
    {
        SetCounterCueVisible(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < m_counterCueShapes.Count; i++)
            DestroyCueObject(m_counterCueShapes[i].Root);

        DestroyCueObject(m_counterCueRoot);
    }

    private void Update()
    {
        if (m_counterCueRoot == null && m_showCounterCue)
        {
            BuildCounterCue();
            SetCounterCueVisible(false);
        }

        bool shouldShow = ShouldShowCounterCue();
        SetCounterCueVisible(shouldShow);

        if (shouldShow)
            UpdateCounterCue();
    }

    public void OnHit(HitData hit)
    {
        if (m_health == null)
        {
            ChannelLogger.LogGuardReturn("EnemyBossA", "Health未設定");
            return;
        }

        // HPダメージは全状態で適用
        if (m_bossAI != null && m_bossAI.IsInvincibleState)
            return;

        m_health.Damage(hit.damage);

        // 振り下ろし攻撃中（AttackStance/AttackMotion）に手へ当たった＝逆極カウンター成立。
        // この時だけスタン（Stunned）を発火する。スタン中はプレイヤーがスタブを決められる。
        // IsStunned は bool なので連打されても入り直さない（旧 BeInterrupted 連打のループは起きない）。
        // スタンゲージ（よろけ）の蓄積はボス本体ヒット側（EnemyBossAI.OnBodyHit）が担当するので、ここでは触らない。
        if (m_animator != null && (m_animator.IsInAttackStance || m_animator.IsInAttackMotion))
        {
            m_animator.SetIsStunnedTrue();
            SetCounterCueVisible(false);
            ChannelLogger.Log("EnemyBossA",
                $"[ArmStunHitbox] 振り下ろしカウンター成立 → Stun src={(hit.source != null ? hit.source.name : "null")}");
        }

        OnHitEvent?.Invoke(hit);
    }

    private bool ShouldShowCounterCue()
    {
        return m_showCounterCue
            && m_animator != null
            && !m_animator.IsStunned
            && (m_animator.IsInAttackStance || m_animator.IsInAttackMotion);
    }

    private void BuildCounterCue()
    {
        if (m_counterCueRoot != null)
            return;

        m_counterCueRoot = new GameObject("ArmStunCounterCue");
        m_counterCueRoot.transform.SetParent(transform, false);
        m_counterCueRoot.transform.localPosition = Vector3.zero;
        m_counterCueRoot.transform.localRotation = Quaternion.identity;
        m_counterCueRoot.transform.localScale = Vector3.one;

        List<Collider> colliders = CollectCounterCueColliders();
        for (int i = 0; i < colliders.Count; i++)
            CreateShapeForCollider(colliders[i]);

        if (m_counterCueShapes.Count == 0)
            CreateFallbackSphereCue(transform);
    }

    private List<Collider> CollectCounterCueColliders()
    {
        Transform root = m_counterCueTarget != null ? m_counterCueTarget : transform;
        Collider[] all = root.GetComponentsInChildren<Collider>(true);

        var nonTrigger = new List<Collider>();
        var any = new List<Collider>();

        for (int i = 0; i < all.Length; i++)
        {
            Collider c = all[i];
            if (c == null)
                continue;

            any.Add(c);
            if (!c.isTrigger)
                nonTrigger.Add(c);
        }

        return nonTrigger.Count > 0 ? nonTrigger : any;
    }

    private void CreateShapeForCollider(Collider collider)
    {
        var shape = new CounterCueShape { Collider = collider };

        if (collider is BoxCollider)
            CreateBoxLines(shape, 12);
        else if (collider is CapsuleCollider)
            CreateCapsuleLines(shape);
        else if (collider is SphereCollider)
            CreateSphereLines(shape);
        else
            CreateBoxLines(shape, 12);

        m_counterCueShapes.Add(shape);
    }

    private void CreateFallbackSphereCue(Transform parent)
    {
        var fallback = new GameObject("FallbackSphereCue");
        fallback.transform.SetParent(m_counterCueRoot.transform, false);

        var shape = new CounterCueShape { Root = fallback };
        for (int i = 0; i < 3; i++)
            shape.Lines.Add(CreateLine(fallback.transform, $"Ring_{i}", RingSegments, true));

        m_counterCueShapes.Add(shape);
    }

    private void CreateBoxLines(CounterCueShape shape, int lineCount)
    {
        Transform parent = CreateColliderCueParent(shape.Collider, "BoxCue");
        shape.Root = parent.gameObject;
        for (int i = 0; i < lineCount; i++)
            shape.Lines.Add(CreateLine(parent, $"Edge_{i}", 2, false));
    }

    private void CreateCapsuleLines(CounterCueShape shape)
    {
        Transform parent = CreateColliderCueParent(shape.Collider, "CapsuleCue");
        shape.Root = parent.gameObject;

        shape.Lines.Add(CreateLine(parent, "TopRing", RingSegments, true));
        shape.Lines.Add(CreateLine(parent, "BottomRing", RingSegments, true));
        shape.Lines.Add(CreateLine(parent, "MiddleRing", RingSegments, true));

        for (int i = 0; i < 4; i++)
            shape.Lines.Add(CreateLine(parent, $"Side_{i}", 2, false));
    }

    private void CreateSphereLines(CounterCueShape shape)
    {
        Transform parent = CreateColliderCueParent(shape.Collider, "SphereCue");
        shape.Root = parent.gameObject;
        for (int i = 0; i < 3; i++)
            shape.Lines.Add(CreateLine(parent, $"Ring_{i}", RingSegments, true));
    }

    private Transform CreateColliderCueParent(Collider collider, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(collider.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private LineRenderer CreateLine(Transform parent, string name, int positionCount, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = positionCount;
        lr.widthMultiplier = m_counterCueLineWidth;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        SetupCounterCueLineMaterial(lr);

        return lr;
    }

    private void SetCounterCueVisible(bool visible)
    {
        if (m_counterCueRoot != null && m_counterCueRoot.activeSelf != visible)
            m_counterCueRoot.SetActive(visible);

        for (int i = 0; i < m_counterCueShapes.Count; i++)
        {
            CounterCueShape shape = m_counterCueShapes[i];
            if (shape.Root != null && shape.Root.activeSelf != visible)
                shape.Root.SetActive(visible);

            for (int j = 0; j < shape.Lines.Count; j++)
            {
                LineRenderer lr = shape.Lines[j];
                if (lr != null)
                    lr.enabled = visible;
            }
        }
    }

    private void UpdateCounterCue()
    {
        float pulse = 1f;
        float alphaScale = 1f;
        if (m_counterCuePulseSpeed > 0f)
        {
            float t = (Mathf.Sin(Time.time * m_counterCuePulseSpeed) + 1f) * 0.5f;
            pulse = 1f + ((t * 2f) - 1f) * m_counterCuePulseAmount;
            alphaScale = Mathf.Lerp(0.55f, 1f, t);
        }

        float shapeScale = Mathf.Max(0.01f, m_counterCueShapeScale * pulse);
        float lineWidth = Mathf.Max(0.001f, m_counterCueLineWidth);
        Color color = m_counterCueColor;
        color.a *= alphaScale;

        for (int i = 0; i < m_counterCueShapes.Count; i++)
        {
            CounterCueShape shape = m_counterCueShapes[i];
            ApplyLineStyle(shape, color, lineWidth);
            UpdateColliderShape(shape, shapeScale);
        }
    }

    private static void ApplyLineStyle(CounterCueShape shape, Color color, float lineWidth)
    {
        for (int i = 0; i < shape.Lines.Count; i++)
        {
            LineRenderer lr = shape.Lines[i];
            if (lr == null)
                continue;

            lr.widthMultiplier = lineWidth;
            lr.startColor = color;
            lr.endColor = color;
        }
    }

    private void UpdateColliderShape(CounterCueShape shape, float shapeScale)
    {
        if (shape.Collider is BoxCollider box)
            UpdateBoxShape(shape, box, shapeScale);
        else if (shape.Collider is CapsuleCollider capsule)
            UpdateCapsuleShape(shape, capsule, shapeScale);
        else if (shape.Collider is SphereCollider sphere)
            UpdateSphereShape(shape, sphere, shapeScale);
        else
            UpdateFallbackSphereShape(shape, shapeScale);
    }

    private void UpdateBoxShape(CounterCueShape shape, BoxCollider box, float shapeScale)
    {
        if (shape.Lines.Count < 12)
            return;

        Vector3 center = box.center + m_counterCueLocalOffset;
        Vector3 half = box.size * (0.5f * shapeScale);

        Vector3[] p =
        {
            center + new Vector3(-half.x, -half.y, -half.z),
            center + new Vector3( half.x, -half.y, -half.z),
            center + new Vector3( half.x, -half.y,  half.z),
            center + new Vector3(-half.x, -half.y,  half.z),
            center + new Vector3(-half.x,  half.y, -half.z),
            center + new Vector3( half.x,  half.y, -half.z),
            center + new Vector3( half.x,  half.y,  half.z),
            center + new Vector3(-half.x,  half.y,  half.z)
        };

        SetLine(shape.Lines[0], p[0], p[1]);
        SetLine(shape.Lines[1], p[1], p[2]);
        SetLine(shape.Lines[2], p[2], p[3]);
        SetLine(shape.Lines[3], p[3], p[0]);
        SetLine(shape.Lines[4], p[4], p[5]);
        SetLine(shape.Lines[5], p[5], p[6]);
        SetLine(shape.Lines[6], p[6], p[7]);
        SetLine(shape.Lines[7], p[7], p[4]);
        SetLine(shape.Lines[8], p[0], p[4]);
        SetLine(shape.Lines[9], p[1], p[5]);
        SetLine(shape.Lines[10], p[2], p[6]);
        SetLine(shape.Lines[11], p[3], p[7]);
    }

    private void UpdateCapsuleShape(CounterCueShape shape, CapsuleCollider capsule, float shapeScale)
    {
        if (shape.Lines.Count < 7)
            return;

        GetCapsuleAxes(capsule.direction, out Vector3 axis, out Vector3 u, out Vector3 v);

        Vector3 center = capsule.center + m_counterCueLocalOffset;
        float radius = Mathf.Max(0.01f, capsule.radius * shapeScale);
        float height = Mathf.Max(capsule.height * shapeScale, radius * 2f);
        float halfLine = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 top = center + axis * halfLine;
        Vector3 bottom = center - axis * halfLine;

        UpdateRing(shape.Lines[0], top, u, v, radius);
        UpdateRing(shape.Lines[1], bottom, u, v, radius);
        UpdateRing(shape.Lines[2], center, u, v, radius);

        SetLine(shape.Lines[3], top + u * radius, bottom + u * radius);
        SetLine(shape.Lines[4], top - u * radius, bottom - u * radius);
        SetLine(shape.Lines[5], top + v * radius, bottom + v * radius);
        SetLine(shape.Lines[6], top - v * radius, bottom - v * radius);
    }

    private void UpdateSphereShape(CounterCueShape shape, SphereCollider sphere, float shapeScale)
    {
        if (shape.Lines.Count < 3)
            return;

        Vector3 center = sphere.center + m_counterCueLocalOffset;
        float radius = Mathf.Max(0.01f, sphere.radius * shapeScale);
        UpdateSphereRings(shape, center, radius);
    }

    private void UpdateFallbackSphereShape(CounterCueShape shape, float shapeScale)
    {
        Vector3 center = m_counterCueLocalOffset;
        float radius = Mathf.Max(0.01f, shapeScale);
        UpdateSphereRings(shape, center, radius);
    }

    private static void UpdateSphereRings(CounterCueShape shape, Vector3 center, float radius)
    {
        if (shape.Lines.Count < 3)
            return;

        UpdateRing(shape.Lines[0], center, Vector3.right, Vector3.up, radius);
        UpdateRing(shape.Lines[1], center, Vector3.right, Vector3.forward, radius);
        UpdateRing(shape.Lines[2], center, Vector3.up, Vector3.forward, radius);
    }

    private static void UpdateRing(LineRenderer lr, Vector3 center, Vector3 u, Vector3 v, float radius)
    {
        if (lr == null)
            return;

        if (lr.positionCount != RingSegments)
            lr.positionCount = RingSegments;

        for (int i = 0; i < RingSegments; i++)
        {
            float a = (float)i / RingSegments * Mathf.PI * 2f;
            Vector3 point = center + u * (Mathf.Cos(a) * radius) + v * (Mathf.Sin(a) * radius);
            lr.SetPosition(i, point);
        }
    }

    private static void SetLine(LineRenderer lr, Vector3 a, Vector3 b)
    {
        if (lr == null)
            return;

        if (lr.positionCount != 2)
            lr.positionCount = 2;

        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    private static void GetCapsuleAxes(int direction, out Vector3 axis, out Vector3 u, out Vector3 v)
    {
        if (direction == 0)
        {
            axis = Vector3.right;
            u = Vector3.up;
            v = Vector3.forward;
            return;
        }

        if (direction == 2)
        {
            axis = Vector3.forward;
            u = Vector3.right;
            v = Vector3.up;
            return;
        }

        axis = Vector3.up;
        u = Vector3.right;
        v = Vector3.forward;
    }

    private static void SetupCounterCueLineMaterial(LineRenderer lr)
    {
        if (s_counterCueLineMaterial == null)
            s_counterCueLineMaterial = new Material(Shader.Find("Sprites/Default"));

        lr.material = s_counterCueLineMaterial;
    }

    private static void DestroyCueObject(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }
}
