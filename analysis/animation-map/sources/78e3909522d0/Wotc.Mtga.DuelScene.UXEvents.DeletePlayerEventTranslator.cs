using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DeletePlayerEventTranslator : IEventTranslator
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IAvatarViewController _avatarViewController;

	private readonly ICardHolderController _cardHolderController;

	private readonly ICardViewManager _cardViewManager;

	public DeletePlayerEventTranslator(IGameStateProvider gameStateProvider, IAvatarViewController avatarViewController, ICardHolderController cardHolderController, ICardViewManager cardViewManager)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_avatarViewController = avatarViewController ?? NullAvatarViewController.Default;
		_cardHolderController = cardHolderController ?? NullCardHolderController.Default;
		_cardViewManager = cardViewManager ?? NullCardViewManager.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is DeletePlayerEvent deletePlayerEvent)
		{
			events.Add(new DeletePlayerUXEvent(deletePlayerEvent.PlayerId, _gameStateProvider, _avatarViewController, _cardHolderController, _cardViewManager));
		}
	}
}
