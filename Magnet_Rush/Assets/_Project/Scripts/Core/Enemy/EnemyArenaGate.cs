using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// 範囲内の敵を全滅させるまでプレイヤーを閉じ込める正方形アリーナ。
/// プレイヤーが範囲に踏み込むと四方をフォースフィールド壁で封鎖し、範囲内の敵(Health持ち)を
/// 全部倒すと壁を解除する。倒すべき敵は封鎖した瞬間に範囲内から自動収集する（手動指定も併用可）。
///
/// プレイヤー検出はトリガーではなく BoxCollider.bounds.Contains ポーリングで行う。
/// プレイヤーは kinematic + autoSync off で動くため、静的トリガーの OnTriggerEnter が発火しないため。
///
/// 壁は「見た目（加算半透明のフォースフィールド）」と「当たり判定（Wall 層の実コライダー）」を
/// 同じ Cube に持たせて生成する。見た目は封じ込め(Wall層)とは独立に調整できる。
///
/// 依存: BoxCollider(範囲形状), Health, EnemyBossBase(除外用), GameTags.Player
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class EnemyArenaGate : MonoBehaviour
{
    private enum ArenaState { Idle, Sealed, Cleared }

    private const int k_GridSize = 64;

    [Header("範囲")]
    [Tooltip("正方形アリーナの範囲を表す BoxCollider（トリガー扱い・形状参照専用）")]
    [SerializeField] private BoxCollider m_areaBox;
    [Tooltip("閉じ込める対象（プレイヤー）を探すタグ")]
    [SerializeField] private string m_playerTag = GameTags.Player;
    [Tooltip("封鎖が作動する内側マージン(m)。壁は範囲の縁に出るが、プレイヤーがこのぶん内側に入ってから封鎖する。0だと縁で封鎖されて出現した壁に押し出される")]
    [SerializeField] private float m_sealTriggerInset = 3f;

    [Header("対象の敵")]
    [Tooltip("ON: 封鎖した瞬間に範囲内の敵(Health)を自動で対象にする。OFF: 下の手動リストだけを対象にする")]
    [SerializeField] private bool m_autoCollectEnemies = true;
    [Tooltip("手動で対象に含める敵(任意)。自動収集と併用される")]
    [SerializeField] private List<Health> m_manualEnemies = new();
    [Tooltip("ON: タレットと神風(自爆)は全滅条件から除外する。それ以外の敵を全部倒した時点で壁が解除される")]
    [SerializeField] private bool m_ignoreTurretsAndKamikaze = true;

    [Header("壁（フォースフィールド）")]
    [Tooltip("ON: 四方の壁を自前で生成する。OFF: 下の barriers だけを使う")]
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
    [Tooltip("プレイヤーと敵を閉じ込める当たり判定の壁レイヤー名")]
    [SerializeField] private string m_wallLayerName = GameTags.Wall;

    [Header("イベント")]
    [Tooltip("封鎖した瞬間に呼ぶ（BGM切替・カメラ演出など）")]
    [SerializeField] private UnityEvent m_onSealed;
    [Tooltip("全滅して壁を解除した瞬間に呼ぶ（扉を開ける・宝箱を出すなど）")]
    [SerializeField] private UnityEvent m_onCleared;

    /// <summary>プレイヤーが踏み込み、壁で封鎖した瞬間に発火する。</summary>
    public event Action Sealed;
    /// <summary>範囲内の敵を全滅させ、壁を解除した瞬間に発火する。</summary>
    public event Action Cleared;

    /// <summary>封鎖中かどうか。</summary>
    public bool IsSealed => m_state == ArenaState.Sealed;
    /// <summary>既に攻略（全滅）済みかどうか。</summary>
    public bool IsCleared => m_state == ArenaState.Cleared;

    private ArenaState m_state = ArenaState.Idle;
    private Transform m_player;
    private bool m_playerWasOutside;
    private readonly List<Health> m_tracked = new();
    private readonly HashSet<Health> m_defeated = new();
    private readonly Dictionary<Health, Action> m_deathHandlers = new();
    private readonly List<GameObject> m_generatedWalls = new();
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
                if (CountAlive() == 0)
                    Open();
                break;
        }
    }

    private void DetectPlayer()
    {
        if (m_areaBox == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyArenaGate: areaBox 未設定"); return; }

        if (m_player == null)
        {
            var go = GameObject.FindGameObjectWithTag(m_playerTag);
            if (go == null) return; // プレイヤー未生成。次フレーム再試行
            m_player = go.transform;
        }

        // 壁は範囲の縁に出るが、封鎖トリガーは縁より内側(insetぶん)で判定する。
        // 縁ぴったりで封鎖すると、出現した壁にプレイヤーが押し出される（外に弾かれる）ため。
        // inset は水平(X/Z)のみ。Y は床に立つプレイヤーを拾えるよう範囲そのまま。
        Bounds area = m_areaBox.bounds;
        float insetX = Mathf.Clamp(m_sealTriggerInset, 0f, area.extents.x - 0.5f);
        float insetZ = Mathf.Clamp(m_sealTriggerInset, 0f, area.extents.z - 0.5f);
        var inner = new Bounds(area.center, new Vector3(area.size.x - 2f * insetX, area.size.y, area.size.z - 2f * insetZ));
        bool inside = inner.Contains(m_player.position);

        // 「外→中に踏み込んだ瞬間」だけ封鎖する。スポーン直後にプレイヤーが箱の中にいても
        // （スポーン地点へ移動される前の初期位置が箱の中でも）、一度外を通るまでは封鎖しない。
        // これをしないと開始フレームに即封鎖され、外へ出されたプレイヤーが入れなくなる。
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
        CollectEnemies();

        if (m_tracked.Count == 0)
        {
            // 倒す相手がいないなら封鎖せず即解放（空アリーナ）
            ChannelLogger.Log("Enemy", "EnemyArenaGate: 範囲内に敵がいないため封鎖せず解放");
            m_state = ArenaState.Cleared;
            return;
        }

        m_state = ArenaState.Sealed;
        EnableBarriers(true);
        ChannelLogger.Log("Enemy", $"EnemyArenaGate: 封鎖開始 対象敵={m_tracked.Count}");
        m_onSealed?.Invoke();
        Sealed?.Invoke();
    }

    private void Open()
    {
        m_state = ArenaState.Cleared;
        UnsubscribeTrackedEnemies();
        EnableBarriers(false);
        ChannelLogger.Log("Enemy", "EnemyArenaGate: 範囲内の敵を全滅 → 壁を解除");
        m_onCleared?.Invoke();
        Cleared?.Invoke();
    }

    private void CollectEnemies()
    {
        UnsubscribeTrackedEnemies();
        m_tracked.Clear();
        m_defeated.Clear();

        if (m_manualEnemies != null)
        {
            for (int i = 0; i < m_manualEnemies.Count; i++)
            {
                Health h = m_manualEnemies[i];
                if (h != null && !h.IsDead && !m_tracked.Contains(h))
                    m_tracked.Add(h);
            }
        }

        if (!m_autoCollectEnemies)
            return;

        Bounds area = m_areaBox.bounds;
        Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Health h = all[i];
            if (h == null || h.IsDead) continue;
            if (m_tracked.Contains(h)) continue;
            if (!area.Contains(h.transform.position)) continue;
            if (IsExcluded(h)) continue;
            m_tracked.Add(h);
        }

        SubscribeTrackedEnemies();
    }

    // プレイヤー（閉じ込める側）とボス（ステージの別ギミック）はアリーナの対象から外す。
    private bool IsExcluded(Health h)
    {
        Transform root = h.transform.root;
        if (root.CompareTag(m_playerTag)) return true;
        if (root.GetComponentInChildren<EnemyBossBase>(true) != null) return true;

        // タレットと神風は全滅条件から外す（生き残っていても壁を解除する）。
        if (m_ignoreTurretsAndKamikaze)
        {
            if (root.GetComponentInChildren<EnemyTurretBase>(true) != null) return true;
            if (root.GetComponentInChildren<EnemyAirKamikazeAi>(true) != null) return true;
        }

        return false;
    }

    private int CountAlive()
    {
        int alive = 0;
        for (int i = 0; i < m_tracked.Count; i++)
        {
            Health h = m_tracked[i];
            if (h != null && !h.IsDead && !m_defeated.Contains(h))
                alive++;
        }
        return alive;
    }

    private void SubscribeTrackedEnemies()
    {
        for (int i = 0; i < m_tracked.Count; i++)
        {
            Health h = m_tracked[i];
            if (h == null || m_deathHandlers.ContainsKey(h))
                continue;

            Action handler = () => MarkDefeated(h);
            m_deathHandlers.Add(h, handler);
            h.OnDie += handler;
        }
    }

    private void UnsubscribeTrackedEnemies()
    {
        foreach (var kv in m_deathHandlers)
        {
            Health h = kv.Key;
            if (h != null)
                h.OnDie -= kv.Value;
        }

        m_deathHandlers.Clear();
    }

    private void MarkDefeated(Health h)
    {
        if (h == null)
            return;

        m_defeated.Add(h);
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
        if (m_areaBox == null) { ChannelLogger.LogGuardReturn("Enemy", "EnemyArenaGate: areaBox 未設定で壁生成不可"); return; }

        EnsureMaterial();
        int wallLayer = LayerMask.NameToLayer(m_wallLayerName);

        Vector3 c = m_areaBox.center;
        Vector3 s = m_areaBox.size;
        float halfX = s.x * 0.5f;
        float halfZ = s.z * 0.5f;
        float floorY = c.y - s.y * 0.5f;
        float wallY = floorY + m_wallHeight * 0.5f;
        float t = m_wallThickness;

        // 角がすき間なく閉じるよう、Z 壁を厚みぶん外へ伸ばして X 壁と重ねる
        CreateWall("ArenaWall_PX", new Vector3(c.x + halfX, wallY, c.z), new Vector3(t, m_wallHeight, s.z), wallLayer);
        CreateWall("ArenaWall_NX", new Vector3(c.x - halfX, wallY, c.z), new Vector3(t, m_wallHeight, s.z), wallLayer);
        CreateWall("ArenaWall_PZ", new Vector3(c.x, wallY, c.z + halfZ), new Vector3(s.x + t * 2f, m_wallHeight, t), wallLayer);
        CreateWall("ArenaWall_NZ", new Vector3(c.x, wallY, c.z - halfZ), new Vector3(s.x + t * 2f, m_wallHeight, t), wallLayer);
    }

    private void CreateWall(string wallName, Vector3 localPos, Vector3 localScale, int layer)
    {
        // Cube プリミティブで見た目(MeshRenderer)と封じ込め(BoxCollider・実体)を一枚で持たせる
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = wallName;
        if (layer >= 0)
            go.layer = layer;

        Transform tf = go.transform;
        tf.SetParent(transform, false);
        tf.localRotation = Quaternion.identity;
        tf.localPosition = localPos;
        tf.localScale = localScale;

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

    // マテリアルを加算半透明のフォースフィールド調にする。グリッドテクスチャで光のラインを出す。
    private void ConfigureForceField(Material m)
    {
        if (m_gridTexture == null)
            m_gridTexture = BuildGridTexture();

        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);          // Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);              // Additive
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off); // 内外どちらからも見える

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;

        m.color = m_wallColor;          // _BaseColor / _Color のどちらにも効く
        m.mainTexture = m_gridTexture;  // _BaseMap / _MainTex
        m.mainTextureScale = new Vector2(4f, 2f);
    }

    // 端と中央にラインを持つ白いグリッド。加算合成＋色乗算で光る格子状フィールドになる。
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
                byte a = (byte)(line ? 255 : 28); // 線=濃く、内側=ほんのり
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

        // 上方向へ UV を流してエネルギーが昇る感じを出す
        m_wallMaterialInstance.mainTextureOffset = new Vector2(0f, -m_animTime * 0.25f);

        // ゆっくり明滅させて「触れない」圧を出す
        float pulse = 0.78f + 0.22f * Mathf.Sin(m_animTime * 3f);
        Color c = m_wallColor;
        c.a = m_wallColor.a * pulse;
        m_wallMaterialInstance.color = c;
    }

    private void OnDestroy()
    {
        UnsubscribeTrackedEnemies();
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
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.18f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = prev;
    }
}
