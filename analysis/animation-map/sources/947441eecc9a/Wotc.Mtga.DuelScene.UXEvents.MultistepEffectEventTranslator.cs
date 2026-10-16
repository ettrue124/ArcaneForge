using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class MultistepEffectEventTranslator : IEventTranslator
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IVfxProvider _vfxProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public MultistepEffectEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IVfxProvider>(), context.Get<ICardHolderProvider>(), assetLookupSystem)
	{
	}

	public MultistepEffectEventTranslator(ICardDatabaseAdapter cardDatabase, IVfxProvider vfxProvider, ICardHolderProvider cardHolderProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase;
		_vfxProvider = vfxProvider;
		_cardHolderProvider = cardHolderProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges == null)
		{
			throw new ArgumentNullException("allChanges");
		}
		if (changeIndex < 0 || changeIndex >= allChanges.Count)
		{
			throw new ArgumentOutOfRangeException("changeIndex");
		}
		if (events == null)
		{
			throw new ArgumentNullException("events");
		}
		if (!(allChanges[changeIndex] is MultistepEffectEvent multistepEffectEvent))
		{
			throw new ArgumentException("Event at given index does not implement MultistepEffectEvent");
		}
		UXEvent item = (multistepEffectEvent.IsStarting ? ((MultistepEffectUXEventBase)new MultistepEffectStartedUXEvent(multistepEffectEvent.Affected, multistepEffectEvent.Affector, multistepEffectEvent.AbilityCategory, _cardDatabase, _cardHolderProvider, _vfxProvider, _assetLookupSystem)) : ((MultistepEffectUXEventBase)new MultistepEffectCompletedUXEvent(multistepEffectEvent.Affected, multistepEffectEvent.Affector, multistepEffectEvent.AbilityCategory, _cardDatabase, _cardHolderProvider, _vfxProvider, _assetLookupSystem)));
		events.Add(item);
	}
}
