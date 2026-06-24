using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲームオーバー演出。プレイヤーが HP0 で死亡したら起動する。
/// スプライトシート(1〜4: 横m_columns×縦m_rows のコマ)を順番にアニメ再生 → 5 を静止表示 →
/// RETRY/SELECT メニューを表示し、矢印カーソルを上下で動かして決定する。
/// RETRY=同じステージを再読込 / SELECT=ステージ選択シーンへ。
/// 演出中は Time.timeScale=0 で凍結する(PlayerRespawner の自動リスポーン待機も止まる)。
/// 依存: Player(static Current/OnPlayerReady), Health.OnDie, UnityEngine.SceneManagement
/// 設計: テクスチャは RawImage の uvRect でコマ送りするのでスプライト化(スライス)不要。
/// </summary>
public class GameOverPresentation : MonoBehaviour
{
    [Header("演出テクスチャ")]
    [Tooltip("順番に再生するスプライトシート(1〜4)。各シートを m_columns×m_rows のコマでアニメする")]
    [SerializeField] private Texture[] m_sheetTextures;
    [Tooltip("1〜4の後に再生する最後のシート(5)。m_staticColumns×m_staticRows のコマでアニメする")]
    [SerializeField] private Texture m_staticTexture;
    [Tooltip("RETRY/SELECT メニュー画像")]
    [SerializeField] private Texture m_menuTexture;
    [Tooltip("選択カーソルの矢印画像")]
    [SerializeField] private Texture m_arrowTexture;

    [Header("シート設定")]
    [Tooltip("シート1枚の横のコマ数")]
    [SerializeField] private int m_columns = 4;
    [Tooltip("シート1枚の縦のコマ数")]
    [SerializeField] private int m_rows = 7;
    [Tooltip("コマ送りの速さ(1秒あたりのコマ数)")]
    [SerializeField] private float m_fps = 16f;
    [Tooltip("5(最後のシート)の横のコマ数")]
    [SerializeField] private int m_staticColumns = 4;
    [Tooltip("5(最後のシート)の縦のコマ数")]
    [SerializeField] private int m_staticRows = 3;
    [Tooltip("5を再生し終えたあと、メニュー表示までの追加待ち秒数")]
    [SerializeField] private float m_staticHold = 0.6f;

    [Header("UI参照")]
    [Tooltip("演出全体の表示ON/OFFを切り替える親オブジェクト")]
    [SerializeField] private GameObject m_root;
    [Tooltip("1〜5・メニューを表示する全画面 RawImage")]
    [SerializeField] private RawImage m_sequenceImage;
    [Tooltip("選択カーソルの RawImage")]
    [SerializeField] private RawImage m_arrowImage;

    [Header("黒フェード")]
    [Tooltip("背景を黒く暗転させる全画面 Image(演出の後ろに置く)")]
    [SerializeField] private Image m_fadeImage;
    [Tooltip("黒フェードの所要時間(秒)")]
    [SerializeField] private float m_fadeDuration = 0.5f;
    [Tooltip("黒フェードの最終濃さ(1で真っ黒)。シート再生中はこの濃さ(現状0.4)で背後のゲーム画面をうっすら見せる")]
    [SerializeField] private float m_fadeTargetAlpha = 1f;
    [Tooltip("シートを再生し終えたあと、さらに真っ黒まで深める二段目フェードの所要時間(秒)")]
    [SerializeField] private float m_finalFadeDuration = 0.5f;
    [Tooltip("二段目フェードの最終濃さ(1で真っ黒)。RETRY/SELECTメニューはこの黒背景の上に出る")]
    [SerializeField] private float m_finalFadeTargetAlpha = 1f;

    [Header("死亡ビート")]
    [Tooltip("ゲームオーバー画面を出す前に、スローで死亡アニメを見せる時間(秒・実時間)。0で即ゲームオーバー")]
    [SerializeField] private float m_deathBeatDuration = 1.5f;
    [Tooltip("死亡を見せている間の時間の遅さ。0=完全停止だが、0.3前後のスローの方が死亡モーションが見えて良い")]
    [SerializeField] private float m_deathBeatTimeScale = 0.35f;

    [Header("矢印の位置(anchoredPosition)")]
    [Tooltip("RETRY を選んでいるときの矢印位置")]
    [SerializeField] private Vector2 m_arrowRetryPos = new Vector2(-450f, -150f);
    [Tooltip("SELECT を選んでいるときの矢印位置")]
    [SerializeField] private Vector2 m_arrowSelectPos = new Vector2(-450f, -250f);

    [Header("遷移先シーン")]
    [Tooltip("RETRY で読み込むシーン名(空なら現在のシーンを再読込)")]
    [SerializeField] private string m_retrySceneName = "";
    [Tooltip("SELECT で読み込むシーン名")]
    [SerializeField] private string m_selectSceneName = "StageSelectScene";

    private Health m_health;
    private bool m_menuActive;
    private bool m_playing;
    private int m_index;

    void Awake()
    {
        if (m_root != null) m_root.SetActive(false);
    }

    void OnEnable()
    {
        Player.OnPlayerReady += HandlePlayerReady;
        if (Player.Current != null) Bind(Player.Current);
    }

    void OnDisable()
    {
        Player.OnPlayerReady -= HandlePlayerReady;
        Unbind();
    }

    private void HandlePlayerReady(Player player)
    {
        Hide();
        Bind(player);
    }

    private void Bind(Player player)
    {
        Unbind();
        if (player == null) { ChannelLogger.LogGuardReturn("Game", "Player が null"); return; }
        m_health = player.GetComponent<Health>();
        if (m_health == null) { ChannelLogger.LogGuardReturn("Game", "Player に Health が無い"); return; }
        m_health.OnDie += HandleDie;
    }

    private void Unbind()
    {
        if (m_health != null) m_health.OnDie -= HandleDie;
        m_health = null;
    }

    private void HandleDie() => PlayGameOver();

    /// <summary>プレイヤーの死亡を待たずにゲームオーバー演出を再生する。デバッグボタンからも呼ぶ。</summary>
    public void PlayGameOver()
    {
        if (m_playing) { ChannelLogger.LogGuardReturn("Game", "ゲームオーバー演出が既に再生中"); return; }
        if (m_sequenceImage == null) { ChannelLogger.LogGuardReturn("Game", "m_sequenceImage 未設定"); return; }
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        m_playing = true;
        m_menuActive = false;

        // ゲームオーバーUIを出す前に、スローで死亡アニメをそのまま見せる短いビート。
        // 死亡した瞬間に timeScale=0＋暗転すると死亡モーションが0フレームで凍って見えないため挟む。
        yield return DeathBeatRoutine();

        if (m_root != null) m_root.SetActive(true);
        if (m_arrowImage != null) m_arrowImage.gameObject.SetActive(false);

        Time.timeScale = 0f;

        // 死亡直後から背景を黒くフェードイン(演出の後ろ)。シート再生と並行で進める。
        SetFadeAlpha(0f);
        StartCoroutine(FadeRoutine());

        float frameDur = m_fps > 0f ? 1f / m_fps : 0.0625f;
        int perSheet = Mathf.Max(1, m_columns * m_rows);

        if (m_sheetTextures != null)
        {
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

        if (m_staticTexture != null)
        {
            m_sequenceImage.texture = m_staticTexture;
            int finalTotal = Mathf.Max(1, m_staticColumns * m_staticRows);
            for (int f = 0; f < finalTotal; f++)
            {
                m_sequenceImage.uvRect = FrameUv(f, m_staticColumns, m_staticRows);
                yield return WaitUnscaled(frameDur);
            }
            if (m_staticHold > 0f) yield return WaitUnscaled(m_staticHold);
        }

        // シートを再生し終えたら、背景をさらに真っ黒まで深めてからメニューを出す
        yield return FadeToBlackRoutine();

        ShowMenu();
    }

    // 死亡直後、ゲームオーバー画面の前にスローで死亡アニメを見せる間。実時間(unscaled)で計る。
    private IEnumerator DeathBeatRoutine()
    {
        if (m_deathBeatDuration <= 0f) yield break;

        Time.timeScale = Mathf.Clamp(m_deathBeatTimeScale, 0.01f, 1f);

        float t = 0f;
        while (t < m_deathBeatDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        // この後 PlayRoutine が timeScale=0 にして暗転へ進む
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

    private void ShowMenu()
    {
        if (m_menuTexture != null)
        {
            m_sequenceImage.texture = m_menuTexture;
            m_sequenceImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
        if (m_arrowImage != null)
        {
            if (m_arrowTexture != null) m_arrowImage.texture = m_arrowTexture;
            m_arrowImage.gameObject.SetActive(true);
        }
        m_index = 0;
        UpdateArrow();
        m_menuActive = true;
    }

    void Update()
    {
        if (m_menuActive)
        {
            int dir = ReadVertical();
            if (dir != 0)
            {
                int next = Mathf.Clamp(m_index + dir, 0, 1);
                if (next != m_index)
                {
                    m_index = next;
                    UpdateArrow();
                    // 矢印が実際に動いた時だけ移動音を鳴らす（端で押し続けても鳴らさない）
                    Sound.Play(SoundData.CueSheet.SE, SoundData.SE.Select);
                }
            }
            if (ReadSubmit()) Confirm();
        }
    }

    private void UpdateArrow()
    {
        if (m_arrowImage != null)
            m_arrowImage.rectTransform.anchoredPosition = m_index == 0 ? m_arrowRetryPos : m_arrowSelectPos;
    }

    private void Confirm()
    {
        m_menuActive = false;
        // 決定音。SoundManager は DontDestroyOnLoad なのでシーン遷移しても鳴り切る（SceneSelectUI と同じ）
        Sound.Play(SoundData.CueSheet.SE, SoundData.SE.Button);
        Time.timeScale = 1f;

        if (m_index == 0)
        {
            string scene = string.IsNullOrEmpty(m_retrySceneName) ? SceneManager.GetActiveScene().name : m_retrySceneName;
            SceneManager.LoadScene(scene);
        }
        else
        {
            SceneManager.LoadScene(m_selectSceneName);
        }
    }

    private void Hide()
    {
        StopAllCoroutines();
        m_playing = false;
        m_menuActive = false;
        Time.timeScale = 1f;   // 死亡ビートのスローが残らないよう、復帰時は必ず通常速度へ戻す
        SetFadeAlpha(0f);
        if (m_root != null) m_root.SetActive(false);
    }

    private IEnumerator FadeRoutine()
    {
        if (m_fadeImage == null) yield break;
        float t = 0f;
        while (t < m_fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = m_fadeDuration > 0f ? Mathf.Clamp01(t / m_fadeDuration) : 1f;
            SetFadeAlpha(k * m_fadeTargetAlpha);
            yield return null;
        }
        SetFadeAlpha(m_fadeTargetAlpha);
    }

    // シート再生後、現在の濃さ(m_fadeTargetAlpha)からさらに真っ黒(m_finalFadeTargetAlpha)まで深める二段目フェード。
    private IEnumerator FadeToBlackRoutine()
    {
        if (m_fadeImage == null) yield break;
        float start = m_fadeImage.color.a;
        if (m_finalFadeDuration <= 0f) { SetFadeAlpha(m_finalFadeTargetAlpha); yield break; }
        float t = 0f;
        while (t < m_finalFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / m_finalFadeDuration);
            SetFadeAlpha(Mathf.Lerp(start, m_finalFadeTargetAlpha, k));
            yield return null;
        }
        SetFadeAlpha(m_finalFadeTargetAlpha);
    }

    private void SetFadeAlpha(float a)
    {
        if (m_fadeImage == null) return;
        var c = m_fadeImage.color;
        c.a = a;
        m_fadeImage.color = c;
    }

    // 下=+1(SELECTへ) / 上=-1(RETRYへ)。キーボードとゲームパッド両対応。
    private int ReadVertical()
    {
        int d = 0;
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) d = 1;
            else if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) d = -1;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            if (g.dpad.down.wasPressedThisFrame || g.leftStick.down.wasPressedThisFrame) d = 1;
            else if (g.dpad.up.wasPressedThisFrame || g.leftStick.up.wasPressedThisFrame) d = -1;
        }
        return d;
    }

    private bool ReadSubmit()
    {
        var k = Keyboard.current;
        if (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) return true;
        var g = Gamepad.current;
        if (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame)) return true;
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
