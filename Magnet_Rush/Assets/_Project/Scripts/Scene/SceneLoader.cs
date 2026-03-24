using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * 概要：簡易的なシーンロードクラス
 * シングルトン
 */
public class SceneLoader : MonoBehaviour
{
    // インスタンスを取得
    public static SceneLoader Instance { get; private set; }

    // メインのシーンの種類を定義
    public enum SceneType
    {
        Title,
        Game,
        Result
    }

    // シングルトンの初期化
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // 最初にタイトルシーンをロード
    private void Start()
    {
        LoadScene(SceneType.Title);
    }

    // シーンがロードされたときに呼び出されるイベントにコールバックを登録/解除
    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /*
     * 概要：シーンがロードされたときに呼び出されるコールバックメソッド
     * 引数①：scene ロードされたシーンの情報
     * 引数②：mode ロードモード（シングルかアディティブか）
     */
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // メインシーン(Single)が切り替わった時のみ、追加シーンをロードする
        if (mode == LoadSceneMode.Single)
        {
            LoadAdditiveScenes(scene.name);
        }
    }

    /*
     * 概要：メインシーンに応じて、必要な追加シーンをロードするメソッド
     * 引数：baseSceneName 現在ロードされたメインシーンの名前
     */
    private void LoadAdditiveScenes(string baseSceneName)
    {
        // --- 全シーン共通の追加ロード ---

        //SceneManager.LoadScene("UIScene", LoadSceneMode.Additive);
        //SceneManager.LoadScene("AudioScene", LoadSceneMode.Additive);

        // --- 特定のシーン限定の追加ロード ---

        // ゲームシーンがロードされた場合、マップシーンも追加でロードする
        if (baseSceneName == SceneType.Game.ToString())
        {
            SceneManager.LoadScene("MapScene", LoadSceneMode.Additive);
        }
    }

    /*
     * 概要：シーンをロードするメソッド
     * 引数：SceneType - ロードしたいシーンの種類
     * この関数ではシングルモードでメインシーンを切り替えると同時に、必要に応じて追加シーンもロードする
     */
    public void LoadScene(SceneType sceneType)
    {
        SceneManager.LoadScene(sceneType.ToString(), LoadSceneMode.Single);
    }
}