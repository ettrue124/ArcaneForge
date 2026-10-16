using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SyntheticEventTranslator : IEventTranslator
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _altSystem;

	public SyntheticEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	private SyntheticEventTranslator(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_altSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is SyntheticEvent syntheticEvent)
		{
			events.Add(new SyntheticEventUXEvent(syntheticEvent.Affector, syntheticEvent.Affected, syntheticEvent.Type, _cardDatabase, _gameStateProvider, _cardViewProvider, _vfxProvider, _altSystem));
		}
	}
}
