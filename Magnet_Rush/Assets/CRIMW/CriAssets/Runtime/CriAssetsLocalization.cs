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
	 * <summary>CRI Assets の多言語対応制御クラス</summary>
	 * <remarks>
	 * <para header='説明'>CRI Assets における多言語対応の制御を行うクラスです。<br/>
	 * 現在はACBアセットのみ多言語対応を行っております。</para>
	 * </remarks>
	 */
	[System.Obsolete]
	public class CriAssetsLocalization
	{
		/**
		 * <summary>現在指定されている言語</summary>
		 * <remarks>
		 * <para header='説明'>現在指定されているローカライズ名を返します。<br/>
		 * <see cref='ChangeLanguage(string)'/> で指定した文字列となります。</para>
		 * </remarks>
		 */
		public static string CurrentLanguage { get; private set; }

		/**
		 * <summary>ローカライズ言語の指定</summary>
		 * <remarks>
		 * <para header='説明'>ローカライズ先の言語を指定します。<br/>
		 * 多言語対応ACBアセットで指定したローカライズ名のいずれかを指定してください。</para>
		 * </remarks>
		 */
		public static void ChangeLanguage(string name)
		{
			CurrentLanguage = name;
		}
	}
}

/** @} */
