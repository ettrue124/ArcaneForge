using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ChoiceResultEventTranslator : IEventTranslator
{
	private readonly IObjectPool _objectPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly IEntityDialogControllerProvider _dialogueProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ChoiceResultEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<IVfxProvider>(), context.Get<IEntityDialogControllerProvider>(), assetLookupSystem)
	{
	}

	public ChoiceResultEventTranslator(IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, IEntityDialogControllerProvider dialogueProvider, AssetLookupSystem assetLookupSystem)
	{
		_objectPool = objectPool;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_vfxProvider = vfxProvider;
		_dialogueProvider = dialogueProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		UXEvent uXEvent = Translate_Internal(allChanges[changeIndex] as ChoiceResultEvent);
		if (uXEvent != null)
		{
			events.Add(uXEvent);
		}
	}

	private UXEvent Translate_Internal(ChoiceResultEvent cre)
	{
		if (cre == null)
		{
			return null;
		}
		if (cre.IsRandom)
		{
			return new ChoiceResultUXEvent_Random(cre, _cardViewProvider);
		}
		if (cre.IsPermanent)
		{
			return new ChoiceResultUXEvent_Permanent(cre, _objectPool, _cardDatabase.CardDataProvider, _gameStateProvider, _cardViewProvider, _vfxProvider, _assetLookupSystem);
		}
		return new ChoiceResultUXEvent_General(cre, _dialogueProvider, new ChoiceResultUXEvent_General.ChoiceResultOutputter(_cardDatabase.GreLocProvider, _cardDatabase.ClientLocProvider), new ChoiceResultUXEvent_General.ChoiceResultVfxPlayer(cre.AffectorId, _cardDatabase, _gameStateProvider, _vfxProvider, _assetLookupSystem));
	}
}
