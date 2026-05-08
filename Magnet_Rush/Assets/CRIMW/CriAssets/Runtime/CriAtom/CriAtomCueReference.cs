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
	 * <summary>キュー参照情報構造体</summary>
	 * <remarks>
	 * <para header='説明'>Acb アセットとキューID のセットによってキューを指定する構造体です。<br/>
	 * CriAtomCueReference 型のフィールドをシリアライズすることで、<br/>
	 * Editor 上でドロップダウンリストによるキュー選択が可能になります。<br/></para>
	 * </remarks>
	 */
	[System.Serializable]
	public struct CriAtomCueReference
	{
		[SerializeField]
		CriAtomAcbAsset acbAsset;
		[SerializeField]
		int cueId;

		public CriAtomAcbAsset AcbAsset => acbAsset;
		public int CueId => cueId;

		public CriAtomCueReference(CriAtomAcbAsset acbAsset, int cueId)
		{
			this.acbAsset = acbAsset;
			this.cueId = cueId;
		}
	}
}

/** @} */
