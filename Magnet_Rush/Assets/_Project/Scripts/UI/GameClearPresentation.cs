using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲームクリア演出。"Boss" タグの敵を撃破(Health.OnDie)したら起動する。
/// スプライトシート(1〜4: m_columns×m_rows のコマ)を順番にアニメ再生 → 5(m_finalColumns×m_finalRows)を続けて再生 →
/// 最終コマで静止し、いずれかのボタンが押されたら次のシーンへ遷移する。
/// 演出中は Time.timeScale=0 で凍結する(GameOverPresentation と同方式)。
/// 依存: "Boss" タグの GameObject が持つ Health.OnDie, UnityEngine.SceneManagement, Input System
/// 設計: テクスチャは RawImage の uvRect でコマ送りするのでスプライト化(スライス)不要。
///       ボスは Additive マップで遅延ロードされるため、見つかるまで Update で解決を試みる。
/// </summary>
public class GameClearPresentation : MonoBehaviour
{
    [Header("演出テクスチャ")]
    [Tooltip("順番に再生するスプライトシート(1〜4)。各シートを m_columns×m_rows のコマでアニメする")]
    [SerializeField] private Texture[] m_sheetTextures;
    [Tooltip("最後に再生するシート(5)。m_finalColumns×m_finalRows のコマでアニメする")]
    [SerializeField] private Texture m_finalSheetTexture;

    [Header("シート設定")]
    [Tooltip("シート1〜4の横のコマ数")]
    [SerializeField] private int m_columns = 4;
    [Tooltip("シート1〜4の縦のコマ数")]
    [SerializeField] private int m_rows = 7;
    [Tooltip("シート5の横のコマ数")]
    [SerializeField] private int m_finalColumns = 4;
    [Tooltip("シート5の縦のコマ数")]
    [SerializeField] private int m_finalRows = 3;
    [Tooltip("コマ送りの速さ(1秒あたりのコマ数)")]
    [SerializeField] private float m_fps = 16f;
    [Tooltip("ボス撃破から演出開始までの待ち時間(秒)。撃破モーションを見せたいときに使う。この間は時間を止めない")]
    [SerializeField] private float m_startDelay = 0f;

    [Header("UI参照")]
    [Tooltip("演出全体の表示ON/OFFを切り替える親オブジェクト")]
    [SerializeField] private GameObject m_root;
    [Tooltip("1〜5を表示する全画面 RawImage")]
    [SerializeField] private RawImage m_sequenceImage;

    [Header("遷移先シーン")]
    [Tooltip("演出後にボタンで読み込むシーン名")]
    [SerializeField] private string m_nextSceneName = "StageSelectScene";

    private Health m_bossHealth;
    private bool m_warnedNoBossTag;
    private bool m_playing;
    private bool m_waitingForInput;

    void Awake()
    {
        if (m_root != null) m_root.SetActive(false);
    }

    void OnEnable()
    {
        BindBoss();
    }

    void OnDisable()
    {
        if (m_bossHealth != null) m_bossHealth.OnDie -= HandleBossDie;
        m_bossHealth = null;
    }

    void Update()
    {
        // ボスは Additive マップで遅延ロードされるので、見つかるまで購読を試み続ける
        if (m_bossHealth == null && !m_playing) BindBoss();

        if (m_waitingForInput && AnyButtonPressed()) GoNext();
    }

    /// <summary>"Boss" タグから Health を解決して OnDie を購読する。タグ未登録時もログ1回で安全に抜ける。</summary>
    private void BindBoss()
    {
        if (m_bossHealth != null) return;

        GameObject bossObj;
        try { bossObj = GameObject.FindWithTag("Boss"); }
        catch (UnityException)
        {
            if (!m_warnedNoBossTag)
            {
                ChannelLogger.LogWarning("Game", "Bossタグが未登録 (TagManager に Boss を追加)");
                m_warnedNoBossTag = true;
            }
            return;
        }

        if (bossObj == null) { ChannelLogger.LogGuardReturn("Game", "Boss未配置"); return; }

        var health = bossObj.GetComponent<Health>();
        if (health == null) { ChannelLogger.LogGuardReturn("Game", "Boss に Health が無い"); return; }

        m_bossHealth = health;
        m_bossHealth.OnDie += HandleBossDie;
    }

    private void HandleBossDie() => PlayClear();

    /// <summary>ボス撃破を待たずにクリア演出を再生する。デバッグボタンからも呼ぶ。</summary>
    public void PlayClear()
    {
        if (m_playing) { ChannelLogger.LogGuardReturn("Game", "クリア演出が既に再生中"); return; }
        if (m_sequenceImage == null) { ChannelLogger.LogGuardReturn("Game", "m_sequenceImage 未設定"); return; }
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        m_playing = true;
        m_waitingForInput = false;

        // 撃破モーションを見せるための猶予。ここではまだ時間を止めない
        if (m_startDelay > 0f) yield return WaitUnscaled(m_startDelay);

        if (m_root != null) m_root.SetActive(true);
        Time.timeScale = 0f;

        float frameDur = m_fps > 0f ? 1f / m_fps : 0.0625f;

        // シート1〜4
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

        // シート5(グリッドが 4×3 と異なる)
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

        m_waitingForInput = true;
    }

    // frame=0 を左上として、左→右・上→下の順にコマを取り出す。uvRect は左下原点。
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
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(m_nextSceneName))
        { ChannelLogger.LogGuardReturn("Game", "遷移先シーン名が未設定"); return; }

        SceneManager.LoadScene(m_nextSceneName);
    }

    // キーボード・ゲームパッド・マウスのいずれかの押下を検出する(「なんでもいいボタン」)。
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
