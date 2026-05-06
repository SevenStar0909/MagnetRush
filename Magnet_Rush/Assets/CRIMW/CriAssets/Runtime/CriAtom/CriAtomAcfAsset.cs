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
	 * <summary>Acf アセットクラス</summary>
	 * <remarks>
	 * <para header='説明'>Unity アセットとしてインポートされた Acf ファイルを扱うクラスです。<br/></para>
	 * </remarks>
	 */
	public class CriAtomAcfAsset : CriAssetBase
	{
		/**
		 * <summary>Acf の登録</summary>
		 * <remarks>
		 * <para header='説明'>Acf をライブラリにロードします。</para>
		 * </remarks>
		 */
		public bool Register()
		{
			if (!CriAtomPlugin.IsLibraryInitialized())
				throw new System.Exception("[CRIWARE] Library not initialized");

			var fileImpl = Implementation as ICriFileAssetImpl;
			if (fileImpl != null)
			{
				CriAtomEx.RegisterAcf(null, fileImpl.Path);
				return true;
			}

			var memoryImpl = Implementation as ICriMemoryAssetImpl;
			if (memoryImpl != null)
			{
				CriAtomEx.RegisterAcf(memoryImpl.PinnedAddress, memoryImpl.Size);
				return true;
			}

			return false;
		}

		void Unload()
		{
			if (!CriAtomPlugin.IsLibraryInitialized()) return;
			CriAtomEx.UnregisterAcf();
		}

		private void OnDisable() =>
			Unload();

		private void OnDestroy() =>
			Unload();

		~CriAtomAcfAsset() =>
			Unload();
	}
}

/** @} */
