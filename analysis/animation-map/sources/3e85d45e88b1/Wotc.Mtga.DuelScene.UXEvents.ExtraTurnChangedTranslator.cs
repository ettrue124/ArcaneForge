using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ExtraTurnChangedTranslator : IEventTranslator
{
	private readonly ITurnController _turnController;

	private readonly IExtraTurnRenderer _extraTurnRenderer;

	public ExtraTurnChangedTranslator(ITurnController turnController, IExtraTurnRenderer extraTurnRenderer)
	{
		_turnController = turnController ?? NullTurnController.Default;
		_extraTurnRenderer = extraTurnRenderer ?? NullExtraTurnRenderer.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ExtraTurnsChanged extraTurnsChanged)
		{
			events.Add(new UpdateExtraTurnUXEvent(extraTurnsChanged.ExtraTurns, _turnController, _extraTurnRenderer));
		}
	}
}
