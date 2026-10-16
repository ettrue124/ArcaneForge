using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateZoneUXEvent : UXEvent
{
	private readonly MtgZone _zone;

	private readonly ICardHolderProvider _cardHolderProvider;

	public UpdateZoneUXEvent(MtgZone zone, ICardHolderProvider cardHolderProvider)
	{
		_zone = zone;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
	}

	public override void Execute()
	{
		foreach (ZoneCardHolderBase item in GetCardHoldersForZone(_zone))
		{
			item.UpdateZoneModel(_zone);
		}
		Complete();
	}

	private IEnumerable<ZoneCardHolderBase> GetCardHoldersForZone(MtgZone zone)
	{
		if (!_cardHolderProvider.TryGetCardHolderByZoneId(zone.Id, out var cardHolder))
		{
			yield break;
		}
		if (cardHolder is IExileCardHolder exileCardHolder)
		{
			foreach (ICardHolder allSubCardHolder in exileCardHolder.GetAllSubCardHolders())
			{
				if (allSubCardHolder is ZoneCardHolderBase zoneCardHolderBase)
				{
					yield return zoneCardHolderBase;
				}
			}
		}
		else if (cardHolder is ICommandCardHolder commandCardHolder)
		{
			foreach (ICardHolder allSubCardHolder2 in commandCardHolder.GetAllSubCardHolders())
			{
				if (allSubCardHolder2 is ZoneCardHolderBase zoneCardHolderBase2)
				{
					yield return zoneCardHolderBase2;
				}
			}
		}
		else if (cardHolder is ZoneCardHolderBase zoneCardHolderBase3)
		{
			yield return zoneCardHolderBase3;
		}
	}
}
