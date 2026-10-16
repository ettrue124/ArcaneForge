using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizards.Mtga.Platforms;
using WorkflowVisuals;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

public class ActionsAvailableWorkflow : WorkflowBase<ActionsAvailableRequest>, IAutoRespondWorkflow, IClickableWorkflow, IDraggableWorkflow, IYieldWorkflow, IKeybindingWorkflow, ICardStackWorkflow
{
	private class BatchManaSubmission : WorkflowVariant, IKeybindingWorkflow, IClickableWorkflow, ICardStackWorkflow
	{
		private readonly ManaColorSelection _manaColorSelection;

		private readonly IBattlefieldCardHolder _battlefield;

		private readonly Dictionary<uint, List<Wotc.Mtgo.Gre.External.Messaging.Action>> _manaActions = new Dictionary<uint, List<Wotc.Mtgo.Gre.External.Messaging.Action>>();

		private readonly Dictionary<ManaPaymentOption, int> _mpoIdx = new Dictionary<ManaPaymentOption, int>();

		public BatchManaSubmission(ManaColorSelection manaColorSelection, IBattlefieldCardHolder battlefield, List<Wotc.Mtgo.Gre.External.Messaging.Action> manaActions)
		{
			_manaColorSelection = manaColorSelection;
			_battlefield = battlefield;
			foreach (Wotc.Mtgo.Gre.External.Messaging.Action manaAction in manaActions)
			{
				uint instanceId = manaAction.InstanceId;
				if (_manaActions.TryGetValue(instanceId, out var value))
				{
					value.Add(manaAction);
				}
				else
				{
					_manaActions[instanceId] = new List<Wotc.Mtgo.Gre.External.Messaging.Action> { manaAction };
				}
				IList<ManaPaymentOption> manaPaymentOptions = manaAction.ManaPaymentOptions;
				for (int i = 0; i < manaPaymentOptions.Count; i++)
				{
					_mpoIdx[manaPaymentOptions[i]] = i;
				}
			}
			Application.focusChanged += OnFocusChanged;
		}

		private void OnFocusChanged(bool focused)
		{
			Cancelled?.Invoke();
		}

		public override void Open()
		{
			UpdateHighlights();
			UpdateButtons();
		}

		public override void Close()
		{
			_manaColorSelection.Close();
			_manaActions.Clear();
			SelectedActions.Clear();
			Submitted = null;
			Cancelled = null;
			Application.focusChanged -= OnFocusChanged;
		}

		protected override void UpdateHighlights()
		{
			_highlights.Clear();
			foreach (uint key in _manaActions.Keys)
			{
				_highlights.IdToHighlightType_Workflow[key] = HighlightType.Hot;
			}
			foreach (Wotc.Mtgo.Gre.External.Messaging.Action selectedAction in SelectedActions)
			{
				_highlights.IdToHighlightType_Workflow[selectedAction.InstanceId] = HighlightType.Selected;
			}
			base.UpdateHighlights();
		}

		public bool CanKeyDown(Key key)
		{
			return true;
		}

		public void OnKeyDown(Key key)
		{
			if (key == Key.Escape)
			{
				Cancelled?.Invoke();
			}
		}

		public bool CanKeyHeld(Key key, float holdDuration)
		{
			return true;
		}

		public void OnKeyHeld(Key key, float holdDuration)
		{
		}

		public bool CanKeyUp(Key key)
		{
			return true;
		}

		public void OnKeyUp(Key key)
		{
			if (key == Key.Q)
			{
				Submitted?.Invoke();
			}
		}

		public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
		{
			if (entity is DuelScene_CDC)
			{
				return _manaActions.ContainsKey(entity.InstanceId);
			}
			return false;
		}

		public void OnClick(IEntityView entity, SimpleInteractionType clickType)
		{
			uint instanceId = entity.InstanceId;
			IBattlefieldStack stackForInstanceId = _battlefield.GetStackForInstanceId(instanceId);
			if (stackForInstanceId == null)
			{
				return;
			}
			ICardDataAdapter stackParentModel = stackForInstanceId.StackParentModel;
			if (stackParentModel == null)
			{
				return;
			}
			uint parentId = stackParentModel.InstanceId;
			List<Wotc.Mtgo.Gre.External.Messaging.Action> value;
			if (SelectedActions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.InstanceId == parentId))
			{
				foreach (DuelScene_CDC cdc in stackForInstanceId.AllCards)
				{
					if ((bool)cdc)
					{
						SelectedActions.RemoveAll((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.InstanceId == cdc.InstanceId);
					}
				}
				UpdateHighlights();
				UpdateButtons();
			}
			else if (_manaActions.TryGetValue(parentId, out value))
			{
				if (_manaColorSelection.UseColorPicker(value.ToArray()))
				{
					ManaColorSelection manaColorSelection = _manaColorSelection;
					manaColorSelection.Submitted = (System.Action)Delegate.Combine(manaColorSelection.Submitted, new System.Action(OnManaSelectionSubmitted));
					_manaColorSelection.ShowColorSelection(entity as DuelScene_CDC, value.ToArray());
				}
				else
				{
					BatchActionsForCardStack(stackForInstanceId, value[0]);
				}
			}
		}

		public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
		{
			return false;
		}

		public void OnClickStack(CdcStackCounterView entity)
		{
		}

		public void OnBattlefieldClick()
		{
		}

		private void OnManaSelectionSubmitted()
		{
			ManaColorSelection manaColorSelection = _manaColorSelection;
			manaColorSelection.Submitted = (System.Action)Delegate.Remove(manaColorSelection.Submitted, new System.Action(OnManaSelectionSubmitted));
			List<Wotc.Mtgo.Gre.External.Messaging.Action> selectedActions = _manaColorSelection.SelectedActions;
			if (selectedActions.Count > 0)
			{
				Wotc.Mtgo.Gre.External.Messaging.Action action = selectedActions[0];
				IBattlefieldStack stackForInstanceId = _battlefield.GetStackForInstanceId(action.InstanceId);
				BatchActionsForCardStack(stackForInstanceId, action);
			}
		}

		private void BatchActionsForCardStack(IBattlefieldStack stack, Wotc.Mtgo.Gre.External.Messaging.Action srcAction)
		{
			if (stack == null || srcAction == null)
			{
				return;
			}
			IList<ManaPaymentOption> manaPaymentOptions = srcAction.ManaPaymentOptions;
			if (manaPaymentOptions == null || manaPaymentOptions.Count == 0)
			{
				return;
			}
			if (_mpoIdx.TryGetValue(manaPaymentOptions[0], out var value))
			{
				foreach (DuelScene_CDC allCard in stack.AllCards)
				{
					if (_manaActions.TryGetValue(allCard.InstanceId, out var value2))
					{
						Wotc.Mtgo.Gre.External.Messaging.Action action = value2.Find((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == srcAction.AbilityGrpId);
						if (action != null)
						{
							SelectedActions.Add(BatchedAction(action, value));
						}
					}
				}
			}
			_battlefield.LayoutNow();
			UpdateHighlights();
			UpdateButtons();
		}

		private Wotc.Mtgo.Gre.External.Messaging.Action BatchedAction(Wotc.Mtgo.Gre.External.Messaging.Action original, int mpoIdx)
		{
			Wotc.Mtgo.Gre.External.Messaging.Action action = new Wotc.Mtgo.Gre.External.Messaging.Action(original);
			IList<ManaPaymentOption> manaPaymentOptions = action.ManaPaymentOptions;
			for (int num = manaPaymentOptions.Count - 1; num >= 0; num--)
			{
				if (num != mpoIdx)
				{
					manaPaymentOptions.RemoveAt(num);
				}
			}
			return action;
		}

		public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
		{
			bool flag = SelectedActions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.InstanceId == lhs.InstanceId);
			bool flag2 = SelectedActions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.InstanceId == rhs.InstanceId);
			return flag == flag2;
		}
	}

	private class ManaColorSelection : WorkflowVariant
	{
		private readonly IAbilityDataProvider _abilityDatabase;

		private readonly ICardHolderProvider _cardHolderProvider;

		private readonly ManaColorSelector _manaColorSelector;

		public ManaColorSelection(IAbilityDataProvider abilityDatabase, ICardHolderProvider cardHolderProvider, ManaColorSelector manaColorSelector)
		{
			_abilityDatabase = abilityDatabase ?? NullAbilityDataProvider.Default;
			_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
			_manaColorSelector = manaColorSelector;
			manaColorSelector.TryCloseSelector();
		}

		public bool UseColorPicker(params Wotc.Mtgo.Gre.External.Messaging.Action[] actions)
		{
			return ActionsAvailableWorkflowUtils_ColorPicker.CanUseColorPicker(_abilityDatabase, actions);
		}

		public void ShowColorSelection(DuelScene_CDC cdc, params Wotc.Mtgo.Gre.External.Messaging.Action[] actions)
		{
			SelectedActions.Clear();
			ActionsAvailableWorkflowUtils_ColorPicker.ShowManaColorSelection(cdc, actions, OnColorPickerSubmit, _manaColorSelector, _abilityDatabase, _cardHolderProvider);
		}

		private void OnColorPickerSubmit(GreInteraction submittedAction)
		{
			Wotc.Mtgo.Gre.External.Messaging.Action greAction = submittedAction.GreAction;
			SelectedActions.Add(greAction);
			Submitted?.Invoke();
			if (_manaColorSelector.IsOpen)
			{
				_manaColorSelector.CloseSelector();
			}
		}

		public override void Open()
		{
		}

		public override void Close()
		{
			if (_manaColorSelector.IsOpen)
			{
				_manaColorSelector.CloseSelector();
			}
			Submitted = null;
			Cancelled = null;
		}
	}

	private class ActionSourceSelection : WorkflowVariant, IClickableWorkflow, ICardStackWorkflow
	{
		private readonly List<GreInteraction> _actions;

		private readonly DuelScene_CDC _cardToCast;

		private readonly IGameStateProvider _gameStateProvider;

		private readonly IEntityViewProvider _entityViewProvider;

		private readonly ICardHolderProvider _cardHolderProvider;

		private readonly ICardMovementController _cardMovementController;

		private readonly IModalBrowserCardHeaderProvider _headerProvider;

		private readonly Dictionary<IEntityView, GreInteraction> _entitiesToInteractions = new Dictionary<IEntityView, GreInteraction>();

		private IEntityView _selected;

		public ActionSourceSelection(List<GreInteraction> actions, uint cardToCastId, IContext context)
			: this(actions, cardToCastId, context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<ICardMovementController>(), context.Get<IModalBrowserCardHeaderProvider>())
		{
		}

		private ActionSourceSelection(List<GreInteraction> actions, uint cardToCast, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, ICardHolderProvider cardHolderProvider, ICardMovementController cardMovementController, IModalBrowserCardHeaderProvider headerProvider)
		{
			_actions = actions;
			_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
			_entityViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
			_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
			_cardMovementController = cardMovementController ?? NullCardMovementController.Default;
			_headerProvider = headerProvider ?? NullBrowserCardHeaderProvider.Default;
			_cardToCast = _entityViewProvider.GetCardView(cardToCast);
		}

		protected override void UpdateHighlights()
		{
			_highlights.Clear();
			if (_selected != null)
			{
				_highlights.EntityHighlights[_selected] = HighlightType.Selected;
			}
			else
			{
				foreach (KeyValuePair<IEntityView, GreInteraction> entitiesToInteraction in _entitiesToInteractions)
				{
					_highlights.EntityHighlights.Add(entitiesToInteraction.Key, HighlightType.Hot);
				}
			}
			base.UpdateHighlights();
		}

		protected override void UpdateButtons()
		{
			_buttons.Cleanup();
			_buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit"
				},
				Style = ButtonStyle.StyleType.Main,
				ButtonCallback = () =>
				{
					SelectedActions.Add(_entitiesToInteractions[_selected].GreAction);
					Submitted?.Invoke();
				},
				Enabled = (_selected != null)
			});
			_buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel"
				},
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = Cancel
			});
			base.UpdateButtons();
		}

		private void Cancel()
		{
			if ((bool)_cardToCast)
			{
				uint id = _cardToCast.Model.Zone.Id;
				if (_cardHolderProvider.TryGetCardHolderByZoneId(id, out var cardHolder) && cardHolder != _cardToCast.CurrentCardHolder)
				{
					_cardMovementController.MoveCard(_cardToCast, cardHolder);
				}
			}
			Cancelled?.Invoke();
		}

		protected override void UpdatePrompt()
		{
			_prompt.Reset();
			BrowserCardHeader.BrowserCardHeaderData browserCardInfo = _headerProvider.GetBrowserCardInfo(_cardToCast.Model, _actions[0].GreAction);
			_prompt.LocKey = "DuelScene/ClientPrompt/Cast_With_Selection";
			_prompt.LocParams = new (string, string)[1] { ("CastWith", browserCardInfo?.HeaderText + " " + browserCardInfo?.SubheaderText) };
			base.UpdatePrompt();
		}

		public override void Open()
		{
			foreach (GreInteraction action in _actions)
			{
				MtgEntity mtgEntity = action.GreAction.SourceInstance(_gameStateProvider.LatestGameState);
				if (mtgEntity != null && _entityViewProvider.TryGetEntity(mtgEntity.InstanceId, out var entityView))
				{
					_entitiesToInteractions[entityView] = action;
				}
			}
			UpdatePrompt();
			UpdateHighlights();
			UpdateButtons();
		}

		public override void Close()
		{
			_prompt.Reset();
			_highlights.Clear();
			_buttons.Cleanup();
			SelectedActions.Clear();
			Submitted = null;
			Cancelled = null;
		}

		public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
		{
			if (clickType != SimpleInteractionType.Primary)
			{
				return false;
			}
			if (_selected == null)
			{
				return _entitiesToInteractions.ContainsKey(entity);
			}
			return _selected == entity;
		}

		public void OnClick(IEntityView entity, SimpleInteractionType clickType)
		{
			_selected = ((entity == _selected) ? null : entity);
			UpdateHighlights();
			UpdateButtons();
		}

		public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
		{
			return false;
		}

		public void OnClickStack(CdcStackCounterView entity)
		{
		}

		public void OnBattlefieldClick()
		{
		}

		public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
		{
			return !_actions.Exists((GreInteraction x) => x.GreAction.AlternativeSourceZcid == lhs.InstanceId || x.GreAction.SourceId == lhs.InstanceId || x.GreAction.AlternativeSourceZcid == rhs.InstanceId || x.GreAction.SourceId == rhs.InstanceId);
		}
	}

	private class ActionsAvailableHighlightsGenerator : IHighlightsGenerator
	{
		private static readonly IListFilter<Wotc.Mtgo.Gre.External.Messaging.Action> _affordableActionFilter = new AffordableActionFilter();

		private static readonly IActionComparer _autoTapActionComparer = new AutoTapActionTypeComparer();

		private readonly ICardDatabaseAdapter _cardDatabase;

		private readonly IEntityViewManager _entityViewManager;

		private readonly IQualificationController _qualificationController;

		private readonly Func<WorkflowVariant> _getCurrentVariant;

		private readonly IGameStateProvider _gameStateProvider;

		private readonly IReadOnlyDictionary<uint, List<GreInteraction>> _actionsByInstanceId;

		private readonly IReadOnlyDictionary<uint, Dictionary<uint, List<DisqualificationType>>> _disqualifiersByInstanceId;

		private readonly Func<bool> _getIsAnyBrowserOpen;

		private readonly Func<DuelScene_CDC, bool> _getCardIsAboveDragThreshold;

		private readonly Func<bool> _getDragPointIsAboveCastThreshold;

		private readonly Func<uint, DuelScene_CDC> _getSideboardActionCardView;

		private readonly ClientSideInteraction _clientSideInteraction;

		private readonly Highlights _highlights = new Highlights();

		public ActionsAvailableHighlightsGenerator(ICardDatabaseAdapter cardDatabase, IEntityViewManager entityViewManager, IQualificationController qualificationController, Func<WorkflowVariant> currentVariant, IGameStateProvider gameStateProvider, IReadOnlyDictionary<uint, List<GreInteraction>> actionsByInstanceId, IReadOnlyDictionary<uint, Dictionary<uint, List<DisqualificationType>>> disqualifiersByInstanceId, ClientSideInteraction clientSideInteraction, Func<bool> isAnyBrowserOpen, Func<DuelScene_CDC, bool> cardIsAboveDragThreshold, Func<bool> dragPointIsAboveCastThreshold, Func<uint, DuelScene_CDC> sideboardActionCardView)
		{
			_cardDatabase = cardDatabase;
			_entityViewManager = entityViewManager;
			_qualificationController = qualificationController ?? NullQualificationController.Default;
			_getCurrentVariant = currentVariant;
			_gameStateProvider = gameStateProvider;
			_actionsByInstanceId = actionsByInstanceId;
			_disqualifiersByInstanceId = disqualifiersByInstanceId;
			_getIsAnyBrowserOpen = isAnyBrowserOpen;
			_getCardIsAboveDragThreshold = cardIsAboveDragThreshold;
			_getDragPointIsAboveCastThreshold = dragPointIsAboveCastThreshold;
			_getSideboardActionCardView = sideboardActionCardView;
			_clientSideInteraction = clientSideInteraction;
		}

		public Highlights GetHighlights()
		{
			WorkflowVariant workflowVariant = _getCurrentVariant?.Invoke();
			if (workflowVariant != null)
			{
				return workflowVariant.GetHighlights();
			}
			_highlights.Clear();
			MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
			RelatedHighlights();
			TopOfStackHighlights(mtgGameState);
			StandardHighlights(mtgGameState);
			ConditionalHighlights(mtgGameState);
			SideboardActionHighlights();
			return _highlights;
		}

		private void RelatedHighlights()
		{
			foreach (KeyValuePair<uint, HighlightType> relatedUserHighlight in CardHoverController.GetRelatedUserHighlights())
			{
				_highlights.IdToHighlightType_User.Add(relatedUserHighlight.Key, relatedUserHighlight.Value);
			}
		}

		private void TopOfStackHighlights(MtgGameState latestGameState)
		{
			if (latestGameState == null)
			{
				return;
			}
			MtgCardInstance topCardOnStack = latestGameState.GetTopCardOnStack();
			if (topCardOnStack == null)
			{
				return;
			}
			if (topCardOnStack != null && topCardOnStack.AdditionalCostIds.Count > 0)
			{
				foreach (uint additionalCostId in topCardOnStack.AdditionalCostIds)
				{
					_highlights.IdToHighlightType_Workflow[additionalCostId] = HighlightType.Selected;
				}
			}
			if (topCardOnStack == null || !IsDelveAction(latestGameState) || !topCardOnStack.Abilities.Exists((AbilityPrintingData x) => x.Id == 67))
			{
				return;
			}
			foreach (uint cardId in latestGameState.LocalGraveyard.CardIds)
			{
				_highlights.IdToHighlightType_Workflow[cardId] = HighlightType.Hot;
			}
		}

		private bool IsDelveAction(MtgGameState gameState)
		{
			List<GreInteraction> value;
			return _actionsByInstanceId.TryGetValue(gameState.LocalGraveyard.Id, out value);
		}

		private void StandardHighlights(MtgGameState gameState)
		{
			foreach (KeyValuePair<uint, List<GreInteraction>> item in _actionsByInstanceId)
			{
				uint key = item.Key;
				List<GreInteraction> value = item.Value;
				HighlightType hottestHighlightForActions = HighlightUtil.GetHottestHighlightForActions(gameState.GetEntityById(key), value);
				if (hottestHighlightForActions != HighlightType.None)
				{
					_highlights.IdToHighlightType_Workflow[key] = hottestHighlightForActions;
				}
			}
		}

		private void ConditionalHighlights(MtgGameState latestGameState)
		{
			if ((bool)CardDragController.DraggedCard && CardDragController.DraggedCard.Model?.Instance != null)
			{
				DraggedCardHighlights(CardDragController.DraggedCard.Model.Instance);
			}
			else if ((bool)CardHoverController.HoveredCard && CardHoverController.HoveredCard.Model?.Instance != null)
			{
				HoveredCardHighlights(CardHoverController.HoveredCard.Model.Instance, latestGameState);
			}
		}

		private void DraggedCardHighlights(MtgCardInstance cardInstance)
		{
			bool flag = _getDragPointIsAboveCastThreshold?.Invoke() ?? false;
			bool flag2 = cardInstance.PlayWarnings.Count > 0;
			uint instanceId = cardInstance.InstanceId;
			GreInteraction interactionWithLowestCost = ManaUtilities.GetInteractionWithLowestCost(_cardDatabase.AbilityDataProvider, GetActionsForCardId(instanceId), NullListFilter<Wotc.Mtgo.Gre.External.Messaging.Action>.Default, _affordableActionFilter, null, _autoTapActionComparer);
			if (interactionWithLowestCost != null)
			{
				_highlights.IdToHighlightType_User[instanceId] = (flag ? HighlightType.Selected : (flag2 ? HighlightType.Cold : HighlightType.Hot));
				foreach (uint autoTapInstanceId in interactionWithLowestCost.AutoTapInstanceIds)
				{
					_highlights.IdToHighlightType_User[autoTapInstanceId] = HighlightType.AutoPay;
				}
				{
					foreach (KeyValuePair<uint, uint> autoTapManaId in interactionWithLowestCost.AutoTapManaIds)
					{
						_highlights.ManaHighlights[autoTapManaId.Key] = new ManaHighlight(autoTapManaId.Value, HighlightType.AutoPay);
					}
					return;
				}
			}
			_highlights.IdToHighlightType_User[instanceId] = (flag ? HighlightType.Selected : HighlightType.None);
		}

		private void HoveredCardHighlights(MtgCardInstance cardInstance, MtgGameState gameState)
		{
			GreInteraction action = null;
			bool flag = cardInstance.PlayWarnings.Count > 0;
			uint instanceId = cardInstance.InstanceId;
			IReadOnlyList<GreInteraction> actionsForCardId = GetActionsForCardId(instanceId);
			if (instanceId == 0)
			{
				_clientSideInteraction.TryGetActionForCard(CardHoverController.HoveredCard, out action);
			}
			else
			{
				action = ManaUtilities.GetInteractionWithLowestCost(_cardDatabase.AbilityDataProvider, GetActionsForCardId(instanceId), NullListFilter<Wotc.Mtgo.Gre.External.Messaging.Action>.Default, _affordableActionFilter, null, _autoTapActionComparer);
			}
			LinkedFaceHighlighting.SetHighlights(in _highlights, actionsForCardId, cardInstance);
			if (action != null)
			{
				_highlights.IdToHighlightType_User[instanceId] = (flag ? HighlightType.Cold : HighlightType.Hot);
				foreach (uint autoTapInstanceId in action.AutoTapInstanceIds)
				{
					_highlights.IdToHighlightType_User[autoTapInstanceId] = HighlightType.AutoPay;
				}
				foreach (KeyValuePair<uint, uint> autoTapManaId in action.AutoTapManaIds)
				{
					_highlights.ManaHighlights[autoTapManaId.Key] = new ManaHighlight(autoTapManaId.Value, HighlightType.AutoPay);
				}
				if (_getIsAnyBrowserOpen())
				{
					_highlights.IdToHighlightType_User[CardHoverController.HoveredCard.InstanceId] = _highlights.IdToHighlightType_User[instanceId];
					foreach (uint autoTapInstanceId2 in action.AutoTapInstanceIds)
					{
						_highlights.IdToHighlightType_User[autoTapInstanceId2] = HighlightType.AutoPay;
					}
				}
			}
			if (!_disqualifiersByInstanceId.TryGetValue(instanceId, out var value) || value == null)
			{
				return;
			}
			foreach (QualificationData qualification in gameState.Qualifications)
			{
				foreach (uint key in value.Keys)
				{
					if ((qualification.AffectorId == key || qualification.SourceParent == key) && (_qualificationController.TryGetRelatedMiniCDC(qualification, out var relatedMiniCDC) || _entityViewManager.TryGetFakeCard(qualification.Key, out relatedMiniCDC)))
					{
						_highlights.EntityHighlights[relatedMiniCDC] = HighlightType.Selected;
					}
				}
			}
		}

		private IReadOnlyList<GreInteraction> GetActionsForCardId(uint cardId)
		{
			if (!_actionsByInstanceId.TryGetValue(cardId, out var value))
			{
				return Array.Empty<GreInteraction>();
			}
			return value;
		}

		private void SideboardActionHighlights()
		{
			foreach (KeyValuePair<uint, List<GreInteraction>> item in _actionsByInstanceId)
			{
				foreach (GreInteraction item2 in item.Value)
				{
					DuelScene_CDC duelScene_CDC = _getSideboardActionCardView(item2.GreAction.SourceId);
					duelScene_CDC?.UpdateHighlight(_getCardIsAboveDragThreshold(duelScene_CDC) ? HighlightType.Selected : HighlightType.Hot);
				}
			}
		}
	}

	private readonly IContext _context;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IActionEffectController _actionEffectController;

	private readonly BrowserManager _browserManager;

	private readonly AutoResponseManager _autoRespondManager;

	private readonly EntityViewManager _viewManager;

	private readonly CardDragController _cardDragController;

	private readonly DuelSceneLogger _logger;

	private readonly IButtonDataProvider _passButtonDataProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardMovementController _cardMovementController;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly GameManager _gameManagerInternal;

	private readonly ClientSideInteraction _clientSideInteraction;

	private bool _pendingAutoPass;

	private readonly List<GreInteraction> _allActions = new List<GreInteraction>(25);

	private readonly Dictionary<uint, List<GreInteraction>> _actionsByInstanceId = new Dictionary<uint, List<GreInteraction>>(25);

	private readonly Dictionary<uint, List<GreInteraction>> _duplicateCastActions = new Dictionary<uint, List<GreInteraction>>(25);

	private readonly Dictionary<uint, Dictionary<uint, List<DisqualificationType>>> _instanceToGreActionDisqualifiers = new Dictionary<uint, Dictionary<uint, List<DisqualificationType>>>();

	private readonly IVariantWorkflowTranslator _onClickVariantTranslator;

	private WorkflowVariant _currentVariant;

	private const uint ABIL_ID_KRRIK = 174957u;

	private DuelScene_CDC _previousPreemptivelyMovedCard;

	private uint _predictedZoneId;

	private const uint TOC_GILD_GRPID = 987u;

	private const uint TOC_TAP_UNTAP_GRPID = 989u;

	private const uint TOC_DONATE_GRPID = 990u;

	private const uint TOC_BOUNCE_TO_HAND_GRPID = 991u;

	private const uint TOC_DRAWCARD_ABILITY_GRPID = 992u;

	private const uint TOC_WAIVE_COST_GRPID = 993u;

	private const uint TOC_WISH_ABILITY_GRPID = 996u;

	private const uint TOC_PUT_LIBRARY_GRPID = 997u;

	private const uint TOC_GAIN_HASTE_GRPID = 998u;

	private const uint TOC_ADD_COLORS_GRPID = 999u;

	private DateTime _doubleTapCompareTime;

	private const float _doubleTapThreshold = 0.5f;

	private bool CanPass
	{
		get
		{
			if (_request.CanPass)
			{
				return AppliedState == InteractionAppliedState.Applied;
			}
			return false;
		}
	}

	public ActionsAvailableWorkflow(ActionsAvailableRequest actionsAvailableRequest, GameManager gameManager, ClientSideInteraction clientSideInteraction, IContext context)
		: base(actionsAvailableRequest)
	{
		_gameManagerInternal = gameManager;
		_clientSideInteraction = clientSideInteraction;
		_context = context ?? (context = NullContext.Default);
		_cardDatabase = context.Get<ICardDatabaseAdapter>();
		_autoRespondManager = gameManager.AutoRespManager;
		CardHolderManager cardHolderManager = gameManager.CardHolderManager;
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderManager);
		_browserManager = gameManager.BrowserManager;
		_viewManager = gameManager.ViewManager;
		_cardDragController = gameManager.InteractionSystem.DragController;
		_logger = gameManager.Logger;
		_actionEffectController = context.Get<IActionEffectController>() ?? NullActionEffectController.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_cardMovementController = context.Get<ICardMovementController>() ?? NullCardMovementController.Default;
		AssetLookupSystem assetLookupSystem = gameManager.AssetLookupSystem;
		_highlightsGenerator = new ActionsAvailableHighlightsGenerator(_cardDatabase, _gameManagerInternal.ViewManager, context.Get<IQualificationController>(), () => _currentVariant, _gameStateProvider, _actionsByInstanceId, _instanceToGreActionDisqualifiers, _clientSideInteraction, () => _gameManagerInternal.BrowserManager.IsAnyBrowserOpen, (DuelScene_CDC cardView) => _cardDragController.IsCardAboveThreshold(cardView), _cardDragController.IsDragPointAboveThreshold, (uint sourceId) => _actionEffectController.GetController<SideboardActionEffectController>().GetSideboardCdc(sourceId));
		_passButtonDataProvider = new PassButtonDataProvider(assetLookupSystem);
		_onClickVariantTranslator = new CardViewVariantTranslator(new ConfirmWidgetTranslation(assetLookupSystem, _cardDatabase.AbilityDataProvider, _cardDatabase.ClientLocProvider, _cardDatabase.GreLocProvider, _gameManagerInternal.CardHolderManager, _gameManagerInternal.UIManager.ConfirmWidget), new ConfirmBrowserTranslation(_gameManagerInternal.Context));
	}

	private void AddAction(uint id, GreInteraction interaction)
	{
		_allActions.Add(interaction);
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		Wotc.Mtgo.Gre.External.Messaging.Action greAction = interaction.GreAction;
		id = GetInstanceIdForAction(id, greAction, gameState);
		if (_actionsByInstanceId.TryGetValue(id, out var value))
		{
			value.Add(interaction);
		}
		else
		{
			_actionsByInstanceId[id] = new List<GreInteraction> { interaction };
		}
		if (!interaction.GreAction.IsCastAction())
		{
			return;
		}
		List<GreInteraction> list = new List<GreInteraction>(_actionsByInstanceId[id]);
		for (int i = 0; i < list.Count; i++)
		{
			GreInteraction greInteraction = list[i];
			for (int num = list.Count - 1; num > i; num--)
			{
				GreInteraction greInteraction2 = list[num];
				if (greInteraction2.GreAction.IsDuplicateCastActionOf(greInteraction.GreAction, gameState, _cardDatabase.AbilityDataProvider))
				{
					uint duplicateActionId = GetDuplicateActionId(greInteraction2.GreAction);
					if (_duplicateCastActions.TryGetValue(duplicateActionId, out var value2))
					{
						value2.Add(greInteraction2);
					}
					else
					{
						_duplicateCastActions[duplicateActionId] = new List<GreInteraction> { greInteraction, greInteraction2 };
					}
					list.Remove(greInteraction2);
				}
			}
		}
		_actionsByInstanceId[id] = list;
	}

	private static uint GetInstanceIdForAction(uint id, Wotc.Mtgo.Gre.External.Messaging.Action action, MtgGameState gameState)
	{
		if (action.AbilityGrpId == 174957 && action.IsActionType(ActionType.MakePayment) && gameState.TryGetCard(id, out var card) && card.Controller != null)
		{
			return card.Controller.InstanceId;
		}
		return id;
	}

	private uint GetDuplicateActionId(Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action == null)
		{
			return 0u;
		}
		return ((17 * 23 + action.InstanceId) * 23 + action.GrpId) * 23 + action.AbilityGrpId;
	}

	public override bool CanApply(List<UXEvent> events)
	{
		foreach (UXEvent @event in events)
		{
			if (@event is UserActionTakenUXEvent || @event is ZoneTransferGroup || @event is ResolutionEffectUXEventBase || @event is CombatFrame { DamageType: DamageType.Combat })
			{
				return false;
			}
		}
		return true;
	}

	public bool TryAutoRespond()
	{
		if (_pendingAutoPass && _gameManagerInternal.AutoRespManager.AutoPassEnabled && _request.CanPass)
		{
			PassAction();
			return true;
		}
		return false;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			return clickableWorkflow.CanClick(entity, clickType);
		}
		if (clickType == SimpleInteractionType.Secondary || clickType == SimpleInteractionType.None)
		{
			return false;
		}
		if (_gameManagerInternal.BrowserManager.IsAnyBrowserOpen && !_gameManagerInternal.BrowserManager.IsBrowserVisible)
		{
			return false;
		}
		uint instanceId = entity.InstanceId;
		if (_actionsByInstanceId.TryGetValue(instanceId, out var value))
		{
			DuelScene_CDC cdc = entity as DuelScene_CDC;
			DuelScene_AvatarView duelScene_AvatarView = entity as DuelScene_AvatarView;
			foreach (GreInteraction item in value)
			{
				if (item.IsActive)
				{
					if (CanClickOnCDC(cdc, clickType))
					{
						return true;
					}
					if (duelScene_AvatarView != null)
					{
						return true;
					}
				}
			}
		}
		DuelScene_CDC duelScene_CDC = entity as DuelScene_CDC;
		if (CanClickOnCDC(duelScene_CDC, clickType) && _actionEffectController.GetController<SideboardActionEffectController>().IsSideboardCdc(duelScene_CDC))
		{
			return true;
		}
		return false;
	}

	private bool CanClickOnCDC(DuelScene_CDC cdc, SimpleInteractionType clickType)
	{
		if (cdc == null)
		{
			return false;
		}
		switch (cdc.CurrentCardHolder.CardHolderType)
		{
		case CardHolderType.Library:
		case CardHolderType.Hand:
			if (_cardDragController.IsCardAboveThreshold(cdc))
			{
				if (PlatformUtils.IsHandheld() && !_cardDragController.IsMousePointAboveThreshold())
				{
					return false;
				}
				return clickType == SimpleInteractionType.Primary;
			}
			return clickType == SimpleInteractionType.DoublePrimary;
		case CardHolderType.Graveyard:
			return false;
		default:
			return clickType == SimpleInteractionType.Primary;
		}
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: true, out var reroutedEntityView))
		{
			entity = reroutedEntityView;
		}
		List<GreInteraction> value;
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			clickableWorkflow.OnClick(entity, clickType);
		}
		else if (_actionsByInstanceId.TryGetValue(entity.InstanceId, out value))
		{
			WorkflowVariant workflowVariant = _onClickVariantTranslator.Translate(entity, value);
			if (workflowVariant != null)
			{
				ClearVariant();
				_currentVariant = workflowVariant;
				WorkflowVariant currentVariant = _currentVariant;
				currentVariant.HighlightsUpdated = (Action<Highlights>)Delegate.Combine(currentVariant.HighlightsUpdated, new Action<Highlights>(OnUpdateHighlights));
				WorkflowVariant currentVariant2 = _currentVariant;
				currentVariant2.ButtonsUpdated = (Action<Buttons>)Delegate.Combine(currentVariant2.ButtonsUpdated, new Action<Buttons>(OnUpdateButtons));
				WorkflowVariant currentVariant3 = _currentVariant;
				currentVariant3.ArrowsUpdated = (Action<Arrows>)Delegate.Combine(currentVariant3.ArrowsUpdated, new Action<Arrows>(OnUpdateArrows));
				WorkflowVariant currentVariant4 = _currentVariant;
				currentVariant4.Submitted = (System.Action)Delegate.Combine(currentVariant4.Submitted, new System.Action(OnVariantSubmit));
				WorkflowVariant currentVariant5 = _currentVariant;
				currentVariant5.Cancelled = (System.Action)Delegate.Combine(currentVariant5.Cancelled, new System.Action(CloseVariant));
				_currentVariant.Open();
			}
			else
			{
				_clientSideInteraction.HandleActions(entity, value, SubmitAction, MDNPlayerPrefs.GameplayWarningsEnabled);
			}
		}
		else if (entity is DuelScene_CDC cardView)
		{
			SideboardActionEffectController controller = _actionEffectController.GetController<SideboardActionEffectController>();
			if (controller != null && controller.IsSideboardCdc(cardView))
			{
				uint sourceId = controller.GetSourceId(cardView);
				List<uint> affectedInstanceIds = controller.InstanceIds(sourceId);
				StartSideboardAction(affectedInstanceIds);
			}
		}
	}

	public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
	{
		return false;
	}

	public void OnClickStack(CdcStackCounterView entity)
	{
	}

	public void OnBattlefieldClick()
	{
	}

	public bool CanCommenceDrag(IEntityView beginningEntityView)
	{
		return IsDraggingToPlayOrCast(beginningEntityView, _gameManagerInternal.CurrentInteraction, _actionEffectController);
	}

	public void OnDragCommenced(IEntityView beginningEntityView)
	{
	}

	public bool CanCompleteDrag(IEntityView endingEntityView)
	{
		return IsDraggingToPlayOrCast(endingEntityView, _gameManagerInternal.CurrentInteraction, _actionEffectController);
	}

	public void OnDragCompleted(IEntityView beginningEntityView, IEntityView endingEntityView)
	{
		if (CanClick(beginningEntityView, SimpleInteractionType.Primary) && _cardDragController.IsMousePointAboveThreshold())
		{
			OnClick(beginningEntityView, SimpleInteractionType.Primary);
		}
	}

	protected virtual void SubmitAction(GreInteraction interaction)
	{
		List<GreInteraction> value;
		if (interaction.GreAction.AlternativeGrpId == 0 && interaction.GreAction.AbilityGrpId == 0)
		{
			SubmitAction(interaction.GreAction);
		}
		else if (_duplicateCastActions.TryGetValue(GetDuplicateActionId(interaction.GreAction), out value) && (_currentVariant == null || !(_currentVariant is ActionSourceSelection)))
		{
			ClearVariant();
			_currentVariant = new ActionSourceSelection(value, interaction.GreAction.InstanceId, _context);
			WorkflowVariant currentVariant = _currentVariant;
			currentVariant.Submitted = (System.Action)Delegate.Combine(currentVariant.Submitted, new System.Action(OnVariantSubmit));
			WorkflowVariant currentVariant2 = _currentVariant;
			currentVariant2.Cancelled = (System.Action)Delegate.Combine(currentVariant2.Cancelled, new System.Action(CloseVariant));
			WorkflowVariant currentVariant3 = _currentVariant;
			currentVariant3.HighlightsUpdated = (Action<Highlights>)Delegate.Combine(currentVariant3.HighlightsUpdated, new Action<Highlights>(OnUpdateHighlights));
			WorkflowVariant currentVariant4 = _currentVariant;
			currentVariant4.ButtonsUpdated = (Action<Buttons>)Delegate.Combine(currentVariant4.ButtonsUpdated, new Action<Buttons>(OnUpdateButtons));
			WorkflowVariant currentVariant5 = _currentVariant;
			currentVariant5.ArrowsUpdated = (Action<Arrows>)Delegate.Combine(currentVariant5.ArrowsUpdated, new Action<Arrows>(OnUpdateArrows));
			WorkflowVariant currentVariant6 = _currentVariant;
			currentVariant6.PromptUpdated = (Action<WorkflowPrompt>)Delegate.Combine(currentVariant6.PromptUpdated, new Action<WorkflowPrompt>(OnUpdatePrompt));
			_currentVariant.Open();
			RefreshWorkflow();
		}
		else
		{
			SubmitAction(interaction.GreAction);
		}
	}

	private void SubmitAction(Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		LogActionSubmission(action, _gameStateProvider.LatestGameState);
		PredictiveCardMovement(action);
		if (action.ActionType == ActionType.Pass)
		{
			_request.SubmitPass();
		}
		else
		{
			_request.SubmitAction(action, _gameManagerInternal.AutoRespManager.FullControlDisabled);
		}
	}

	private void SubmitActions(params Wotc.Mtgo.Gre.External.Messaging.Action[] actions)
	{
		MtgGameState latestGameState = _gameManagerInternal.LatestGameState;
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in actions)
		{
			LogActionSubmission(action, latestGameState);
			PredictiveCardMovement(action);
		}
		_request.SubmitActions(actions, _autoRespondManager.FullControlDisabled);
	}

	private void LogActionSubmission(Wotc.Mtgo.Gre.External.Messaging.Action action, MtgGameState currentState)
	{
		if (action.ActionType == ActionType.Pass)
		{
			_logger?.PriorityPassed();
		}
		else if (ActionsAvailableRequest.IsAbilityAction(action))
		{
			_logger.AbilityUsed(action.AbilityGrpId);
		}
		_logger.UpdateActionsPerPhaseStep(currentState.CurrentPhase, currentState.CurrentStep);
	}

	private void PredictiveCardMovement(Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		uint num = 0u;
		switch (action.ActionType)
		{
		case ActionType.Play:
			num = mtgGameState.Battlefield.Id;
			break;
		case ActionType.Cast:
		case ActionType.CastLeft:
		case ActionType.CastRight:
			num = mtgGameState.Stack.Id;
			break;
		}
		if (num != 0 && _viewManager.TryGetCardView(action.InstanceId, out var cardView))
		{
			_gameManagerInternal.SplineMovementSystem.AddPermanentGoal(cardView.Root, new IdealPoint(cardView.Root));
			_previousPreemptivelyMovedCard = cardView;
			_cardMovementController.MoveCard(cardView, num);
			_gameManagerInternal.UXEventQueue.EventExecutionCommenced += OnUxEventCommenced;
		}
	}

	private void OnUxEventCommenced(UXEvent uxEvent)
	{
		if (!(uxEvent is GameStatePlaybackCompletedUXEvent))
		{
			return;
		}
		if (_previousPreemptivelyMovedCard != null && _predictedZoneId != 0)
		{
			uint id = _previousPreemptivelyMovedCard.Model.Zone.Id;
			if (id != _predictedZoneId)
			{
				_cardMovementController.MoveCard(_previousPreemptivelyMovedCard, id);
			}
		}
		_predictedZoneId = 0u;
		_previousPreemptivelyMovedCard = null;
		_gameManagerInternal.UXEventQueue.EventExecutionCommenced -= OnUxEventCommenced;
	}

	protected override void ApplyInteractionInternal()
	{
		_logger?.PriorityReceived();
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_gain_priority, AudioManager.Default);
		CardHoverController.OnHoveredCardUpdated += OnCardHoveredOrDragged;
		_cardDragController.DraggedCardUpdated += OnCardHoveredOrDragged;
		_battlefield.Get().LayoutNow();
		Wotc.Mtgo.Gre.External.Messaging.Action action = null;
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action2 in _request.Actions)
		{
			if (action == null && action2.ActionType == ActionType.Pass)
			{
				action = action2;
				continue;
			}
			uint instanceId = action2.InstanceId;
			if (instanceId != 0)
			{
				AddAction(instanceId, new GreInteraction(action2));
			}
		}
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action inactiveAction in _request.InactiveActions)
		{
			uint instanceId2 = inactiveAction.InstanceId;
			if (instanceId2 == 0)
			{
				continue;
			}
			GreInteraction interaction = new GreInteraction(inactiveAction, isActive: false);
			AddAction(instanceId2, interaction);
			uint disqualifyingSourceId = inactiveAction.DisqualifyingSourceId;
			DisqualificationType disqualificationType = inactiveAction.GetDisqualificationType();
			if (disqualifyingSourceId != 0 && disqualificationType != DisqualificationType.None)
			{
				if (!_instanceToGreActionDisqualifiers.ContainsKey(instanceId2))
				{
					_instanceToGreActionDisqualifiers[instanceId2] = new Dictionary<uint, List<DisqualificationType>>();
				}
				if (!_instanceToGreActionDisqualifiers[instanceId2].ContainsKey(disqualifyingSourceId))
				{
					_instanceToGreActionDisqualifiers[instanceId2][disqualifyingSourceId] = new List<DisqualificationType>();
				}
				if (!_instanceToGreActionDisqualifiers[instanceId2][disqualifyingSourceId].Contains(disqualificationType))
				{
					_instanceToGreActionDisqualifiers[instanceId2][disqualifyingSourceId].Add(disqualificationType);
				}
			}
		}
		foreach (KeyValuePair<uint, List<GreInteraction>> item in _actionsByInstanceId)
		{
			if (_viewManager.TryGetCardView(item.Key, out var cardView))
			{
				cardView.UpdateVisuals();
			}
		}
		SetButtons();
	}

	public void StartSideboardAction(List<uint> affectedInstanceIds)
	{
		ModalBrowserProvider_ClientSide.ModalBrowserData modalBrowserData = new ModalBrowserProvider_ClientSide.ModalBrowserData
		{
			Header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Multi_Browser_Title"),
			SubHeader = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Multi_Browser_SubTitle"),
			CancelText = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Browser_CancelText"),
			CanCancel = true,
			OnSelectionMade = (DuelScene_CDC cdc) =>
			{
				if ((bool)cdc)
				{
					if (_actionsByInstanceId.TryGetValue(cdc.InstanceId, out var value2))
					{
						_clientSideInteraction.HandleActions(cdc, value2, SubmitAction, MDNPlayerPrefs.GameplayWarningsEnabled);
					}
				}
				else
				{
					_gameManagerInternal.BrowserManager.CloseCurrentBrowser();
				}
			}
		};
		foreach (uint affectedInstanceId in affectedInstanceIds)
		{
			if (!_viewManager.TryGetCardView(affectedInstanceId, out var cardView) || !_actionsByInstanceId.TryGetValue(affectedInstanceId, out var value))
			{
				continue;
			}
			foreach (GreInteraction item in value)
			{
				if ((item?.CanAffordToCast).Value)
				{
					modalBrowserData.Selectable.Add(cardView);
				}
				else
				{
					modalBrowserData.NonSelectable.Add(cardView);
				}
			}
		}
		ModalBrowserProvider_ClientSide modalBrowserProvider_ClientSide = new ModalBrowserProvider_ClientSide(modalBrowserData, _gameManagerInternal.ViewManager);
		IBrowser openedBrowser = _gameManagerInternal.BrowserManager.OpenBrowser(modalBrowserProvider_ClientSide);
		modalBrowserProvider_ClientSide.SetOpenedBrowser(openedBrowser);
	}

	public override void CleanUp()
	{
		ClearVariant();
		CardHoverController.OnHoveredCardUpdated -= OnCardHoveredOrDragged;
		if (_cardDragController != null)
		{
			_cardDragController.DraggedCardUpdated -= OnCardHoveredOrDragged;
		}
		if ((bool)_gameManagerInternal && _gameManagerInternal.UXEventQueue != null)
		{
			_gameManagerInternal.UXEventQueue.EventExecutionCommenced -= OnUxEventCommenced;
		}
		_battlefield.ClearCache();
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_lose_priority, AudioManager.Default);
		base.CleanUp();
	}

	private void ClearVariant()
	{
		if (_currentVariant != null)
		{
			WorkflowVariant currentVariant = _currentVariant;
			currentVariant.HighlightsUpdated = (Action<Highlights>)Delegate.Remove(currentVariant.HighlightsUpdated, new Action<Highlights>(OnUpdateHighlights));
			WorkflowVariant currentVariant2 = _currentVariant;
			currentVariant2.ButtonsUpdated = (Action<Buttons>)Delegate.Remove(currentVariant2.ButtonsUpdated, new Action<Buttons>(OnUpdateButtons));
			WorkflowVariant currentVariant3 = _currentVariant;
			currentVariant3.ArrowsUpdated = (Action<Arrows>)Delegate.Remove(currentVariant3.ArrowsUpdated, new Action<Arrows>(OnUpdateArrows));
			WorkflowVariant currentVariant4 = _currentVariant;
			currentVariant4.PromptUpdated = (Action<WorkflowPrompt>)Delegate.Remove(currentVariant4.PromptUpdated, new Action<WorkflowPrompt>(OnUpdatePrompt));
			WorkflowVariant currentVariant5 = _currentVariant;
			currentVariant5.Submitted = (System.Action)Delegate.Remove(currentVariant5.Submitted, new System.Action(OnVariantSubmit));
			WorkflowVariant currentVariant6 = _currentVariant;
			currentVariant6.Cancelled = (System.Action)Delegate.Remove(currentVariant6.Cancelled, new System.Action(CloseVariant));
			_currentVariant.Close();
			_currentVariant = null;
		}
	}

	public IReadOnlyList<GreInteraction> GetAllActions()
	{
		return _allActions;
	}

	public IReadOnlyList<GreInteraction> GetActionsForId(uint instanceId)
	{
		if (_actionsByInstanceId.TryGetValue(instanceId, out var value))
		{
			return value;
		}
		return Array.Empty<GreInteraction>();
	}

	private IReadOnlyDictionary<uint, List<DisqualificationType>> GetInteractionDisqualifiers(uint instanceId)
	{
		if (_instanceToGreActionDisqualifiers.TryGetValue(instanceId, out var value))
		{
			return value;
		}
		return new Dictionary<uint, List<DisqualificationType>>();
	}

	protected virtual System.Action GetPromptButtonAction()
	{
		return PassAction;
	}

	private void PassAction()
	{
		_logger?.PriorityPassed();
		_request.SubmitPass();
	}

	private void ToggleResolveAll()
	{
		bool flag = !_autoRespondManager.ResolveAllEnabled;
		_autoRespondManager.SetResolveAll(flag);
		if (flag)
		{
			if (CanPass)
			{
				PassAction();
			}
		}
		else
		{
			SetButtons();
		}
	}

	public bool CanKeyDown(Key key)
	{
		if (_currentVariant is IKeybindingWorkflow keybindingWorkflow)
		{
			return keybindingWorkflow.CanKeyDown(key);
		}
		if (_browserManager.IsAnyBrowserOpen)
		{
			return false;
		}
		return key == Key.Q;
	}

	public void OnKeyDown(Key key)
	{
		if (_currentVariant is IKeybindingWorkflow keybindingWorkflow)
		{
			keybindingWorkflow.OnKeyDown(key);
		}
	}

	public bool CanKeyHeld(Key key, float holdDuration)
	{
		if (_currentVariant is IKeybindingWorkflow keybindingWorkflow)
		{
			return keybindingWorkflow.CanKeyHeld(key, holdDuration);
		}
		if (_browserManager.IsBrowserVisible)
		{
			return false;
		}
		if (key == Key.Q && holdDuration >= 0.5f)
		{
			return true;
		}
		return false;
	}

	public void OnKeyHeld(Key key, float holdDuration)
	{
		if (_currentVariant is IKeybindingWorkflow keybindingWorkflow)
		{
			keybindingWorkflow.OnKeyHeld(key, holdDuration);
			return;
		}
		WorkflowVariant variant_KeyHeld = GetVariant_KeyHeld(key, holdDuration);
		if (variant_KeyHeld != null)
		{
			ClearVariant();
			_currentVariant = variant_KeyHeld;
			WorkflowVariant currentVariant = _currentVariant;
			currentVariant.HighlightsUpdated = (Action<Highlights>)Delegate.Combine(currentVariant.HighlightsUpdated, new Action<Highlights>(OnUpdateHighlights));
			WorkflowVariant currentVariant2 = _currentVariant;
			currentVariant2.ButtonsUpdated = (Action<Buttons>)Delegate.Combine(currentVariant2.ButtonsUpdated, new Action<Buttons>(OnUpdateButtons));
			WorkflowVariant currentVariant3 = _currentVariant;
			currentVariant3.ArrowsUpdated = (Action<Arrows>)Delegate.Combine(currentVariant3.ArrowsUpdated, new Action<Arrows>(OnUpdateArrows));
			WorkflowVariant currentVariant4 = _currentVariant;
			currentVariant4.Submitted = (System.Action)Delegate.Combine(currentVariant4.Submitted, new System.Action(OnVariantSubmit));
			WorkflowVariant currentVariant5 = _currentVariant;
			currentVariant5.Cancelled = (System.Action)Delegate.Combine(currentVariant5.Cancelled, new System.Action(CloseVariant));
			_currentVariant.Open();
		}
	}

	public virtual bool CanKeyUp(Key key)
	{
		if (_currentVariant is IKeybindingWorkflow keybindingWorkflow)
		{
			return keybindingWorkflow.CanKeyUp(key);
		}
		return key switch
		{
			Key.W => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 993 || x.AbilityGrpId == 996), 
			Key.D => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 992), 
			Key.A => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 999), 
			Key.H => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 998), 
			Key.N => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 990), 
			Key.B => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 991), 
			Key.P => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 997), 
			Key.T => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 989), 
			Key.G => _request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 987), 
			Key.Q => true, 
			_ => false, 
		};
	}

	public void OnKeyUp(Key key)
	{
		if (_currentVariant is IKeybindingWorkflow keybindingWorkflow)
		{
			keybindingWorkflow.OnKeyUp(key);
			return;
		}
		switch (key)
		{
		case Key.W:
		{
			uint grpId = (_request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.AbilityGrpId == 996) ? 996u : 993u);
			SubmitActionForThisGrpId(grpId);
			break;
		}
		case Key.D:
			SubmitActionForThisGrpId(992u);
			break;
		case Key.A:
			SubmitActionForThisGrpId(999u);
			break;
		case Key.H:
			SubmitActionForThisGrpId(998u);
			break;
		case Key.N:
			SubmitActionForThisGrpId(990u);
			break;
		case Key.B:
			SubmitActionForThisGrpId(991u);
			break;
		case Key.P:
			SubmitActionForThisGrpId(997u);
			break;
		case Key.T:
			SubmitActionForThisGrpId(989u);
			break;
		case Key.G:
			SubmitActionForThisGrpId(987u);
			break;
		case Key.Q:
		{
			DateTime now = DateTime.Now;
			if (now.Subtract(_doubleTapCompareTime).TotalSeconds <= 0.5)
			{
				SubmitActionByActionType(ActionType.FloatMana);
			}
			_doubleTapCompareTime = now;
			break;
		}
		}
	}

	private void SubmitActionForThisGrpId(uint grpId)
	{
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in _request.Actions)
		{
			if (action.AbilityGrpId == grpId)
			{
				_request.SubmitAction(action, _autoRespondManager.FullControlDisabled);
				break;
			}
		}
	}

	private void SubmitActionByActionType(ActionType actionType)
	{
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in _request.Actions)
		{
			if (action.ActionType == actionType)
			{
				_request.SubmitAction(action, _autoRespondManager.FullControlDisabled);
				break;
			}
		}
	}

	protected override void SetButtons()
	{
		if (_currentVariant != null)
		{
			Buttons = _currentVariant.GetButtons();
		}
		else
		{
			Buttons.Cleanup();
			if (_request.CanPass)
			{
				MtgZone stack = _gameManagerInternal.LatestGameState.Stack;
				if (stack != null && stack.TotalCardCount != 0)
				{
					Buttons.WorkflowButtons.Add(new PromptButtonData
					{
						ButtonText = "DuelScene/ClientPrompt/PhaseStep/PhaseStep_Resolve",
						Style = ButtonStyle.StyleType.Main,
						ButtonCallback = GetPromptButtonAction()
					});
					if (stack.TotalCardCount > 1)
					{
						Buttons.WorkflowButtons.Add(new PromptButtonData
						{
							ButtonText = "DuelScene/ClientPrompt/ResolveAll",
							ButtonCallback = ToggleResolveAll,
							Style = (_autoRespondManager.ResolveAllEnabled ? ButtonStyle.StyleType.ToggleOn : ButtonStyle.StyleType.ToggleOff),
							Tag = ButtonTag.ResolveAll,
							ClearsInteractions = false
						});
					}
				}
				else
				{
					MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
					Buttons.WorkflowButtons.Add(new PromptButtonData
					{
						ButtonText = _passButtonDataProvider.GetLocKey(),
						ButtonSFX = _passButtonDataProvider.GetSfx(),
						Style = _passButtonDataProvider.GetStyle(),
						Tag = ButtonTag.Primary,
						ClearsInteractions = false,
						ButtonCallback = GetPromptButtonAction(),
						NextPhase = mtgGameState.NextPhase,
						NextStep = mtgGameState.NextStep
					});
				}
			}
			if (_request.AllowUndo)
			{
				Buttons.UndoData = new PromptButtonData
				{
					ButtonCallback = () =>
					{
						_request.Undo();
					}
				};
			}
		}
		OnUpdateButtons(Buttons);
	}

	private void OnCardHoveredOrDragged(DuelScene_CDC card)
	{
		UpdateHighlightsAndDimming();
	}

	public static IReadOnlyList<GreInteraction> GetInteractionsForId(uint instanceId, WorkflowBase currentInteraction)
	{
		if (currentInteraction == null)
		{
			return Array.Empty<GreInteraction>();
		}
		ActionsAvailableWorkflow actionsAvailableWorkflow = currentInteraction as ActionsAvailableWorkflow;
		if (currentInteraction is PayCostWorkflow payCostWorkflow)
		{
			foreach (WorkflowBase childWorkflow in payCostWorkflow.ChildWorkflows)
			{
				actionsAvailableWorkflow = childWorkflow as ActionsAvailableWorkflow;
				if (actionsAvailableWorkflow != null)
				{
					break;
				}
			}
		}
		if (actionsAvailableWorkflow == null)
		{
			return Array.Empty<GreInteraction>();
		}
		return actionsAvailableWorkflow.GetActionsForId(instanceId);
	}

	public static IReadOnlyDictionary<uint, List<DisqualificationType>> GetDisqualifiersForId(uint instanceId, WorkflowBase workflow)
	{
		if (workflow is ActionsAvailableWorkflow actionsAvailableWorkflow)
		{
			return actionsAvailableWorkflow.GetInteractionDisqualifiers(instanceId);
		}
		if (workflow is PayCostWorkflow payCostWorkflow)
		{
			foreach (WorkflowBase childWorkflow in payCostWorkflow.ChildWorkflows)
			{
				if (childWorkflow is ActionsAvailableWorkflow actionsAvailableWorkflow2)
				{
					return actionsAvailableWorkflow2.GetInteractionDisqualifiers(instanceId);
				}
			}
		}
		return DictionaryExtensions.Empty<uint, List<DisqualificationType>>();
	}

	public static bool IsDraggingToPlayOrCast(IEntityView entityView, WorkflowBase currentInteraction, IActionEffectController actionEffectController)
	{
		if (entityView is DuelScene_CDC { CurrentCardHolder: { PlayerNum: GREPlayerNum.LocalPlayer, CardHolderType: var cardHolderType } } duelScene_CDC)
		{
			switch (cardHolderType)
			{
			case CardHolderType.Hand:
				if (GetInteractionsForId(duelScene_CDC.InstanceId, currentInteraction).Count <= 0)
				{
					return actionEffectController.GetController<SideboardActionEffectController>().IsSideboardCdc(duelScene_CDC);
				}
				return true;
			case CardHolderType.Library:
				if (GetInteractionsForId(duelScene_CDC.InstanceId, currentInteraction).Count > 0)
				{
					return !duelScene_CDC.Model.IsDisplayedFaceDown;
				}
				return false;
			}
		}
		return false;
	}

	public void OnAutoYieldEnabled()
	{
		if (AppliedState != InteractionAppliedState.Applied)
		{
			_pendingAutoPass = true;
		}
		else if (_request.CanPass)
		{
			PassAction();
		}
	}

	private WorkflowVariant GetVariant_KeyHeld(Key key, double duration)
	{
		if (key == Key.Q && duration >= 0.5)
		{
			List<Wotc.Mtgo.Gre.External.Messaging.Action> list = _request.Actions.FindAll((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.ActionType == ActionType.ActivateMana && x.ManaCost.Count == 0);
			if (list.Count > 1)
			{
				return new BatchManaSubmission(new ManaColorSelection(_cardDatabase.AbilityDataProvider, _gameManagerInternal.CardHolderManager, _gameManagerInternal.UIManager.ManaColorSelector), _battlefield.Get(), list);
			}
		}
		return null;
	}

	private void CloseVariant()
	{
		ClearVariant();
		RefreshWorkflow();
	}

	private void RefreshWorkflow()
	{
		SetHighlights();
		SetButtons();
		SetDimming();
		SetArrows();
	}

	private void OnVariantSubmit()
	{
		if (_currentVariant != null)
		{
			List<Wotc.Mtgo.Gre.External.Messaging.Action> selectedActions = _currentVariant.SelectedActions;
			if (selectedActions.Count == 1)
			{
				SubmitAction(selectedActions[0]);
			}
			else if (selectedActions.Count > 1)
			{
				SubmitActions(selectedActions.ToArray());
			}
			CloseVariant();
		}
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		if (_currentVariant is ICardStackWorkflow cardStackWorkflow)
		{
			return cardStackWorkflow.CanStack(lhs, rhs);
		}
		bool flag = _actionsByInstanceId.TryGetValue(lhs.InstanceId, out var value);
		bool flag2 = _actionsByInstanceId.TryGetValue(rhs.InstanceId, out var value2);
		if (!flag && !flag2)
		{
			return true;
		}
		if (flag != flag2)
		{
			return false;
		}
		if (value.Count != value2.Count)
		{
			return false;
		}
		if (value.Count((GreInteraction x) => x.IsActive) != value2.Count((GreInteraction x) => x.IsActive))
		{
			return false;
		}
		return true;
	}
}
