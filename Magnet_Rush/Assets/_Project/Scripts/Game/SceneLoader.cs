using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
/// <summary>
/// シーンローダー。シングルトンでシーン遷移を管理する。
/// アディティブシーン（マップ等）はInspectorで設定。
/// 遷移は非同期ロード＋ロード画面（LoadingScreenView）を挟む。
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

    [Header("ロード画面")]
    [Tooltip("シーン遷移中に表示するロード画面プレハブ")]
    [SerializeField] private LoadingScreenView m_loadingScreenPrefab;
    [Tooltip("ロード画面の見た目設定（背景画像・動画・表示時間）")]
    [SerializeField] private LoadingScreenSettings m_loadingScreenSettings;

    private LoadingScreenView m_loadingScreen;
    private bool m_isLoading;
    private readonly List<AsyncOperation> m_pendingAdditiveOps = new List<AsyncOperation>();

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

    /// <summary>アディティブシーンをすべて非同期で読み込む。完了待ちはLoadRoutineが行う。</summary>
    private void LoadAdditiveScenes()
    {
        foreach (var sceneName in m_additiveScenes)
        {
            if (string.IsNullOrEmpty(sceneName)) continue;
            if (IsSceneLoaded(sceneName)) continue;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null) m_pendingAdditiveOps.Add(op);
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

    /// <summary>指定シーンをロード画面を挟んで非同期ロードする。</summary>
    public void LoadScene(SceneType sceneType)
    {
        StartLoad(sceneType.ToString());
    }

    /// <summary>ロード処理を開始する。多重呼び出しは無視される。</summary>
    private void StartLoad(string sceneName)
    {
        if (m_isLoading)
        {
            ChannelLogger.LogGuardReturn("Game", $"ロード中のため '{sceneName}' の遷移要求を無視");
            return;
        }
        StartCoroutine(LoadRoutine(sceneName));
    }

    /// <summary>
    /// ロード画面を表示してシーンを非同期ロードする。
    /// アディティブシーン（OnSceneLoaded経由で積まれる）の完了も待ってから閉じる。
    /// </summary>
    private IEnumerator LoadRoutine(string sceneName)
    {
        m_isLoading = true;
        float startTime = Time.unscaledTime;

        var screen = GetLoadingScreen();
        if (screen != null)
            yield return screen.ShowRoutine();

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            ChannelLogger.LogGuardReturn("Game", $"エラー: シーン '{sceneName}' をロードできない（Build Settings未登録？）");
            if (screen != null) yield return screen.HideRoutine();
            m_isLoading = false;
            yield break;
        }

        while (!op.isDone)
            yield return null;

        // OnSceneLoadedが積んだアディティブシーン（マップ等）の完了を待つ
        while (m_pendingAdditiveOps.Count > 0)
        {
            m_pendingAdditiveOps.RemoveAll(o => o == null || o.isDone);
            yield return null;
        }

        if (screen != null)
        {
            float minTime = m_loadingScreenSettings != null ? m_loadingScreenSettings.minDisplayTime : 0f;
            while (Time.unscaledTime - startTime < minTime)
                yield return null;
            yield return screen.HideRoutine();
        }

        m_isLoading = false;
    }

    /// <summary>ロード画面を遅延生成して返す。プレハブ未設定ならnull（ロード画面なしで遷移）。</summary>
    private LoadingScreenView GetLoadingScreen()
    {
        if (m_loadingScreen != null) return m_loadingScreen;

        if (m_loadingScreenPrefab == null || m_loadingScreenSettings == null)
        {
            ChannelLogger.LogGuardReturn("Game", "ロード画面プレハブ/設定が未割当（ロード画面なしで遷移）");
            return null;
        }

        m_loadingScreen = Instantiate(m_loadingScreenPrefab, transform);
        m_loadingScreen.Initialize(m_loadingScreenSettings);
        return m_loadingScreen;
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
            StartLoad(selectedSceneName);
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
