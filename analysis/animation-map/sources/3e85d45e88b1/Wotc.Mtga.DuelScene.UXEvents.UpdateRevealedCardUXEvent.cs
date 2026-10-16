using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateRevealedCardUXEvent : UXEvent
{
	private readonly MtgCardInstance _revealInstance;

	private readonly IRevealedCardsController _revealedCardsController;

	public UpdateRevealedCardUXEvent(MtgCardInstance revealInstance, IRevealedCardsController revealedCardsController)
	{
		_revealInstance = revealInstance;
		_revealedCardsController = revealedCardsController;
	}

	public override void Execute()
	{
		_revealedCardsController.UpdateRevealedCard(_revealInstance);
		Complete();
	}
}
