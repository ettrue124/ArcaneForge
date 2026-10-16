using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DeletePlayerUXEvent : UXEvent
{
	private readonly uint _playerId;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IAvatarViewController _avatarViewController;

	private readonly ICardHolderController _cardHolderController;

	private readonly ICardViewManager _cardViewManager;

	public DeletePlayerUXEvent(uint playerId, IGameStateProvider gameStateProvider, IAvatarViewController avatarViewController, ICardHolderController cardHolderController, ICardViewManager cardViewManager)
	{
		_playerId = playerId;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_avatarViewController = avatarViewController ?? NullAvatarViewController.Default;
		_cardHolderController = cardHolderController ?? NullCardHolderController.Default;
		_cardViewManager = cardViewManager ?? NullCardViewManager.Default;
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		_cardHolderController.DeleteSubCardHolder(mtgGameState.Exile.Id, _playerId);
		_cardHolderController.DeleteSubCardHolder(mtgGameState.Command.Id, _playerId);
		foreach (KeyValuePair<uint, MtgZone> zone in mtgGameState.Zones)
		{
			MtgZone value = zone.Value;
			if (value.Owner != null && value.Owner.InstanceId == _playerId)
			{
				_cardHolderController.DeleteCardHolder(value.Id);
			}
		}
		_avatarViewController.DeleteAvatar(_playerId);
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (DuelScene_CDC allCard in _cardViewManager.GetAllCards())
		{
			if (allCard.Model?.Owner?.InstanceId == _playerId)
			{
				list.Add(allCard);
			}
		}
		foreach (DuelScene_CDC item in list)
		{
			_cardViewManager.DeleteCard(item.Model.InstanceId);
		}
		Complete();
	}
}
