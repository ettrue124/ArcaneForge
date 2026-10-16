using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ResolutionCastTranslation : IWorkflowTranslation<ActionsAvailableRequest>
{
	private readonly IContext _context;

	private readonly DuelSceneLogger _logger;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly ClientSideInteraction _clientSideInteraction;

	public ResolutionCastTranslation(IContext context, ClientSideInteraction clientSideInteraction, DuelSceneLogger logger, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_cardViewProvider = context.Get<ICardViewProvider>() ?? NullCardViewProvider.Default;
		_browserManager = context.Get<IBrowserManager>() ?? NullBrowserManager.Default;
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
		_clientSideInteraction = clientSideInteraction;
		_assetLookupSystem = assetLookupSystem;
		_context = context;
		_logger = logger;
	}

	public WorkflowBase Translate(ActionsAvailableRequest req)
	{
		IActionSubmission actionSubmission = new ActionSubmission(req, _logger, _context);
		return new ResolutionCastWorkflow(req, _cardDatabase, _gameStateProvider, _cardViewProvider, _browserManager, actionSubmission, new ActionProcessor(actionSubmission.SubmitAction, _clientSideInteraction), _headerTextProvider, _assetLookupSystem);
	}

	public static bool IsResolutionCast(MtgGameState gameState, IEnumerable<Action> activeActions, IEnumerable<Action> inactiveActions)
	{
		if (gameState == null || activeActions == null || inactiveActions == null)
		{
			return false;
		}
		return IsResolutionCast((gameState.ResolvingCardInstance != null) ? gameState.ResolvingCardInstance.InstanceId : 0u, activeActions, inactiveActions);
	}

	public static bool IsResolutionCast(uint resolvingCardId, IEnumerable<Action> activeActions, IEnumerable<Action> inactiveActions)
	{
		if (AllActionsAreResolutionCasts(resolvingCardId, activeActions))
		{
			return AllActionsAreResolutionCasts(resolvingCardId, inactiveActions);
		}
		return false;
	}

	public static bool AllActionsAreResolutionCasts(uint resolvingCardId, IEnumerable<Action> actions)
	{
		if (resolvingCardId == 0)
		{
			return false;
		}
		foreach (Action action in actions)
		{
			if (action.ActionType != ActionType.Pass && !IsResolutionCastAction(action, resolvingCardId))
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsResolutionCastAction(Action action, uint resolvingCardId)
	{
		if (action.IsCastAction())
		{
			return action.SourceId == resolvingCardId;
		}
		return false;
	}
}
