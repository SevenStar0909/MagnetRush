/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ADDRESSABLES_INTEGRATION
 * @{
 */

#if CRI_USE_ADDRESSABLES

using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;

[assembly:InternalsVisibleTo("CriMw.CriWare.Assets.Addressables.Editor")]

namespace CriWare.Assets
{
	/**
	 * <summary>CRI Addressables の必須機能を提供するクラス</summary>
	 */
	public static class CriAddressables
	{
		private const string scriptVersionString = "0.10.03";

		internal static string VersionString => scriptVersionString;

#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
		/**
		 * <summary>リソース情報更新処理</summary>
		 * <remarks>
		 * <para header='説明'>コンテンツカタログから読み込んだ CRIWARE リソース情報を更新します。<br/>
		 * Addressables の初期化後、すべてのコンテンツカタログのロードが完了した後に呼び出してください。<br/>
		 * 本メソッドを呼び出さなかった場合、コンテンツのダウンロードサイズが正しく取得できません。</para>
		 * </remarks>
		 */
		public static void ModifyLocators()
		{
			var original2criLocation = new Dictionary<IResourceLocation, IResourceLocation>();

			var locations =
					Addressables.ResourceLocators.
					Where(locator => locator is ResourceLocationMap).
					SelectMany(map => (map as ResourceLocationMap).Locations);
			foreach (var list in locations)
			{
				for (int i = 0; i < list.Value.Count; i++)
				{
					if (list.Value[i].ProviderId != typeof(CriResourceProvider).FullName) continue;
					if (list.Value[i] is CriResourceLocation) continue;

					if (!original2criLocation.ContainsKey(list.Value[i]))
						original2criLocation.Add(list.Value[i], new CriResourceLocation(list.Value[i]));

					list.Value[i] = original2criLocation[list.Value[i]];
				}
			}
		}
#endif

		/**
		 * <summary>アセットのキャッシュのクリア</summary>
		 * <remarks>
		 * <para header='説明'>Addressables 経由でダウンロードされた実データのキャッシュを削除します。<br/>
		 * アセットの DeployType が Addressables でない場合は何も行われません。</para>
		 * </remarks>
		 */
		public static void ClearAddressableCache(this CriAssetBase asset)
		{
			(asset.Implementation as CriAddressableAssetImpl)?.ClearCache();
		}

		static Dictionary<string, string> _filename2Path { get; } = new Dictionary<string, string>();
		static Dictionary<string, long> _filename2Size { get; } = new Dictionary<string, long>();
		internal static void AddCachePath(string fileName, string path, long size)
		{
			if (_filename2Path.ContainsKey(fileName)) return;
			_filename2Path.Add(fileName, path);
			_filename2Size.Add(fileName, size);
		}
		internal static string Filename2CachePath(string fileName) => _filename2Path.ContainsKey(fileName) ? _filename2Path[fileName] : null;
		internal static long Filename2CahceSize(string fileName) => _filename2Size.ContainsKey(fileName) ? _filename2Size[fileName] : 0;

		internal static (string remote, string local) ResourceLocation2Path(IResourceLocation location)
		{
			var internalId =
#if ADDRESSABLES_1_13_1_OR_NEWER
				Addressables.ResourceManager.TransformInternalId(location);
#else
				location.InternalId;
#endif
			var remotePath = internalId;

			var data = location.Data as AssetBundleRequestOptions;

			// AsssetBundleProvider.GetLoadInfo
			// returning set with the same path means load directory wothout webrequest.
			if (Application.platform == RuntimePlatform.Android && internalId.StartsWith("jar:"))
				if (!data.UseUnityWebRequestForLocalBundles)
					return (remotePath, remotePath);
			if (!ResourceManagerConfig.ShouldPathUseWebRequest(internalId))
				if (!data.UseUnityWebRequestForLocalBundles)
					return (remotePath, remotePath);
				else
					remotePath = $"file:///{Path.GetFullPath(remotePath)}";


#if ENABLE_CACHING
			var cacheRoot = Caching.currentCacheForWriting.path;
#else
			var cacheRoot = CriWare.Common.installCachePath;
#endif // ENABLE_CACHING

			if (!Directory.Exists(cacheRoot))
				Directory.CreateDirectory(cacheRoot);

			var requestOptions = location.Data as AssetBundleRequestOptions;

			var localPath = Path.Combine(cacheRoot, requestOptions.BundleName, requestOptions.Hash, Path.GetFileName(location.InternalId));

			return (remotePath, localPath);
		}
	}
}

#endif

/** @} */
