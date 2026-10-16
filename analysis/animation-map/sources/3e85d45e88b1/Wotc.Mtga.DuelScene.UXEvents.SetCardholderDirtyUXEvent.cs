using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SetCardholderDirtyUXEvent : UXEvent
{
	private readonly ZoneType _zoneType;

	private readonly GREPlayerNum _playerType;

	private readonly ICardHolderProvider _cardHolderProvider;

	public SetCardholderDirtyUXEvent(ZoneType zoneType, GREPlayerNum playerType, ICardHolderProvider cardHolderProvider)
	{
		_zoneType = zoneType;
		_playerType = playerType;
		_cardHolderProvider = cardHolderProvider;
	}

	public override void Execute()
	{
		if (_cardHolderProvider.TryGetCardHolder(_playerType, _zoneType.ToCardHolderType(), out var cardHolder))
		{
			cardHolder.LayoutNow();
		}
		Complete();
	}
}
