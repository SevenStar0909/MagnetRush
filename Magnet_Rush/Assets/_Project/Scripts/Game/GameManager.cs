using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の管理。リスタート、リスポーン、テレポートを担当する。
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : Singleton<GameManager>
{
    [SerializeField] private Transform spawnPoint;

    protected override void Awake()
    {
        base.Awake();
        // _Managersの子なのでDontDestroyOnLoadは使わない
        // シーン遷移時はシーンと一緒に破棄される
    }

    void Update()
    {
        // Rキーでリスタート（Input System）
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Restart();
        }

        // 1キーでスポーン地点にテレポート
        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            TeleportToStart();
        }
    }

    /// <summary>
    /// シーンリロードでゲームをリスタートする。
    /// </summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// プレイヤーをスポーン地点に移動する。
    /// </summary>
    public void TeleportToStart()
    {
        var player = GameObject.FindWithTag(GameTags.Player);
        if (player != null && spawnPoint != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = spawnPoint.position;
            if (cc != null) cc.enabled = true;
        }
    }

    /// <summary>
    /// リスポーン位置を取得する。
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        return spawnPoint != null ? spawnPoint.position : Vector3.up;
    }
}
