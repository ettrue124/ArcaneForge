using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.UXEventData;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Duel;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

internal class DieRollEventTranslator : IEventTranslator
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public DieRollEventTranslator(ICardDatabaseAdapter cardDatabase, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges == null)
		{
			throw new ArgumentNullException("allChanges");
		}
		if (changeIndex < 0 || changeIndex >= allChanges.Count)
		{
			throw new ArgumentOutOfRangeException("changeIndex");
		}
		if (events == null)
		{
			throw new ArgumentNullException("events");
		}
		if (!(allChanges[changeIndex] is DieRollEvent dieRollEvent))
		{
			throw new ArgumentException("Event at given index does not implement DieRollEvent");
		}
		uint affectorId = dieRollEvent.Results.AffectorId;
		MtgCardInstance cardById = newState.GetCardById(affectorId);
		if (cardById == null)
		{
			cardById = oldState.GetCardById(affectorId);
		}
		if (cardById == null)
		{
			SimpleLog.LogError($"DieRollEventTranslator.Translate: unable to find affectorCard with id[{affectorId}]! No die rolls will appear.");
			return;
		}
		CardData cardData = CardDataExtensions.CreateWithDatabase(cardById, _cardDatabase);
		List<DieRollResultData> list = new List<DieRollResultData> { dieRollEvent.Results };
		if (allChanges is List<GameRulesEvent> list2)
		{
			for (int i = changeIndex + 1; i < list2.Count; i++)
			{
				if (list2[i] is DieRollEvent dieRollEvent2 && dieRollEvent.Results.AffectorId == dieRollEvent2.Results.AffectorId)
				{
					list.Add(dieRollEvent2.Results);
					list2.RemoveAt(i);
					i--;
				}
				else if (!(allChanges[i] is CardChangedEvent cardChangedEvent) || dieRollEvent.Results.AffectorId != cardChangedEvent.AffectorId || cardChangedEvent.Property != PropertyType.DieRollResult)
				{
					break;
				}
			}
		}
		IDieRollUxEventData dieRollData = GetDieRollData(cardData);
		if (dieRollData != null)
		{
			events.Add(new DieRollUXEvent(list, dieRollData, cardData.Controller.ClientPlayerEnum, _vfxProvider, _assetLookupSystem));
		}
	}

	private IDieRollUxEventData GetDieRollData(ICardDataAdapter affector)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(affector);
		DieRollData payload = _assetLookupSystem.TreeLoader.LoadTree<DieRollData>().GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null)
		{
			return null;
		}
		return AssetLoader.GetObjectData(payload.DieRollUxEventDataRef);
	}
}
