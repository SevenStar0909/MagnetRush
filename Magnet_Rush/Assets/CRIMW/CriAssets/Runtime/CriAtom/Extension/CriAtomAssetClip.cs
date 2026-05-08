/****************************************************************************
 *
 * Copyright (c) 2022 CRI Middleware Co., Ltd.
 *
 ****************************************************************************/

/**
 * \addtogroup CRIADDON_ASSETS_INTEGRATION
 * @{
 */


#if CRIWARE_TIMELINE_1_OR_NEWER

using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using CriWare.CriTimeline.Atom;

namespace CriWare.Assets
{
	public class CriAtomAssetClip : CriWare.CriTimeline.Atom.CriAtomClipBase
	{
		[SerializeField]
		CriAtomCueReference cue;
		[SerializeField]
		CriAtomBehaviour templateBehaviour = new CriAtomBehaviour();

		public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
			return ScriptPlayable<CriAtomBehaviour>.Create(graph, templateBehaviour);
		}

		public override string CueName
		{
			get
			{
				var acb = GetAcb();
#if UNITY_EDITOR
				if (acb == null)
					acb = CriWare.CriTimeline.Atom.CriAtomTimelinePreviewer.Instance.GetAcb(AcbPath, AwbPath);
#endif
				if (acb == null) return null;
				acb.GetCueInfo(cue.CueId, out CriAtomEx.CueInfo info);
				return info.name;
			}
		}

		public override CriAtomExAcb GetAcb() => cue.AcbAsset.Handle;

		public override string AcbPath =>
			cue.AcbAsset.FilePath;

		public override string AwbPath =>
			cue.AcbAsset.Awb?.FilePath;

		public override void SetCueFromAtomSource(CriAtomSourceBase atomSource)
		{
			if (!(atomSource is CriAtomSourceForAsset)) return;

			if (cue.AcbAsset == null)
				cue = (atomSource as CriAtomSourceForAsset).Cue;
		}
	}
}

#endif

/** @} */
