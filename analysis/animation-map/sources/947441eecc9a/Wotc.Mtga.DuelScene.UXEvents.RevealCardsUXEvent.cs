using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Examine;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RevealCardsUXEvent : UXEvent
{
	private const float STANDARD_DELAY = 0f;

	private const float HAND_DELAY = 1f;

	private readonly List<CardRevealedEvent> _revealEvents = new List<CardRevealedEvent>();

	private readonly InstanceConverter _instanceConverter = new InstanceConverter();

	private readonly IObjectPool _objectPool;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IRevealedCardsManager _revealedCardsManager;

	private float _completionDelay;

	public RevealEventType RevealType => _revealEvents[0].EventType;

	public override bool IsBlocking => true;

	public RevealCardsUXEvent(List<CardRevealedEvent> revealEvents, IContext context)
	{
		_revealEvents.AddRange(revealEvents);
		_objectPool = context.Get<IObjectPool>() ?? NullObjectPool.Default;
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_cardBuilder = context.Get<ICardBuilder<DuelScene_CDC>>() ?? NullCardBuilder<DuelScene_CDC>.Default;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_revealedCardsManager = context.Get<IRevealedCardsManager>() ?? NullRevealedCardsManager.Default;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		switch (RevealType)
		{
		case RevealEventType.Reveal:
			foreach (CardRevealedEvent revealEvent in _revealEvents)
			{
				MtgCardInstance revealedInstance = revealEvent.RevealedInstance;
				if (revealedInstance == null)
				{
					continue;
				}
				CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(revealedInstance.GrpId, revealedInstance.SkinCode);
				if (cardPrintingById != null && (cardPrintingById.GetAftermathPrintingData() != null || cardPrintingById.LinkedFaceType != LinkedFace.SplitCard))
				{
					ICardDataAdapter cardData = _instanceConverter.ConvertModel(revealedInstance.ToCardData(_cardDatabase));
					DuelScene_CDC cardView = _cardBuilder.CreateCDC(cardData);
					MtgZone zoneForPlayer = mtgGameState.GetZoneForPlayer(revealEvent.OwnerId, ZoneType.Revealed);
					if (_cardHolderProvider.TryGetCardHolder<ICardHolder>(zoneForPlayer.Id, out var result))
					{
						result.AddCard(cardView);
					}
				}
			}
			_completionDelay = 0f;
			break;
		case RevealEventType.Create:
		{
			HashSet<OpponentHandCardHolder> hashSet2 = _objectPool.PopObject<HashSet<OpponentHandCardHolder>>();
			foreach (CardRevealedEvent revealEvent2 in _revealEvents)
			{
				_revealedCardsManager.CreateRevealedCard(revealEvent2.OwnerId, revealEvent2.CreateInstance);
				if (_revealedCardsManager.TryGetAssociatedCardHolder<OpponentHandCardHolder>(revealEvent2.CreateInstance.InstanceId, out var cardHolder2))
				{
					hashSet2.Add(cardHolder2);
				}
			}
			foreach (OpponentHandCardHolder item in hashSet2)
			{
				item.Shuffle();
			}
			hashSet2.Clear();
			_objectPool.PushObject(hashSet2, tryClear: false);
			_completionDelay = 1f;
			break;
		}
		case RevealEventType.Delete:
		{
			HashSet<OpponentHandCardHolder> hashSet = _objectPool.PopObject<HashSet<OpponentHandCardHolder>>();
			foreach (CardRevealedEvent revealEvent3 in _revealEvents)
			{
				if (_revealedCardsManager.TryGetAssociatedCardHolder<OpponentHandCardHolder>(revealEvent3.DeleteId, out var cardHolder))
				{
					hashSet.Add(cardHolder);
				}
				_revealedCardsManager.DeleteRevealedCard(revealEvent3.DeleteId);
			}
			foreach (OpponentHandCardHolder item2 in hashSet)
			{
				item2.Shuffle();
			}
			hashSet.Clear();
			_objectPool.PushObject(hashSet, tryClear: false);
			_completionDelay = 1f;
			break;
		}
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_timeRunning >= _completionDelay)
		{
			Complete();
		}
	}
}
