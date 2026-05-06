using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace CriWare.Assets {
    [CustomEditor(typeof(CriAtomAcfAssetImporter))]
    class CriAtomAcfAssetImporterEditor : CriAssetImporterEditor
    {
		List<ListPreviewer<CriAtomExAcf.CategoryInfo>> _categoriesPreview;
		List<ListPreviewer<CriAtomExAcf.CategoryInfo>> CategoriesPreview => _categoriesPreview ?? (_categoriesPreview =
			Enumerate<CriAtomExAcf.CategoryInfo>(CriAtomExAcf.GetNumCategories, CriAtomExAcf.GetCategoryInfoByIndex)
			.GroupBy(info => info.groupNo)
			.Select(g => new ListPreviewer<CriAtomExAcf.CategoryInfo>(
				g.ToList(),
				$"Group{g.Key}", 
				info => ($"{info.id: 000}:\t{info.name}", info.name)
			)).ToList());

		ListPreviewer<CriAtomEx.GameVariableInfo> _variablesPreview;
		ListPreviewer<CriAtomEx.GameVariableInfo> VariablesPreview => _variablesPreview ?? (_variablesPreview = new ListPreviewer<CriAtomEx.GameVariableInfo>(
						Enumerate<CriAtomEx.GameVariableInfo>(CriAtomEx.GetNumGameVariables, CriAtomEx.GetGameVariableInfo).ToList(),
						"Game Variables",
						info => ($"{info.id: 000}:\t{info.name}", info.name)));

		ListPreviewer<CriAtomEx.AisacControlInfo> _aisacControlPreview;
		ListPreviewer<CriAtomEx.AisacControlInfo> AisacControlPreview => _aisacControlPreview ?? (_aisacControlPreview = new ListPreviewer<CriAtomEx.AisacControlInfo>(
						Enumerate<CriAtomEx.AisacControlInfo>(CriAtomExAcf.GetNumAisacControls, CriAtomExAcf.GetAisacControlInfo).ToList(),
						"AISAC Control",
						info => ($"{info.id: 000}:\t{info.name}", info.name)));

		ListPreviewer<CriAtomExAcf.GlobalAisacInfo> _globalAisacPreview;
		ListPreviewer<CriAtomExAcf.GlobalAisacInfo> GlobalAisacPreview => _globalAisacPreview ?? (_globalAisacPreview = new ListPreviewer<CriAtomExAcf.GlobalAisacInfo>(
				Enumerate<CriAtomExAcf.GlobalAisacInfo>(CriAtomExAcf.GetNumGlobalAisacs, CriAtomExAcf.GetGlobalAisacInfoByIndex).ToList(), "Global AISAC", info => ($"Control : {info.controlId}\t{info.name}", info.name)
			));

		ListPreviewer<string> _dspPreview;
		ListPreviewer<string> DspPreview => _dspPreview ?? (_dspPreview = new ListPreviewer<string>(
				Enumerable.Range(0, CriAtomExAcf.GetNumDspSettings()).Select(i => CriAtomExAcf.GetDspSettingNameByIndex((ushort)i)).ToList(), "Dsp Settings", info => ($"{info}", info)
			));

		ListPreviewer<CriAtomExAcf.AcfDspBusInfo> _busPreview;
		ListPreviewer<CriAtomExAcf.AcfDspBusInfo> BusPreview => _busPreview ?? (_busPreview = new ListPreviewer<CriAtomExAcf.AcfDspBusInfo>(
				Enumerate<CriAtomExAcf.AcfDspBusInfo>(CriAtomExAcf.GetNumBuses, CriAtomExAcf.GetDspBusInformation).ToList(), "Buses", info => ($"{info.busNo: 000}:{info.name}", info.name)
			));

		List<ListPreviewer<CriAtomExAcf.SelectorLabelInfo>> _selectorPreviews;
		List<ListPreviewer<CriAtomExAcf.SelectorLabelInfo>> SelectorPreviews => _selectorPreviews ?? (_selectorPreviews =
			Enumerate<CriAtomExAcf.SelectorInfo>(CriAtomExAcf.GetNumSelectors, CriAtomExAcf.GetSelectorInfoByIndex)
			.Select(selector => new ListPreviewer<CriAtomExAcf.SelectorLabelInfo>(
				Enumerate(() => selector.numLabels, (ushort index, out CriAtomExAcf.SelectorLabelInfo info) => CriAtomExAcf.GetSelectorLabelInfo(selector, index, out info)).ToList(), 
				selector.name, 
				info => ($"{info.labelName}", info.labelName), 
				info => CriAtomAssetsPreviewPlayer.Instance.SetSelectorLabel(info.selectorName, info.labelName))).ToList());

		static readonly string[] tabOptions = new[] { "Categories", "AISAC/Variables", "Selector", "DSP/Buses" };
		int tab = 0;

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (EditorApplication.isPlayingOrWillChangePlaymode) return;

			CriAtomAssetsPreviewPlayer.Instance.RegisterAcf(AssetDatabase.LoadAssetAtPath<CriAtomAcfAsset>((target as AssetImporter).assetPath));

			tab = GUILayout.Toolbar(tab, tabOptions);
			EditorGUILayout.Space();

			if (tab == 0)
			{
				foreach (var group in CategoriesPreview)
					group.Draw();
			}
			if(tab == 1)
			{
				VariablesPreview.Draw();
				AisacControlPreview.Draw();
				GlobalAisacPreview.Draw();
			}
			if (tab == 2)
			{
				foreach (var selector in SelectorPreviews)
					selector.Draw();
			}
			if (tab == 3)
			{
				DspPreview.Draw();
				BusPreview.Draw();
			}
		}

		protected override void Apply()
		{
			base.Apply();
			Clear();
		}

		public override void OnDisable()
		{
			base.OnDisable();
			Clear();
		}

		void Clear()
		{
			CriAtomAssetsPreviewPlayer.Instance.Dispose();
			_aisacControlPreview = null;
			_categoriesPreview = null;
			_dspPreview = null;
			_globalAisacPreview = null;
			_selectorPreviews = null;
		}

		class ListPreviewer<T>
		{
			UnityEditorInternal.ReorderableList reorderableList;

			public ListPreviewer(List<T> list, string name, System.Func<T, (string text, string clipBoard)> drawElement, System.Action<T> onSelected = null){
				reorderableList = new UnityEditorInternal.ReorderableList(
					list,
					typeof(T)
				);
				reorderableList.draggable = false;
				reorderableList.displayAdd = false;
				reorderableList.displayRemove = false;
				reorderableList.drawHeaderCallback += rect => GUI.Label(rect, name);
				reorderableList.drawElementCallback += (rect, index, active, focus) => {
					var element = drawElement((T)reorderableList.list[index]);
					rect.width -= 30f;
					GUI.Label(rect, element.text);
					rect.x += rect.width;
					rect.width = 30f;
					if (GUI.Button(rect, EditorGUIUtility.IconContent("Clipboard"), EditorStyles.miniButton))
						EditorGUIUtility.systemCopyBuffer = element.clipBoard;
				};
				reorderableList.onSelectCallback += l => onSelected?.Invoke((T)l.list[l.index]);
			}

			public void Draw() => reorderableList.DoLayoutList();
		}

		public delegate bool GetByIndexDelegate<T>(ushort index, out T output);
		IEnumerable<T> Enumerate<T>(System.Func<int> getNum, GetByIndexDelegate<T> getByIndex)
		{
			var count = getNum();
			for (ushort i = 0; i < count; i++)
			{
				getByIndex(i, out T output);
				yield return output;
			}
		}
	} 
}
