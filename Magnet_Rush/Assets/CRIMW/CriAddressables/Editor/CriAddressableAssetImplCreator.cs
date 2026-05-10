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

using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor;
using System.IO;
using System.Linq;
#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace CriWare.Assets
{
#if ADDRESSABLES_1_15_1_OR_NEWER
	/**
	 * <summary>"Addressables" DeployType実装クラス</summary>
	 * <remarks>
	 * <para header='説明'>
	 * "Addressables"DeployTypeでの実データインポート処理を定義するクラスです。<br/>
	 * <see cref="CriAssetImporter.implementation"/>に指定することでスクリプトからDeployTypeを指定できます。<br/>
	 * </para>
	 * </remarks>
	 */
	[CriDisplayName("Addressables")]
	public class CriCustomAddressableAssetImplCreator : ICriAssetImplCreator
	{
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
		[SerializeField]
		CriAddressablesPathPair customPathPair = null;
#endif

		public CriCustomAddressableAssetImplCreator() { }

#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
		/**
		 * <summary>"Addressables" DeployType実装インスタンスの生成</summary>
		 * <param name="pathPair">指定するデプロイ先グループ</param>
		 * <remarks>
		 * <para header='説明'>
		 * デプロイ先グループを指定してインスタンスを設定します。
		 * グループ定義はデフォルトで生成されるRemoteまたはLocalに加えて<br/>
		 * 独自に作成したグループ定義アセットを指定可能です。<br/>
		 * </para>
		 * </remarks>
		 * <seealso cref="CriAddressablesPathPair"/>
		 * <seealso cref="CriAddressablesSetting.Remote"/>
		 * <seealso cref="CriAddressablesSetting.Local"/>
		 */
		public CriCustomAddressableAssetImplCreator(CriAddressablesPathPair pathPair) => customPathPair = pathPair;
#endif

		public string Description =>
@"Available for assets that are delivered via the Addressables system.
When the assets are loaded, their respective data files are provided from the loadpath.";

		public ICriAssetImpl CreateAssetImpl(AssetImportContext ctx)
		{
			ctx.DependsOnCriAddressablesVersion();

#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
			if (CriAddressablesSetting.Exists)
				ctx.DependsOnSourceAsset(AssetDatabase.GetAssetPath(CriAddressablesSetting.Instance));
#endif
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
			if (CriAddressablesSetting.Mode == CriAddressablesSetting.DeployMode.UseCriBuildScript)
			{
#endif
				var guid = AssetDatabase.AssetPathToGUID(ctx.assetPath);
				var bundleName = UnityEditor.Build.Pipeline.Utilities.HashingMethods.Calculate("cribundle_" + guid).ToString();
				return new CriAddressableAssetImpl(null, CriAddressablesEditor.GetLocalAnchor(), guid, bundleName);
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
			}
#endif

#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
			if (AddressableAssetSettingsDefaultObject.Settings == null)
				throw new System.Exception($"[CRIWARE] AddressableAssetSettingsDefaultObject.Settings is null.\nCreate Addresasbles Settings and reimport the CRI Asset ({ctx.assetPath})");
			if (customPathPair == null)
				customPathPair = CriAddressablesSetting.Instance.Remote;
			ctx.DependsOnSourceAsset(AssetDatabase.GetAssetPath(customPathPair));
			var anchor = CriAddressableGroupGenerator.CreateAnchor(ctx.assetPath, customPathPair.CreateGroup());
			var fileName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(anchor.anchor)).ToLowerInvariant();
			var impl = new CriAddressableAssetImpl(fileName, anchor.anchor, AssetDatabase.AssetPathToGUID(ctx.assetPath), anchor.bundleName);
			return impl;
#endif
		}
	}

#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET
	[CustomPropertyDrawer(typeof(CriCustomAddressableAssetImplCreator))]
	public class CriAddressableAssetImplCreatorDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (CriAddressablesSetting.Mode == CriAddressablesSetting.DeployMode.UseCriBuildScript) return;

			// make setting instance exist.
			var remote = CriAddressablesSetting.Instance.Remote;
			var local = CriAddressablesSetting.Instance.Local;

			var pathPairProp = property.FindPropertyRelative("customPathPair");
			var groups = CriAddressablesPathPairEditor.CollectReference();
			var currentIndex = groups.IndexOf(pathPairProp.objectReferenceValue as CriAddressablesPathPair);
			var newIndex = EditorGUI.Popup(position, "Group", currentIndex, groups.Select(g => g.name).ToArray());
			if (currentIndex != newIndex)
			{
				pathPairProp.objectReferenceValue = groups[newIndex];
			}
		}
	}
#endif
}

#endif
#endif

/** @} */
