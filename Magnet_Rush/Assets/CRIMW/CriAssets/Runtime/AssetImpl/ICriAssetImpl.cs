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
	 * <summary>実データ格納先インターフェイス</summary>
	 * <remarks>
	 * <para header='説明'>アセットの持つ実データの格納先を表現するクラスの共通インターフェイスです。<br/>
	 * <see cref='CriAssetBase'/> が本インターフェイスのメンバを持つことでデータの格納先を持ちます。</para>
	 * </remarks>
	 */
	public interface ICriAssetImpl {

		/**
		 * <summary>データが利用可能な状態にあるか</summary>
		 */
		bool IsReady { get; }

		/**
		 * <summary>アセットの Enable 時の処理</summary>
		 * <remarks>
		 * <para header='説明'>対応するCRIアセットが Enable になった際に呼ばれます。<br/>
		 * データの参照のセットアップ処理などが行われます。</para>
		 * </remarks>
		 */
		void OnEnable();

		/**
		 * <summary>アセットの Disable 時の処理</summary>
		 * <remarks>
		 * <para header='説明'>対応するCRIアセットが Disable になった際に呼ばれます。<br/>
		 * インスタンス内で確保したリソースの破棄などが行われます。</para>
		 * </remarks>
		 */
		void OnDisable();
	}

	/**
	 * <summary>実データ格納先インターフェイス (オンメモリ)</summary>
	 * <remarks>
	 * <para header='説明'>メモリ上に展開されるデータの情報を取得するインターフェイスを提供します。<br/>
	 * 本インターフェイスを継承することで CRIWARE のアセットで利用できる独自の格納先実装が可能です。</para>
	 * <seealso cref='CriSerializedBytesAssetImpl'/>
	 * </remarks>
	 */
	public interface ICriMemoryAssetImpl : ICriAssetImpl
	{
		/**
		 * <summary>オンメモリデータへのポインタ</summary>
		 * <remarks>
		 * <para header='説明'>メモリ上に固定したデータの先頭ポインタを返します。<br/>
		 * ネイティブモジュールのAPIにポインタ経由でデータを渡す際に利用可能です。</para>
		 * </remarks>
		 */
		System.IntPtr PinnedAddress { get; }
		/**
		 * <summary>データのサイズ</summary>
		 * <remarks>
		 * <para header='説明'>メモリ上でのデータサイズです。<br/>
		 * <see cref='PinnedAddress'/> を用いてポインタ渡しする際に利用してください。</para>
		 * </remarks>
		 */
		System.Int32 Size { get; }
		/**
		 * <summary>オンメモリデータ</summary>
		 * <remarks>
		 * <para header='説明'>バイト配列として表現されたデータです。</para>
		 * </remarks>
		 */
		System.Byte[] Data { get; }
	}

	/**
	 * <summary>実データ格納先インターフェイス (ファイル)</summary>
	 * <remarks>
	 * <para header='説明'>ファイルとして存在するデータの情報を取得するインターフェイスを提供します。<br/>
	 * 本インターフェイスを継承することで CRIWARE のアセットで利用できる独自の格納先実装が可能です。</para>
	 * <seealso cref='CriStreamingFolderAssetImpl'/>
	 * </remarks>
	 */
	public interface ICriFileAssetImpl : ICriAssetImpl
	{
		/**
		 * <summary>Non-Asset CRIデータのパス</summary>
		 * <remarks>
		 * <para header='説明'>実際のデータを持つNon-Asset CRIデータへのパスです。<br/></para>
		 * </remarks>
		 */
		string Path { get; }

		/**
		 * <summary>ファイル内でのデータ位置</summary>
		 * <remarks><see cref='Path'/> で指定されているファイル内での対象のデータのオフセットです。<br/></remarks>
		 */
		ulong Offset { get; }

		/**
		 * <summary>データサイズ</summary>
		 * <remarks>
		 * <para header='説明'>ファイル内でのデータサイズです。</para>
		 * </remarks>
		 */
		long Size { get; }
	}

	public interface ICriFsAssetImpl : ICriAssetImpl
	{
		CriFsBinder Binder { get; }
		int ContentId { get; }
	}
}

/** @} */
