using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Card;
using AssetLookupTree.Payloads.SyntheticEvent;
using GreClient.CardData;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SyntheticEventUXEvent : UXEvent
{
	public readonly uint AffectorId;

	public readonly IReadOnlyList<uint> AffectedIds;

	private readonly SyntheticEventType _syntheticEventType;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _altSystem;

	public SyntheticEventUXEvent(uint affector, List<uint> affected, SyntheticEventType syntheticEventType, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		AffectorId = affector;
		AffectedIds = affected;
		_syntheticEventType = syntheticEventType;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_altSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		ICardDataAdapter cardDataAdapter = null;
		if (mtgGameState.TryGetCard(AffectorId, out var card))
		{
			cardDataAdapter = ((!_cardViewProvider.TryGetCardView(AffectorId, out var cardView)) ? CardDataExtensions.CreateWithDatabase(card, _cardDatabase) : cardView.Model);
		}
		if (_altSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SyntheticEventEffects> loadedTree))
		{
			_altSystem.Blackboard.Clear();
			_altSystem.Blackboard.SetCardDataExtensive(cardDataAdapter);
			_altSystem.Blackboard.SyntheticEvent = _syntheticEventType;
			SyntheticEventEffects payload = loadedTree.GetPayload(_altSystem.Blackboard);
			if (payload != null)
			{
				foreach (uint affectedId in AffectedIds)
				{
					if (!mtgGameState.TryGetEntity(affectedId, out var mtgEntity))
					{
						continue;
					}
					foreach (VfxData vfxData in payload.VfxDatas)
					{
						GameObject gameObject = _vfxProvider.PlayVFX(vfxData, cardDataAdapter, mtgEntity);
						if ((bool)gameObject)
						{
							AudioManager.PlayAudio(payload.SfxData.AudioEvents, gameObject);
						}
					}
				}
			}
			_altSystem.Blackboard.Clear();
		}
		foreach (DuelScene_CDC allCard in _cardViewProvider.GetAllCards())
		{
			if (allCard == null || allCard.CurrentCardHolder == null)
			{
				continue;
			}
			MtgCardInstance cardById = mtgGameState.GetCardById(allCard.InstanceId);
			if (cardById == null)
			{
				continue;
			}
			CardData cardData = CardDataExtensions.CreateWithDatabase(cardById, _cardDatabase);
			if (cardData == null)
			{
				continue;
			}
			CardHolderType cardHolderType = allCard.CurrentCardHolder.CardHolderType;
			if (!_altSystem.TreeLoader.TryLoadTree(out AssetLookupTree<PropertyUpdateVFX> loadedTree2))
			{
				continue;
			}
			_altSystem.Blackboard.Clear();
			_altSystem.Blackboard.SetCardDataExtensive(cardData);
			_altSystem.Blackboard.CardHolderType = cardHolderType;
			PropertyUpdateVFX payload2 = loadedTree2.GetPayload(_altSystem.Blackboard);
			if (payload2 == null)
			{
				continue;
			}
			foreach (VfxData vfxData2 in payload2.VfxDatas)
			{
				_vfxProvider.PlayAnchoredVFX(vfxData2, payload2.AnchorPointType, allCard.ActiveScaffold, cardData);
			}
		}
		Complete();
	}
}
