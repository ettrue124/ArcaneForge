using System;
using System.Collections.Generic;
using System.Text;
using GreClient.Rules;
using InteractionSystem;
using UnityEngine.EventSystems;
using WorkflowVisuals;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class PayCostWorkflow : WorkflowBase<PayCostsRequest>, IParentWorkflow, IAutoRespondWorkflow
{
	private class PayCostHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly Highlights _filteredHighlights = new Highlights();

		private readonly IReadOnlyCollection<WorkflowBase> _childWorkflows;

		private readonly IReadOnlyCollection<ManaRequirement> _manaRequirements;

		private readonly IGameStateProvider _gameStateProvider;

		private readonly Func<DuelScene_CDC> _getHoveredCard;

		public PayCostHighlightsGenerator(IReadOnlyCollection<WorkflowBase> childWorkflows, IReadOnlyCollection<ManaRequirement> manaRequirements, IGameStateProvider gameStateProvider, Func<DuelScene_CDC> getHoveredCard)
		{
			_childWorkflows = childWorkflows;
			_manaRequirements = manaRequirements;
			_gameStateProvider = gameStateProvider;
			_getHoveredCard = getHoveredCard;
		}

		public Highlights GetHighlights()
		{
			MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
			_highlights.Clear();
			foreach (WorkflowBase childWorkflow in _childWorkflows)
			{
				_highlights.Merge(childWorkflow.Highlights);
			}
			if (ContainsAutoPay(_highlights))
			{
				_filteredHighlights.Clear();
				CopyAutoPayHighlights(_highlights.IdToHighlightType_Workflow, _filteredHighlights.IdToHighlightType_Workflow);
				CopyAutoPayHighlights(_highlights.ManaHighlights, _filteredHighlights.ManaHighlights);
				CopyAutoPayHighlights(_highlights.IdToHighlightType_User, _filteredHighlights.IdToHighlightType_User);
				CopyAutoPayHighlights(_highlights.EntityHighlights, _filteredHighlights.EntityHighlights);
				_highlights.Clear();
				_highlights.Merge(_filteredHighlights);
			}
			MtgCardInstance mtgCardInstance = mtgGameState?.GetTopCardOnStack();
			if (mtgCardInstance != null)
			{
				uint instanceId = mtgCardInstance.InstanceId;
				uint num = _getHoveredCard()?.InstanceId ?? 0;
				foreach (ManaRequirement manaRequirement in _manaRequirements)
				{
					uint objectId = manaRequirement.ObjectId;
					if (objectId != 0 && objectId != instanceId && (num == 0 || num == instanceId))
					{
						_highlights.IdToHighlightType_Workflow[objectId] = HighlightType.Selected;
					}
				}
			}
			return _highlights;
		}

		private static bool ContainsAutoPay(Highlights highlights)
		{
			foreach (KeyValuePair<uint, ManaHighlight> manaHighlight in highlights.ManaHighlights)
			{
				if (manaHighlight.Value.HighlightType == HighlightType.AutoPay)
				{
					return true;
				}
			}
			if (!highlights.IdToHighlightType_Workflow.ContainsValue(HighlightType.AutoPay) && !highlights.IdToHighlightType_User.ContainsValue(HighlightType.AutoPay))
			{
				return highlights.EntityHighlights.ContainsValue(HighlightType.AutoPay);
			}
			return true;
		}

		private static void CopyAutoPayHighlights<T>(Dictionary<T, HighlightType> copyFrom, Dictionary<T, HighlightType> copyTo)
		{
			foreach (KeyValuePair<T, HighlightType> item in copyFrom)
			{
				if (item.Value == HighlightType.AutoPay)
				{
					copyTo[item.Key] = HighlightType.AutoPay;
				}
			}
		}

		private static void CopyAutoPayHighlights(Dictionary<uint, ManaHighlight> copyFrom, Dictionary<uint, ManaHighlight> copyTo)
		{
			foreach (KeyValuePair<uint, ManaHighlight> item in copyFrom)
			{
				if (item.Value.HighlightType == HighlightType.AutoPay)
				{
					copyTo[item.Key] = item.Value;
				}
			}
		}
	}

	private readonly List<WorkflowBase> _childWorkflows = new List<WorkflowBase>();

	private readonly ICardViewProvider _cardViewProvider;

	private readonly GameInteractionSystem _gameInteractionSystem;

	private readonly UIMessageHandler _uiMessageHandler;

	private readonly UIManager _uiManager;

	private readonly DuelSceneLogger _logger;

	private DuelScene_CDC _hoveredCard;

	public IEnumerable<WorkflowBase> ChildWorkflows => _childWorkflows;

	public PayCostWorkflow(PayCostsRequest request, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, GameInteractionSystem gameInteractionSystem, UIMessageHandler uiMessageHandler, UIManager uiManager, IWorkflowTranslator workflowTranslation, DuelSceneLogger logger)
		: base(request)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_gameInteractionSystem = gameInteractionSystem;
		_uiMessageHandler = uiMessageHandler;
		_uiManager = uiManager;
		_logger = logger;
		foreach (BaseUserRequest childRequest in _request.ChildRequests)
		{
			if (childRequest != null)
			{
				WorkflowBase workflowBase = workflowTranslation.Translate(childRequest);
				if (workflowBase != null)
				{
					workflowBase.UpdateHighlights += OnUpdateChildHighlights;
					workflowBase.UpdateDimming += OnUpdateChildDimming;
					workflowBase.UpdateArrows += OnUpdateChildArrows;
					workflowBase.UpdateButtons += OnUpdateChildButtons;
					workflowBase.UpdatePrompt += OnUpdateChildPrompt;
					_childWorkflows.Add(workflowBase);
				}
			}
		}
		AttackerCost attackerCost = _uiManager.AttackerCost;
		attackerCost.HoverStateChanged = (Action<AttackerCost, AttackerCost.HoverState>)Delegate.Combine(attackerCost.HoverStateChanged, new Action<AttackerCost, AttackerCost.HoverState>(OnHoverStateChanged));
		CardHoverController.OnHoveredCardUpdated += OnHoveredCardUpdated;
		_highlightsGenerator = new PayCostHighlightsGenerator(_childWorkflows, _request.ManaRequirements, gameStateProvider, () => _hoveredCard);
	}

	private void OnHoveredCardUpdated(DuelScene_CDC cardView)
	{
		_hoveredCard = cardView;
		SetHighlights();
	}

	private void OnUpdateChildHighlights(Highlights highlights)
	{
		SetHighlights();
	}

	private void OnUpdateChildArrows(Arrows arrows)
	{
		SetArrows();
	}

	private void OnUpdateChildDimming(Dimming dimming)
	{
		SetDimming();
	}

	private void OnUpdateChildButtons(Buttons buttons)
	{
		SetButtons();
	}

	private void OnUpdateChildPrompt(WorkflowPrompt prompt)
	{
		_workflowPrompt.LocKey = prompt.LocKey;
		_workflowPrompt.LocParams = prompt.LocParams;
		_workflowPrompt.GrePrompt = prompt.GrePrompt;
		OnUpdatePrompt(_workflowPrompt);
	}

	private void OnHoverStateChanged(AttackerCost attackerCost, AttackerCost.HoverState state)
	{
		uint? firstCostSource = attackerCost.GetFirstCostSource();
		if (firstCostSource.HasValue && _cardViewProvider.TryGetCardView(firstCostSource.Value, out var cardView))
		{
			if (state == AttackerCost.HoverState.Start)
			{
				_gameInteractionSystem.HandleHover(cardView, new PointerEventData(null));
				_uiMessageHandler.TrySendHoverMessage(cardView.Model.InstanceId);
			}
			else
			{
				_gameInteractionSystem.HandleHoverEnd(cardView);
				_uiMessageHandler.TrySendHoverMessage(0u);
			}
		}
	}

	public override bool CanApply(List<UXEvent> events)
	{
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			if (!childWorkflow.CanApply(events))
			{
				return false;
			}
		}
		return true;
	}

	protected override void ApplyInteractionInternal()
	{
		for (int i = 0; i < _childWorkflows.Count; i++)
		{
			_childWorkflows[i].ApplyInteraction();
		}
		SetButtons();
		List<uint> list = new List<uint>();
		foreach (ManaRequirement manaRequirement in _request.ManaRequirements)
		{
			list.AddRange(_uiManager.AttackerCost.GetAbilityCostSources(manaRequirement.AbilityGrpId));
		}
		if (list.Count > 0)
		{
			_uiManager.AttackerCost.Enable();
		}
		_hoveredCard = CardHoverController.HoveredCard;
	}

	public override void CleanUp()
	{
		while (_childWorkflows.Count > 0)
		{
			WorkflowBase workflowBase = _childWorkflows[0];
			workflowBase.CleanUp();
			workflowBase.UpdateHighlights -= OnUpdateChildHighlights;
			workflowBase.UpdateDimming -= OnUpdateChildDimming;
			workflowBase.UpdateArrows -= OnUpdateChildArrows;
			workflowBase.UpdateButtons -= OnUpdateChildButtons;
			workflowBase.UpdatePrompt -= OnUpdateChildPrompt;
			_childWorkflows.RemoveAt(0);
		}
		AttackerCost attackerCost = _uiManager.AttackerCost;
		attackerCost.HoverStateChanged = (Action<AttackerCost, AttackerCost.HoverState>)Delegate.Remove(attackerCost.HoverStateChanged, new Action<AttackerCost, AttackerCost.HoverState>(OnHoverStateChanged));
		_uiManager.AttackerCost.Disable();
		CardHoverController.OnHoveredCardUpdated -= OnHoveredCardUpdated;
		base.CleanUp();
	}

	protected override void SetDimming()
	{
		Dimming = new Dimming();
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			Dimming.Merge(Dimming, childWorkflow.Dimming);
		}
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows = new Arrows();
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			Arrows.Merge(Arrows, childWorkflow.Arrows);
		}
		OnUpdateArrows(Arrows);
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			Buttons.Merge(Buttons, childWorkflow.Buttons);
		}
		if (_request.CanCancel)
		{
			ButtonStyle.StyleType style = ((Buttons.WorkflowButtons.Count == 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				Style = style,
				ButtonCallback = () =>
				{
					if (EffectCostSelected())
					{
						_logger.LogNonManaPaymentInteraction("cancel");
					}
					_request.Cancel();
				},
				ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
			};
		}
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = () =>
				{
					if (EffectCostSelected())
					{
						_logger.LogNonManaPaymentInteraction("undo");
					}
					_request.Undo();
				}
			};
		}
		OnUpdateButtons(Buttons);
	}

	private bool EffectCostSelected()
	{
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			if (childWorkflow is EffectCostWorkflow && childWorkflow.Highlights.IdToHighlightType_Workflow.ContainsValue(HighlightType.Selected))
			{
				return true;
			}
		}
		return false;
	}

	public override void TryUndo()
	{
		if (EffectCostSelected())
		{
			_logger.LogNonManaPaymentInteraction("undo");
		}
		base.TryUndo();
	}

	protected override void SetPrompt()
	{
		if (string.IsNullOrEmpty(_workflowPrompt.LocKey))
		{
			base.SetPrompt();
		}
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("PayCostWorkflow");
		stringBuilder.AppendLine();
		stringBuilder.Append("-Children:");
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			stringBuilder.AppendLine();
			stringBuilder.Append("--");
			stringBuilder.Append(childWorkflow.ToString());
		}
		return stringBuilder.ToString();
	}

	public bool TryAutoRespond()
	{
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			if (childWorkflow is IAutoRespondWorkflow autoRespondWorkflow)
			{
				return autoRespondWorkflow.TryAutoRespond();
			}
		}
		return false;
	}
}
