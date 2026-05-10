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

#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using System.IO;

namespace CriWare.Assets
{
	[ScriptedImporter(2, "acb", 2)]
	public partial class CriAtomAcbAssetImporter : CriAssetImporter
	{
		/* deprecated */
		[SerializeField]
		internal CriAtomAwbAsset awb = null;

		[SerializeField]
		internal AcbAssetInfo assetInfo;

		[System.Serializable]
		internal partial struct AcbAssetInfo
		{
			public CriAtomAwbAsset awb;
			public CriAtomAwbAsset[] additionalAwbs;
		}

		public CriAtomAwbAsset AwbAsset {get => assetInfo.awb; set => assetInfo.awb = value; }
		public IEnumerable<CriAtomAwbAsset> AdditionalAwbAssets { get => assetInfo.additionalAwbs; set => assetInfo.additionalAwbs = value.ToArray(); }

		public override void OnImportAsset(AssetImportContext ctx)
		{
			/* for compatibility */
			assetInfo.awb = assetInfo.awb ?? awb;

			var main = ScriptableObject.CreateInstance<CriAtomAcbAsset>();
			main.implementation = CreateAssetImpl(ctx);
			main.awb = assetInfo.awb;
			main.additionalAwbs = assetInfo.additionalAwbs;
			ctx.AddObjectToAsset("main", main);
			ctx.SetMainObject(main);
		}

		public override bool IsAssetImplCompatible => true;

		void Reset()
		{
			if (assetInfo.awb == null)
				assetInfo.awb = UnityEditor.AssetDatabase.LoadAssetAtPath<CriAtomAwbAsset>(Path.ChangeExtension(assetPath, "awb"));
		}
	}
}

/** @} */
