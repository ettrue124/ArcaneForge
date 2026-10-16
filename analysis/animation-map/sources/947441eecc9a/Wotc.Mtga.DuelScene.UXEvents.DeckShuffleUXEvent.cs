using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DeckShuffleUXEvent : UXEvent
{
	private readonly uint _playerId;

	private readonly List<uint> _oldIds;

	private readonly List<uint> _newIds;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewManager _cardViewManager;

	private readonly ICardHolderProvider _cardHolderProvider;

	public override bool IsBlocking => true;

	public DeckShuffleUXEvent(uint playerId, List<uint> oldIds, List<uint> newIds, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewManager cardViewManager, ICardHolderProvider cardHolderProvider)
	{
		_playerId = playerId;
		_oldIds = oldIds;
		_newIds = newIds;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewManager = cardViewManager ?? NullCardViewManager.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		MtgPlayer mtgPlayer = ((mtgGameState.LocalPlayer.InstanceId == _playerId) ? mtgGameState.LocalPlayer : mtgGameState.Opponent);
		LibraryCardHolder cardHolder = _cardHolderProvider.GetCardHolder<LibraryCardHolder>(mtgPlayer.ClientPlayerEnum, CardHolderType.Library);
		AudioManager.PlayAudio(WwiseEvents.sfx_basicloc_shuff, cardHolder.gameObject);
		DuelScene_CDC duelScene_CDC = null;
		foreach (uint item in _oldIds.FindAll((uint x) => _newIds.Contains(x)))
		{
			_oldIds.Remove(item);
			_newIds.Remove(item);
			MtgCardInstance mtgCardInstance = mtgGameState.GetCardById(item);
			if (mtgCardInstance == null)
			{
				mtgCardInstance = MtgCardInstance.UnknownCardData(item, mtgGameState.GetZone(ZoneType.Library, mtgPlayer));
			}
			duelScene_CDC = _cardViewManager.GetCardView(item);
			duelScene_CDC.SetModel(mtgCardInstance.ToCardData(_cardDatabase));
		}
		for (int num = 0; num < _oldIds.Count; num++)
		{
			uint num2 = _oldIds[num];
			duelScene_CDC = _cardViewManager.GetCardView(num2);
			uint num3 = _newIds[num];
			MtgCardInstance mtgCardInstance2 = mtgGameState.GetCardById(num3);
			if (mtgCardInstance2 == null)
			{
				mtgCardInstance2 = MtgCardInstance.UnknownCardData(num3, mtgGameState.GetZone(ZoneType.Library, mtgPlayer));
			}
			if (duelScene_CDC != null)
			{
				_cardViewManager.UpdateIdForCardView(num2, num3);
				duelScene_CDC.SetModel(mtgCardInstance2.ToCardData(_cardDatabase));
			}
			else
			{
				Debug.LogError("no cdc found to shuffle with old Id " + num2);
				_cardViewManager.CreateCardView(mtgCardInstance2.ToCardData(_cardDatabase));
			}
		}
		cardHolder.LayoutNow();
		Complete();
	}
}
