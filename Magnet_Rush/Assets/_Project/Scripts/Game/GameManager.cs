using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// ゲーム全体の管理。リスタート、リスポーン、テレポートを担当する。
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : Singleton<GameManager>
{
    [FormerlySerializedAs("spawnPoint")]
    [SerializeField] private Transform m_spawnPoint;

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
        if (player != null && m_spawnPoint != null)
        {
            player.transform.position = m_spawnPoint.position;
        }
    }

    /// <summary>
    /// リスポーン位置を取得する。
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        return m_spawnPoint != null ? m_spawnPoint.position : Vector3.up;
    }
}
