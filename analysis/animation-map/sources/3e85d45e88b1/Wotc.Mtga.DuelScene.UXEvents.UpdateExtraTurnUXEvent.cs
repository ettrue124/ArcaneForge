using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateExtraTurnUXEvent : UXEvent
{
	private readonly IReadOnlyList<ExtraTurn> _extraTurns;

	private readonly ITurnController _turnController;

	private readonly IExtraTurnRenderer _extraTurnRenderer;

	public UpdateExtraTurnUXEvent(IReadOnlyList<ExtraTurn> extraTurns, ITurnController turnController, IExtraTurnRenderer extraTurnRenderer)
	{
		_extraTurns = extraTurns ?? Array.Empty<ExtraTurn>();
		_turnController = turnController ?? NullTurnController.Default;
		_extraTurnRenderer = extraTurnRenderer ?? NullExtraTurnRenderer.Default;
	}

	public override void Execute()
	{
		_turnController.SetExtraTurns(_extraTurns);
		_extraTurnRenderer.Render(_extraTurns);
		Complete();
	}
}
