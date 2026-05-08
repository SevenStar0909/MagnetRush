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
using System.Runtime.InteropServices;
using System;
using System.Linq;

namespace CriWare.Assets
{
	/**
	 * <summary>Acb アセットクラス</summary>
	 * <remarks>
	 * <para header='説明'>Unity アセットとしてインポートされた Acb ファイルを扱うクラスです。<br/></para>
	 * </remarks>
	 */
	public class CriAtomAcbAsset : CriAssetBase
	{
		[SerializeField]
		internal CriAtomAwbAsset awb;
		[SerializeField]
		internal CriAtomAwbAsset[] additionalAwbs;

		CriAtomExAcb _handle = null;

		public virtual CriAtomAwbAsset Awb => awb;

		/**
		 * <summary>Acb インスタンス</summary>
		 * <remarks>
		 * <para header='説明'>ロード済みの Acb のインスタンスを取得します。<br/>
		 * ロードが完了していない場合は null を返します。</para>
		 * </remarks>
		 */
		public CriAtomExAcb Handle { get {
				if (!CriAtomPlugin.IsLibraryInitialized()) return null;
				if(_handle == null)
				{
					if(Status == CriAtomExAcbLoader.Status.Complete)
					{
						_handle = asyncLoader.MoveAcb();
						asyncLoader.Dispose();
						asyncLoader = null;
					}
				}
				return _handle;
			} }

		/**
		 * <summary>ロード完了コールバック</summary>
		 * <remarks>
		 * <para header='説明'>Acb ファイルのライブラリへのロード完了時に呼び出されます。<br/>
		 * 本イベントに追加したコールバックは次回のロード完了後に全て登録解除されます。</para>
		 * </remarks>
		 */
		public event Action<CriAtomAcbAsset> OnLoaded = null;

		/**
		 * <summary>ロード要求があったか</summary>
		 * <remarks>
		 * <para header='説明'>CriAtomAcbAsset.LoadAsync などの呼び出しによるロードが要求されたか。</para>
		 * </remarks>
		 */
		[field : System.NonSerialized]
		public bool LoadRequested { get; private set; } = false;

		/**
		 * <summary>ロードが完了しているか</summary>
		 * <remarks>
		 * <para header='説明'>CriAtomAcbAsset.LoadAsync の呼出後、ロードが完了すると true になります。</para>
		 * </remarks>
		 */
		public bool Loaded {
			get => Status == CriAtomExAcbLoader.Status.Complete;
		}

		/**
		 * <summary>キューシートのロード (非同期)</summary>
		 * <remarks>
		 * <para header='説明'>キューシートを非同期でロードします。<br/>
		 * 本メソッドの実行後、CriAtomAcbAsset.Loaded が true になってから<br/>
		 * キューシートへのアクセスを行ってください。<br/></para>
		 * </remarks>
		 */
		public void LoadAsync()
		{
			if (LoadRequested)
				throw new InvalidOperationException($"[CRIWARE] {name} ({nameof(CriAtomAcfAsset)}) is already loaded.");

			if (!InternalLoadAsync())
				throw new System.Exception("[CRIWARE] Load Acb Failed");
			LoadRequested = true;
			return;
		}

		/**
		 * <summary>キューシートのロード (完了復帰)</summary>
		 * <remarks>
		 * <para header='説明'>キューシートをロードします。<br/>
		 * 本メソッドは完了復帰であり、呼出スレッドを長時間ブロックする可能性があります。<br/></para>
		 * </remarks>
		 */
		public void LoadImmediate()
		{
			if (LoadRequested)
				throw new InvalidOperationException($"[CRIWARE] {name} ({nameof(CriAtomAcfAsset)}) is already loaded.");

			if (!InternalLoadImmediate())
				throw new System.Exception("[CRIWARE] Load Acb Failed");
			LoadRequested = true;
			return;
		}

		/**
		 * <summary>キューシートのアンロード</summary>
		 * <remarks>
		 * <para header='説明'>キューシートをアンロードします。</para>
		 * </remarks>
		 */
		public void Unload()
		{
			if (!LoadRequested) return;
			LoadRequested = false;
			InternalUnload();
		}

		bool InternalLoadAsync()
		{
			if (!CriAtomPlugin.IsLibraryInitialized()) return false;

			bool result = false;

#if UNITY_WEBGL && !UNITY_EDITOR
			var loadAwbOnMemory = true;
#else
			var loadAwbOnMemory = false;
#endif

			if (!string.IsNullOrEmpty(FilePath))
			{
				asyncLoader = CriAtomExAcbLoader.LoadAcbFileAsync(null, FilePath, (Awb?.Implementation as ICriFileAssetImpl)?.Path, loadAwbOnMemory);
				result = true;
			}
			if (Data != null)
			{
				asyncLoader = CriAtomExAcbLoader.LoadAcbDataAsync(Data, null, (Awb?.Implementation as ICriFileAssetImpl)?.Path, loadAwbOnMemory);
				result = true;
			}

			if (result)
				CriAtomServer.instance.StartCoroutine(UpdateRoutine());

			return result;
		}

		bool InternalLoadImmediate()
		{
			if (!CriAtomPlugin.IsLibraryInitialized()) return false;

			bool result = false;

			if (!string.IsNullOrEmpty(FilePath))
			{
				_handle = CriAtomExAcb.LoadAcbFile(null, FilePath, (Awb?.Implementation as ICriFileAssetImpl)?.Path);
				result = true;
			}
			if (Data != null)
			{
				_handle = CriAtomExAcb.LoadAcbData(Data, null, (Awb?.Implementation as ICriFileAssetImpl)?.Path);
				result = true;
			}

			foreach (var awbAsset in additionalAwbs ?? Array.Empty<CriAtomAwbAsset>())
				AttachAwb(awbAsset);

			if (result)
				OnLoaded?.Invoke(this);
			OnLoaded = null;

			return result;
		}

		void InternalUnload()
		{
			LoadRequested = false;
			asyncLoader?.Dispose();
			asyncLoader = null;
			Handle?.Dispose();
			_handle = null;
		}

		IEnumerator UpdateRoutine()
		{
			while (Status != CriAtomExAcbLoader.Status.Complete)
				yield return null;
			foreach(var awbAsset in additionalAwbs ?? Array.Empty<CriAtomAwbAsset>())
				AttachAwb(awbAsset);
			OnLoaded?.Invoke(this);
			OnLoaded = null;
		}

		private void OnDisable()
		{
			Unload();
		}

		private void OnDestroy()
		{
			Unload();
		}

		~CriAtomAcbAsset()
		{
			Unload();
		}

		CriAtomExAcbLoader asyncLoader = null;

		/**
		 * <summary>アセットのロードステータス</summary>
		 * <remarks>
		 * <para header='説明'>アセットのロード状態を返します。<br/>
		 * <see cref='LoadAsync'/> によってロード要求を行ったアセットのロード完了などを本ステータスで確認することができます。</para>
		 * </remarks>
		 */
		public CriAtomExAcbLoader.Status Status {
			get => asyncLoader?.GetStatus() ?? ((_handle == null) ? CriAtomExAcbLoader.Status.Stop : CriAtomExAcbLoader.Status.Complete);
		}

		/**
		 * <summary>ストリーム用AWBアセットのアタッチ</summary>
		 * <remarks>
		 * <para header='説明'>ロード済みのACBに対してストリーム用のAWBをアタッチします。<br/>
		 * 本APIの呼び出し前に<see cref='LoadAsync'/>等でACBのロードを行い、<br/>
		 * ロードが完了した状態でAWBのアタッチを行ってください。</para>
		 * </remarks>
		 */
		public void AttachAwb(CriAtomAwbAsset awbAsset)
		{
			Handle?.AttachAwbFile(null, awbAsset.FilePath, awbAsset.name);
		}
	}
}

/** @} */
