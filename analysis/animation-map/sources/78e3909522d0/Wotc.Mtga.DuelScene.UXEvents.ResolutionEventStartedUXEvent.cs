using System.Collections.Generic;
using AssetLookupTree.Payloads.Resolution;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ResolutionEventStartedUXEvent : ResolutionEffectUXEventBase
{
	private readonly ResolutionEffectModel _resolutionEffectModel;

	public ResolutionEventStartedUXEvent()
	{
	}

	public ResolutionEventStartedUXEvent(ResolutionEffectModel resolutionEffectModel, IResolutionEffectController resolutionEffectController, ICardDatabaseAdapter cardDatabase, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCache, IVfxProvider vfxProvider, ICardHolderProvider cardHolderProvider, IEntityViewProvider entityViewProvider, MtgCardInstance instigator, CardPrintingData cardPrinting, AbilityPrintingData abilityPrinting, Data resolutionData, IReadOnlyCollection<VFX_Base> vfx, IReadOnlyCollection<SFX_Base> sfx, Projectile projectile, float defaultDuration, float duration)
		: base(resolutionEffectController, cardDatabase, splineMovementSystem, splineCache, vfxProvider, cardHolderProvider, entityViewProvider, instigator, cardPrinting, abilityPrinting, resolutionData, vfx, sfx, projectile, defaultDuration, duration)
	{
		_resolutionEffectModel = resolutionEffectModel;
	}

	public ResolutionEventStartedUXEvent(MtgCardInstance instigator)
		: base(instigator)
	{
	}

	public override void Execute()
	{
		_resolutionEffectController.ResolutionStart(_resolutionEffectModel);
		base.Execute();
	}
}
