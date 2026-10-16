using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardNamedEventTranslator : IEventTranslator
{
	private readonly IEntityDialogControllerProvider _dialogueProvider;

	private readonly IGreLocProvider _locManager;

	public CardNamedEventTranslator(IContext context)
		: this(context.Get<IEntityDialogControllerProvider>(), context.Get<IGreLocProvider>())
	{
	}

	private CardNamedEventTranslator(IEntityDialogControllerProvider dialogueProvider, IGreLocProvider locManager)
	{
		_dialogueProvider = dialogueProvider ?? NullEntityDialogControllerProvider.Default;
		_locManager = locManager ?? NullGreLocManager.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is CardNamedEvent cardNamedEvent)
		{
			events.Add(new CardNamedUXEvent(cardNamedEvent.PlayerId, cardNamedEvent.LocId, _dialogueProvider, _locManager));
		}
	}
}
