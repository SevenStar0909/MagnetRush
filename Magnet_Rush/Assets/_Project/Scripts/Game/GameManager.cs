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
    private const string Stage2MapSceneName = "Stage2_MAP";
    private const string BossPositionObjectName = "BossPosition";

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
        if (Keyboard.current.bKey.wasPressedThisFrame) TeleportToBossPosition();
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
        TeleportPlayer(GetSpawnPosition());
    }

    /// <summary>Stage2_MAP の BossPosition にプレイヤーを移動する。</summary>
    public void TeleportToBossPosition()
    {
        var stage2Map = SceneManager.GetSceneByName(Stage2MapSceneName);
        if (!stage2Map.IsValid() || !stage2Map.isLoaded) return;

        Transform bossPosition = null;
        foreach (var rootObject in stage2Map.GetRootGameObjects())
        {
            foreach (var child in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != BossPositionObjectName) continue;
                bossPosition = child;
                break;
            }

            if (bossPosition != null) break;
        }

        if (bossPosition == null) { ChannelLogger.LogGuardReturn("Game", "BossPosition未発見"); return; }
        TeleportPlayer(bossPosition.position);
    }

    private void TeleportPlayer(Vector3 position)
    {
        // Player タグは _Player(root) と Hurtbox(子) の両方に付いているので transform.root で寄せる
        var tagged = GameObject.FindWithTag(GameTags.Player);
        if (tagged == null) { ChannelLogger.LogGuardReturn("Game", "Playerタグ未発見"); return; }
        var root = tagged.transform.root;
        root.position = position;
        var rb = root.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.position = position;
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
