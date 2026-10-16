using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ReplacementEffectAppliedEventTranslator : IEventTranslator
{
	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ReplacementEffectAppliedEventTranslator(IAbilityDataProvider abilityDataProvider, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_abilityDataProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ReplacementEffectAppliedEvent replacementEffectAppliedEvent)
		{
			events.Add(new ReplacementEffectAppliedUXEvent(replacementEffectAppliedEvent.AffectedId, replacementEffectAppliedEvent.AffectedId, _abilityDataProvider, _cardViewProvider, _vfxProvider, _assetLookupSystem));
		}
	}
}
