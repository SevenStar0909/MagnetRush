/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ADDRESSABLES_INTEGRATION
 * @{
 */

#if CRI_USE_ADDRESSABLES
#if !CRI_ADDRESSABLES_DISABLE_ANCHOR_ASSET

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using System.Linq;

namespace CriWare.Assets
{
	public class CriAddressablesSetting : CriAssetSettingsBase<CriAddressablesSetting>
	{
		[System.Serializable]
		internal enum DeployMode
		{
			UseAnchorAsset,
			UseCriBuildScript,
		}

		static bool? _exists = null;
		internal static bool Exists { get {
				if(!_exists.HasValue)
					_exists = AssetDatabase.FindAssets("t:" + nameof(CriAddressablesSetting)).Length > 0;
				return _exists.Value;
			}
		}
		internal static DeployMode Mode => Exists ? Instance.mode : DeployMode.UseCriBuildScript;

		[SerializeField]
		DeployMode mode = DeployMode.UseAnchorAsset;

		[SerializeField]
		public string anchorFolderPath = "CriData/Addressables";
		[SerializeField]
		public string deployFolderPath = "CriAddressables";
		[SerializeField]
		CriAddressablesPathPair remote;
		[SerializeField]
		CriAddressablesPathPair local;
		[SerializeField]
		bool appendGuidToAnchor = true;

		public string AnchorFolderPath => anchorFolderPath.StartsWith("Packages") ?
			anchorFolderPath :
			System.IO.Path.Combine("Assets", anchorFolderPath);

		public bool AppendGuidToAnchor => appendGuidToAnchor;

		static AddressableAssetSettings Settings => UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

		public CriAddressablesPathPair Remote { get
			{
				if (remote == null)
				{
					remote = CreateInstance<CriAddressablesPathPair>();
					remote.name = "Remote";
					remote.buildPath = new ProfileValueReference();
					remote.buildPath.SetVariableByName(Settings, Settings.profileSettings.GetVariableNames().Where(name => name.ToLowerInvariant().Contains("remote") && name.ToLowerInvariant().Contains("build")).FirstOrDefault());
					remote.loadPath = new ProfileValueReference();
					remote.loadPath.SetVariableByName(Settings, Settings.profileSettings.GetVariableNames().Where(name => name.ToLowerInvariant().Contains("remote") && name.ToLowerInvariant().Contains("load")).FirstOrDefault());
					AssetDatabase.AddObjectToAsset(remote, this);
					AssetDatabase.SaveAssets();
					AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));
				}
				return remote;
			} }

		public CriAddressablesPathPair Local
		{
			get
			{
				if (local == null)
				{
					local = CreateInstance<CriAddressablesPathPair>();
					local.name = "Local";
					local.buildPath = new ProfileValueReference();
					local.buildPath.SetVariableByName(Settings, Settings.profileSettings.GetVariableNames().Where(name => name.ToLowerInvariant().Contains("local") && name.ToLowerInvariant().Contains("build")).FirstOrDefault());
					local.loadPath = new ProfileValueReference();
					local.loadPath.SetVariableByName(Settings, Settings.profileSettings.GetVariableNames().Where(name => name.ToLowerInvariant().Contains("local") && name.ToLowerInvariant().Contains("load")).FirstOrDefault());
					AssetDatabase.AddObjectToAsset(local, this);
					AssetDatabase.SaveAssets();
					AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));
				}
				return local;
			}
		}
	}

	[CustomEditor(typeof(CriAddressablesSetting))]
	public class CriAddressablesSettingEditor : UnityEditor.Editor
	{
		Dictionary<CriAddressablesPathPair, Editor> pathEditors = new Dictionary<CriAddressablesPathPair, Editor>();

		void DrawPathEditor(CriAddressablesPathPair pairObject) {
			if (!pathEditors.ContainsKey(pairObject))
				pathEditors.Add(pairObject, CreateEditor(pairObject));
			EditorGUILayout.LabelField(pairObject.name, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			pathEditors[pairObject].OnInspectorGUI();
			EditorGUI.indentLevel--;
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var modeProp = serializedObject.FindProperty("mode");
			EditorGUILayout.PropertyField(modeProp);
			if (modeProp.intValue == (int)CriAddressablesSetting.DeployMode.UseAnchorAsset)
			{
				EditorGUILayout.HelpBox("Use AnchorAsset Mode will be removed in a forthcoming release.", MessageType.Warning);
				EditorGUI.indentLevel++;
				DrawPathEditor((target as CriAddressablesSetting).Local);
				DrawPathEditor((target as CriAddressablesSetting).Remote);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("appendGuidToAnchor"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("anchorFolderPath"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("deployFolderPath"), new GUIContent("Deploy Folder Suffix"));
				EditorGUI.indentLevel--;
			}
			else
			{
				var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
				if (!typeof(CriAddressablesBuildScript).IsAssignableFrom(settings.ActivePlayerDataBuilder.GetType()))
				{
					EditorGUILayout.HelpBox("CRI Addressables needs CriAddressablesBuildScript or its derived class for build script.", MessageType.Warning);
					if (GUILayout.Button("Setup Build Script"))
						SetupBuildScript();
				}
			}
			serializedObject.ApplyModifiedProperties();
		}

		void SetupBuildScript()
		{
			var existing = AssetDatabase.LoadAssetAtPath<CriAddressablesBuildScript>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"t:{nameof(CriAddressablesBuildScript)}").FirstOrDefault()));
			if (existing == null)
			{
				var path = EditorUtility.SaveFilePanelInProject(
					"Save New CriAddressablesBuildScript", 
					"New CriAddressablesBuildScript", "asset", 
					"Please enter a file name", 
					"Assets");
				if (string.IsNullOrEmpty(path)) return;
				existing = CreateInstance<CriAddressablesBuildScript>();
				AssetDatabase.CreateAsset(existing, path);
			}
			var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
			settings.DataBuilders[settings.ActivePlayerDataBuilderIndex] = existing;
			EditorUtility.SetDirty(settings);
		}
	}
}

#endif
#endif

/** @} */
