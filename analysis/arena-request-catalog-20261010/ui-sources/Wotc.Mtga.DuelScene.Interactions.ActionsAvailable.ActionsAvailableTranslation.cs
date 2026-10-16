using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ActionsAvailableTranslation : IWorkflowTranslation<ActionsAvailableRequest>
{
	private readonly IContext _context;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewManager _cardViewManager;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _browserHeaderTextProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly DuelSceneLogger _logger;

	private readonly IWorkflowTranslation<ActionsAvailableRequest> _resolutionCastTranslation;

	private readonly IWorkflowTranslation<ActionsAvailableRequest> _defaultTranslation;

	private readonly ClientSideInteraction _clientSideInteraction;

	private const uint CannotTakeActionWorkflow_PromptId = 1134u;

	public ActionsAvailableTranslation(IContext context, AssetLookupSystem assetLookupSystem, ClientSideInteraction clientSideInteraction, DuelSceneLogger duelSceneLogger, IWorkflowTranslation<ActionsAvailableRequest> defaultTranslation)
	{
		_context = context;
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_cardViewManager = context.Get<ICardViewManager>() ?? NullCardViewManager.Default;
		_fakeCardController = context.Get<IFakeCardViewController>() ?? NullFakeCardViewController.Default;
		_browserManager = context.Get<IBrowserManager>() ?? NullBrowserManager.Default;
		_browserHeaderTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_logger = duelSceneLogger;
		_resolutionCastTranslation = new ResolutionCastTranslation(context, clientSideInteraction, duelSceneLogger, assetLookupSystem);
		_defaultTranslation = defaultTranslation ?? NullWorkflowTranslation<ActionsAvailableRequest>.Default;
		_clientSideInteraction = clientSideInteraction;
	}

	public WorkflowBase Translate(ActionsAvailableRequest request)
	{
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		if (TranslatesToCannotTakeActionWorkflow(request, gameState))
		{
			return new CannotTakeActionWorkflow(request, _cardDatabase, _gameStateProvider, _cardViewManager, _fakeCardController, _browserManager, _logger);
		}
		if (DoesHaveOpeningHandActions(request))
		{
			return new OpeningHandActionWorkflow(request, _cardDatabase.ClientLocProvider, _cardViewManager, _browserManager);
		}
		if (ResolutionCastTranslation.IsResolutionCast(gameState, request.Actions, request.InactiveActions))
		{
			return _resolutionCastTranslation.Translate(request);
		}
		List<uint> list = new List<uint>();
		HashSet<uint> hashSet = new HashSet<uint>();
		foreach (Action action in request.Actions)
		{
			uint instanceId = action.InstanceId;
			if (instanceId != 0)
			{
				list.Add(instanceId);
				if (action.SourceId != 0)
				{
					hashSet.Add(action.SourceId);
				}
			}
		}
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		bool addNHCastables = IncludeNHCInHand(hashSet, mtgGameState.Stack);
		IReadOnlyCollection<MtgZone> zonesFromCardIds = GetZonesFromCardIds(mtgGameState, list, addNHCastables);
		if (AreAllActionsInHandWithAResolvingSource(mtgGameState, hashSet, zonesFromCardIds) || ShouldOpenSingleZoneBrowser(mtgGameState, zonesFromCardIds, request.Actions, request.InactiveActions))
		{
			ActionSubmission actionSubmission = new ActionSubmission(request, _logger, _context);
			return new ActionsAvailableWorkflow_Browser(request, actionSubmission, new ActionProcessor(actionSubmission.SubmitAction, _clientSideInteraction), _cardDatabase.ClientLocProvider, _gameStateProvider, _cardViewManager, _browserManager, _browserHeaderTextProvider, _assetLookupSystem, _logger);
		}
		if (ShouldOpenMultiZoneBrowser(mtgGameState, zonesFromCardIds))
		{
			ActionSubmission actionSubmission2 = new ActionSubmission(request, _logger, _context);
			return new ActionsAvailableWorkflow_MultiZoneBrowser(request, actionSubmission2, new ActionProcessor(actionSubmission2.SubmitAction, _clientSideInteraction), _cardDatabase, _cardDatabase.ClientLocProvider, _gameStateProvider, _cardViewManager, _browserManager, _browserHeaderTextProvider, _assetLookupSystem, _logger);
		}
		return _defaultTranslation.Translate(request);
	}

	private static IReadOnlyCollection<MtgZone> GetZonesFromCardIds(MtgGameState gameState, IEnumerable<uint> cardIds, bool addNHCastables)
	{
		HashSet<MtgZone> hashSet = new HashSet<MtgZone>();
		foreach (uint id in cardIds)
		{
			if (!gameState.TryGetCard(id, out var card))
			{
				continue;
			}
			ZoneType type = card.Zone.Type;
			MtgZone item = card.Zone;
			if (type == ZoneType.Limbo)
			{
				continue;
			}
			if (addNHCastables && (type == ZoneType.Exile || type == ZoneType.Graveyard || type == ZoneType.Sideboard))
			{
				uint localPlayerId = gameState.LocalPlayer.InstanceId;
				List<ActionInfo> list = gameState.Actions.FindAll((ActionInfo ai) => (ai.Action.InstanceId == id || ai.Action.SourceId == id) && ai.SeatId == localPlayerId);
				if (list.Count > 0 && list.Exists((ActionInfo ai) => ai.SeatId == localPlayerId))
				{
					item = gameState.GetZoneForPlayer(localPlayerId, ZoneType.Hand);
				}
			}
			hashSet.Add(item);
		}
		return hashSet;
	}

	public static bool AreAllActionsInHandWithAResolvingSource(MtgGameState currentState, HashSet<uint> actionSources, IReadOnlyCollection<MtgZone> involvedZones)
	{
		if (involvedZones.Count != 1)
		{
			return false;
		}
		MtgZone mtgZone = involvedZones.First();
		if (mtgZone.Type != ZoneType.Hand)
		{
			return false;
		}
		if (mtgZone.OwnerNum != GREPlayerNum.LocalPlayer)
		{
			return false;
		}
		if (actionSources.Count != 1)
		{
			return false;
		}
		if (currentState.ResolvingCardInstance == null)
		{
			return false;
		}
		return actionSources.Contains(currentState.ResolvingCardInstance.InstanceId);
	}

	public static bool ShouldOpenSingleZoneBrowser(MtgGameState gameState, IReadOnlyCollection<MtgZone> involvedZones, IEnumerable<Action> actions, IEnumerable<Action> inactiveActions)
	{
		if (involvedZones.Count != 1)
		{
			return false;
		}
		MtgZone mtgZone = involvedZones.First();
		return ShouldOpenSingleZoneBrowser(gameState, mtgZone.Type, actions, inactiveActions);
	}

	public static bool ShouldOpenMultiZoneBrowser(MtgGameState gameState, IReadOnlyCollection<MtgZone> involvedZones)
	{
		if (involvedZones.Count <= 1)
		{
			return false;
		}
		HashSet<uint> hashSet = new HashSet<uint>();
		foreach (MtgZone involvedZone in involvedZones)
		{
			if (involvedZone.OwnerNum != GREPlayerNum.Invalid)
			{
				hashSet.Add(involvedZone.OwnerId);
			}
		}
		if (hashSet.Count <= 1)
		{
			return false;
		}
		foreach (MtgZone involvedZone2 in involvedZones)
		{
			ZoneType type = involvedZone2.Type;
			if ((uint)(type - 1) > 1u && type != ZoneType.Graveyard)
			{
				return false;
			}
		}
		return true;
	}

	private static bool ShouldOpenSingleZoneBrowser(MtgGameState gameState, ZoneType zoneType, IEnumerable<Action> actions, IEnumerable<Action> inactiveActions)
	{
		return zoneType switch
		{
			ZoneType.Sideboard => true, 
			ZoneType.Graveyard => true, 
			ZoneType.Exile => true, 
			ZoneType.Library => AreMultipleInstancesInLibraryWithActions(gameState, actions, inactiveActions) || gameState.ResolvingCardInstance != null, 
			_ => false, 
		};
	}

	private static bool AreMultipleInstancesInLibraryWithActions(MtgGameState gs, IEnumerable<Action> actions, IEnumerable<Action> inactiveActions)
	{
		uint num = 0u;
		foreach (Action action in actions)
		{
			if ((action.IsPlayAction() || action.IsCastAction()) && gs.TryGetCard(action.InstanceId, out var card) && card.Zone.Type == ZoneType.Library)
			{
				if (num == 0)
				{
					num = action.InstanceId;
				}
				else if (action.InstanceId != num)
				{
					return true;
				}
			}
		}
		foreach (Action inactiveAction in inactiveActions)
		{
			if ((inactiveAction.IsPlayAction() || inactiveAction.IsCastAction()) && gs.TryGetCard(inactiveAction.InstanceId, out var card2) && card2.Zone.Type == ZoneType.Library)
			{
				if (num == 0)
				{
					num = inactiveAction.InstanceId;
				}
				else if (inactiveAction.InstanceId != num)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool DoesHaveOpeningHandActions(ActionsAvailableRequest availableActionsDecision)
	{
		List<Action> actions = availableActionsDecision.Actions;
		for (int i = 0; i < actions.Count; i++)
		{
			if (actions[i].ActionType == ActionType.OpeningHandAction)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IncludeNHCInHand(HashSet<uint> sources, MtgZone stack)
	{
		if (sources.Count == 1)
		{
			uint item = 0u;
			foreach (uint source in sources)
			{
				item = source;
			}
			if (stack.CardIds.Contains(item))
			{
				return false;
			}
		}
		return true;
	}

	private static bool TranslatesToCannotTakeActionWorkflow(ActionsAvailableRequest request, MtgGameState gameState)
	{
		if (request.InactiveActions.Count == 0)
		{
			return false;
		}
		if (request.CancellationType != AllowCancel.No)
		{
			return false;
		}
		if (request.Actions.Count != 1)
		{
			return false;
		}
		if (request.Actions[0].ActionType != ActionType.Pass)
		{
			return false;
		}
		if (gameState.ResolvingCardInstance == null)
		{
			Prompt prompt = request.Prompt;
			if (prompt != null)
			{
				return prompt.PromptId == 1134;
			}
			return false;
		}
		return true;
	}
}
