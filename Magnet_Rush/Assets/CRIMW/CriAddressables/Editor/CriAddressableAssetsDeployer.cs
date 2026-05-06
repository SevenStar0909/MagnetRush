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
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET

using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using System.Linq;
using System.IO;
using System.Collections.Generic;

using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace CriWare.Assets
{
	/**
	 * <summary>CRI Addressables のデータデプロイを行うエディタクラス</summary>
	 */
	public static class CriAddressableAssetsDeployer
	{
#pragma warning disable CS0067
		[System.Obsolete]
		public static event System.Action OnComplete;
#pragma warning restore CS0067

		[InitializeOnLoadMethod]
		static void RegisterHook()
		{
			ContentPipeline.BuildCallbacks.PostWritingCallback += (param, deps, data, result) => {
				// only addressable build.
				if (!(param is UnityEditor.AddressableAssets.Build.DataBuilders.AddressableAssetsBundleBuildParameters))
					return ReturnCode.Success;
				var buildResult = result as IBundleBuildResults;
				if (buildResult == null) return ReturnCode.Success;
				return DeployAsWriteBundle(buildResult);
			};
		}

		static ReturnCode DeployAsWriteBundle(IBundleBuildResults buildResults)
		{
			if (CriAddressablesSetting.Mode == CriAddressablesSetting.DeployMode.UseCriBuildScript) return ReturnCode.Success;
			var addressableEntries = 
				CriAddressablesEditor.CollectDependentAssets<CriAddressableAssetImpl>()
				.Select(a => new {
					asset = a,
					entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath((a.Implementation as CriAddressableAssetImpl).anchor)))
				})
				.Where(pair => pair.entry.parentGroup.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>()?.IncludeInBuild ?? false)
				.Distinct().ToList();
			var groups = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetGroup>();
			foreach(var pair in addressableEntries)
			{
				groups.Add(pair.entry.parentGroup);
				var bundleName = CriAddressablesEditor.CalclateBundleName(pair.entry);
				var path = buildResults.BundleInfos.FirstOrDefault(info => Path.ChangeExtension(info.Key, null) == bundleName).Value.FileName;
				CriAddressablesEditor.DeployData(AssetDatabase.GetAssetPath(pair.asset), path);
			}

			foreach (var g in groups.Distinct())
				CriAddressableGroupGenerator.ValidateGroup(g);

			Debug.Log($"[CRIWARE] CriAddressableAssetsDeployer copied {addressableEntries.Count} files.");
			return ReturnCode.Success;
		}
	}
}

#endif
#endif

/** @} */
