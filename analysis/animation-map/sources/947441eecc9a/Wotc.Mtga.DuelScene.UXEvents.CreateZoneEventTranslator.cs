using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CreateZoneEventTranslator : IEventTranslator
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderManager _cardHolderManager;

	public CreateZoneEventTranslator(IGameStateProvider gameStateProvider, ICardHolderManager cardHolderManager)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardHolderManager = cardHolderManager ?? NullCardHolderManager.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (!(allChanges[changeIndex] is CreateZoneEvent { Zone: var zone }) || IgnoreZone(zone.Type))
		{
			return;
		}
		events.Add(new CreateZoneUXEvent(zone, _cardHolderManager));
		if (zone.Type == ZoneType.Battlefield)
		{
			events.Add(new RegisterPlayersToBattlefield(_cardHolderManager, _gameStateProvider));
			return;
		}
		ZoneType type = zone.Type;
		if (type == ZoneType.Exile || type == ZoneType.Command)
		{
			events.Add(new CreateSubCardHolders(zone, _gameStateProvider, _cardHolderManager));
		}
		else if (zone.Type == ZoneType.Hand && zone.OwnerNum == GREPlayerNum.LocalPlayer)
		{
			events.Add(new UpdatePlayerHandSizeUXEvent(zone.Owner.MaxHandSize, _cardHolderManager));
		}
	}

	private static bool IgnoreZone(ZoneType zoneType)
	{
		return zoneType switch
		{
			ZoneType.None => true, 
			ZoneType.Suppressed => true, 
			ZoneType.Pending => true, 
			ZoneType.Limbo => true, 
			ZoneType.Sideboard => true, 
			ZoneType.PhasedOut => true, 
			_ => false, 
		};
	}
}
