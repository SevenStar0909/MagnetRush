using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// シーンローダー。シングルトンでシーン遷移を管理する。
/// アディティブシーン（マップ等）はInspectorで設定。
/// </summary>
public class SceneLoader : Singleton<SceneLoader>
{
    public enum SceneType
    {
        TitleScene,
        StageSelectScene,
        GameScene,
        TestScene,
        Result
    }

    [Header("アディティブシーン")]
    [Tooltip("ゲームプレイ時にアディティブで読み込むシーン名（マップ等）")]
    [SerializeField] private string[] m_additiveScenes = { "MapScene" };

    [Header("タイトル遷移をスキップするシーン名パターン")]
    [Tooltip("これらの文字列を含むシーンではタイトルに遷移せずアディティブシーンだけ読み込む")]
    [SerializeField] private string[] m_skipTitlePatterns = { "Test", "NS_", "SSS_", "Shimomoto", "Nishigori" };

    protected override void Awake()
    {
        base.Awake();
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        string current = SceneManager.GetActiveScene().name;

        if (ShouldSkipTitle(current))
        {
            LoadAdditiveScenes();
            ChannelLogger.LogGuardReturn("Game", "タイトル遷移をスキップ");
            return;
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /// <summary>シーンロード完了時のコールバック。Singleモードでゲームプレイなら追加シーンをロード、メニューならカーソルを解除する。</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (ShouldLoadAdditives(scene.name))
        {
            LoadAdditiveScenes();
        }
        else
        {
            // ゲームプレイ中にロックされたカーソルがメニューに持ち越されてUI操作不能になるのを防ぐ
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>アディティブシーンをすべて読み込む。</summary>
    private void LoadAdditiveScenes()
    {
        foreach (var sceneName in m_additiveScenes)
        {
            if (string.IsNullOrEmpty(sceneName)) continue;
            if (IsSceneLoaded(sceneName)) continue;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }
    }

    /// <summary>指定シーンが既にロード済みか。</summary>
    private bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).name == sceneName)
                return true;
        }
        return false;
    }

    /// <summary>タイトル遷移をスキップするか。</summary>
    private bool ShouldSkipTitle(string sceneName)
    {
        foreach (var pattern in m_skipTitlePatterns)
        {
            if (!string.IsNullOrEmpty(pattern) && sceneName.Contains(pattern))
                return true;
        }
        return false;
    }

    /// <summary>アディティブシーンを読み込むべきシーンか。</summary>
    private bool ShouldLoadAdditives(string sceneName)
    {
        // Titleはマップ不要
        if (sceneName == SceneType.TitleScene.ToString()) return false;
        if (sceneName == SceneType.StageSelectScene.ToString()) return false;
        if (sceneName == SceneType.Result.ToString()) return false;
        return true;
    }

    /// <summary>指定シーンをSingleモードでロードする。</summary>
    public void LoadScene(SceneType sceneType)
    {
        SceneManager.LoadScene(sceneType.ToString(), LoadSceneMode.Single);
    }

    /// <summary> メインシーンとアディティブマップを指定してロードする </summary>
    /// <param name="selectedSceneName">メインシーンの名前</param>
    /// <param name="selectedMapName">アディティブで読み込むマップシーンの名前</param>
    public void LoadGameWithMap(string selectedSceneName, string selectedMapName)
    {
        ChannelLogger.LogGuardReturn("Game", "=== LoadGameWithMap開始 ===");
        ChannelLogger.LogGuardReturn("Game", $"メインシーン: {selectedSceneName}");
        ChannelLogger.LogGuardReturn("Game", $"アディティブマップ: {selectedMapName}");

        // 入力値の検証
        if (string.IsNullOrEmpty(selectedSceneName) || string.IsNullOrEmpty(selectedMapName))
        {
            ChannelLogger.LogGuardReturn("Game", "エラー: シーン名が空です");
            return;
        }

        // アディティブで読み込むマップシーンを設定
        m_additiveScenes = new string[] { selectedMapName };
        ChannelLogger.LogGuardReturn("Game", $"m_additiveScenes設定: {string.Join(", ", m_additiveScenes)}");

        // SceneType列挙型と一致するシーン名を検索して変換
        if (TryConvertToSceneType(selectedSceneName, out SceneType sceneType))
        {
            ChannelLogger.LogGuardReturn("Game", $"SceneType列挙型に一致: {sceneType}");
            LoadScene(sceneType);
        }
        else
        {
            // 列挙型に一致しない場合は、シーン名のまま直接ロード
            ChannelLogger.LogGuardReturn("Game", $"警告: SceneType列挙型に一致しないため、シーン名で直接ロード: {selectedSceneName}");
            SceneManager.LoadScene(selectedSceneName, LoadSceneMode.Single);
        }
    }

    /// <summary> シーン名をSceneType列挙型に変換を試みる </summary>
    /// <param name="sceneName">シーン名</param>
    /// <param name="sceneType">変換後のSceneType</param>
    /// <returns>変換成功時true、失敗時false</returns>
    private bool TryConvertToSceneType(string sceneName, out SceneType sceneType)
    {
        sceneType = SceneType.GameScene;  // デフォルト値

        try
        {
            // SceneType列挙型に該当する名前を探す
            foreach (SceneType type in System.Enum.GetValues(typeof(SceneType)))
            {
                if (type.ToString() == sceneName)
                {
                    sceneType = type;
                    return true;
                }
            }

            ChannelLogger.LogGuardReturn("Game", $"警告: 列挙型に'{sceneName}'は見つかりません");
            return false;
        }
        catch (System.Exception ex)
        {
            ChannelLogger.LogGuardReturn("Game", $"エラー: SceneType変換時に例外発生 - {ex.Message}");
            return false;
        }
    }
}
