namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardViewImmediateUpdateUXEvent : UXEvent
{
	private readonly uint _instanceId;

	private readonly ICardViewProvider _cardViewProvider;

	public CardViewImmediateUpdateUXEvent(uint instanceId, ICardViewProvider cardViewProvider)
	{
		_instanceId = instanceId;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
	}

	public override void Execute()
	{
		if (_cardViewProvider.TryGetCardView(_instanceId, out var cardView))
		{
			cardView.ImmediateUpdate();
		}
		Complete();
	}
}
