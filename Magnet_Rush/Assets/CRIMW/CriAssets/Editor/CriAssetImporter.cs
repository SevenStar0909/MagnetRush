/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ASSETS_INTEGRATION
 * @{
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.CompilerServices;
#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

[assembly: InternalsVisibleTo("CriMw.CriWare.Assets.Addressables.Editor")]

namespace CriWare.Assets
{
	/**
	 * <summary>CRIアセットのインポータ基底クラス</summary>
	 * <remarks>
	 * <para header='説明'>
	 * CRIアセットのインポータとしての共通インターフェイスを持つ抽象クラスです。<br/>
	 * </para>
	 * </remarks>
	 */
	public abstract class CriAssetImporter : ScriptedImporter
	{
		/**
		 * <summary>CRIアセットのDeployType</summary>
		 * <remarks>
		 * <para header = '説明'>
		 * DeployTypeを表現するクラスインスタンスを持つフィールドです。<br/>
		 * このフィールドを操作することでスクリプトからDeployTypeの変更が可能です。<br/>
		 * </para>
		 * </remarks>
		 * <seealso cref="ICriAssetImplCreator"/>
		 * <see cref="CriSerializedBytesAssetImplCreator"/>
		 * <see cref="CriStreamingFolderAssetImplCreator"/>
		 */
		[SerializeReference]
		public ICriAssetImplCreator implementation = new CriSerializedBytesAssetImplCreator();

		public abstract bool IsAssetImplCompatible { get; }

		protected ICriAssetImpl CreateAssetImpl(AssetImportContext ctx)
		{
			OnCreateImpl?.Invoke(ctx.assetPath);

			var ret = implementation.CreateAssetImpl(ctx);

			return ret;
		}

		public static System.Action<string> OnCreateImpl;
	}
}

/** @} */
