/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ASSETS_INTEGRATION
 * @{
 */



namespace CriWare.Assets {

    /**
     * <summary>プレビュー向けプレーヤーインターフェース</summary>
     * <remarks>
     * <para header='説明'>
     * 音声や動画など、再生元リソースの形式を問わず扱えるようにした、<br/>
     * 「プレビュー」を行うためのプレーヤーインターフェースです。<br/>
     * </para>
     *  <para header='注意'>
     * 本インターフェースのAPIはコントロール用クラスから呼び出されることを想定しています。<br/>
     * </para>
     * </remarks>
     */
    internal interface IPreviewPlayer {
        bool IsPlaying { get; }
        float Speed { get; set; }
        float GetPlaybackTimeFromPlayer { get; }

        void CreatePlayer();

        void OnEnable();

        void OnDisable();

        void DestroyPlayer();

        void Play();

        void Pause();

        void Stop();

        bool UpdatePlayer();

        void SetAsset(CriAssetBase asset);

        void Seek(float seekPosition);
    }
}

/** @} */
