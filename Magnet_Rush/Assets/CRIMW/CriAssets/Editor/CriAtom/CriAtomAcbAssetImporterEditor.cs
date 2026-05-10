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
using UnityEditor;
using UnityEditorInternal;
using System.Linq;

namespace CriWare.Assets
{
	[CustomEditor(typeof(CriAtomAcbAssetImporter)), CanEditMultipleObjects]
	class CriAtomAcbAssetImporterEditor : CriAssetImporterEditor
	{
		ReorderableList reorderableList;
		CriAtomExAcb handleCache;

		CriAtomEx.CueInfo selectedCue;

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var asset = AssetDatabase.LoadAssetAtPath<CriAtomAcbAsset>((target as CriAssetImporter).assetPath);
			if (asset == null) return;
			var handle = CriAtomAssetsPreviewPlayer.Instance.GetAcb(asset);
			if (handle == null) return;

			if (GUILayout.Button("Stop"))
				CriAtomAssetsPreviewPlayer.Instance.Stop();

			if (handle != handleCache)
			{
				reorderableList = new ReorderableList(handle.GetCueInfoList(), typeof(CriAtomEx.CueInfo));
				reorderableList.draggable = false;
				reorderableList.displayAdd = false;
				reorderableList.displayRemove = false;
				reorderableList.drawHeaderCallback += rect => GUI.Label(rect, "Cue List");
				reorderableList.drawElementCallback += (rect, index, active, focus) =>
				{
					var cue = (CriAtomEx.CueInfo)reorderableList.list[index];
					var buttonRect = rect;
					buttonRect.width = 30f;
					rect.width -= buttonRect.width * 2f;
					rect.x += buttonRect.width;
					if(GUI.Button(buttonRect, EditorGUIUtility.IconContent("Animation.Play"), EditorStyles.miniButton))
						CriAtomAssetsPreviewPlayer.Instance.Play(asset, cue.id);
					GUI.Label(rect, $"{cue.id: 000} : {cue.name}");
					buttonRect.x += rect.width + buttonRect.width;
					if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("Clipboard"), EditorStyles.miniButton))
						EditorGUIUtility.systemCopyBuffer = cue.name;
				};
				reorderableList.onSelectCallback += list => selectedCue = (CriAtomEx.CueInfo)list.list[list.index];
				handleCache = handle;
			}

			reorderableList.DoLayoutList();
		}

		protected override void Apply()
		{
			base.Apply();
			CriAtomAssetsPreviewPlayer.Instance.Dispose();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			CriAtomAssetsPreviewPlayer.Instance.Dispose();
		}

		public override bool HasPreviewGUI() => true;
		public override void OnPreviewGUI(Rect r, GUIStyle background) => UpdatePreview(r);
		public override void DrawPreview(Rect r) =>UpdatePreview(r);

		void UpdatePreview(Rect r)
		{
			r.height = EditorGUIUtility.singleLineHeight + 2f;

			var acfAssets = AssetDatabase.FindAssets($"t:{nameof(CriAtomAcfAsset)}").Select(guid => AssetDatabase.LoadAssetAtPath<CriAtomAcfAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
			var currentIndex = acfAssets.IndexOf(CriAtomAssetsPreviewPlayer.Instance.RegisterdAcf);
			var newIndex = EditorGUI.Popup(r, "ACF", currentIndex, acfAssets.Select(acf => acf.name).ToArray());
			if (currentIndex == -1 && acfAssets.Count > 0)
				newIndex = 0;
			if (currentIndex != newIndex)
				CriAtomAssetsPreviewPlayer.Instance.RegisterAcf(acfAssets[newIndex]);
			r.y += r.height;

			if (CriAtomAssetsPreviewPlayer.Instance.RegisterdAcf != null && selectedCue.categories != null)
			{
				GUI.Label(r, $@"Category : {string.Join(", ", selectedCue.categories.Where(c => c != ushort.MaxValue).Select(c => {
					CriAtomExAcf.GetCategoryInfoByIndex(c, out var categoryInfo);
					return categoryInfo.name;
				}))}");
				r.y += r.height;
			}

			GUI.Label(r, $"ID : {selectedCue.id}");
			r.y += r.height;
			GUI.Label(r, $"Name : {selectedCue.name}");
			r.y += r.height;
			GUI.Label(r, $"Type : {selectedCue.type}");
			r.y += r.height;
			GUI.Label(r, $"Volume : {selectedCue.volume}");
			r.y += r.height;
			GUI.Label(r, $"Priority : {selectedCue.priority}");
			r.y += r.height;
			GUI.Label(r, $"Probability : {selectedCue.probability}");
		}

		public override GUIContent GetPreviewTitle() => new GUIContent(selectedCue.name);
	}
}

/** @} */
