namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdatePlayerHandSizeUXEvent : UXEvent
{
	private readonly uint _count;

	private readonly ICardHolderProvider _cardHolderProvider;

	public UpdatePlayerHandSizeUXEvent(uint count, ICardHolderProvider cardHolderProvider)
	{
		_count = count;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
	}

	public override void Execute()
	{
		if (_cardHolderProvider.TryGetCardHolder(GREPlayerNum.LocalPlayer, CardHolderType.Hand, out HandCardHolder result))
		{
			result.SetMaxHandSize(_count);
		}
		Complete();
	}
}
