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

using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Settings;

namespace CriWare.Assets
{
	/// <summary>
	/// CRI Addressables向けビルドスクリプト
	/// </summary>
	[CreateAssetMenu(menuName = "CRIWARE/Cri Addressables Build Script")]
	public class CriAddressablesBuildScript : BuildScriptPackedMode
	{
		[InitializeOnLoadMethod]
		static void RegisterValidation() =>
			BuildScript.buildCompleted += (AddressableAssetBuildResult result) => {
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
				if (CriAddressablesSetting.Mode != CriAddressablesSetting.DeployMode.UseCriBuildScript) return;
#endif
				if (typeof(CriAddressablesBuildScript).IsAssignableFrom(UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder.GetType())) return;
				Debug.LogWarning("CRI Addressables needs CriAddressablesBuildScript or its derived class for build script.");
			};

		[SerializeField]
		bool autoRegisterAnchor = true;
		[SerializeField, SerializedTypeRestriction(type = typeof(IResourceProvider))]
		protected SerializedType criProviderType = new SerializedType() { Value = typeof(CriResourceProvider) };
		[Space, SerializeField]
		string subFolderName;
		[Space, SerializeField]
		string suffix =
#if UNITY_WEBGL
			".bundle";
#else
			"";
#endif

		const string dummyGroupGuid = "00000000000000000000000000000000";
		const string dummyGroupName = "cri_addressables_anchor_group";
		const string dummyAnchorName = "cri_addressables_dummy_anchor";
		static readonly List<string> guidFields = new List<string> {
			"m_guid",
			"m_GUID",
		};

		string AnchorGuid => AssetDatabase.FindAssets($"t:{nameof(CriLocalAddressablesAnchor)} LocalAnchor").First();

		protected BundledAssetGroupSchema SetupDummyGroupSchema(AddressableAssetSettings settings, BundledAssetGroupSchema original, SerializedType providerType)
		{
			var ret = Instantiate(original);

			var variables = settings.profileSettings.GetVariableNames();
			var buildPath = variables.FirstOrDefault(name => name.ToLowerInvariant().Contains("local") && name.ToLowerInvariant().Contains("build"));
			var loadPath = variables.FirstOrDefault(name => name.ToLowerInvariant().Contains("local") && name.ToLowerInvariant().Contains("load"));
			if (string.IsNullOrEmpty(buildPath) || string.IsNullOrEmpty(loadPath))
				throw new Exception("CRI Addressables Build Script needs Profile Varibales names as LocalLoadPath/LocalBuildPath");
			ret.BuildPath.SetVariableByName(settings, buildPath);
			ret.LoadPath.SetVariableByName(settings, loadPath);

			var providerProp = new SerializedObject(ret).FindProperty("m_AssetBundleProviderType");
			providerProp.FindPropertyRelative("m_AssemblyName").stringValue = providerType.AssemblyName;
			providerProp.FindPropertyRelative("m_ClassName").stringValue = providerType.ClassName;
			providerProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();

			return ret;
		}

		// Add dummy Group to reqire CriResourceProvider.
		protected override string ProcessAllGroups(AddressableAssetsBuildContext aaContext)
		{
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
			if(CriAddressablesSetting.Mode != CriAddressablesSetting.DeployMode.UseCriBuildScript)
				return base.ProcessAllGroups(aaContext);
#endif
			if(!autoRegisterAnchor)
				return base.ProcessAllGroups(aaContext);

			var existing = aaContext.Settings.FindGroup(dummyGroupName);
			if (existing != null)
				aaContext.Settings.RemoveGroup(existing);

			var template = AssetDatabase.LoadAssetAtPath<AddressableAssetGroupTemplate>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"t:{nameof(AddressableAssetGroupTemplate)} CriPackedAssetsTemplate").First()))
				.SchemaObjects.Select(s => (s is BundledAssetGroupSchema bs) ? SetupDummyGroupSchema(aaContext.Settings, bs, criProviderType) : s).ToList();
			var dummyGroup = aaContext.Settings.CreateGroup(dummyGroupName, false, true, false, template);
			aaContext.Settings.CreateOrMoveEntry(AnchorGuid, dummyGroup).address = dummyAnchorName;

			// ineject fixed id.
			var serializedGroup = new SerializedObject(dummyGroup);
			foreach(var prop in guidFields.Select(name => serializedGroup.FindProperty(name)).Where(p => p != null))
					prop.stringValue = dummyGroupGuid;
			serializedGroup.ApplyModifiedPropertiesWithoutUndo();

			return base.ProcessAllGroups(aaContext);
		}

		protected override TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
		{
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
			if (CriAddressablesSetting.Mode != CriAddressablesSetting.DeployMode.UseCriBuildScript)
				return base.DoBuild<TResult>(builderInput, aaContext);
#endif

			Func<IBuildParameters, IDependencyData, IWriteData, IBuildResults, ReturnCode> postWriting = (param, deps, data, results) => {
				AddCriLocations(aaContext, data as IBundleWriteData);
				return ReturnCode.Success;
			};

			ContentPipeline.BuildCallbacks.PostWritingCallback += postWriting;
			var result = base.DoBuild<TResult>(builderInput, aaContext);
			ContentPipeline.BuildCallbacks.PostWritingCallback -= postWriting;

			// Remove dummy Group.
			var entry = aaContext.Settings.FindAssetEntry(AnchorGuid);
			var anchorBundlePath = Path.Combine(entry.parentGroup.GetSchema<BundledAssetGroupSchema>().BuildPath.GetValue(aaContext.Settings), $"{entry.parentGroup.Name}_assets_{entry.address}.bundle");
			if (autoRegisterAnchor)
				aaContext.Settings.RemoveGroup(entry.parentGroup);
			if (File.Exists(anchorBundlePath))
				File.Delete(anchorBundlePath);

			return result;
		}

		// After running buildtasks, before generate contents catalog.
		protected virtual void AddCriLocations(AddressableAssetsBuildContext aaContext, IBundleWriteData writeData)
		{
			string[] allCriAssetBasePaths = AssetDatabase.FindAssets($"t:{nameof(CriAssetBase)}")
				.Select(guid => AssetDatabase.GUIDToAssetPath(guid))
				.ToArray();

			var anchorGuid = new GUID(AnchorGuid);
			var anchorBundle = writeData.AssetToFiles.ContainsKey(anchorGuid) ?
					writeData.FileToBundle[writeData.AssetToFiles[anchorGuid].First()] :
					null;
			if (anchorBundle == null)
				throw new Exception("[CRIWARE] Anchor Group not found. Create a Group contains LocalAnchor or make enable [Auto Register Anchor].");

			var newLocations = new List<ContentCatalogDataEntry>();

			foreach (var bundle in writeData.FileToBundle.Values.Distinct())
			{
				var guids = writeData.FileToBundle.Where(pair => pair.Value == bundle)
					.SelectMany(pair => writeData.FileToObjects[pair.Key])
					.Select(id => id.guid.ToString()).ToList();

				var criAssets = guids
					.Select(id => AssetDatabase.GUIDToAssetPath(id)).Distinct()
					.Where(path => allCriAssetBasePaths.Contains(path))
					.Select(path => AssetDatabase.LoadAssetAtPath<CriAssetBase>(path))
					.Where(asset => !(asset is ICriReferenceAsset) && (asset.Implementation is CriAddressableAssetImpl)).ToList();

				if (criAssets.Count <= 0) continue;

				var entries = aaContext.assetEntries.Where(e => guids.Contains(e.guid)).ToList();

				var schema = aaContext.Settings.groups.FirstOrDefault(g => g.Guid == aaContext.bundleToAssetGroup[bundle]).GetSchema<BundledAssetGroupSchema>();
				var buildPath = schema.BuildPath.GetValue(aaContext.Settings);
				var loadPath = schema.LoadPath.GetValue(aaContext.Settings);
				if (!string.IsNullOrWhiteSpace(subFolderName))
				{
					buildPath = Path.Combine(buildPath, subFolderName.ToLowerInvariant());
					loadPath = loadPath + "/" + subFolderName.ToLowerInvariant();
				}
				var targetLocations = aaContext.locations.Where(loc => loc.Dependencies.Contains(bundle)).ToList();

				var results = GenerateCriBundle(criAssets, entries, schema, buildPath, loadPath);

				newLocations.AddRange(results);
				foreach (var loc in targetLocations)
				{
					loc.Dependencies.AddRange(results.Select(l => l.Keys.Last()));
					loc.Dependencies.Remove(anchorBundle);
				}
			}
			aaContext.locations.AddRange(newLocations);
			Debug.Log($"CriAddresasbleBuildScript copied {newLocations.Count} files.");
		}

		protected virtual List<ContentCatalogDataEntry> GenerateCriBundle(List<CriAssetBase> criAssets, List<AddressableAssetEntry> entries, BundledAssetGroupSchema schema, string buildPath, string loadPath)
		{
			List<ContentCatalogDataEntry> results = new List<ContentCatalogDataEntry>();

			var providerId = schema.Group.Settings.FindAssetEntry(AnchorGuid).parentGroup.GetSchema<BundledAssetGroupSchema>().GetBundleCachedProviderId();

			foreach (var asset in criAssets)
			{
				var bundleName = (asset.Implementation as CriAddressableAssetImpl)._bundleName;
				var hash128 = new Hash128();
				HashUtilities.ComputeHash128(File.ReadAllBytes(AssetDatabase.GetAssetPath(asset)), ref hash128);
				var hash = hash128.ToString();
				var fileName = ConstructCriBundleName(AssetDatabase.GetAssetPath(asset), hash, schema).ToLowerInvariant() + suffix;

				CriAddressablesEditor.DeployData(AssetDatabase.GetAssetPath(asset), Path.Combine(buildPath, fileName));
				results.Add(new ContentCatalogDataEntry(
					typeof(IAssetBundleResource),
					loadPath + "/" + fileName,
					providerId,
					new string[] { fileName, bundleName },
					null,
					new AssetBundleRequestOptions()
					{
						Crc = /* schema.UseAssetBundleCrc ? info.Crc : 0, */ 0,
						UseCrcForCachedBundle = schema.UseAssetBundleCrcForCachedBundles,
						UseUnityWebRequestForLocalBundles = schema.UseUnityWebRequestForLocalBundles,
						Hash = schema.UseAssetBundleCache ? hash : "",
						ChunkedTransfer = schema.ChunkedTransfer,
						RedirectLimit = schema.RedirectLimit,
						RetryCount = schema.RetryCount,
						Timeout = schema.Timeout,
						BundleName = bundleName,
#if ADDRESSABLES_1_19_4_OR_NEWER
						AssetLoadMode = schema.AssetLoadMode,
#endif
						BundleSize = new FileInfo(Path.Combine(buildPath, fileName)).Length,
						ClearOtherCachedVersionsWhenLoaded = schema.AssetBundledCacheClearBehavior == BundledAssetGroupSchema.CacheClearBehavior.ClearWhenWhenNewVersionLoaded,
					}
				));
			}

			return results;
		}

		/// <summary>
		/// 出力ファイル名生成メソッド
		/// </summary>
		/// <param name="path">対象アセットのパス</param>
		/// <param name="hash">対象のデータハッシュ</param>
		/// <param name="schema">対象を含むグループのバンドルスキーマ</param>
		/// <returns>出力ファイル名</returns>
		/// <remarks>
		/// <para header='説明'>
		/// 出力ファイル名を決定する処理です。<br/>
		/// カスタムビルドスクリプトを作成する場合、このメソッドをオーバーライドすることでファイル名を変更できます<br/>
		/// </para>
		/// </remarks>
		protected virtual string ConstructCriBundleName(string path, string hash, BundledAssetGroupSchema schema)
		{
			path = schema.Group.GetAssetEntry(AssetDatabase.AssetPathToGUID(path), true)?.address ?? path;
			var prefix = schema.Group.Name.ToLowerInvariant() + "_assets_";
			switch (schema.BundleNaming)
			{
				default:
				case BundledAssetGroupSchema.BundleNamingStyle.AppendHash:
					return prefix + path + "_" + hash;
				case BundledAssetGroupSchema.BundleNamingStyle.NoHash:
					return prefix + path + "_data";
				case BundledAssetGroupSchema.BundleNamingStyle.OnlyHash:
					return hash;
				case BundledAssetGroupSchema.BundleNamingStyle.FileNameHash:
					return Hash128.Compute(path).ToString();
			}
		}
	}
}

#endif

/** @} */
