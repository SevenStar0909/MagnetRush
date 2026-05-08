/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ASSETS_COMPONENT
 * @{
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CriWare.Assets
{
	/**
	 * <summary>Atom 向けアセットのロードを行うコンポーネント</summary>
	 * <remarks>
	 * <para header='説明'>ACF アセット / ACB アセットのロード/リリースを行うコンポーネントです。<br/>
	 * インスタンス生成時にインスペクタで指定した ACF・ACBがロードされ、<br/>
	 * インスタンス破棄時ににインスペクタで指定したACBがリリースされます。<br/></para>
	 * </remarks>
	 */
	public class CriAtomAssets : MonoBehaviour
	{
		[SerializeField]
		CriAtomAcfAsset acfAsset = null;
		[SerializeField]
		string dspBusSetting = null;
		[SerializeField]
		CriAtomAcbAsset[] acbAssets = null;

		private void Awake()
		{
			CriAtomPlugin.InitializeLibrary();

			if (acfAsset != null)
			{
				if (!acfAsset.Register())
					throw new Exception("[CRIWARE] Register Acf Failed");
				if (!string.IsNullOrEmpty(dspBusSetting))
					CriAtomEx.AttachDspBusSetting(dspBusSetting);
			}
			foreach (var asset in acbAssets)
			{
				if (asset == null) continue;
				CriAtomAssetsLoader.AddCueSheet(asset);
			}
		}

		private void OnDestroy()
		{
			foreach (var asset in acbAssets)
			{
				if (asset == null) continue;
				CriAtomAssetsLoader.ReleaseCueSheet(asset, true);
			}

			CriAtomPlugin.FinalizeLibrary();
		}
	}

	/**
	 * <summary>Asset Support アドオンでのキューシート管理クラス</summary>
	 * <remarks>
	 * <para header='説明'>CriAtomSourceForAsset での自動ロードや CriAtomAssets でのACBロードが行われた場合、本クラスでの管理となります。<br/>
	 * ロード済みACBはリファレンスカウントによって管理されます。<br/></para>
	 * </remarks>
	 */
	public class CriAtomAssetsLoader
	{
		static CriAtomAssetsLoader _instance = null;
		public static CriAtomAssetsLoader Instance {
			get => _instance ?? (_instance = new CriAtomAssetsLoader());
		}

		/**
		 * <summary>キューシート情報クラス</summary>
		 * <remarks>
		 * <para header='説明'>ロードされているキューシートエントリを表現するクラスです。<br/></para>
		 * </remarks>
		 */
		public class CueSheet
		{
			public CueSheet(string name, CriAtomAcbAsset asset)
			{
				Name = name;
				AcbAsset = asset;
				ReferenceCount = 0;
			}

			public string Name { get;private set; }
			public CriAtomAcbAsset AcbAsset { get; private set; }

			public int ReferenceCount { get; internal set; }
		}

		List<CueSheet> _cueSheets = new List<CueSheet>();

		/**
		 * <summary>ロード済みキューシート一覧</summary>
		 * <remarks>
		 * <para header='説明'>ロードされているキューシートの一覧を返します。<br/></para>
		 * </remarks>
		 */
		public IEnumerable<CueSheet> CueSheets {
			get => _cueSheets;
		}

		/**
		 * <summary>キューシートの取得 (ACB アセット指定)</summary>
		 * <remarks>
		 * <para header='説明'>ロードされているキューシート情報をACBアセット指定で取得します。<br/></para>
		 * </remarks>
		 */
		public CueSheet GetCueSheet(CriAtomAcbAsset asset)
		{
			foreach (var cueSheet in CueSheets)
				if (cueSheet.AcbAsset == asset)
					return cueSheet;
			return null;
		}

		/**
		 * <summary>キューシートの取得 (キューシート名指定)</summary>
		 * <remarks>
		 * <para header='説明'>ロードされているキューシート情報をキューシート名指定で取得します。<br/>
		 * キューシート名を指定せずに登録したアセットについては、アセット名がキューシート名となります。<br/></para>
		 * </remarks>
		 */
		public CueSheet GetCueSheet(string name)
		{
			foreach (var cueSheet in CueSheets)
				if (cueSheet.Name == name)
					return cueSheet;
			return null;
		}

		/**
		 * <summary>キューシートのロード (キューシート名指定)</summary>
		 * <remarks>
		 * <para header='説明'>ACB アセットをロードします。既にロード済みの場合はキューシートのリファレンスカウントが加算されます。<br/>
		 * 本メソッドで指定したキューシート名を利用し、 GetCueSheet メソッドでキューシート情報を取得可能です。<br/></para>
		 * </remarks>
		 */
		public void AddCueSheet(CriAtomAcbAsset acbAsset, string name)
		{
			var cueSheet = GetCueSheet(acbAsset);
			if (cueSheet == null)
			{
				cueSheet = new CueSheet(name, acbAsset);
				_cueSheets.Add(cueSheet);
				acbAsset.LoadAsync();
			}
			cueSheet.ReferenceCount++;
		}

		/**
		 * <summary>キューシートのロード</summary>
		 * <remarks>
		 * <para header='説明'>ACB アセットをロードします。既にロード済みの場合はキューシートのリファレンスカウントが加算されます。<br/>
		 * 本メソッドでロードしたキューシートは、 アセット名がキューシート名となります。<br/></para>
		 * </remarks>
		 */
		public static void AddCueSheet(CriAtomAcbAsset acbAsset)
		{
			Instance.AddCueSheet(acbAsset, acbAsset.name);
		}

		/**
		 * <summary>キューシートのリリース (ACB アセット指定)</summary>
		 * <remarks>
		 * <para header='説明'>ACB アセットをリリースします。リファレンスカウントが 0 となった場合はキューシートをアンロードします。<br/>
		 * unloadImmediate を false にした場合はキューシートのアンロードが遅延されます。<br/>
		 * 本関数でアンロードを遅延した場合は適切なタイミングで UnloadUnusedCueSheets を呼び出してください。<br/></para>
		 * </remarks>
		 */
		public static void ReleaseCueSheet(CriAtomAcbAsset acbAsset, bool unloadImmediate = true)
		{
			Instance.ReleaseCueSheet(Instance.GetCueSheet(acbAsset), unloadImmediate);
		}

		/**
		 * <summary>キューシートのリリース (キューシート名指定)</summary>
		 * <remarks>
		 * <para header='説明'>ACB アセットをリリースします。リファレンスカウントが 0 となった場合はキューシートをアンロードします。<br/>
		 * unloadImmediate を false にした場合はキューシートのアンロードが遅延されます。<br/>
		 * 本関数でアンロードを遅延した場合は適切なタイミングで UnloadUnusedCueSheets を呼び出してください。<br/></para>
		 * </remarks>
		 */
		public static void ReleaseCueSheet(string name, bool unloadImmediate = true)
		{
			Instance.ReleaseCueSheet(Instance.GetCueSheet(name), unloadImmediate);
		}

		/**
		 * <summary>キューシートのリリース</summary>
		 * <remarks>
		 * <para header='説明'>ACB アセットをリリースします。リファレンスカウントが 0 となった場合はキューシートをアンロードします。<br/>
		 * unloadImmediate を false にした場合はキューシートのアンロードが遅延されます。<br/>
		 * 本関数でアンロードを遅延した場合は適切なタイミングで UnloadUnusedCueSheets を呼び出してください。<br/></para>
		 * </remarks>
		 */
		public void ReleaseCueSheet(CueSheet cueSheet, bool unloadImmediate)
		{
			if (cueSheet == null) return;
			cueSheet.ReferenceCount--;
			if (unloadImmediate)
				UnloadUnusedCueSheetsInternal();
		}

		internal void UnloadUnusedCueSheetsInternal()
		{
			for(int i = _cueSheets.Count - 1; i >= 0; i--)
			{
				if (_cueSheets[i].ReferenceCount <= 0)
				{
					_cueSheets[i].AcbAsset.Unload();
					_cueSheets.RemoveAt(i);
				}
			}
		}

		/**
		 * <summary>参照のないキューシートのアンロード</summary>
		 * <remarks>
		 * <para header='説明'>リファレンスカウントを持たないキューシートを一括でアンロードします。<br/>
		 * アンロードを遅延した場合は適切なタイミングで本関数を呼び出してください。<br/></para>
		 * </remarks>
		 */
		public static void UnloadUnusedCueSheets() => Instance.UnloadUnusedCueSheetsInternal();
	}
}

/** @} */
