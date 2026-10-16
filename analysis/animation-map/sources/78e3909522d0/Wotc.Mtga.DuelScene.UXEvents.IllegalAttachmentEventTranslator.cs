using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class IllegalAttachmentEventTranslator : IEventTranslator
{
	private readonly IAbilityDataProvider _abilityProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public IllegalAttachmentEventTranslator(IAbilityDataProvider abilityProvider, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_abilityProvider = abilityProvider ?? NullAbilityDataProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is IllegalAttachmentEvent illegalAttachmentEvent)
		{
			events.Add(new IllegalAttachmentUXEvent(illegalAttachmentEvent.AffectedId, illegalAttachmentEvent.InvalidatingGrpid, _abilityProvider, _cardViewProvider, _vfxProvider, _assetLookupSystem));
		}
	}
}
