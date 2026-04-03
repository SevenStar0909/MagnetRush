using UnityEngine;

/// <summary>
/// Rendering Layer Maskの一元管理。アウトライン等のレンダリング用フィルタリングに使用。
/// Physics Layer（PhysicsLayers.cs）とは独立。
/// </summary>
public static class RenderingLayers
{
    /// <summary>磁化オブジェクトのアウトライン描画対象。TagManager.asset Rendering Layers Index 8。</summary>
    public static uint Magnetized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        // Index 8はTagManager.assetのRendering Layersの9番目に"Magnetized"がある前提。
        // 変更する場合はここも更新すること。
        Magnetized = 1u << 8;
    }
}
