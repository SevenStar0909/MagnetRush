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

using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement;
using UnityEngine.AddressableAssets;

namespace CriWare.Assets
{
	public class CriDummyAssetBundleResource : IAssetBundleResource
	{
		public AssetBundle GetAssetBundle() => null;
	}

	/**
	 * <summary>CRIアセットのキャッシュ向けの Addressables 向けリソースプロバイダークラス</summary>
	 */
	[System.ComponentModel.DisplayName("Cri Resource Provider")]
	public class CriResourceProvider : ResourceProviderBase
	{
		public override string ProviderId => GetType().FullName;

		public override void Provide(ProvideHandle providerInterface)
		{
			var task = ProvideAsync(providerInterface);
		}

		async Task ProvideAsync(ProvideHandle providerInterface)
		{
			try
			{
				var location = providerInterface.Location;

				var pathset = CriAddressables.ResourceLocation2Path(location);
				var data = location.Data as AssetBundleRequestOptions;

				if (!File.Exists(pathset.local) && (pathset.remote != pathset.local))
				{
					var dir = Path.GetDirectoryName(pathset.local);
					if (Directory.Exists(dir))
						Directory.Delete(dir, true);
					var filename = Path.GetFileName(pathset.remote).Split('?')[0];
					var request = new UnityWebRequest(pathset.remote.Replace(filename, Uri.EscapeDataString(filename)), UnityWebRequest.kHttpVerbGET);
					var tmpPath = Path.Combine(dir, Guid.NewGuid().ToString().Replace("-", ""));
					var handler = new DownloadHandlerFile(tmpPath);
					handler.removeFileOnAbort = true;
					request.downloadHandler = handler;
					// request.timeout = data.Timeout;
#if ADDRESSABLES_1_19_4_OR_NEWER
					providerInterface.ResourceManager.WebRequestOverride?.Invoke(request);
#endif
					providerInterface.SetProgressCallback(() => request.downloadProgress);
#if ADDRESSABLES_1_14_2_OR_NEWER
					providerInterface.SetDownloadProgressCallbacks(() => new DownloadStatus()
					{
						IsDone = request.isDone,
						DownloadedBytes = (long)request.downloadedBytes,
						TotalBytes = data.BundleSize,
					});
#endif

					var task =
#if ADDRESSABLES_1_20_0_OR_NEWER
						(await UnityEngine.ResourceManagement.WebRequestQueue.QueueRequest(request).ToTask()).ToTask();
#else
						request.SendWebRequest().ToTask();
#endif

					float currentTime = 0;
					ulong currentBytes = 0;
					using var updateReciever = data.Timeout > 0 ?
						new AnonymousUpdateReciever(deltaTime => {
							if(request.downloadedBytes != currentBytes)
								currentTime = 0;
							currentBytes = request.downloadedBytes;
							currentTime += deltaTime;
							if(currentTime > data.Timeout)
								request.Abort();
						}) : null;

					await task;

#if UNITY_2020_2_OR_NEWER
					if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError || request.result == UnityWebRequest.Result.ProtocolError)
#else
					if (request.isHttpError || request.isNetworkError)
#endif
					{
						if (File.Exists(tmpPath))
							File.Delete(tmpPath);
						var exception = new Exception(string.Format(
								"CriResourceProvider unable to load from url {0}, result='{1}'.", request.url,
								currentTime > data.Timeout ? "Request timeout" : request.error));
						providerInterface.Complete<CriDummyAssetBundleResource>(null, false, exception);
						return;
					}
					
					File.Move(tmpPath, pathset.local);
#if ENABLE_CACHING
					var infoPath = Path.Combine(Path.GetDirectoryName(pathset.local), "__info");
					if (!File.Exists(infoPath))
#if UNITY_2021_3_OR_NEWER
						await File.WriteAllTextAsync(infoPath, $"-1\n0\n1\n{Path.GetFileName(pathset.local)}\n");
#else
						File.WriteAllText(infoPath, $"-1\n0\n1\n{Path.GetFileName(pathset.local)}\n");
#endif
#endif
				}

#if ENABLE_CACHING
				Caching.MarkAsUsed(data.BundleName, Hash128.Parse(data.Hash));
#endif
				CriAddressables.AddCachePath(Path.GetFileName(pathset.local), pathset.local, data.BundleSize);
				CriAddressables.AddCachePath(data.BundleName, pathset.local, data.BundleSize);

				providerInterface.Complete(new CriDummyAssetBundleResource(), true, null);
			}
			catch (Exception e)
			{
				providerInterface.Complete<CriDummyAssetBundleResource>(null, false, e);
				return;
			}
		}

		public override Type GetDefaultType(IResourceLocation location) => typeof(IAssetBundleResource);

		public override void Release(IResourceLocation location, object asset) { }
	}

	class AnonymousUpdateReciever : IUpdateReceiver, IDisposable
	{
		Action<float> update;
		public AnonymousUpdateReciever(Action<float> update){
			this.update = update;
			Addressables.ResourceManager.AddUpdateReceiver(this);
		}

		public void Dispose()
		{
			this.update = null;
			Addressables.ResourceManager.RemoveUpdateReciever(this);
		}

		public void Update(float unscaledDeltaTime) => update?.Invoke(unscaledDeltaTime);
	}

	static class AsyncOperationExtensions
	{
#if ADDRESSABLES_1_20_0_OR_NEWER
		public static Task<UnityWebRequestAsyncOperation> ToTask(this UnityEngine.ResourceManagement.WebRequestQueueOperation operation)
		{
			var source = new TaskCompletionSource<UnityWebRequestAsyncOperation>();
			if (operation.IsDone)
				source.SetResult(operation.Result);
			else
				operation.OnComplete += op => source.SetResult(op);
			return source.Task;
		}
#endif

		public static Task<bool> ToTask(this AsyncOperation operation)
		{
			var source = new TaskCompletionSource<bool>();
			if (operation.isDone)
				source.SetResult(true);
			else
				operation.completed += op => source.SetResult(true);
			return source.Task;
		}
	}
}

#endif

/** @} */
