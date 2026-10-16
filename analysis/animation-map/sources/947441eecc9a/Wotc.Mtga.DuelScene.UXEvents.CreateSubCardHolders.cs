using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CreateSubCardHolders : UXEvent
{
	private readonly MtgZone _zone;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderController _cardHolderController;

	public CreateSubCardHolders(MtgZone zone, IGameStateProvider gameStateProvider, ICardHolderController cardHolderController)
	{
		_zone = zone;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardHolderController = cardHolderController ?? NullCardHolderController.Default;
	}

	public override void Execute()
	{
		foreach (MtgPlayer player in ((MtgGameState)_gameStateProvider.CurrentGameState).Players)
		{
			_cardHolderController.CreateSubCardHolder(_zone, player);
		}
		Complete();
	}
}
