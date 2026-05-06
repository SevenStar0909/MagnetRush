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

namespace CriWare.Assets
{
	/**
	 * <summary>CRI アセットの基底クラス</summary>
	 * <remarks>
	 * <para header='説明'>Asset Support アドオンで取り扱うアセットの基底クラスです。<br/>
	 * 本クラスでは各アセットで共通となる、実データの取り扱い方法の定義が含まれます。<br/></para>
	 * </remarks>
	 */
	public abstract class CriAssetBase : ScriptableObject
	{
		[SerializeReference]
		internal ICriAssetImpl implementation;

		/**
		 * <summary>実データのデプロイ先情報</summary>
		 * <remarks>
		 * <para header='説明'>データの実体がどのように配置されているかを持つフィールドです。<br/>
		 * 継承先の型ごとに実データの持ち方が異なります。</para>
		 * </remarks>
		 */
		public virtual ICriAssetImpl Implementation {
			get => implementation;
		}

		/**
		 * <summary>データの生ファイルへのパス</summary>
		 * <remarks>
		 * <para header='説明'>ストリーミング再生対象のデータのファイルへのパスを取得します。<br/>
		 * 実データをアセット内に格納している場合は null を返します。</para>
		 * </remarks>
		 */
		public string FilePath {
			get => (Implementation is ICriFileAssetImpl) ? (Implementation as ICriFileAssetImpl).Path : null;
		}

		/**
		 * <summary>シリアライズされたデータ</summary>
		 * <remarks>
		 * <para header='説明'>アセット内に格納した実データを取得します。<br/>
		 * 実データをアセット外のファイルに持たせている場合は null を返します。</para>
		 * </remarks>
		 */
		public byte[] Data {
			get => (Implementation is ICriMemoryAssetImpl) ? (Implementation as ICriMemoryAssetImpl).Data : null;
		}

		void OnEnable() => Implementation?.OnEnable();
		void OnDisable() => Implementation?.OnDisable();
	}
}

/** @} */
