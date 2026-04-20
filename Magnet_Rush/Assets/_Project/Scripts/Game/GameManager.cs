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
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
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

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.f5Key.wasPressedThisFrame) Restart();
        if (Keyboard.current.digit1Key.wasPressedThisFrame) TeleportToStart();
    }

    /// <summary>シーンリロードでゲームをリスタートする。</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
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
}
