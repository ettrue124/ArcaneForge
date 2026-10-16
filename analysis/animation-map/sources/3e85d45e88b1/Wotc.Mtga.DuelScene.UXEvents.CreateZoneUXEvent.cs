using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CreateZoneUXEvent : UXEvent
{
	private readonly MtgZone _zone;

	private readonly ICardHolderController _controller;

	public CreateZoneUXEvent(MtgZone zone, ICardHolderController controller)
	{
		_zone = zone;
		_controller = controller ?? NullCardHolderController.Default;
	}

	public override void Execute()
	{
		_controller.CreateCardHolder(_zone);
		Complete();
	}
}
