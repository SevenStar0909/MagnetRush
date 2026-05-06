using System;
using CriWare;
using CriWare.Assets;

/// <summary>
/// サウンド管理クラス
/// 再生の要求を受けて、CriAtomExPlayer を用いてサウンドを再生する。
/// 再生した後のサウンドの制御は Playback 構造体のインスタンスを通して行う。
/// </summary>
public class SoundManager : Singleton<SoundManager>
{
    private CriAtomExPlayer m_player;
    public CriAtomExPlayer Player { get => m_player; }

    /// <summary>
    /// サウンド再生の制御構造体
    /// CriAtomExPlayback のラッパーで、再生の一時停止や再開、音量やピッチの変更などを行う。
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
    /// サウンド再生要求を受けて、CriAtomExPlayer を用いてサウンドを再生する。
    /// 制御を行うための Playback 構造体のインスタンスを返す。
    /// </summary>
    public Playback StartPlayback(CriAtomCueReference cue, float vol = 1.0f, float pitch = 0)
    {
        Player.SetCue(cue.AcbAsset.Handle, cue.CueId);
        Player.SetVolume(vol);
        Player.SetPitch(pitch);
        Playback pb = new Playback(Player, Player.Start());
        return pb;
    }
}