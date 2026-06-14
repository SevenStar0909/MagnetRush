using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class TitleMenuSheetController : MonoBehaviour
{
    [Header("Any Button設定")]
    [SerializeField] private Image m_anyButtonImage;
    [SerializeField] private float m_anyButtonFps = 12f;
    [SerializeField] private Sprite[] m_anyButtonFadeInFrames;
    [SerializeField] private Sprite[] m_anyButtonIdleFrames; // 発光ループ
    [SerializeField] private Sprite[] m_anyButtonFadeOutFrames;

    [Header("メニューボタン設定")]
    [Tooltip("ボタンをまとめる親オブジェクト（最初は非表示）")]
    [SerializeField] private GameObject m_menuPanel;

    [Tooltip("AnyButtonが消えてからメニューが表示されるまでの待機時間（秒）")]
    [SerializeField] private float m_menuDisplayDelay = 0.5f;

    [SerializeField] private Image m_startButtonImage;
    [SerializeField] private Image m_exitButtonImage;

    [Tooltip("メニューボタンが透明から不透明になるまでの秒数")]
    [SerializeField] private float m_menuFadeInDuration = 0.5f;

    [Tooltip("選択されていない時の静止画")]
    [SerializeField] private Sprite m_startStaticSprite;
    [SerializeField] private Sprite m_exitStaticSprite;
    [Tooltip("選択されている時の待機ループコマ配列")]
    [SerializeField] private Sprite[] m_startIdleFrames;
    [SerializeField] private Sprite[] m_exitIdleFrames;

    [Tooltip("メニューボタンのアニメーション速度")]
    [SerializeField] private float m_menuButtonFps = 12f;

    // --- 内部状態管理 ---
    private bool m_canAnyButtonInput = false; // 「Any Button」入力待ち
    private bool m_isMenuInputActive = false; // メニュー入力が有効か
    private System.IDisposable m_anyButtonListener; // AnyButtonリスナー

    // メニューボタンのリストと現在選択中のインデックス
    private List<Image> m_menuButtons;
    private int m_currentSelectedIndex = 0;

    // アニメーション用コルーチンの参照
    private Coroutine m_anyButtonIdleCoroutine;
    private Coroutine m_menuAnimationCoroutine;

    void Start()
    {
        // 初期状態：Any Buttonのみ表示、メニューは非表示
        m_anyButtonImage.enabled = false;
        if (m_menuPanel != null) m_menuPanel.SetActive(false);

        StartCoroutine(StartSequence());
    }

    void OnEnable()
    {
        m_anyButtonListener = InputSystem.onAnyButtonPress.Call(ctrl => OnAnyButtonPressed());
    }

    void OnDisable()
    {
        m_anyButtonListener?.Dispose();
    }

    private void OnAnyButtonPressed()
    {
        if (m_canAnyButtonInput)
        {
            m_canAnyButtonInput = false;
            // アニメーション完了後にメニュー表示へ遷移
            StartCoroutine(AnyButtonFadeOutRoutine());
        }
    }

    private IEnumerator StartSequence()
    {
        yield return new WaitForSeconds(1.0f);
        m_anyButtonImage.enabled = true;

        yield return StartCoroutine(PlayAnimationOneShot(m_anyButtonImage, m_anyButtonFps, m_anyButtonFadeInFrames));

        m_canAnyButtonInput = true;
        m_anyButtonIdleCoroutine = StartCoroutine(LoopIdleAnimation(m_anyButtonImage, m_anyButtonFps, m_anyButtonIdleFrames, () => !m_isMenuInputActive));
    }

    private IEnumerator AnyButtonFadeOutRoutine()
    {
        // 待機ループ停止（コルーチンを強制終了させる）
        if (m_anyButtonIdleCoroutine != null) StopCoroutine(m_anyButtonIdleCoroutine);

        // ループの最後のコマ（明るい状態）を表示
        m_anyButtonImage.sprite = m_anyButtonIdleFrames[m_anyButtonIdleFrames.Length - 1];
        yield return null; // 1フレーム待機

        // フェードアウト再生
        yield return StartCoroutine(PlayAnimationOneShot(m_anyButtonImage, m_anyButtonFps, m_anyButtonFadeOutFrames));

        // 完全に消す
        m_anyButtonImage.enabled = false;

        // AnyButton入力リスナー解除
        m_anyButtonListener?.Dispose();
        m_anyButtonListener = null;

        // メニューを表示する前に指定した秒数だけ待機する
        yield return new WaitForSeconds(m_menuDisplayDelay);

        StartCoroutine(SetupMenuRoutine());
    }

    private IEnumerator SetupMenuRoutine()
    {
        m_menuButtons = new List<Image> { m_startButtonImage, m_exitButtonImage };

        // フェードイン開始前に、両方のボタンを静止画にしてアルファ値を0（完全透明）にする
        UpdateAllButtonsToStatic();
        SetAlpha(m_startButtonImage, 0f);
        SetAlpha(m_exitButtonImage, 0f);

        if (m_menuPanel != null) m_menuPanel.SetActive(true);

        // STARTとEXITの両方を同時にアルファフェードインさせる
        Coroutine startFade = StartCoroutine(FadeInAlpha(m_startButtonImage, m_menuFadeInDuration));
        Coroutine exitFade = StartCoroutine(FadeInAlpha(m_exitButtonImage, m_menuFadeInDuration));

        // 両方のフェードインが終わるまで待機
        yield return startFade;
        yield return exitFade;

        // 入力を有効化
        m_isMenuInputActive = true;

        // 0番（START）を選択状態にしてアニメーション開始
        m_currentSelectedIndex = 0;
        UpdateSelectedVisuals();
    }

    private void SetAlpha(Image img, float alpha)
    {
        if (img != null)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    private IEnumerator FadeInAlpha(Image targetImage, float duration)
    {
        float time = 0f;
        Color c = targetImage.color;

        while (time < duration)
        {
            time += Time.deltaTime; // 経過時間を加算
            c.a = Mathf.Clamp01(time / duration); // 0.0 ～ 1.0 の範囲に収める
            targetImage.color = c;
            yield return null; // 次のフレームまで待機
        }

        // 最後に確実に1.0（不透明）にする
        c.a = 1f;
        targetImage.color = c;
    }

    void Update()
    {
        // メニュー入力が無効な時は何もしない
        if (!m_isMenuInputActive) return;

        // --- 選択切り替え（Navigate） ---
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

        // キーボードの矢印キー、GamepadのD-Padまたはスティック「右」「左」
        bool moveRight = (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) ||
                         (gamepad != null && gamepad.dpad.right.wasPressedThisFrame) ||
                         (gamepad != null && gamepad.leftStick.right.wasPressedThisFrame && gamepad.leftStick.x.ReadValue() > 0.5f);

        bool moveLeft = (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) ||
                        (gamepad != null && gamepad.dpad.left.wasPressedThisFrame) ||
                        (gamepad != null && gamepad.leftStick.left.wasPressedThisFrame && gamepad.leftStick.x.ReadValue() < -0.5f);

        // 右を押したらEXIT(1)方向へ(+1)、左を押したらSTART(0)方向へ(-1)移動
        if (moveRight) ChangeSelection(1);
        if (moveLeft) ChangeSelection(-1);

        // --- 決定（Submit） ---
        // Enterキー、GamepadのAボタン/南ボタン
        bool submit = (keyboard != null && keyboard.enterKey.wasPressedThisFrame) ||
                      (keyboard != null && keyboard.numpadEnterKey.wasPressedThisFrame) ||
                      (gamepad != null && gamepad.aButton.wasPressedThisFrame) ||
                      (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

        if (submit)
        {
            m_isMenuInputActive = false; // 入力無効化
            StopMenuAnimation(); // アニメーション停止
            HandleSubmit();
        }
    }

    private void ChangeSelection(int direction)
    {
        // 移動後のインデックスを計算
        int nextIndex = m_currentSelectedIndex + direction;

        if (nextIndex < 0 || nextIndex >= m_menuButtons.Count)
        {
            return;
        }

        // 以前のアニメーション停止
        StopMenuAnimation();

        // 古い選択を静止画に戻す
        m_menuButtons[m_currentSelectedIndex].sprite = GetStaticSpriteByIndex(m_currentSelectedIndex);

        // インデックス更新（そのまま代入）
        m_currentSelectedIndex = nextIndex;

        // 新しい選択の見た目を更新
        UpdateSelectedVisuals();
    }

    private void UpdateSelectedVisuals()
    {
        // 選択されたボタンのアニメーションループを開始
        Image targetImage = m_menuButtons[m_currentSelectedIndex];
        Sprite[] frames = GetIdleFramesByIndex(m_currentSelectedIndex);

        // 制御フラグを使ったループ開始
        m_menuAnimationCoroutine = StartCoroutine(LoopIdleAnimation(targetImage, m_menuButtonFps, frames, () => m_isMenuInputActive));
    }

    private void UpdateAllButtonsToStatic()
    {
        for (int i = 0; i < m_menuButtons.Count; i++)
        {
            m_menuButtons[i].sprite = GetStaticSpriteByIndex(i);
        }
    }

    private void StopMenuAnimation()
    {
        if (m_menuAnimationCoroutine != null)
        {
            StopCoroutine(m_menuAnimationCoroutine);
            m_menuAnimationCoroutine = null;
        }
    }

    private Sprite GetStaticSpriteByIndex(int index)
    {
        return index == 0 ? m_startStaticSprite : m_exitStaticSprite;
    }

    private Sprite[] GetIdleFramesByIndex(int index)
    {
        return index == 0 ? m_startIdleFrames : m_exitIdleFrames;
    }

    private void HandleSubmit()
    {
        if (m_currentSelectedIndex == 0)
        {
            // START
            Debug.Log("STARTボタンが決定されました。");
            SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StageSelectScene);
        }
        else
        {
            // EXIT
            Debug.Log("EXITボタンが決定されました。");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }
    }

    // 1回だけ再生するコルーチン
    private IEnumerator PlayAnimationOneShot(Image targetImage, float fps, Sprite[] frames)
    {
        float delay = 1f / fps; // 1フレームあたりの待機時間
        for (int i = 0; i < frames.Length; i++)
        {
            targetImage.sprite = frames[i];
            yield return new WaitForSeconds(delay);
        }
    }

    // ループ再生するコルーチン（制御フラグを追加）
    private IEnumerator LoopIdleAnimation(Image targetImage, float fps, Sprite[] frames, System.Func<bool> shouldContinueCondition)
    {
        float delay = 1f / fps;
        int index = 0;

        // 制御条件がtrueの間、無限ループ
        while (shouldContinueCondition())
        {
            targetImage.sprite = frames[index];
            index = (index + 1) % frames.Length; // 最後まで行ったら0に戻る計算
            yield return new WaitForSeconds(delay);
        }
    }
}