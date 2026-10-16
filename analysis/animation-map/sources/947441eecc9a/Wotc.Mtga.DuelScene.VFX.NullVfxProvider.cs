using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;

namespace Wotc.Mtga.DuelScene.VFX;

public class NullVfxProvider : IVfxProvider
{
	public static readonly IVfxProvider Default = new NullVfxProvider();

	public IReadOnlyCollection<SplineEvent> GenerateEtbSplineEvents(DuelScene_CDC cardView, IBattlefieldStack stack, bool playHitEffects, Transform localSpaceOverride = null)
	{
		return Array.Empty<SplineEvent>();
	}

	public void GenerateEtbTriggerEvents(DuelScene_CDC cardView, List<SplineEvent> events, IBattlefieldStack stack, Transform localSpaceOverride = null)
	{
	}

	public GameObject PlayAnchoredVFX(VfxData vfxData, AnchorPointType anchorType, ScaffoldingBase scaffold, ICardDataAdapter effectContext, MtgEntity spaceContext = null, Transform localSpaceOverride = null)
	{
		return null;
	}

	public GameObject PlayVFX(VfxData vfxData, ICardDataAdapter effectContext, MtgEntity spaceContext = null, Transform localSpaceOverride = null, string prefabPath = null)
	{
		return null;
	}

	public IEnumerable<Transform> ResolveSpaceIntoTransforms(RelativeSpace space, MtgEntity spaceContext, Transform localSpaceOverride = null, bool ResolveForStackChildren = false)
	{
		yield break;
	}
}
