using System;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class PayCostsTranslation : IWorkflowTranslation<PayCostsRequest>
{
	private readonly GameManager _gameManager;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IWorkflowTranslator _workflowTranslator;

	private readonly DuelSceneLogger _logger;

	private bool ItemsSelected;

	public PayCostsTranslation(IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IWorkflowTranslator workflowTranslator, DuelSceneLogger logger, GameManager gameManager)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_workflowTranslator = workflowTranslator;
		_gameManager = gameManager;
		_logger = logger;
	}

	public WorkflowBase Translate(PayCostsRequest req)
	{
		if (ListenForLogging(req))
		{
			req.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(req.OnSubmit, new Action<ClientToGREMessage>(OnRequestSubmit));
		}
		return new PayCostWorkflow(req, _gameStateProvider, _cardViewProvider, _gameManager.InteractionSystem, _gameManager.UIMessageHandler, _gameManager.UIManager, _workflowTranslator, _logger);
	}

	private void OnRequestSubmit(ClientToGREMessage outMsg)
	{
		if (outMsg.EffectCostResp != null && outMsg.EffectCostResp.CostSelection != null)
		{
			_logger.LogNonManaPaymentInteraction("submit");
		}
	}

	private bool ListenForLogging(PayCostsRequest request)
	{
		foreach (BaseUserRequest childRequest in request.ChildRequests)
		{
			if (IsNonManaPayment(childRequest))
			{
				return true;
			}
		}
		return false;
	}

	private bool IsNonManaPayment(BaseUserRequest request)
	{
		if (!(request is EffectCostRequest effectCostRequest))
		{
			return false;
		}
		if (effectCostRequest.EffectCostType != EffectCostType.Select)
		{
			return false;
		}
		SelectNRequest costSelection = effectCostRequest.CostSelection;
		if (costSelection == null)
		{
			return false;
		}
		if (costSelection.MinSel == 1 && costSelection.MaxSel == 1 && costSelection.Context == SelectionContext.NonManaPayment)
		{
			return costSelection.OptionContext == OptionContext.Payment;
		}
		return false;
	}
}
