using AssetLookupTree;
using AssetLookupTree.Payloads.Card.MultistepEffects_FX;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class MultistepEffectStartedUXEvent : MultistepEffectUXEventBase
{
	public MultistepEffectStartedUXEvent(MtgPlayer affected, MtgCardInstance affector, AbilitySubCategory abilityCategory, ICardDatabaseAdapter cardDatabase, ICardHolderProvider cardHolderProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(affected, affector, abilityCategory, cardDatabase, cardHolderProvider, vfxProvider, assetLookupSystem)
	{
		assetLookupSystem = GetALTWithParams(_affectorData);
		MultistepEffect_Start_VFX payload = assetLookupSystem.TreeLoader.LoadTree<MultistepEffect_Start_VFX>().GetPayload(assetLookupSystem.Blackboard);
		if (payload != null)
		{
			_vfxs.AddRange(payload.VfxDatas);
		}
		MultistepEffect_Start_SFX payload2 = assetLookupSystem.TreeLoader.LoadTree<MultistepEffect_Start_SFX>().GetPayload(assetLookupSystem.Blackboard);
		if (payload2 != null)
		{
			_sfxData = payload2.SfxData;
		}
	}
}
