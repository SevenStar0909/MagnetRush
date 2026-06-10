using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

/// <summary>
/// シーン遷移中のロード画面。背景プール（画像＋動画）からランダムに1つ表示する。
/// timeScaleに依存せず動くので、ゲームオーバー中（timeScale=0）の遷移でも機能する。
/// 依存: CanvasGroup, Image, RawImage, VideoPlayer
/// </summary>
public class LoadingScreenView : MonoBehaviour
{
    [SerializeField] private CanvasGroup m_canvasGroup;
    [SerializeField] private Image m_pictureImage;
    [SerializeField] private RawImage m_videoImage;
    [SerializeField] private VideoPlayer m_videoPlayer;

    private LoadingScreenSettings m_settings;
    private RenderTexture m_videoTexture;

    /// <summary>設定を受け取り非表示状態で初期化する。生成直後に必ず呼ぶ</summary>
    public void Initialize(LoadingScreenSettings settings)
    {
        m_settings = settings;
        m_canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>背景を抽選して表示し、フェードインする</summary>
    public IEnumerator ShowRoutine()
    {
        gameObject.SetActive(true);
        SetupRandomBackground();
        yield return FadeRoutine(0f, 1f);
    }

    /// <summary>フェードアウトして非表示に戻す</summary>
    public IEnumerator HideRoutine()
    {
        yield return FadeRoutine(1f, 0f);
        StopVideo();
        gameObject.SetActive(false);
    }

    /// <summary>画像と動画を合算したプールからランダムに1つ選んで表示する</summary>
    private void SetupRandomBackground()
    {
        StopVideo();
        m_pictureImage.enabled = false;
        m_videoImage.enabled = false;

        int spriteCount = m_settings.backgroundSprites != null ? m_settings.backgroundSprites.Length : 0;
        int videoCount = m_settings.backgroundVideos != null ? m_settings.backgroundVideos.Length : 0;
        int total = spriteCount + videoCount;
        if (total == 0)
        {
            ChannelLogger.LogGuardReturn("UI", "ロード画面の背景が未設定（黒背景のみ表示）");
            return;
        }

        int index = Random.Range(0, total);
        if (index < spriteCount)
        {
            m_pictureImage.sprite = m_settings.backgroundSprites[index];
            m_pictureImage.enabled = true;
        }
        else
        {
            PlayVideo(m_settings.backgroundVideos[index - spriteCount]);
        }
    }

    private void PlayVideo(VideoClip clip)
    {
        if (clip == null)
        {
            ChannelLogger.LogGuardReturn("UI", "背景動画リストにnullが入っている");
            return;
        }

        // 動画サイズに合わせたRenderTextureを毎回作る（解像度の違う動画が混在できるように）
        m_videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
        m_videoPlayer.targetTexture = m_videoTexture;
        m_videoImage.texture = m_videoTexture;
        m_videoImage.enabled = true;

        m_videoPlayer.clip = clip;
        m_videoPlayer.isLooping = m_settings.loopVideo;
        m_videoPlayer.audioOutputMode = m_settings.playVideoAudio
            ? VideoAudioOutputMode.Direct
            : VideoAudioOutputMode.None;
        m_videoPlayer.Play();
    }

    private void StopVideo()
    {
        if (m_videoPlayer != null && m_videoPlayer.isPlaying)
            m_videoPlayer.Stop();

        if (m_videoTexture != null)
        {
            m_videoPlayer.targetTexture = null;
            m_videoImage.texture = null;
            m_videoTexture.Release();
            Destroy(m_videoTexture);
            m_videoTexture = null;
        }
    }

    /// <summary>unscaledDeltaTimeでフェードする（timeScale=0でも動かすため）</summary>
    private IEnumerator FadeRoutine(float from, float to)
    {
        float duration = m_settings != null ? m_settings.fadeDuration : 0.3f;
        if (duration <= 0f)
        {
            m_canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            m_canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        m_canvasGroup.alpha = to;
    }
}
