using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdatePlayerHandSizeTranslator : IEventTranslator
{
	private readonly ICardHolderProvider _cardHolderProvider;

	public UpdatePlayerHandSizeTranslator(ICardHolderProvider cardHolderProvider)
	{
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UpdatePlayerHandSize { Player: GREPlayerNum.LocalPlayer } updatePlayerHandSize)
		{
			events.Add(new UpdatePlayerHandSizeUXEvent(updatePlayerHandSize.MaxHandSize, _cardHolderProvider));
		}
	}
}
