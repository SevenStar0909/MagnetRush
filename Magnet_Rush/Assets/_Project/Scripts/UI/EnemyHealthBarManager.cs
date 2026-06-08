using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ボス・プレイヤー以外の敵の頭上にHPバーを表示する。
/// 3枚のスプライト(枠/白/赤)を重ね、赤=現在HP(即時)、白=直近ダメージ(遅れて追従)で表す。
/// 白・赤は同じ薄灰スプライトを Image.color で着色して使う。
/// ScreenSpaceOverlay キャンバス上で敵ごとのバーをプールし、LateUpdate で頭上へ配置する。
/// 配置は WorldToScreenPoint → RectTransformUtility.ScreenPointToLocalPointInRectangle で行う
/// (DPI・CanvasScaler のスケールを自動補正するため。OnGUI や position 直代入は高DPIでずれる)。
/// ボス(EnemyBossBase)・プレイヤー(Player)は専用HPバーを持つので対象から除外する。
/// 依存: Health, Player, EnemyBossBase, Camera.main, ScreenSpaceOverlay Canvas
/// </summary>
public class EnemyHealthBarManager : MonoBehaviour
{
    [Header("スプライト")]
    [Tooltip("枠+背景 (enemy_gauge)")]
    [SerializeField] private Sprite m_frameSprite;
    [Tooltip("直近ダメージを表す追従ゲージ (enemy_whitegauge)")]
    [SerializeField] private Sprite m_trailFillSprite;
    [Tooltip("現在HPを表すゲージ (enemy_redgauge)")]
    [SerializeField] private Sprite m_healthFillSprite;

    [Header("見た目")]
    [Tooltip("バーの表示サイズ(px)。枠の縦横比(約2.5:1)に合わせる")]
    [SerializeField] private Vector2 m_barSize = new Vector2(140f, 56f);
    [Tooltip("敵コライダー上端からさらに上へ離す高さ(ワールドm)")]
    [SerializeField] private float m_headOffset = 0.5f;
    [Tooltip("現在HPゲージの色(薄灰スプライトに乗算)")]
    [SerializeField] private Color m_healthColor = new Color(1f, 0.25f, 0.2f, 1f);
    [Tooltip("追従ゲージ(直近ダメージ)の色")]
    [SerializeField] private Color m_trailColor = Color.white;

    [Header("フィル範囲(スプライト内のバー実寸。HP比をこの範囲へ線形マップ)")]
    [Tooltip("バー左端の正規化X")]
    [SerializeField] private float m_fillStart = 0.203f;
    [Tooltip("バー右端の正規化X")]
    [SerializeField] private float m_fillEnd = 0.855f;

    [Header("ダメージ追従")]
    [Tooltip("白ゲージが赤に追いつき始めるまでの待ち(秒)")]
    [SerializeField] private float m_trailDelay = 0.4f;
    [Tooltip("白ゲージが赤に追いつく速さ(1秒あたりのHP割合)")]
    [SerializeField] private float m_trailSpeed = 0.5f;

    [Header("対象スキャン")]
    [Tooltip("対象Healthを探し直す間隔(秒)")]
    [SerializeField] private float m_scanInterval = 1f;
    [Tooltip("HP満タンのときバーを隠す")]
    [SerializeField] private bool m_hideWhenFull = true;
    [Tooltip("プレイヤーからこの距離(m)以内の敵だけバーを出す。0以下で距離制限なし")]
    [SerializeField] private float m_displayRange = 30f;

    private Camera m_camera;
    private RectTransform m_containerRect;
    private Vector3 m_playerPos;
    private bool m_hasPlayer;
    private float m_nextScanTime;
    private readonly Dictionary<Health, BarView> m_bars = new();
    private readonly List<Health> m_removeBuffer = new();

    private class BarView
    {
        public RectTransform root;
        public Image health;
        public Image trail;
        public Collider headSource;
        public float trailValue;
        public float lastRatio;
        public float trailTimer;
    }

    void Awake()
    {
        m_camera = Camera.main;
        m_containerRect = transform as RectTransform;

        // 過去のシーンセーブ等で取り残された子バーを起動時に掃除する
        // (中央=レティクル位置に幽霊バーが残るのを防ぐ)。
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    void LateUpdate()
    {
        if (m_camera == null) m_camera = Camera.main;
        if (m_camera == null) { ChannelLogger.LogGuardReturn("Game", "メインカメラなし"); return; }
        if (m_containerRect == null) { ChannelLogger.LogGuardReturn("Game", "UIコンテナがRectTransformでない"); return; }

        if (Time.time >= m_nextScanTime)
        {
            m_nextScanTime = Time.time + m_scanInterval;
            ScanTargets();
        }

        var player = Player.Current;
        m_hasPlayer = player != null;
        if (m_hasPlayer) m_playerPos = player.transform.position;

        float dt = Time.deltaTime;
        foreach (var kv in m_bars)
            UpdateBar(kv.Key, kv.Value, dt);

        SweepRemoved();
    }

    private void ScanTargets()
    {
        var all = FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var h = all[i];
            if (h == null || h.IsDead) continue;
            if (m_bars.ContainsKey(h)) continue;
            if (IsExcluded(h)) continue;

            m_bars[h] = CreateBar(h);
        }
    }

    // プレイヤーとボスは専用HPバーUIを持つので対象外。
    private bool IsExcluded(Health h)
    {
        if (h.GetComponentInParent<Player>() != null) return true;
        if (h.transform.root.GetComponentInChildren<EnemyBossBase>(true) != null) return true;
        return false;
    }

    private BarView CreateBar(Health h)
    {
        var go = new GameObject("EnemyHPBar", typeof(RectTransform));
        var root = (RectTransform)go.transform;
        root.SetParent(m_containerRect, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = m_barSize;

        // 後ろから 枠 → 白(追従) → 赤(現在HP) の順で重ねる。赤が一番手前。
        AddLayer(root, m_frameSprite, "Frame", Color.white, false);
        var trail = AddLayer(root, m_trailFillSprite, "Trail", m_trailColor, true);
        var health = AddLayer(root, m_healthFillSprite, "Health", m_healthColor, true);

        float ratio = h.HealthRatio;
        var view = new BarView
        {
            root = root,
            health = health,
            trail = trail,
            headSource = ResolveHeadCollider(h),
            trailValue = ratio,
            lastRatio = ratio,
            trailTimer = 0f
        };
        ApplyFill(health, ratio);
        ApplyFill(trail, ratio);
        return view;
    }

    private Image AddLayer(RectTransform parent, Sprite sprite, string layerName, Color color, bool filled)
    {
        var go = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        return img;
    }

    private void UpdateBar(Health h, BarView view, float dt)
    {
        if (h == null || h.IsDead)
        {
            view.root.gameObject.SetActive(false);
            m_removeBuffer.Add(h);
            return;
        }

        float ratio = h.HealthRatio;

        // プレイヤーから一定距離より遠い敵はバーを出さない
        if (m_displayRange > 0f && m_hasPlayer &&
            (h.transform.position - m_playerPos).sqrMagnitude > m_displayRange * m_displayRange)
        {
            if (view.root.gameObject.activeSelf) view.root.gameObject.SetActive(false);
            view.lastRatio = ratio;
            return;
        }

        if (m_hideWhenFull && ratio >= 1f && view.trailValue >= 1f)
        {
            if (view.root.gameObject.activeSelf) view.root.gameObject.SetActive(false);
            view.lastRatio = ratio;
            return;
        }

        Vector3 head = GetHeadWorldPos(h, view);
        Vector3 screen = m_camera.WorldToScreenPoint(head);
        if (screen.z <= 0f) // カメラ後方は描かない
        {
            if (view.root.gameObject.activeSelf) view.root.gameObject.SetActive(false);
            return;
        }

        if (!view.root.gameObject.activeSelf) view.root.gameObject.SetActive(true);
        // スクリーン座標→コンテナのローカル座標(DPI/CanvasScalerを自動補正)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_containerRect, new Vector2(screen.x, screen.y), null, out Vector2 local))
            view.root.anchoredPosition = local;

        ApplyFill(view.health, ratio); // 赤=現在HPは即時

        if (ratio < view.lastRatio) view.trailTimer = m_trailDelay;
        view.lastRatio = ratio;

        if (view.trailValue > ratio)
        {
            if (view.trailTimer > 0f) view.trailTimer -= dt;
            else view.trailValue = Mathf.MoveTowards(view.trailValue, ratio, m_trailSpeed * dt);
        }
        else
        {
            view.trailValue = ratio;
        }
        ApplyFill(view.trail, view.trailValue);
    }

    // HP比をスプライト内のバー実寸 [m_fillStart, m_fillEnd] へ線形マップする。
    private void ApplyFill(Image img, float ratio)
    {
        img.fillAmount = m_fillStart + Mathf.Clamp01(ratio) * (m_fillEnd - m_fillStart);
    }

    private void SweepRemoved()
    {
        if (m_removeBuffer.Count == 0) return;
        for (int i = 0; i < m_removeBuffer.Count; i++)
        {
            var h = m_removeBuffer[i];
            if (m_bars.TryGetValue(h, out var view))
            {
                if (view.root != null) Destroy(view.root.gameObject);
                m_bars.Remove(h);
            }
        }
        m_removeBuffer.Clear();
    }

    private Vector3 GetHeadWorldPos(Health h, BarView view)
    {
        if (view.headSource == null) view.headSource = ResolveHeadCollider(h);
        float topY = view.headSource != null ? view.headSource.bounds.max.y : h.transform.position.y + 2f;
        Vector3 p = h.transform.position;
        p.y = topY + m_headOffset;
        return p;
    }

    private Collider ResolveHeadCollider(Health h)
    {
        var col = h.GetComponent<Collider>();
        if (col == null) col = h.GetComponentInChildren<Collider>();
        return col;
    }
}
