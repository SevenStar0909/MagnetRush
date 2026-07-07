using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// ボス専用の12角形アリーナゲート。
/// EnemyArenaGate と同じく BoxCollider を範囲参照に使い、プレイヤーが踏み込むと封鎖する。
/// 対象ボスを倒すと壁を解除する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class EnemyBossArenaGate : MonoBehaviour
{
    private const string kLogPrefix = "[boss gate]";

    private enum ArenaState { Idle, Sealed, Cleared }

    private const int k_SideCount = 12;
    private const int k_GridSize = 64;

    [Header("範囲")]
    [Tooltip("12角形ボスアリーナの外接範囲を表す BoxCollider（トリガー扱い・形状参照専用）")]
    [SerializeField] private BoxCollider m_areaBox;
    [Tooltip("閉じ込める対象（プレイヤー）を探すタグ")]
    [SerializeField] private string m_playerTag = GameTags.Player;
    [Tooltip("封鎖が作動する内側マージン(m)。壁は範囲の縁に出るが、プレイヤーがこのぶん内側に入ってから封鎖する")]
    [SerializeField] private float m_sealTriggerInset = 3f;
    [Tooltip("12角形の向き調整。ボス部屋の床形状に合わせる")]
    [SerializeField] private float m_rotationOffsetDegrees;

    [Header("対象のボス")]
    [Tooltip("倒す対象のボス。未設定なら封鎖時に範囲内の EnemyBossBase を自動検索する")]
    [SerializeField] private EnemyBossBase m_boss;
    [Tooltip("ON: 封鎖した瞬間に範囲内のボスを自動で対象にする")]
    [SerializeField] private bool m_autoFindBossInArea = true;

    [Header("ボス起動")]
    [Tooltip("ON: このゲートが封鎖されるまでボスAIを停止し、封鎖開始と同時に動かす")]
    [SerializeField] private bool m_disableBossAIUntilSealed = true;
    [SerializeField] private bool m_enableBossAIOnSealed = true;
    [Tooltip("停止・起動するボスAI。未設定なら対象ボスから自動取得する")]
    [SerializeField] private EnemyBossAI m_bossAI;

    [Header("壁（フォースフィールド）")]
    [Tooltip("ON: 12角形の壁を自前で生成する。OFF: 下の barriers だけを使う")]
    [SerializeField] private bool m_autoGenerateWalls = true;
    [Tooltip("封鎖中だけ表示する既製の壁オブジェクト(任意)。自前生成と併用される")]
    [SerializeField] private GameObject[] m_barriers;
    [Tooltip("壁の見た目に使うマテリアル(任意)。未指定なら加算半透明のフォースフィールドを自動生成する")]
    [SerializeField] private Material m_wallMaterial;
    [Tooltip("壁の色。明るい色ほど強く光って見える")]
    [SerializeField] private Color m_wallColor = new Color(0.25f, 0.8f, 1f, 0.7f);
    [Tooltip("壁の高さ(m)")]
    [SerializeField] private float m_wallHeight = 6f;
    [Tooltip("壁の厚み(m)")]
    [SerializeField] private float m_wallThickness = 0.3f;
    [Tooltip("隣の壁と少し重ねて角のすき間を消す長さ(m)")]
    [SerializeField] private float m_wallSegmentOverlap = 0.25f;
    [Tooltip("プレイヤーと敵を閉じ込める当たり判定の壁レイヤー名")]
    [SerializeField] private string m_wallLayerName = GameTags.Wall;

    [Header("イベント")]
    [Tooltip("封鎖した瞬間に呼ぶ（BGM切替・カメラ演出など）")]
    [SerializeField] private UnityEvent m_onSealed;
    [Tooltip("ボスを倒して壁を解除した瞬間に呼ぶ（クリア演出・扉を開けるなど）")]
    [SerializeField] private UnityEvent m_onCleared;

    /// <summary>プレイヤーが踏み込み、壁で封鎖した瞬間に発火する。</summary>
    public event Action Sealed;
    /// <summary>ボスを倒し、壁を解除した瞬間に発火する。</summary>
    public event Action Cleared;

    /// <summary>封鎖中かどうか。</summary>
    public bool IsSealed => m_state == ArenaState.Sealed;
    /// <summary>既に攻略済みかどうか。</summary>
    public bool IsCleared => m_state == ArenaState.Cleared;

    private readonly System.Collections.Generic.List<GameObject> m_generatedWalls = new();

    private ArenaState m_state = ArenaState.Idle;
    private Transform m_player;
    private bool m_playerWasOutside;
    private Health m_bossHealth;
    private Action m_bossDeathHandler;
    private bool m_bossDefeated;
    private Material m_wallMaterialInstance;
    private Texture2D m_gridTexture;
    private float m_animTime;

    private void Awake()
    {
        if (m_areaBox == null)
            m_areaBox = GetComponent<BoxCollider>();

        // 範囲形状は当たり判定を持たせない。トリガーにしておき、ポーリングで内外判定だけに使う
        if (m_areaBox != null)
            m_areaBox.isTrigger = true;

        PrepareBossAI();
    }

    private void Update()
    {
        switch (m_state)
        {
            case ArenaState.Idle:
                DetectPlayer();
                break;

            case ArenaState.Sealed:
                AnimateWalls(Time.deltaTime);
                if (m_bossDefeated || m_bossHealth == null || m_bossHealth.IsDead)
                    Open();
                break;
        }
    }

    private void DetectPlayer()
    {
        if (m_areaBox == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyBossArenaGate: areaBox 未設定"); return; }

        if (m_player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(m_playerTag);
            if (go == null) return;
            m_player = go.transform;
        }

        bool inside = ContainsPoint(m_player.position, m_sealTriggerInset);

        // EnemyArenaGate と同じく、外→中に踏み込んだ瞬間だけ封鎖する
        if (!inside)
        {
            m_playerWasOutside = true;
            return;
        }

        if (!m_playerWasOutside)
            return;

        Seal();
    }

    private void Seal()
    {
        Debug.Log($"{kLogPrefix} seal start", this);
        ResolveBoss();
        Debug.Log($"{kLogPrefix} resolve boss result boss={(m_boss != null ? m_boss.name : "null")} health={(m_bossHealth != null)}", this);

        if (m_bossHealth == null)
        {
            ChannelLogger.Log("Enemy", "EnemyBossArenaGate: 範囲内にボスがいないため封鎖せず解放");
            m_state = ArenaState.Cleared;
            return;
        }

        m_state = ArenaState.Sealed;
        SubscribeBoss();
        Debug.Log($"{kLogPrefix} sealed boss={(m_boss != null ? m_boss.name : "null")} autoEnableAI={m_disableBossAIUntilSealed && m_enableBossAIOnSealed}", this);
     //   if (m_disableBossAIUntilSealed && m_enableBossAIOnSealed)
     //       SetBossAIEnabled(true);
        EnableBarriers(true);
        ChannelLogger.Log("Enemy", $"EnemyBossArenaGate: 封鎖開始 対象ボス={m_boss.name}");
        m_onSealed?.Invoke();
        Sealed?.Invoke();
    }

    private void Open()
    {
        if (m_state == ArenaState.Cleared)
            return;

        m_state = ArenaState.Cleared;
        UnsubscribeBoss();
        EnableBarriers(false);
        ChannelLogger.Log("Enemy", "EnemyBossArenaGate: ボス撃破 → 壁を解除");
        m_onCleared?.Invoke();
        Cleared?.Invoke();
    }

    private void PrepareBossAI()
    {
        if (!m_disableBossAIUntilSealed)
            return;

        EnemyBossAI bossAI = ResolveBossAI();
        Debug.Log($"{kLogPrefix} SetBossAIEnabled({enabled}) resolve bossAI={(bossAI != null)}", this);
        if (bossAI == null)
            return;

     //   SetBossAIEnabled(false);
    }

    private void ResolveBoss()
    {
        UnsubscribeBoss();
        m_bossDefeated = false;

        if (TryTrackBoss(m_boss))
            return;

        if (!m_autoFindBossInArea)
            return;

        EnemyBossBase closest = FindClosestBossInArea(requireAlive: true);

        if (closest != null)
            m_boss = closest;
        TryTrackBoss(m_boss);
    }

    private EnemyBossBase FindClosestBossInArea(bool requireAlive)
    {
        EnemyBossBase[] bosses = FindObjectsByType<EnemyBossBase>(FindObjectsSortMode.None);
        EnemyBossBase closest = null;
        float closestSqr = float.MaxValue;
        Vector3 center = transform.TransformPoint(m_areaBox.center);

        for (int i = 0; i < bosses.Length; i++)
        {
            EnemyBossBase boss = bosses[i];
            if (boss == null || !ContainsPoint(boss.transform.position, 0f))
                continue;

            if (requireAlive)
            {
                Health health = boss.GetComponent<Health>();
                if (health == null || health.IsDead)
                    continue;
            }

            float sqr = (boss.transform.position - center).sqrMagnitude;
            if (sqr >= closestSqr)
                continue;

            closestSqr = sqr;
            closest = boss;
        }

        return closest;
    }

    private EnemyBossAI ResolveBossAI()
    {
        if (m_bossAI != null)
            return m_bossAI;

        if (m_boss == null && m_autoFindBossInArea)
            m_boss = FindClosestBossInArea(requireAlive: false);

        if (m_boss != null)
            m_bossAI = m_boss.GetComponent<EnemyBossAI>();

        return m_bossAI;
    }

    //private void SetBossAIEnabled(bool enabled)
    //{
    //    EnemyBossAI bossAI = ResolveBossAI();
    //    if (bossAI == null)
    //        return;
    //
    //    if (bossAI.enabled == enabled)
    //        return;
    //
    //    bossAI.enabled = enabled;
    //    Debug.Log($"{kLogPrefix} bossAI.enabled = {enabled}", bossAI);
    //    ChannelLogger.Log("Enemy", $"EnemyBossArenaGate: ボスAI {(enabled ? "起動" : "停止")}");
    //}

    private bool TryTrackBoss(EnemyBossBase boss)
    {
        if (boss == null)
            return false;

        Health health = boss.GetComponent<Health>();
        if (health == null || health.IsDead)
            return false;

        m_bossHealth = health;
        return true;
    }

    private void SubscribeBoss()
    {
        if (m_bossHealth == null || m_bossDeathHandler != null)
            return;

        m_bossDeathHandler = MarkBossDefeated;
        m_bossHealth.OnDie += m_bossDeathHandler;
    }

    private void UnsubscribeBoss()
    {
        if (m_bossHealth != null && m_bossDeathHandler != null)
            m_bossHealth.OnDie -= m_bossDeathHandler;

        m_bossDeathHandler = null;
        m_bossHealth = null;
    }

    private void MarkBossDefeated()
    {
        m_bossDefeated = true;
    }

    private bool ContainsPoint(Vector3 worldPoint, float inset)
    {
        if (m_areaBox == null)
            return false;

        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 c = m_areaBox.center;
        Vector3 s = m_areaBox.size;

        float halfY = s.y * 0.5f;
        if (local.y < c.y - halfY || local.y > c.y + halfY)
            return false;

        float halfX = Mathf.Max(0.1f, s.x * 0.5f - Mathf.Clamp(inset, 0f, s.x * 0.5f - 0.5f));
        float halfZ = Mathf.Max(0.1f, s.z * 0.5f - Mathf.Clamp(inset, 0f, s.z * 0.5f - 0.5f));
        Vector3 p = local - c;

        for (int i = 0; i < k_SideCount; i++)
        {
            Vector3 a = GetLocalVertex(i, halfX, halfZ);
            Vector3 b = GetLocalVertex((i + 1) % k_SideCount, halfX, halfZ);
            Vector3 edge = b - a;
            Vector3 toPoint = p - a;
            float cross = edge.x * toPoint.z - edge.z * toPoint.x;

            if (cross < -0.001f)
                return false;
        }

        return true;
    }

    private Vector3 GetLocalVertex(int index, float halfX, float halfZ)
    {
        float angle = (m_rotationOffsetDegrees + (360f * index / k_SideCount)) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle) * halfX, 0f, Mathf.Sin(angle) * halfZ);
    }

    private void EnableBarriers(bool on)
    {
        if (m_barriers != null)
        {
            for (int i = 0; i < m_barriers.Length; i++)
                if (m_barriers[i] != null)
                    m_barriers[i].SetActive(on);
        }

        if (!m_autoGenerateWalls)
            return;

        if (on && m_generatedWalls.Count == 0)
            GenerateWalls();

        for (int i = 0; i < m_generatedWalls.Count; i++)
            if (m_generatedWalls[i] != null)
                m_generatedWalls[i].SetActive(on);
    }

    private void GenerateWalls()
    {
        if (m_areaBox == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyBossArenaGate: areaBox 未設定で壁生成不可"); return; }

        EnsureMaterial();
        int wallLayer = LayerMask.NameToLayer(m_wallLayerName);

        Vector3 c = m_areaBox.center;
        Vector3 s = m_areaBox.size;
        float halfX = s.x * 0.5f;
        float halfZ = s.z * 0.5f;
        float floorY = c.y - s.y * 0.5f;
        float wallY = floorY + m_wallHeight * 0.5f;

        for (int i = 0; i < k_SideCount; i++)
        {
            Vector3 a = c + GetLocalVertex(i, halfX, halfZ);
            Vector3 b = c + GetLocalVertex((i + 1) % k_SideCount, halfX, halfZ);
            a.y = wallY;
            b.y = wallY;
            CreateWall($"BossArenaWall_{i + 1:00}", a, b, wallLayer);
        }
    }

    private void CreateWall(string wallName, Vector3 localA, Vector3 localB, int layer)
    {
        Vector3 edge = localB - localA;
        Vector3 edgeDir = edge.normalized;
        Vector3 outward = new Vector3(edgeDir.z, 0f, -edgeDir.x);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = wallName;
        if (layer >= 0)
            go.layer = layer;

        Transform tf = go.transform;
        tf.SetParent(transform, false);
        tf.localRotation = Quaternion.LookRotation(outward, Vector3.up);
        tf.localPosition = (localA + localB) * 0.5f;
        tf.localScale = new Vector3(edge.magnitude + m_wallSegmentOverlap, m_wallHeight, m_wallThickness);

        MeshRenderer rend = go.GetComponent<MeshRenderer>();
        rend.sharedMaterial = m_wallMaterialInstance;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;

        go.SetActive(false);
        m_generatedWalls.Add(go);
    }

    private void EnsureMaterial()
    {
        if (m_wallMaterialInstance != null)
            return;

        Material baseMat = m_wallMaterial;
        if (baseMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            baseMat = new Material(sh);
        }

        m_wallMaterialInstance = new Material(baseMat);
        ConfigureForceField(m_wallMaterialInstance);
    }

    // EnemyArenaGate と同じフォースフィールド調の見た目にする。
    private void ConfigureForceField(Material m)
    {
        if (m_gridTexture == null)
            m_gridTexture = BuildGridTexture();

        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;

        m.color = m_wallColor;
        m.mainTexture = m_gridTexture;
        m.mainTextureScale = new Vector2(4f, 2f);
    }

    private Texture2D BuildGridTexture()
    {
        var tex = new Texture2D(k_GridSize, k_GridSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[k_GridSize * k_GridSize];
        int mid = k_GridSize / 2;
        for (int y = 0; y < k_GridSize; y++)
        {
            for (int x = 0; x < k_GridSize; x++)
            {
                bool line = x < 2 || x >= k_GridSize - 2 || y < 2 || y >= k_GridSize - 2
                            || (x >= mid - 1 && x <= mid) || (y >= mid - 1 && y <= mid);
                byte a = (byte)(line ? 255 : 28);
                px[y * k_GridSize + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private void AnimateWalls(float dt)
    {
        if (m_wallMaterialInstance == null)
            return;

        m_animTime += dt;
        m_wallMaterialInstance.mainTextureOffset = new Vector2(0f, -m_animTime * 0.25f);

        float pulse = 0.78f + 0.22f * Mathf.Sin(m_animTime * 3f);
        Color c = m_wallColor;
        c.a = m_wallColor.a * pulse;
        m_wallMaterialInstance.color = c;
    }

    private void OnDestroy()
    {
        UnsubscribeBoss();
        if (m_wallMaterialInstance != null) Destroy(m_wallMaterialInstance);
        if (m_gridTexture != null) Destroy(m_gridTexture);
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = m_areaBox != null ? m_areaBox : GetComponent<BoxCollider>();
        if (box == null)
            return;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        DrawPolygonGizmo(box.center, box.size, 0f, new Color(0.25f, 0.8f, 1f, 0.9f));
        DrawPolygonGizmo(box.center, box.size, m_sealTriggerInset, new Color(1f, 0.85f, 0.1f, 0.9f));

        Gizmos.matrix = prev;
    }

    private void DrawPolygonGizmo(Vector3 center, Vector3 size, float inset, Color color)
    {
        Gizmos.color = color;

        float halfX = Mathf.Max(0.1f, size.x * 0.5f - Mathf.Clamp(inset, 0f, size.x * 0.5f - 0.5f));
        float halfZ = Mathf.Max(0.1f, size.z * 0.5f - Mathf.Clamp(inset, 0f, size.z * 0.5f - 0.5f));

        for (int i = 0; i < k_SideCount; i++)
        {
            Vector3 a = center + GetLocalVertex(i, halfX, halfZ);
            Vector3 b = center + GetLocalVertex((i + 1) % k_SideCount, halfX, halfZ);
            Gizmos.DrawLine(a, b);
        }
    }
}
