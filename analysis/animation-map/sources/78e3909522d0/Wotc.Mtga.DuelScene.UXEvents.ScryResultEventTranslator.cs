using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ScryResultEventTranslator : IEventTranslator
{
	private readonly IObjectPool _objectPool;

	private readonly IUnityObjectPool _unityObjPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ISplineMovementSystem _movementSystem;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IEntityDialogControllerProvider _dialogueProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ScryResultEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<IUnityObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<ICardHolderProvider>(), context.Get<ICardViewProvider>(), context.Get<IBrowserManager>(), context.Get<IEntityDialogControllerProvider>(), context.Get<ISplineMovementSystem>(), assetLookupSystem)
	{
	}

	private ScryResultEventTranslator(IObjectPool objectPool, IUnityObjectPool unityObjPool, ICardDatabaseAdapter cardDatabase, ICardHolderProvider cardHolderProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IEntityDialogControllerProvider dialogueProvider, ISplineMovementSystem splineMovementSystem, AssetLookupSystem assetLookupSystem)
	{
		_objectPool = objectPool;
		_unityObjPool = unityObjPool;
		_cardDatabase = cardDatabase;
		_cardHolderProvider = cardHolderProvider;
		_cardViewProvider = cardViewProvider;
		_browserManager = browserManager;
		_dialogueProvider = dialogueProvider;
		_splineMovementSystem = splineMovementSystem;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ScryResultEvent eventData)
		{
			events.Add(new ScryResultUXEvent(eventData, _objectPool, _unityObjPool, _cardDatabase, _cardHolderProvider, _cardViewProvider, _browserManager, _dialogueProvider, _splineMovementSystem, _assetLookupSystem));
		}
	}
}
