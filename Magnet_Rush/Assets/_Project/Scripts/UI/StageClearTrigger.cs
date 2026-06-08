using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ゴール演出スクリプト。特定のオブジェクト（ゴール）にプレイヤーが接触した瞬間に起動する。
/// </summary>
public class GameClearTrigger : MonoBehaviour
{
    [Header("演出テクスチャ")]
    [Tooltip("順番に再生するスプライトシート(1〜4)")]
    [SerializeField] private Texture[] m_sheetTextures;
    [Tooltip("最後に再生するシート(5)")]
    [SerializeField] private Texture m_finalSheetTexture;

    [Header("シート設定")]
    [SerializeField] private int m_columns = 4;
    [SerializeField] private int m_rows = 7;
    [SerializeField] private int m_finalColumns = 4;
    [SerializeField] private int m_finalRows = 3;
    [SerializeField] private float m_fps = 16f;
    [Tooltip("接触した瞬間から、演出（画像表示）が始まるまでの待ち時間（秒）")]
    [SerializeField] private float m_startDelay = 0f;

    [Header("UI参照")]
    [Tooltip("演出全体の表示ON/OFFを切り替える親オブジェクト")]
    [SerializeField] private GameObject m_root;
    [Tooltip("全画面 RawImage")]
    [SerializeField] private RawImage m_sequenceImage;

    [Header("遷移先シーン")]
    [Tooltip("セレクト画面のシーン名")]
    [SerializeField] private string m_nextSceneName = "StageSelectScene";

    [Header("接触判定の設定")]
    [Tooltip("プレイヤーオブジェクトに設定されているタグ名")]
    [SerializeField] private string m_playerTag = "Player";

    private bool m_playing;
    private bool m_waitingForInput;

    void Awake()
    {
        if (m_root != null) m_root.SetActive(false);
    }

    private void Start()
    {
        // 1. シーン全体から「GameClearCanvas」という名前のオブジェクトを検索
        GameObject clearCanvas = GameObject.Find("GameClearCanvas");

        if (clearCanvas != null)
        {
            // 2. GameClearCanvasの直下にある「Root」を探して自動セット
            Transform rootTransform = clearCanvas.transform.Find("Root");
            if (rootTransform != null)
            {
                m_root = rootTransform.gameObject; // ➔ 変数名に合わせて書き換えてください

                // 3. Rootのさらに直下にある「Sequence」を探して自動セット
                Transform sequenceTransform = rootTransform.Find("Sequence");
                if (sequenceTransform != null)
                {
                    // ➔ 変数名に合わせて書き換えてください
                    m_sequenceImage = sequenceTransform.GetComponent<RawImage>();

                    // 💡 もしm_sequenceの型がGameObjectではなく、特定のコンポーネント（例: PlayableDirectorなど）の場合は以下のように記述します
                    // m_sequence = sequenceTransform.GetComponent<型名>();
                }
            }
            Debug.Log("[Goal] シーンの壁を越えて、GameClearCanvasのRootとSequenceを自動接続しました！");
        }
        else
        {
            Debug.LogWarning("[Goal] GameClearCanvasが見つかりませんでした。オブジェクト名を確認してください。");
        }
    }

    void Update()
    {
        // 最終コマで止まっているとき、いずれかのボタンが押されたら次へ
        if (m_waitingForInput && AnyButtonPressed()) GoNext();
    }


    private void OnTriggerEnter(Collider other)
    {
        // 磁力フィールドは完全に無視する
        if (other.name.Contains("MagnetField"))
        {
            return; // ここで処理を終了して追い返す
        }

        // 触れてきた相手自身、またはその親（ルート）が Player タグを持っているかチェック
        bool isPlayer = other.CompareTag(m_playerTag) ||
                        (other.transform.root != null && other.transform.root.CompareTag(m_playerTag));

        if (isPlayer)
        {
            Debug.Log($"[Goal] 確定検知！相手は 【{other.name}】 です。演出を開始します。");
            PlayClear();
        }
    }
    /// <summary>クリア演出を再生する（外部やデバッグボタンから手動で呼ぶことも可能）</summary>
    public void PlayClear()
    {
        if (m_playing) return;
        if (m_sequenceImage == null) return;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        m_playing = true;
        m_waitingForInput = false;

        // 接触直後の余韻
        if (m_startDelay > 0f) yield return WaitUnscaled(m_startDelay);

        if (m_root != null) m_root.SetActive(true);
        Time.timeScale = 0f; // ゲームの世界をフリーズ

        float frameDur = m_fps > 0f ? 1f / m_fps : 0.0625f;

        // シート1〜4の再生
        if (m_sheetTextures != null)
        {
            int perSheet = Mathf.Max(1, m_columns * m_rows);
            for (int s = 0; s < m_sheetTextures.Length; s++)
            {
                var tex = m_sheetTextures[s];
                if (tex == null) continue;
                m_sequenceImage.texture = tex;
                for (int f = 0; f < perSheet; f++)
                {
                    m_sequenceImage.uvRect = FrameUv(f, m_columns, m_rows);
                    yield return WaitUnscaled(frameDur);
                }
            }
        }

        // 最終シート5の再生
        if (m_finalSheetTexture != null)
        {
            int finalFrames = Mathf.Max(1, m_finalColumns * m_finalRows);
            m_sequenceImage.texture = m_finalSheetTexture;
            for (int f = 0; f < finalFrames; f++)
            {
                m_sequenceImage.uvRect = FrameUv(f, m_finalColumns, m_finalRows);
                yield return WaitUnscaled(frameDur);
            }
            m_sequenceImage.uvRect = FrameUv(finalFrames - 1, m_finalColumns, m_finalRows);
        }

        m_waitingForInput = true; // 入力待ち状態にする
    }

    private Rect FrameUv(int frame, int cols, int rows)
    {
        int col = frame % cols;
        int row = frame / cols;
        float w = 1f / cols;
        float h = 1f / rows;
        return new Rect(col * w, 1f - (row + 1) * h, w, h);
    }

    private void GoNext()
    {
        m_waitingForInput = false;
        m_playing = false;
        Time.timeScale = 1f; // フリーズを解除してから遷移

        if (string.IsNullOrEmpty(m_nextSceneName)) return;
        SceneManager.LoadScene(m_nextSceneName);
    }

    private bool AnyButtonPressed()
    {
        var k = Keyboard.current;
        if (k != null && k.anyKey.wasPressedThisFrame) return true;

        var m = Mouse.current;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;

        var g = Gamepad.current;
        if (g != null)
        {
            if (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame
             || g.buttonWest.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame
             || g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame
             || g.leftShoulder.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame)
                return true;
        }
        return false;
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}