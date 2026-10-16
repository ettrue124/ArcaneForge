using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UserActionTakenTranslator : IEventTranslator
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public UserActionTakenTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	private UserActionTakenTranslator(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_entityViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UserActionTakenEvent userActionTakenEvent)
		{
			events.Add(new UserActionTakenUXEvent(userActionTakenEvent.Affector, userActionTakenEvent.Affected, userActionTakenEvent.AbilityGrpId, userActionTakenEvent.Type, _cardDatabase, _gameStateProvider, _entityViewProvider, _vfxProvider, _assetLookupSystem));
		}
	}
}
