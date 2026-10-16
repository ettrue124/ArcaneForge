using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UXEventUpdatePhase : UXEvent
{
	private readonly ITurnController _turnController;

	public Phase Phase { get; }

	public Step Step { get; }

	public UXEventUpdatePhase(Phase phase, Step step, ITurnController turnController)
	{
		Phase = phase;
		Step = step;
		_turnController = turnController ?? NullTurnController.Default;
	}

	public override void Execute()
	{
		_turnController.SetPhase(Phase, Step);
		Complete();
	}
}
