using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RegisterPlayersToBattlefield : UXEvent
{
	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IGameStateProvider _gameStateProvider;

	public RegisterPlayersToBattlefield(ICardHolderProvider cardHolderProvider, IGameStateProvider gameStateProvider)
	{
		_cardHolderProvider = cardHolderProvider;
		_gameStateProvider = gameStateProvider;
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		if (_cardHolderProvider.TryGetCardHolderByZoneId(mtgGameState.Battlefield.Id, out var cardHolder) && cardHolder is BattlefieldCardHolder battlefieldCardHolder)
		{
			foreach (MtgPlayer player in mtgGameState.Players)
			{
				battlefieldCardHolder.RegisterPlayer(player);
			}
		}
		Complete();
	}
}
