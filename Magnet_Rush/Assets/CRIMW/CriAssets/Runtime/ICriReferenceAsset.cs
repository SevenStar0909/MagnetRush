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
	 * <summary>CRIバーチャルアセットを表現するインターフェイス</summary>
	 * <remarks>
	 * <para header='説明'>インポート済みCRIアセットへの参照を持ち、CRIアセットと同様に振るまうアセットのインターフェイスです。<br/>
	 * 多言語対応ACBアセットやマルチプラットフォームアセットが本インターフェイスを実装しています。</para>
	 * <seealso cref='CriAtomAcbLocalizedAsset'/><seealso cref='CriAtomAcbMultiPlatformAsset'/><seealso cref='CriAtomAcfMultiPlatformAsset'/>
	 * </remarks>
	 */
	public interface ICriReferenceAsset
	{
		/**
		 * <summary>参照先のアセット</summary>
		 * <remarks>
		 * <para header='説明'>参照先のCRIアセットを返します。<br/>
		 * 通常はNon-Asset CRIデータのインポートによって生成された実データを持つCRIアセットとなります。</para>
		 * </remarks>
		 */
		CriAssetBase ReferencedAsset { get; }
	}
}

/** @} */
