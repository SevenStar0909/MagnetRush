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
#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace CriWare.Assets
{
	/**
	 * <summary>実データインポート処理インターフェイス</summary>
	 * <remarks>
	 * <para header='説明'>
	 * CRIWARE アセットの実データのインポート処理定義を提供するインターフェイスです。
	 * 本インターフェイスを継承することで CRIWARE アセットに設定可能な DeployType を追加することができます。
	 * </para>
	 * </remarks>
	 */
	public interface ICriAssetImplCreator
	{
		/**
		 * <summary>実データのインポート処理</summary>
		 * <returns>実データの格納先情報</returns>
		 * <remarks>
		 * <para header='説明'>
		 * CRIWARE 向けファイルを読み込んで実データの格納先を返すメソッドです。
		 * アセットのインポート時に呼び出されます。
		 * </para>
		 * </remarks>
		 */
		ICriAssetImpl CreateAssetImpl(AssetImportContext ctx);

		//void Cleanup(string assetPath);

		/**
		 * <summary>DeployType の説明</summary>
		 * <remarks>
		 * <para header='説明'>
		 * DeployType 毎にインスペクターに表示される説明文です。
		 * </para>
		 * </remarks>
		 */
		string Description { get; }
	}

	[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
	public class CriDisplayNameAttribute : System.Attribute
	{
		public CriDisplayNameAttribute(string name)
		{
			Name = name;
		}

		public string Name { get; }
	}
}

/** @} */
