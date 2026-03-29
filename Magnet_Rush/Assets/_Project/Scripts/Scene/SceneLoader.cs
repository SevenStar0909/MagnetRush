using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンローダー。シングルトンでシーン遷移を管理する。
/// </summary>
public class SceneLoader : Singleton<SceneLoader>
{
    public enum SceneType
    {
        Title,
        Game,
        Result
    }

    protected override void Awake()
    {
        base.Awake();
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        string current = SceneManager.GetActiveScene().name;

        // テストシーン・メンバーシーンではタイトル遷移せずMapSceneだけ読み込む
        if (current.Contains("Test") || current.Contains("NS_") || current.Contains("NishigoriTaiki_"))
        {
            LoadAdditiveScenes(current);
            return;
        }

        LoadScene(SceneType.Title);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /// <summary>シーンロード完了時のコールバック。Singleモードの場合のみ追加シーンをロード。</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            LoadAdditiveScenes(scene.name);
        }
    }

    /// <summary>メインシーンに応じて追加シーンをAdditiveロードする。</summary>
    private void LoadAdditiveScenes(string baseSceneName)
    {
        if (baseSceneName == SceneType.Game.ToString()
            || baseSceneName.Contains("Test")
            || baseSceneName.Contains("NS_")
            || baseSceneName.Contains("NishigoriTaiki_"))
        {
            SceneManager.LoadScene("MapScene", LoadSceneMode.Additive);
        }
    }

    /// <summary>指定シーンをSingleモードでロードする。</summary>
    public void LoadScene(SceneType sceneType)
    {
        SceneManager.LoadScene(sceneType.ToString(), LoadSceneMode.Single);
    }
}
