/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ASSETS_INTEGRATION
 * @{
 */

using System;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace CriWare.Assets
{
	/**
	 * <summary>"OnMemory" DeployType実装クラス</summary>
	 * <remarks>
	 * <para header='説明'>
	 * "OnMemory"DeployTypeでの実データインポート処理を定義するクラスです。<br/>
	 * <see cref="CriAssetImporter.implementation"/>に指定することでスクリプトからDeployTypeを指定できます。<br/>
	 * </para>
	 * </remarks>
	 */
	[CriDisplayName("OnMemory")]
	public struct CriSerializedBytesAssetImplCreator : ICriAssetImplCreator
	{
		public string Description =>
	@"The data is stored in the asset itself and expanded in memory when being used.
AWB files cannot be handled by this deploy type.
Futhermore, it is not recommended to use this deploy type for USM files.";

		public ICriAssetImpl CreateAssetImpl(AssetImportContext ctx)
		{
			var data = System.IO.File.ReadAllBytes(ctx.assetPath);
			return new CriSerializedBytesAssetImpl(data);
		}
	}
}

/** @} */
