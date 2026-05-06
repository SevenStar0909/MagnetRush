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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Security.Cryptography;
using System;
using System.Linq;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace CriWare.Assets
{
	/**
	 * <summary>CRI Addressables向けデプロイ先定義アセット</summary>
	 * <remarks>
	 * <para header='説明'>
	 * CRI Addressablesで管理するデータのデプロイ先を定義するアセットです。<br/>
	 * 本アセットが持つビルドパス/ロードパス情報を元にAddressablesGroupが自動的に生成されます。<br/>
	 * </para>
	 * </remarks>
	 * <seealso cref="CriCustomAddressableAssetImplCreator(CriAddressablesPathPair)"/>
	 */
	[CreateAssetMenu(fileName = "NewCriAddressableGroup", menuName = "CRIWARE/Cri Addressable Group")]
	public class CriAddressablesPathPair : ScriptableObject
	{
		[SerializeField]
		public ProfileValueReference buildPath;
		[SerializeField]
		public ProfileValueReference loadPath;

		internal CriAddressableGroup CreateGroup() => new CriAddressableGroup($"CriData_{this.name}", "CriPackedAssetsTemplate", this);
	}

	[CustomEditor(typeof(CriAddressablesPathPair))]
	internal class CriAddressablesPathPairEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("buildPath"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("loadPath"));
			if (EditorGUI.EndChangeCheck())
			{
				EditorUtility.SetDirty(target);
				AssetDatabase.SaveAssets();
			}
			serializedObject.ApplyModifiedProperties();
		}

		static List<CriAddressablesPathPair> references;
		[UnityEditor.Callbacks.DidReloadScripts]
		static void KeepReference() => references = CollectReference();

		internal static List<CriAddressablesPathPair> CollectReference() =>
			AssetDatabase.FindAssets($"t:{nameof(CriAddressablesPathPair)}")
			.SelectMany(guid => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
			.Where(obj => obj is CriAddressablesPathPair)
			.Select(obj => (CriAddressablesPathPair)obj)
			.ToList();
	}
}

#endif
#endif

/** @} */
