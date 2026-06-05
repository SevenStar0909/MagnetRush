using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の管理。リスタート、リスポーン、テレポートを担当する。
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : Singleton<GameManager>
{
    // 初期設定の物理演算ステップを保持する変数
    private float m_originalFixedDeltaTime;

    [Header("Performance Link")]
    [Tooltip("新設したUI演出スクリプトの参照")]
    [SerializeField] private GameClearUI m_gameClearPresenter;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // ボス死亡イベントの購読を開始
        EnemyBossBase.OnBossDefeated += OnBossDefeatedHandler;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        // メモリリーク防止のため必ず購読を解除
        EnemyBossBase.OnBossDefeated -= OnBossDefeatedHandler;
    }

    private void Start()
    {
        // 開始時に元の数値を保存しておく（デフォルトは 0.02f）
        m_originalFixedDeltaTime = Time.fixedDeltaTime;
        StartCoroutine(TeleportNextFrame());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<StageSpawnPoint>() == null) { ChannelLogger.LogGuardReturn("Game", "SpawnPoint未ロード"); return; }
        StartCoroutine(TeleportNextFrame());
    }

    private IEnumerator TeleportNextFrame()
    {
        yield return null;
        TeleportToStart();
    }

    private float m_lastTimeScale = 1f;

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.f5Key.wasPressedThisFrame) Restart();
        if (Keyboard.current.digit1Key.wasPressedThisFrame) TeleportToStart();

        // ==========================================
        // タイムスケールの不意な変更を監視するデバッグコード
        // ==========================================
        if (Time.timeScale != m_lastTimeScale)
        {
            Debug.Log($"[TimeScale変更検知] {m_lastTimeScale} -> {Time.timeScale}");
            m_lastTimeScale = Time.timeScale;
        }
    }

    /// <summary>シーンリロードでゲームをリスタートする。</summary>
    public void Restart()
    {
        // タイムスケールと物理ステップを確実に戻す
        Time.timeScale = 1f;
        Time.fixedDeltaTime = m_originalFixedDeltaTime;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>プレイヤーをスポーン地点に移動する。</summary>
    public void TeleportToStart()
    {
        // Player タグは _Player(root) と Hurtbox(子) の両方に付いているので transform.root で寄せる
        var tagged = GameObject.FindWithTag(GameTags.Player);
        if (tagged == null) { ChannelLogger.LogGuardReturn("Game", "Playerタグ未発見"); return; }
        var root = tagged.transform.root;
        var spawn = GetSpawnPosition();
        root.position = spawn;
        var rb = root.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.position = spawn;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>リスポーン位置を取得する。StageSpawnPoint を都度検索する（pull 方式）。</summary>
    public Vector3 GetSpawnPosition()
    {
        var sp = FindFirstObjectByType<StageSpawnPoint>();
        return sp != null ? sp.transform.position : Vector3.up;
    }

    // ==========================================
    // ボス撃破イベントの受け取り
    // ==========================================
    private void OnBossDefeatedHandler()
    {
        if (m_gameClearPresenter != null)
        {
            // スロー倍率はUI側が知っているので、元の FixedDeltaTime だけを渡して実行
            m_gameClearPresenter.PlayPerformance(m_originalFixedDeltaTime);
        }
        else
        {
            Debug.LogWarning("[GameManager] GameClearPresenter の参照がありません。");
        }
    }
}
