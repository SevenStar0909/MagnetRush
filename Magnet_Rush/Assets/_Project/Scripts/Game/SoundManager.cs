using System;
using CriWare;
using CriWare.Assets;

/// <summary>
/// <para>サウンド管理クラス</para>
/// <para>再生の要求を受けて、CriAtomExPlayer を用いてサウンドを再生する。</para>
/// <para>再生した後のサウンドの制御は Playback 構造体のインスタンスを通して行う。</para>
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    private CriAtomExPlayer m_player;
    public CriAtomExPlayer Player { get => m_player; }

    /// <summary>
    /// <para>サウンド再生の制御構造体</para>
    /// <para>CriAtomExPlayback のラッパーで、再生の一時停止や再開、音量やピッチの変更などを行う。</para>
    /// </summary>
    public struct Playback
    {
        CriAtomExPlayer player;
        CriAtomExPlayback playBack;

        internal Playback(CriAtomExPlayer player, CriAtomExPlayback pb)
        {
            this.player = player;
            this.playBack = pb;
        }

        public void Pause()
        {
            playBack.Pause();
        }

        public void Resume()
        {
            playBack.Resume(CriAtomEx.ResumeMode.PausedPlayback);
        }

        public bool IsPaused()
        {
            return playBack.IsPaused();
        }

        public void SetVolumeAndPitch(float vol, float pitch)
        {
            this.player.SetVolume(vol);
            this.player.SetPitch(pitch);
            this.player.Update(playBack);
        }

        public void Stop()
        {
            this.playBack.Stop();
        }

        public bool IsPlaying()
        {
            return this.playBack.GetStatus() == CriAtomExPlayback.Status.Playing;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        this.m_player = new CriAtomExPlayer();
        DontDestroyOnLoad(gameObject);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        m_player?.Dispose();
        m_player = null;
    }

    /// <summary>
    /// <para>サウンド再生要求を受けて、CriAtomExPlayer を用いてサウンドを再生する。</para>
    /// <para>制御を行うための Playback 構造体のインスタンスを返す。</para>
    /// <para>サウンドキューの定数クラスを使ってファイルを指定する。</para>
    /// </summary>
    public Playback StartPlayback(string cueSheetName, string cueName, float vol = 1.0f, float pitch = 0)
    {
        var acb = CriAtomExAcb.LoadAcbFile(null, cueSheetName + ".acb", "");
        Player.SetCue(acb, cueName);
        Player.SetVolume(vol);
        Player.SetPitch(pitch);
        Playback pb = new Playback(Player, Player.Start());
        return pb;
    }
}