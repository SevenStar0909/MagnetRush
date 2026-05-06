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
        Title,
        Game,
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

        LoadScene(SceneType.Title);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /// <summary>シーンロード完了時のコールバック。Singleモードの場合のみ追加シーンをロード。</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && ShouldLoadAdditives(scene.name))
        {
            LoadAdditiveScenes();
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
        if (sceneName == SceneType.Title.ToString()) return false;
        if (sceneName == SceneType.Result.ToString()) return false;
        return true;
    }

    /// <summary>指定シーンをSingleモードでロードする。</summary>
    public void LoadScene(SceneType sceneType)
    {
        SceneManager.LoadScene(sceneType.ToString(), LoadSceneMode.Single);
    }
}
