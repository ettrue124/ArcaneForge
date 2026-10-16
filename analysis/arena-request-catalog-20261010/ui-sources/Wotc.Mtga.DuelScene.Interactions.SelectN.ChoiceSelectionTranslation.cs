using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceSelectionTranslation : IWorkflowTranslation<SelectNRequest>
{
	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardHolderManager _cardHolderManager;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IObjectPool _objectPool;

	private readonly IChoiceOptionsTranslation _choiceOptionsTranslation;

	public ChoiceSelectionTranslation(IContext context, IChoiceOptionsTranslation choiceOptionsTranslation, AssetLookupSystem assetLookupSystem)
	{
		_assetLookupSystem = assetLookupSystem;
		_cardDatabase = context.Get<ICardDatabaseAdapter>();
		_cardHolderManager = context.Get<ICardHolderManager>();
		_gameplaySettings = context.Get<IGameplaySettingsProvider>();
		_gameStateProvider = context.Get<IGameStateProvider>();
		_browserManager = context.Get<IBrowserManager>();
		_promptTextProvider = context.Get<IPromptTextProvider>();
		_cardBuilder = context.Get<ICardBuilder<DuelScene_CDC>>();
		_resolutionEffectProvider = context.Get<IResolutionEffectProvider>();
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>();
		_objectPool = context.Get<IObjectPool>();
		_choiceOptionsTranslation = choiceOptionsTranslation;
	}

	public WorkflowBase Translate(SelectNRequest req)
	{
		ChoiceOptions choiceOptions = _choiceOptionsTranslation.Translate(req);
		if (UsePromptButtonWorkflow(choiceOptions, req))
		{
			return new ChoicePromptButtonsWorkflow(req, choiceOptions, _cardDatabase, _resolutionEffectProvider, _assetLookupSystem);
		}
		if (choiceOptions.ChoiceToIdMap.Count <= 6 && req.MinSel == 1 && req.MaxSel == 1)
		{
			return new ChoiceButtonsWorkflow(req, choiceOptions, _assetLookupSystem, _cardDatabase, _cardHolderManager, _gameStateProvider, _gameplaySettings, _browserManager);
		}
		if (choiceOptions.SortedChoices.Count > 3)
		{
			return new ChoiceSelectionWorkflow(req, _objectPool, _browserManager, choiceOptions, _promptTextProvider);
		}
		return new ChoiceModalWorkflow(req, choiceOptions, _cardDatabase, _gameStateProvider, _cardBuilder, _browserManager, _headerTextProvider);
	}

	public static bool UsePromptButtonWorkflow(ChoiceOptions choiceOptions, SelectNRequest request)
	{
		if (request.MinSel == 1 && request.MaxSel == 1)
		{
			if (choiceOptions.ChoiceToIdMap.Count != 2 || request.CanCancel)
			{
				if (choiceOptions.ChoiceToIdMap.Count == 1)
				{
					return request.CancellationType == AllowCancel.Continue;
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
