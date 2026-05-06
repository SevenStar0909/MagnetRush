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
using System.Linq;
using UnityEditor;
using System;

namespace CriWare.Assets
{
	internal class CriAtomAssetsPreviewPlayer : System.IDisposable
	{
		static CriAtomAssetsPreviewPlayer _instance = null;
		public static CriAtomAssetsPreviewPlayer Instance => _instance ?? (_instance = new CriAtomAssetsPreviewPlayer());

		Dictionary<string, CriAtomExAcb> loadedAcbs = new Dictionary<string, CriAtomExAcb>();

		internal event System.Action<string, CriAtomExAcb> OnLoaded;

		CriAtomAcfAsset registerdAcf = null;
		public CriAtomAcfAsset RegisterdAcf => registerdAcf;

		bool initialized = false;

		public void RegisterAcf(CriAtomAcfAsset acfAsset)
		{
			CriWare.Editor.CriAtomEditorUtilities.InitializeLibrary();
			initialized = true;

			if (registerdAcf == acfAsset) return;
			acfAsset.Register();
			registerdAcf = acfAsset;
		}

		public CriAtomExAcb GetAcb(CriAtomAcbAsset asset)
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return asset.Loaded ? asset.Handle : null;

			CriWare.Editor.CriAtomEditorUtilities.InitializeLibrary();
			initialized = true;

			var originalPath = (asset is ICriReferenceAsset) ?
				AssetDatabase.GetAssetPath((asset as ICriReferenceAsset).ReferencedAsset) :
				AssetDatabase.GetAssetPath(asset);
			if (loadedAcbs.ContainsKey(originalPath))
				if (!loadedAcbs[originalPath].isAvailable)
					loadedAcbs.Remove(originalPath);
			if (!loadedAcbs.ContainsKey(originalPath))
			{
				loadedAcbs.Add(originalPath, CriAtomExAcb.LoadAcbFile(null, System.IO.Path.GetFullPath(originalPath), (asset.Awb == null) ? null : System.IO.Path.GetFullPath(AssetDatabase.GetAssetPath(asset.Awb))));
				foreach (var awb in asset.additionalAwbs ?? Array.Empty<CriAtomAwbAsset>())
				{
					var awbPath = (awb is ICriReferenceAsset) ?
						AssetDatabase.GetAssetPath((awb as ICriReferenceAsset).ReferencedAsset) :
						AssetDatabase.GetAssetPath(awb);
                    loadedAcbs[originalPath].AttachAwbFile(null, awbPath, awb.name);
				}
				OnLoaded?.Invoke(loadedAcbs.Last().Key, loadedAcbs.Last().Value);
			}
			return loadedAcbs[originalPath];
		}

		CriAtomExPlayer _player = null;
		CriAtomExPlayer Player => _player ?? (_player = new CriAtomExPlayer());

		public CriAtomExPlayback Play(CriAtomAcbAsset asset, int cueId)
		{
			var acb = GetAcb(asset);
			if (acb == null) return new CriAtomExPlayback(CriAtomExPlayback.invalidId);
			Player.SetCue(acb, cueId);
			Player.SetPanType(CriAtomEx.PanType.Pan3d);
			return Player.Start();
		}

		public void Stop()
		{
			Player.Stop();
		}

		public void SetSelectorLabel(string selector, string label)
		{
			Player.SetSelectorLabel(selector, label);
			Player.UpdateAll();
		}

		public void Dispose()
		{
			if (CriAtomPlugin.IsLibraryInitialized())
			{
				_player?.Stop(true);
				_player?.Dispose();
				foreach (var acb in loadedAcbs.Values)
					acb?.Dispose();
				if (initialized)
				{
					CriAtomPlugin.FinalizeLibrary();
					if (CriFsPlugin.IsLibraryInitialized())
						CriFsPlugin.FinalizeLibrary();
				}
			}
			_player = null;
			loadedAcbs.Clear();
			registerdAcf = null;
		}
	}
}

/** @} */
