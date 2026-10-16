using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceWithContextTranslation : IWorkflowTranslation<SelectNRequest>
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IPromptEngine _promptEngine;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly IChoiceOptionsTranslation _choiceOptionsTranslation;

	public ChoiceWithContextTranslation(IContext context, IChoiceOptionsTranslation choiceOptionsTranslation)
		: this(context.Get<IGameStateProvider>(), context.Get<IPromptTextProvider>(), context.Get<IBrowserController>(), choiceOptionsTranslation)
	{
	}

	private ChoiceWithContextTranslation(IGameStateProvider gameStateProvider, IPromptTextProvider promptTextProvider, IBrowserController browserController, IChoiceOptionsTranslation choiceOptionsTranslation)
	{
		_gameStateProvider = gameStateProvider;
		_promptTextProvider = promptTextProvider;
		_browserController = browserController;
		_choiceOptionsTranslation = choiceOptionsTranslation;
	}

	public WorkflowBase Translate(SelectNRequest selectNRequest)
	{
		return new ChoiceWithContextWorkflow(selectNRequest, _gameStateProvider, _promptTextProvider, _browserController, _choiceOptionsTranslation);
	}
}
