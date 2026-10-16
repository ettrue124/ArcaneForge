using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ZoneChangeEventTranslator : IEventTranslator
{
	private readonly IContext _context;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly GameManager _gameManager;

	public ZoneChangeEventTranslator(IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager)
	{
		_context = context;
		_assetLookupSystem = assetLookupSystem;
		_gameManager = gameManager;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ZoneChangeEvent zoneChangeEvent)
		{
			MtgCardInstance cardById = oldState.GetCardById(zoneChangeEvent.Id);
			MtgCardInstance mtgCardInstance = newState.GetCardById(zoneChangeEvent.Id) ?? MtgCardInstance.UnknownCardData(zoneChangeEvent.Id, zoneChangeEvent.NewZone);
			MtgCardInstance instigator = FindInstigator(allChanges, changeIndex, zoneChangeEvent.Id, newState, oldState, zoneChangeEvent);
			bool isMeldSuppression = zoneChangeEvent.IsMeldSuppression;
			events.Add(new ZoneTransferUXEvent(_context, _assetLookupSystem, _gameManager, zoneChangeEvent.Id, cardById, zoneChangeEvent.Id, isMeldSuppression ? null : mtgCardInstance, instigator, zoneChangeEvent.OldZone, zoneChangeEvent.NewZone, isMeldSuppression ? ZoneTransferReason.Delete : zoneChangeEvent.Reason));
		}
	}

	public static MtgCardInstance FindInstigator(IReadOnlyList<GameRulesEvent> changes, int index, uint instanceId, MtgGameState newState, MtgGameState oldState, ZoneChangeEvent zce)
	{
		uint instigatorId = zce.InstigatorId;
		if (newState.TryGetCard(instigatorId, out var card))
		{
			return card.Parent ?? card;
		}
		if (oldState.TryGetCard(instigatorId, out var card2))
		{
			return card2.Parent ?? card2;
		}
		for (int num = index - 1; num >= 0; num--)
		{
			if (changes[num] is DamageDealtEvent damageDealtEvent && damageDealtEvent.Victim.InstanceId == instanceId)
			{
				return damageDealtEvent.Damager;
			}
		}
		MtgCardInstance cardById = newState.GetCardById(instanceId);
		if (cardById != null && cardById.TargetedBy.Count > 0)
		{
			MtgCardInstance mtgCardInstance = (MtgCardInstance)cardById.TargetedBy[cardById.TargetedBy.Count - 1];
			return mtgCardInstance.Parent ?? mtgCardInstance;
		}
		MtgCardInstance cardById2 = oldState.GetCardById(instanceId);
		if (cardById2 != null && cardById2.TargetedBy.Count > 0)
		{
			MtgCardInstance mtgCardInstance2 = (MtgCardInstance)cardById2.TargetedBy[cardById2.TargetedBy.Count - 1];
			return mtgCardInstance2.Parent ?? mtgCardInstance2;
		}
		for (int num2 = index - 1; num2 >= 0; num2--)
		{
			GameRulesEvent gameRulesEvent = changes[num2];
			if (gameRulesEvent is ResolutionEvent)
			{
				ResolutionEvent resolutionEvent = (ResolutionEvent)gameRulesEvent;
				MtgCardInstance cardById3 = newState.GetCardById(resolutionEvent.InstanceID);
				if (cardById3 == null || cardById3.Parent == null)
				{
					return cardById3;
				}
				return cardById3.Parent;
			}
		}
		if (zce.Reason == ZoneTransferReason.ZeroLoyalty)
		{
			return newState.GetCardById(zce.Id) ?? oldState.GetCardById(zce.Id);
		}
		return null;
	}
}
