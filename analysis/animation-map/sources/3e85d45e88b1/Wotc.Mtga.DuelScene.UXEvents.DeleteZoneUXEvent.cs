namespace Wotc.Mtga.DuelScene.UXEvents;

public class DeleteZoneUXEvent : UXEvent
{
	private readonly uint _zoneId;

	private readonly ICardHolderController _cardHolderController;

	public DeleteZoneUXEvent(uint zoneId, ICardHolderController cardHolderController)
	{
		_zoneId = zoneId;
		_cardHolderController = cardHolderController ?? NullCardHolderController.Default;
	}

	public override void Execute()
	{
		_cardHolderController.DeleteCardHolder(_zoneId);
		Complete();
	}
}
