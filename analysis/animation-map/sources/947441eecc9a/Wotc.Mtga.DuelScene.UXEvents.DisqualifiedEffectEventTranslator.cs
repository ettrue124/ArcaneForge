using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DisqualifiedEffectEventTranslator : IEventTranslator
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameEffectController _gameEffectController;

	private readonly IVfxProvider _vfxProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public DisqualifiedEffectEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardViewProvider>(), context.Get<IGameEffectController>(), context.Get<IVfxProvider>(), context.Get<ICardHolderProvider>(), assetLookupSystem)
	{
	}

	public DisqualifiedEffectEventTranslator(ICardViewProvider cardViewProvider, IGameEffectController gameEffectController, IVfxProvider vfxProvider, ICardHolderProvider cardHolderProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardViewProvider = cardViewProvider;
		_gameEffectController = gameEffectController;
		_vfxProvider = vfxProvider;
		_cardHolderProvider = cardHolderProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is DisqualifiedEffectEvent disqualifiedEffectEvent)
		{
			MtgCardInstance disqualifierCard = GetDisqualifierCard(newState, disqualifiedEffectEvent.AffectorId);
			if (disqualifierCard != null)
			{
				events.Add(new DisqualifiedEffectUXEvent(disqualifierCard, _cardViewProvider, _gameEffectController, _vfxProvider, _cardHolderProvider, _assetLookupSystem));
			}
		}
	}

	private MtgCardInstance GetDisqualifierCard(MtgGameState gameState, uint id)
	{
		if (gameState.TryGetCard(id, out var card))
		{
			return card;
		}
		if (gameState.TrackedHistoricCards.TryGetValue(id, out var value))
		{
			return value;
		}
		return null;
	}
}
