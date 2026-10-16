using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Player.PlayerSelectingOrSubmittedTargets_FX;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class PlayerSelectingTargetsEventTranslator : IEventTranslator
{
	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IVfxProvider _vfxProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	public PlayerSelectingTargetsEventTranslator(AssetLookupSystem assetLookupSystem, IVfxProvider vfxProvider, ICardDatabaseAdapter cardDatabase)
	{
		_assetLookupSystem = assetLookupSystem;
		_vfxProvider = vfxProvider ?? new NullVfxProvider();
		_cardDatabase = cardDatabase;
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
		if (!(allChanges[changeIndex] is PlayerSelectingTargetsEvent playerSelectingTargetsEvent))
		{
			throw new ArgumentException("Event at given index does not implement PlayerSelectingTargetsEvent");
		}
		ICardDataAdapter cardDataAdapter = CardDataExtensions.CreateWithDatabase(playerSelectingTargetsEvent.Affected, _cardDatabase);
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardDataAdapter);
		_assetLookupSystem.Blackboard.Player = playerSelectingTargetsEvent.Affector;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<PlayerSelectingTargets_VFX> loadedTree))
		{
			PlayerSelectingTargets_VFX payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null && payload.VfxDatas.Count > 0)
			{
				PlayVFXUXEvent item = new PlayVFXUXEvent(cardDataAdapter, _vfxProvider, payload.VfxDatas);
				events.Add(item);
			}
		}
	}
}
