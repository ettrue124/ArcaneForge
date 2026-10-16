using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GreClient.CardData;
using GreClient.Rules;
using InteractionSystem;
using Pooling;
using UnityEngine;
using UnityEngine.EventSystems;
using Wizards.Mtga.Platforms;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class DeclareAttackersWorkflow : WorkflowBase<DeclareAttackerRequest>, IRoundTripWorkflow, IClickableWorkflow, IDraggableWorkflow, IYieldWorkflow, ICardStackWorkflow
{
	private interface IDeclareAttackersVariant
	{
		event Action<Attacker> AttackerSelected;

		event System.Action Cancelled;

		void Apply();

		void Cleanup();
	}

	private interface IHighlightProvider
	{
		Highlights GetHighlights();
	}

	private interface IButtonProvider
	{
		Buttons GetButtons();
	}

	private interface IArrowProvider
	{
		Arrows GetArrows();
	}

	private interface IDimmingProvider
	{
		Dimming GetDimming();
	}

	private class AttackerSpecification : IDeclareAttackersVariant, IHighlightProvider, IButtonProvider, IDimmingProvider
	{
		private readonly ConfirmWidget _widget;

		private readonly DuelScene_CDC _sourceCard;

		private readonly Attacker[] _attackOptions;

		private readonly Attacker _declaredAttack;

		private readonly Dictionary<ConfirmWidget.Option, Attacker> _optionToAttackerMap = new Dictionary<ConfirmWidget.Option, Attacker>();

		private Highlights _highlights = new Highlights();

		private Dimming _dimming = new Dimming();

		public event Action<Attacker> AttackerSelected;

		public event System.Action Cancelled;

		public AttackerSpecification(ConfirmWidget widget, DuelScene_CDC sourceCard, Attacker declaredAttack, params Attacker[] attackOptions)
		{
			_widget = widget;
			_sourceCard = sourceCard;
			_attackOptions = attackOptions;
			_declaredAttack = declaredAttack;
		}

		public void Apply()
		{
			ConfirmWidget.Option[] array = new ConfirmWidget.Option[_attackOptions.Length];
			for (int i = 0; i < _attackOptions.Length; i++)
			{
				Attacker attacker = ((_declaredAttack?.AlternativeGrpId == _attackOptions[i].AlternativeGrpId) ? _declaredAttack : _attackOptions[i]);
				ConfirmWidget.Option key = (array[i] = new ConfirmWidget.Option
				{
					Text = getOptionText(attacker, Languages.ActiveLocProvider),
					IconPath = null
				});
				_optionToAttackerMap[key] = attacker;
			}
			_widget.Open(_sourceCard, array);
			_widget.OptionSelected += OnOptionSelected;
			_widget.Cancelled += Cancelled;
			static string getOptionText(Attacker attacker2, IClientLocProvider localizationManager)
			{
				if (attacker2.SelectedDamageRecipient != null)
				{
					return localizationManager.GetLocalizedText("DuelScene/Interaction/DeclareAttackers/Undeclare");
				}
				return attacker2.AlternativeGrpId switch
				{
					0u => localizationManager.GetLocalizedText("DuelScene/Interaction/DeclareAttackers/Attack"), 
					162u => localizationManager.GetLocalizedText("DuelScene/Interaction/DeclareAttackers/Exert"), 
					261u => localizationManager.GetLocalizedText("DuelScene/Interaction/DeclareAttackers/Enlist"), 
					_ => $"Attack {attacker2.AlternativeGrpId}", 
				};
			}
		}

		public void Cleanup()
		{
			_widget.OptionSelected -= OnOptionSelected;
			_widget.Cancelled -= Cancelled;
			if (_widget.IsOpen)
			{
				_widget.Close();
			}
			AttackerSelected = null;
			Cancelled = null;
		}

		private void OnOptionSelected(ConfirmWidget.Option option)
		{
			if (_optionToAttackerMap.TryGetValue(option, out var value))
			{
				AttackerSelected?.Invoke(value);
			}
		}

		public Highlights GetHighlights()
		{
			if (_sourceCard != null)
			{
				_highlights.IdToHighlightType_Workflow[_sourceCard.InstanceId] = HighlightType.Selected;
			}
			return _highlights;
		}

		public Buttons GetButtons()
		{
			return new Buttons
			{
				CancelData = new PromptButtonData
				{
					ButtonText = Utils.GetCancelLocKey(AllowCancel.Abort),
					Style = ButtonStyle.StyleType.Secondary,
					ButtonCallback = () =>
					{
						Cancelled?.Invoke();
					},
					ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName,
					ClearsInteractions = false
				}
			};
		}

		public Dimming GetDimming()
		{
			if (_sourceCard != null)
			{
				_dimming.IdToIsDimmed[_sourceCard.InstanceId] = false;
				_dimming.WorkflowActive = true;
			}
			return _dimming;
		}
	}

	private class DeclareAttackersHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyDictionary<uint, Attacker> _pendingAttackers;

		private readonly IReadOnlyDictionary<uint, Attacker> _declaredAttackers;

		private readonly IReadOnlyCollection<uint> _pendingAttackersCommonQuarryIds;

		private readonly Func<IEnumerable<uint>> _getAllAttackerIds;

		private readonly Func<IDeclareAttackersVariant> _getCurrentVariant;

		public DeclareAttackersHighlightsGenerator(IReadOnlyDictionary<uint, Attacker> pendingAttackers, IReadOnlyDictionary<uint, Attacker> declaredAttackers, Func<IEnumerable<uint>> getAllAttackerIds, Func<IDeclareAttackersVariant> getCurrentVariant, IReadOnlyCollection<uint> pendingAttackersCommonQuarryIds)
		{
			_pendingAttackers = pendingAttackers;
			_declaredAttackers = declaredAttackers;
			_getAllAttackerIds = getAllAttackerIds;
			_getCurrentVariant = getCurrentVariant;
			_pendingAttackersCommonQuarryIds = pendingAttackersCommonQuarryIds;
		}

		public Highlights GetHighlights()
		{
			if (_getCurrentVariant?.Invoke() is IHighlightProvider highlightProvider)
			{
				return highlightProvider.GetHighlights();
			}
			Highlights highlights = new Highlights();
			foreach (uint pendingAttackersCommonQuarryId in _pendingAttackersCommonQuarryIds)
			{
				highlights.IdToHighlightType_Workflow[pendingAttackersCommonQuarryId] = HighlightType.Hot;
			}
			foreach (uint item in _getAllAttackerIds())
			{
				if (_declaredAttackers.ContainsKey(item))
				{
					highlights.IdToHighlightType_Workflow[item] = HighlightType.Selected;
					continue;
				}
				if (_pendingAttackers.ContainsKey(item))
				{
					highlights.IdToHighlightType_Workflow[item] = HighlightType.Selected;
					continue;
				}
				highlights.IdToHighlightType_Workflow[item] = HighlightType.Hot;
				if (PlatformUtils.GetCurrentDeviceType() == DeviceType.Handheld && _pendingAttackers.Count > 0)
				{
					highlights.IdToHighlightType_Workflow[item] = HighlightType.None;
				}
			}
			return highlights;
		}
	}

	private static readonly IEqualityComparer<AttackWarning> AttackWarningTypeComparer = new LambdaEqualityComparer<AttackWarning>((AttackWarning warningA, AttackWarning warningB) => warningA.Type.Equals(warningB.Type), (AttackWarning warning) => (int)warning.Type);

	protected readonly Dictionary<uint, List<Attacker>> _attackers;

	private readonly Dictionary<uint, List<Attacker>> _qualifiedAttackers;

	private readonly Dictionary<uint, Attacker> _pendingAttackers;

	protected readonly Dictionary<uint, Attacker> _declaredAttackers;

	private readonly HashSet<uint> _pendingAttackersCommonQuarryIds;

	private readonly HashSet<uint> _idsToDirtyOnRoundtrip;

	private readonly Dictionary<uint, List<AttackWarning>> _attackWarnings;

	private readonly IObjectPool _objPool;

	private readonly IPromptEngine _promptEngine;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBattlefieldCardHolder _battlefield;

	private readonly UIManager _uiManager;

	private readonly ConfirmWidget _confirmWidget;

	private readonly GameInteractionSystem _gameInteractionSystem;

	private readonly UIMessageHandler _uiMessageHandler;

	private IDeclareAttackersVariant _currentVariant;

	private bool _submitted;

	private HashSet<uint> _allIdsCache = new HashSet<uint>();

	private bool HasAttackWarnings => _request.AttackWarnings.Exists((AttackWarning x) => x.Type != AttackWarningType.CannotBeAttackedByMoreThanOne);

	public DeclareAttackersWorkflow(DeclareAttackerRequest declareAttackersReq, IObjectPool objPool, IPromptEngine promptEngine, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBattlefieldCardHolder battlefield, UIManager uiManager, GameInteractionSystem gameInteractionSystem, UIMessageHandler uiMessageHandler)
		: base(declareAttackersReq)
	{
		_objPool = objPool ?? NullObjectPool.Default;
		_promptEngine = promptEngine ?? NullPromptEngine.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_battlefield = battlefield;
		_uiManager = uiManager;
		_confirmWidget = uiManager.ConfirmWidget;
		_gameInteractionSystem = gameInteractionSystem;
		_uiMessageHandler = uiMessageHandler;
		_attackers = _objPool.PopObject<Dictionary<uint, List<Attacker>>>();
		_qualifiedAttackers = _objPool.PopObject<Dictionary<uint, List<Attacker>>>();
		_pendingAttackers = _objPool.PopObject<Dictionary<uint, Attacker>>();
		_declaredAttackers = _objPool.PopObject<Dictionary<uint, Attacker>>();
		_pendingAttackersCommonQuarryIds = _objPool.PopObject<HashSet<uint>>();
		_idsToDirtyOnRoundtrip = _objPool.PopObject<HashSet<uint>>();
		_attackWarnings = _objPool.PopObject<Dictionary<uint, List<AttackWarning>>>();
		_highlightsGenerator = new DeclareAttackersHighlightsGenerator(_pendingAttackers, _declaredAttackers, GetAllAttackerIds, () => _currentVariant, _pendingAttackersCommonQuarryIds);
		AttackerCost attackerCost = _uiManager.AttackerCost;
		attackerCost.HoverStateChanged = (Action<AttackerCost, AttackerCost.HoverState>)Delegate.Combine(attackerCost.HoverStateChanged, new Action<AttackerCost, AttackerCost.HoverState>(OnHoverStateChanged));
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			return clickableWorkflow.CanClick(entity, clickType);
		}
		if (_submitted)
		{
			return false;
		}
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (!TryGetAttackersForId(entity.InstanceId, out var _))
		{
			return IsPendingDamageRecipient(entity.InstanceId);
		}
		return true;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			clickableWorkflow.OnClick(entity, clickType);
			return;
		}
		uint instanceId = entity.InstanceId;
		Attacker declaredAttack;
		List<Attacker> attackers;
		if (IsPendingDamageRecipient(instanceId))
		{
			if (WorkflowBase.TryRerouteClick(instanceId, _battlefield, isSelecting: true, out var reroutedEntityView) && IsPendingDamageRecipient(reroutedEntityView.InstanceId))
			{
				entity = reroutedEntityView;
				instanceId = reroutedEntityView.InstanceId;
			}
			foreach (KeyValuePair<uint, Attacker> pendingAttacker in _pendingAttackers)
			{
				pendingAttacker.Value.SelectedDamageRecipient = GetDamageRecipientForAttacker(pendingAttacker.Value, instanceId);
			}
			UpdateAttackers(_pendingAttackers.Values.ToArray());
		}
		else if (_declaredAttackers.TryGetValue(instanceId, out declaredAttack))
		{
			if (WorkflowBase.TryRerouteClick(instanceId, _battlefield, isSelecting: false, out var reroutedEntityView2) && _declaredAttackers.TryGetValue(reroutedEntityView2.InstanceId, out var value))
			{
				entity = reroutedEntityView2;
				instanceId = reroutedEntityView2.InstanceId;
				declaredAttack = value;
			}
			if (_qualifiedAttackers.TryGetValue(instanceId, out var value2) && value2.Count > 1)
			{
				Attacker[] array = value2.ToArray();
				Array.Sort(array, (Attacker a, Attacker _) => (a.AlternativeGrpId == declaredAttack.AlternativeGrpId) ? 1 : 0);
				_currentVariant = new AttackerSpecification(_confirmWidget, entity as DuelScene_CDC, declaredAttack, array);
				_currentVariant.AttackerSelected += onAttackerSelected;
				_currentVariant.Cancelled += onCancelled;
				_currentVariant.Apply();
				SetHighlights();
				SetDimming();
				SetButtons();
				SetArrows();
			}
			else
			{
				UndeclareAttacker(declaredAttack);
			}
		}
		else if (TryGetAttackersForId(instanceId, out attackers))
		{
			if (IsPendingAttacker(instanceId))
			{
				if (WorkflowBase.TryRerouteClick(instanceId, _battlefield, isSelecting: false, out var reroutedEntityView3) && IsPendingAttacker(reroutedEntityView3.InstanceId))
				{
					entity = reroutedEntityView3;
					instanceId = reroutedEntityView3.InstanceId;
				}
				if (_pendingAttackers.TryGetValue(instanceId, out var value3))
				{
					UnpendAttacker(value3);
				}
			}
			else if (attackers.Count == 1)
			{
				if (WorkflowBase.TryRerouteClick(instanceId, _battlefield, isSelecting: true, out var reroutedEntityView4) && TryGetAttackersForId(reroutedEntityView4.InstanceId, out var attackers2) && attackers2.Count == 1)
				{
					entity = reroutedEntityView4;
					instanceId = reroutedEntityView4.InstanceId;
					attackers = attackers2;
				}
				if (attackers[0].LegalDamageRecipients.Count > 1)
				{
					PendAttacker(attackers[0]);
					if (PlatformUtils.IsHandheld())
					{
						ShowAssignTargetsPrompt(showNotice: false);
					}
				}
				else
				{
					DeclareAttacker(attackers[0], attackers[0].LegalDamageRecipients[0]);
				}
			}
			else
			{
				if (WorkflowBase.TryRerouteClick(instanceId, _battlefield, isSelecting: true, out var reroutedEntityView5) && TryGetAttackersForId(reroutedEntityView5.InstanceId, out var attackers3))
				{
					entity = reroutedEntityView5;
					instanceId = reroutedEntityView5.InstanceId;
					attackers = attackers3;
				}
				_currentVariant = new AttackerSpecification(_confirmWidget, entity as DuelScene_CDC, _declaredAttackers.ContainsKey(instanceId) ? _declaredAttackers[instanceId] : null, attackers.ToArray());
				_currentVariant.AttackerSelected += onAttackerSelected2;
				_currentVariant.Cancelled += onCancelled2;
				_currentVariant.Apply();
				SetHighlights();
				SetDimming();
				SetButtons();
				SetArrows();
			}
		}
		UpdateHighlightsAndDimming();
		SetArrows();
		void closeVariant()
		{
			_currentVariant?.Cleanup();
			_currentVariant = null;
			SetHighlights();
			SetDimming();
			SetButtons();
			SetArrows();
		}
		void closeVariant2()
		{
			_currentVariant?.Cleanup();
			_currentVariant = null;
			SetHighlights();
			SetDimming();
			SetButtons();
			SetArrows();
		}
		void onAttackerSelected(Attacker selectedAttacker)
		{
			closeVariant();
			if (selectedAttacker == declaredAttack)
			{
				UndeclareAttacker(selectedAttacker);
			}
			else
			{
				DeclareAttacker(selectedAttacker, declaredAttack.SelectedDamageRecipient);
			}
		}
		void onAttackerSelected2(Attacker selectedAttacker)
		{
			closeVariant2();
			if (selectedAttacker.LegalDamageRecipients.Count > 1)
			{
				PendAttacker(selectedAttacker);
				UpdateHighlightsAndDimming();
				SetArrows();
			}
			else
			{
				DeclareAttacker(selectedAttacker, selectedAttacker.LegalDamageRecipients[0]);
			}
		}
		void onCancelled()
		{
			closeVariant();
		}
		void onCancelled2()
		{
			closeVariant2();
		}
	}

	private bool TryGetAttackersForId(uint id, out List<Attacker> attackers)
	{
		if (_attackers.TryGetValue(id, out attackers))
		{
			return true;
		}
		if (_qualifiedAttackers.TryGetValue(id, out attackers))
		{
			return true;
		}
		attackers = null;
		return false;
	}

	public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
	{
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			return clickableWorkflow.CanClickStack(entity, clickType);
		}
		if (_submitted)
		{
			return false;
		}
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		List<Attacker> attackers;
		return TryGetAttackersForId(entity.parentInstanceId, out attackers);
	}

	public void OnClickStack(CdcStackCounterView entity)
	{
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			clickableWorkflow.OnClickStack(entity);
			return;
		}
		uint parentInstanceId = entity.parentInstanceId;
		List<Attacker> attackers;
		if (_declaredAttackers.TryGetValue(parentInstanceId, out var value))
		{
			if (_request.HasRequirements || _request.HasRestrictions)
			{
				value.SelectedDamageRecipient = null;
				UpdateAttackers(value);
				if (_pendingAttackers.Count > 0)
				{
					PendAttacker(value);
				}
			}
			else
			{
				List<Attacker> attackersFromBattlefieldStack = GetAttackersFromBattlefieldStack(parentInstanceId);
				foreach (Attacker item in attackersFromBattlefieldStack)
				{
					item.SelectedDamageRecipient = null;
				}
				UpdateAttackers(attackersFromBattlefieldStack.ToArray());
				if (_pendingAttackers.Count > 0)
				{
					foreach (Attacker item2 in attackersFromBattlefieldStack)
					{
						PendAttacker(item2);
					}
				}
			}
		}
		else if (TryGetAttackersForId(parentInstanceId, out attackers) && !IsPendingAttacker(parentInstanceId))
		{
			if (attackers.Count == 1 && attackers[0].LegalDamageRecipients.Count == 1)
			{
				List<Attacker> attackersFromBattlefieldStack2 = GetAttackersFromBattlefieldStack(parentInstanceId);
				foreach (Attacker item3 in attackersFromBattlefieldStack2)
				{
					item3.SelectedDamageRecipient = item3.LegalDamageRecipients[0];
				}
				UpdateAttackers(attackersFromBattlefieldStack2.ToArray());
			}
			else
			{
				foreach (Attacker item4 in GetAttackersFromBattlefieldStack(parentInstanceId))
				{
					_pendingAttackers.Add(item4.AttackerInstanceId, item4);
				}
				UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
			}
		}
		UpdateHighlightsAndDimming();
		SetArrows();
	}

	public void OnBattlefieldClick()
	{
		if (_currentVariant is IClickableWorkflow clickableWorkflow)
		{
			clickableWorkflow.OnBattlefieldClick();
			return;
		}
		_pendingAttackers.Clear();
		UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		UpdateHighlightsAndDimming();
		SetArrows();
		base.SetButtons();
	}

	public bool CanCommenceDrag(IEntityView beginningEntityView)
	{
		if (PlatformUtils.IsHandheld())
		{
			return false;
		}
		if (_submitted)
		{
			return false;
		}
		if (beginningEntityView == null)
		{
			return false;
		}
		if (TryGetAttackersForId(beginningEntityView.InstanceId, out var _))
		{
			return !_pendingAttackers.ContainsKey(beginningEntityView.InstanceId);
		}
		return false;
	}

	public void OnDragCommenced(IEntityView beginningEntityView)
	{
		OnClick(beginningEntityView, SimpleInteractionType.Primary);
	}

	public bool CanCompleteDrag(IEntityView endingEntityView)
	{
		if (PlatformUtils.IsHandheld())
		{
			return false;
		}
		if (_submitted)
		{
			return false;
		}
		if (endingEntityView == null)
		{
			return false;
		}
		return IsPendingDamageRecipient(endingEntityView.InstanceId);
	}

	public void OnDragCompleted(IEntityView beginningEntityView, IEntityView endingEntityView)
	{
		if (endingEntityView != null && CanClick(endingEntityView, SimpleInteractionType.Primary))
		{
			OnClick(endingEntityView, SimpleInteractionType.Primary);
			return;
		}
		_pendingAttackers.Clear();
		UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
	}

	public bool CanHandleRequest(BaseUserRequest req)
	{
		return req is DeclareAttackerRequest;
	}

	public bool IsWaitingForRoundTrip()
	{
		return _submitted;
	}

	public void OnRoundTrip(BaseUserRequest req)
	{
		_request = (DeclareAttackerRequest)req;
		foreach (Attacker declaredAttacker in _request.DeclaredAttackers)
		{
			_pendingAttackers.Remove(declaredAttacker.AttackerInstanceId);
		}
		if (_attackWarnings.Count > 0)
		{
			HashSet<uint> hashSet = new HashSet<uint>(_attackWarnings.Keys);
			HashSet<uint> other = new HashSet<uint>(_request.AttackWarnings.Select((AttackWarning x) => x.InstanceId));
			hashSet.ExceptWith(other);
			foreach (uint item in hashSet)
			{
				if (_cardViewProvider.TryGetCardView(item, out var cardView))
				{
					cardView.UpdateCombatWarningIcon(enabled: false);
					_uiManager.TooltipSystem.RemoveDynamicTooltip(cardView.gameObject);
				}
			}
		}
		_submitted = false;
		UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		ApplyInteraction();
	}

	public bool CanCleanupAfterOutboundMessage(ClientToGREMessage message)
	{
		return message.Type == ClientMessageType.SubmitAttackersReq;
	}

	public void OnAutoYieldEnabled()
	{
		if (_currentVariant is IYieldWorkflow yieldWorkflow)
		{
			yieldWorkflow.OnAutoYieldEnabled();
		}
		else if (!_submitted && AppliedState == InteractionAppliedState.Applied && !HasAttackWarnings)
		{
			TrySubmitAttackers();
		}
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		if (_currentVariant is ICardStackWorkflow cardStackWorkflow)
		{
			return cardStackWorkflow.CanStack(lhs, rhs);
		}
		if (_attackWarnings.TryGetValue(lhs.InstanceId, out var value) != _attackWarnings.TryGetValue(rhs.InstanceId, out var value2))
		{
			return false;
		}
		if (!value.ContainSame(value2, AttackWarningTypeComparer))
		{
			return false;
		}
		if (_pendingAttackers.TryGetValue(lhs.InstanceId, out var value3) != _pendingAttackers.TryGetValue(rhs.InstanceId, out var value4))
		{
			return false;
		}
		if (value3 != null && value4 != null && (value3.MustAttack != value4.MustAttack || value3.AlternativeGrpId != value4.AlternativeGrpId))
		{
			return false;
		}
		if (_declaredAttackers.TryGetValue(lhs.InstanceId, out var value5) != _declaredAttackers.TryGetValue(rhs.InstanceId, out var value6))
		{
			return false;
		}
		if (value5?.AlternativeGrpId != value6?.AlternativeGrpId)
		{
			return false;
		}
		return true;
	}

	public override void TryUndo()
	{
	}

	public override void CleanUp()
	{
		_currentVariant?.Cleanup();
		_currentVariant = null;
		foreach (KeyValuePair<uint, List<Attacker>> attacker in _attackers)
		{
			uint key = attacker.Key;
			if (_cardViewProvider.TryGetCardView(key, out var cardView))
			{
				cardView.UpdateCombatIcons(default);
				_uiManager.TooltipSystem.RemoveDynamicTooltip(cardView.gameObject);
			}
		}
		ClearAttackerCollections();
		_pendingAttackers.Clear();
		UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		_uiManager.AttackerCost.Disable();
		AttackerCost attackerCost = _uiManager.AttackerCost;
		attackerCost.HoverStateChanged = (Action<AttackerCost, AttackerCost.HoverState>)Delegate.Remove(attackerCost.HoverStateChanged, new Action<AttackerCost, AttackerCost.HoverState>(OnHoverStateChanged));
		HideAssignTargetsReminders();
		_attackers.Clear();
		_objPool.PushObject(_attackers, tryClear: false);
		_qualifiedAttackers.Clear();
		_objPool.PushObject(_qualifiedAttackers, tryClear: false);
		_pendingAttackers.Clear();
		_objPool.PushObject(_pendingAttackers, tryClear: false);
		_declaredAttackers.Clear();
		_objPool.PushObject(_declaredAttackers, tryClear: false);
		_pendingAttackersCommonQuarryIds.Clear();
		_objPool.PushObject(_pendingAttackersCommonQuarryIds, tryClear: false);
		_idsToDirtyOnRoundtrip.Clear();
		_objPool.PushObject(_idsToDirtyOnRoundtrip, tryClear: false);
		_attackWarnings.Clear();
		_objPool.PushObject(_attackWarnings, tryClear: false);
		base.CleanUp();
	}

	private void ClearAttackerCollections()
	{
		foreach (KeyValuePair<uint, List<Attacker>> attacker in _attackers)
		{
			attacker.Value.Clear();
		}
		_attackers.Clear();
		foreach (KeyValuePair<uint, List<Attacker>> qualifiedAttacker in _qualifiedAttackers)
		{
			qualifiedAttacker.Value.Clear();
		}
		_qualifiedAttackers.Clear();
		_declaredAttackers.Clear();
		_attackWarnings.Clear();
	}

	protected virtual PromptButtonData NoAttackersButton(string buttonText, string buttonSFX)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		return new PromptButtonData
		{
			ButtonText = buttonText,
			ButtonCallback = OnNoAttackersButtonPressed,
			ButtonSFX = buttonSFX,
			Style = ButtonStyle.StyleType.Secondary,
			Enabled = !HasAttackWarnings,
			NextPhase = mtgGameState.NextPhase,
			NextStep = mtgGameState.NextStep
		};
	}

	protected virtual PromptButtonData AttackWithAllButton(string buttonText, string buttonSFX)
	{
		return new PromptButtonData
		{
			ButtonText = buttonText,
			ButtonCallback = OnAttackAllButtonPressed,
			ButtonSFX = buttonSFX,
			ClearsInteractions = false,
			Style = ButtonStyle.StyleType.Main,
			Enabled = true,
			ShowWarningIcon = (HasAttackWarnings || _request.HasRestrictions)
		};
	}

	private PromptButtonData CancelAttacksButton(string buttonText, string buttonSFX)
	{
		return new PromptButtonData
		{
			ButtonText = buttonText,
			ButtonCallback = OnCancelAttacksButtonPressed,
			ButtonSFX = buttonSFX,
			ClearsInteractions = false,
			Style = ButtonStyle.StyleType.Outlined
		};
	}

	protected virtual PromptButtonData SubmitAttacksButton(MTGALocalizedString buttonText, string buttonSFX, bool enabled = true)
	{
		return new PromptButtonData
		{
			ButtonText = buttonText,
			ButtonCallback = OnSubmitAttackersButtonPressed,
			ButtonSFX = buttonSFX,
			Style = ButtonStyle.StyleType.Escalated,
			Enabled = (_request.CanSubmit & enabled),
			ShowWarningIcon = (HasAttackWarnings || _request.HasRestrictions),
			NextPhase = Phase.Combat,
			NextStep = Step.DeclareBlock,
			Tag = ButtonTag.Primary
		};
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		int count = _declaredAttackers.Count;
		if (_currentVariant is IButtonProvider buttonProvider)
		{
			Buttons = buttonProvider.GetButtons();
		}
		else if (count > 0)
		{
			string key = ((count == 1) ? "DuelScene/ClientPrompt/ClientPrompt_Single_Declared_Attacker" : "DuelScene/ClientPrompt/ClientPrompt_Multiple_Declared_Attackers");
			MTGALocalizedString buttonText = new MTGALocalizedString
			{
				Key = key,
				Parameters = new Dictionary<string, string> { 
				{
					"numAttackers",
					count.ToString()
				} }
			};
			bool enabled = !_request.AttackWarnings.Exists((AttackWarning x) => x.Type == AttackWarningType.CannotAttackAlone);
			Buttons.WorkflowButtons.Add(SubmitAttacksButton(buttonText, "sfx_ui_phasebutton_combatphase_attack", enabled));
			Buttons.WorkflowButtons.Add(CancelAttacksButton("DuelScene/ClientPrompt/ClientPrompt_Button_CancelAttack", "sfx_ui_cancel"));
			HideAssignTargetsReminders();
		}
		else
		{
			Buttons.WorkflowButtons.Add(AttackWithAllButton("DuelScene/ClientPrompt/ClientPrompt_Button_AllAttack", "sfx_ui_phasebutton_combatphase_attack"));
			Buttons.WorkflowButtons.Add(NoAttackersButton("DuelScene/ClientPrompt/ClientPrompt_Button_NoAttackers", "sfx_ui_cancel"));
		}
		OnUpdateButtons(Buttons);
	}

	protected override void SetDimming()
	{
		Dimming = new Dimming();
		if (_currentVariant is IDimmingProvider dimmingProvider)
		{
			Dimming = dimmingProvider.GetDimming();
		}
		else
		{
			foreach (uint pendingAttackersCommonQuarryId in _pendingAttackersCommonQuarryIds)
			{
				Dimming.IdToIsDimmed[pendingAttackersCommonQuarryId] = false;
			}
		}
		foreach (KeyValuePair<uint, List<Attacker>> attacker in _attackers)
		{
			Dimming.IdToIsDimmed[attacker.Key] = false;
		}
		Dimming.WorkflowActive = true;
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows = new Arrows();
		Arrows.ClearLines();
		Arrows.ClearCtMLines();
		if (_currentVariant is IArrowProvider arrowProvider)
		{
			Arrows = arrowProvider.GetArrows();
		}
		else if (_pendingAttackers.Count > 0 && !PlatformUtils.IsHandheld())
		{
			foreach (KeyValuePair<uint, Attacker> pendingAttacker in _pendingAttackers)
			{
				uint key = pendingAttacker.Key;
				if ((bool)_cardViewProvider.GetCardView(key))
				{
					Arrows.AddCtMLine(new Arrows.LineData(key));
				}
			}
		}
		OnUpdateArrows(Arrows);
	}

	protected override void ApplyInteractionInternal()
	{
		_currentVariant?.Cleanup();
		_currentVariant = null;
		ClearAttackerCollections();
		foreach (Attacker declaredAttacker in _request.DeclaredAttackers)
		{
			_declaredAttackers[declaredAttacker.AttackerInstanceId] = declaredAttacker;
		}
		foreach (Attacker attacker in _request.Attackers)
		{
			uint attackerInstanceId = attacker.AttackerInstanceId;
			if (_attackers.TryGetValue(attackerInstanceId, out var value))
			{
				value.Add(attacker);
				continue;
			}
			_attackers[attackerInstanceId] = new List<Attacker> { attacker };
		}
		foreach (Attacker qualifiedAttacker in _request.QualifiedAttackers)
		{
			uint attackerInstanceId2 = qualifiedAttacker.AttackerInstanceId;
			if (_qualifiedAttackers.TryGetValue(attackerInstanceId2, out var value2))
			{
				value2.Add(qualifiedAttacker);
				continue;
			}
			_qualifiedAttackers[attackerInstanceId2] = new List<Attacker> { qualifiedAttacker };
		}
		foreach (AttackWarning attackWarning in _request.AttackWarnings)
		{
			uint instanceId = attackWarning.InstanceId;
			if (_attackWarnings.TryGetValue(instanceId, out var value3))
			{
				value3.Add(attackWarning);
				continue;
			}
			_attackWarnings[attackWarning.InstanceId] = new List<AttackWarning> { attackWarning };
		}
		string attackerCostString = GetAttackerCostString();
		if (string.IsNullOrEmpty(attackerCostString))
		{
			_uiManager.AttackerCost.Disable();
		}
		else
		{
			_uiManager.AttackerCost.SetCostText(attackerCostString);
		}
		SetButtons();
		UpdateCombatIcons();
		foreach (AttackWarning attackWarning2 in _request.AttackWarnings)
		{
			if (attackWarning2.Type != AttackWarningType.CannotBeAttackedByMoreThanOne && _cardViewProvider.TryGetCardView(attackWarning2.InstanceId, out var cardView))
			{
				cardView.UpdateCombatWarningIcon(enabled: true);
				string promptText = _promptEngine.GetPromptText((int)attackWarning2.WarningPromptId);
				_uiManager.TooltipSystem.AddDynamicTooltip(cardView.gameObject, new PromptTooltipData(_promptEngine, attackWarning2.WarningPromptId)
				{
					TooltipStyle = TooltipSystem.TooltipStyle.Prompt,
					RelativePosition = TooltipSystem.TooltipPositionAnchor.MiddleRight,
					Text = promptText
				}, new TooltipProperties
				{
					HoverDurationUntilShow = 0f,
					FontSize = 21f,
					Padding = new Vector2(60f, 5f)
				});
			}
		}
		_idsToDirtyOnRoundtrip.Clear();
	}

	private IEnumerable<uint> GetAllAttackerIds()
	{
		_allIdsCache.Clear();
		foreach (KeyValuePair<uint, List<Attacker>> attacker in _attackers)
		{
			uint key = attacker.Key;
			if (_allIdsCache.Add(key))
			{
				yield return key;
			}
		}
		foreach (KeyValuePair<uint, List<Attacker>> qualifiedAttacker in _qualifiedAttackers)
		{
			uint key2 = qualifiedAttacker.Key;
			if (_allIdsCache.Add(key2))
			{
				yield return key2;
			}
		}
		_allIdsCache.Clear();
	}

	private void UpdateAttackers(params Attacker[] attackers)
	{
		_submitted = true;
		_request.UpdateAttacker(attackers);
		foreach (Attacker attacker in attackers)
		{
			_idsToDirtyOnRoundtrip.Add(attacker.AttackerInstanceId);
		}
	}

	private void DeclareAttacker(Attacker attacker, DamageRecipient damageRecipient)
	{
		if (attacker.LegalDamageRecipients.Contains(damageRecipient))
		{
			attacker.SelectedDamageRecipient = damageRecipient;
			UpdateAttackers(attacker);
			_pendingAttackers.Clear();
			UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		}
	}

	private void UndeclareAttacker(Attacker attacker)
	{
		if (_declaredAttackers.Values.Contains(attacker))
		{
			attacker.SelectedDamageRecipient = null;
			UpdateAttackers(attacker);
		}
		if (_pendingAttackers.Count > 0)
		{
			PendAttacker(attacker);
		}
	}

	private void PendAttacker(Attacker attacker)
	{
		if (!_pendingAttackers.ContainsKey(attacker.AttackerInstanceId))
		{
			_pendingAttackers.Add(attacker.AttackerInstanceId, attacker);
			UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		}
	}

	private void UnpendAttacker(Attacker attacker)
	{
		_pendingAttackers.Remove(attacker.AttackerInstanceId);
		UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		if (_pendingAttackers.Count() < 1)
		{
			HideAssignTargetsReminders();
		}
	}

	private void UpdateCombatIcons()
	{
		foreach (KeyValuePair<uint, List<Attacker>> attacker in _attackers)
		{
			uint key = attacker.Key;
			if (!_cardViewProvider.TryGetCardView(key, out var cardView))
			{
				continue;
			}
			CombatAttackState combatAttackState = CombatAttackState.None;
			List<Attacker> value2;
			if (_declaredAttackers.TryGetValue(key, out var value))
			{
				combatAttackState = AltGrpIdToAttackingState(value.AlternativeGrpId);
			}
			else if (_qualifiedAttackers.TryGetValue(key, out value2))
			{
				combatAttackState = (CombatAttackState)((int)combatAttackState | (value2.Exists((Attacker x) => x.AlternativeGrpId == 0) ? 1 : 0));
				combatAttackState = (CombatAttackState)((int)combatAttackState | (value2.Exists((Attacker x) => x.AlternativeGrpId == 162) ? 2 : 0));
				combatAttackState = (CombatAttackState)((int)combatAttackState | (value2.Exists((Attacker x) => x.AlternativeGrpId == 261) ? 32 : 0));
			}
			cardView.UpdateCombatIcons(new CombatStateData(combatAttackState));
		}
		static CombatAttackState AltGrpIdToAttackingState(uint altGrpId)
		{
			return altGrpId switch
			{
				0u => CombatAttackState.IsAttacking, 
				162u => CombatAttackState.IsExerting, 
				261u => CombatAttackState.IsEnlisting, 
				_ => CombatAttackState.None, 
			};
		}
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

	private void OnNoAttackersButtonPressed()
	{
		_pendingAttackers.Clear();
		TrySubmitAttackers();
		CleanUp();
	}

	private void OnAttackAllButtonPressed()
	{
		int count = _pendingAttackers.Count;
		_pendingAttackers.Clear();
		if (CanFullyDeclareAllAttackers(_request.Attackers))
		{
			TryDeclareAllAttackers(_request.Attackers[0].LegalDamageRecipients[0]);
		}
		else
		{
			AddAllAttackersToPending(_pendingAttackers, _request.QualifiedAttackers);
			if (PlatformUtils.IsHandheld())
			{
				ShowAssignTargetsPrompt(count == _pendingAttackers.Count);
			}
			UpdatePendingAttackersCommonQuarryIds(_pendingAttackersCommonQuarryIds, _pendingAttackers, _request.AttackWarnings);
		}
		UpdateHighlightsAndDimming();
		SetArrows();
	}

	private void ShowAssignTargetsPrompt(bool showNotice)
	{
		_workflowPrompt.Reset();
		_workflowPrompt.LocKey = "DuelScene/ClientPrompt/ClientPrompt_ChooseDamangeRecipient";
		OnUpdatePrompt(_workflowPrompt);
		if (showNotice)
		{
			_uiManager.ShowButtonNotice("DuelScene/ClientPrompt/ClientPrompt_ChooseDamangeRecipient");
		}
	}

	private void HideAssignTargetsReminders()
	{
		_uiManager.HideButtonNotice();
		_workflowPrompt.Reset();
		_workflowPrompt.GrePrompt = Prompt;
		OnUpdatePrompt(_workflowPrompt);
	}

	public static bool CanFullyDeclareAllAttackers(IReadOnlyCollection<Attacker> allAttackers)
	{
		foreach (Attacker allAttacker in allAttackers)
		{
			if (allAttacker.LegalDamageRecipients.Count != 1)
			{
				return false;
			}
		}
		return true;
	}

	public static void AddAllAttackersToPending(IDictionary<uint, Attacker> pendingAttackers, IReadOnlyCollection<Attacker> allAttackers)
	{
		foreach (Attacker allAttacker in allAttackers)
		{
			if (allAttacker.AlternativeGrpId == 0)
			{
				pendingAttackers[allAttacker.AttackerInstanceId] = allAttacker;
			}
		}
	}

	private void OnCancelAttacksButtonPressed()
	{
		List<Attacker> list = new List<Attacker>();
		foreach (Attacker value in _declaredAttackers.Values)
		{
			value.SelectedDamageRecipient = null;
			list.Add(value);
		}
		Arrows.ClearLines();
		UpdateAttackers(list.ToArray());
	}

	private void OnSubmitAttackersButtonPressed()
	{
		_pendingAttackers.Clear();
		TrySubmitAttackers();
		CleanUp();
	}

	private void TryDeclareAllAttackers(DamageRecipient damageRecipient)
	{
		if (!_submitted)
		{
			_submitted = true;
			_request.DeclareAllAttackers(damageRecipient);
		}
	}

	private void TrySubmitAttackers()
	{
		if (!_submitted)
		{
			_submitted = true;
			_request.SubmitAttackers();
		}
	}

	private string GetAttackerCostString()
	{
		StringBuilder stringBuilder = _objPool.PopObject<StringBuilder>();
		IReadOnlyList<ManaRequirement> manaRequirements = _request.ManaRequirements;
		if (manaRequirements.Count > 0)
		{
			Dictionary<uint, List<uint>> dictionary = new Dictionary<uint, List<uint>>();
			foreach (ManaRequirement item in manaRequirements)
			{
				if (!dictionary.ContainsKey(item.AbilityGrpId))
				{
					dictionary.Add(item.AbilityGrpId, new List<uint>());
				}
				dictionary[item.AbilityGrpId].Add(item.ObjectId);
			}
			string text = ManaUtilities.ConvertToOldSchoolManaText(ManaUtilities.ConvertManaCostsToList(manaRequirements));
			text = ManaUtilities.ConvertManaSymbols(text);
			string localizedText = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/ClientPrompt/AttackerManaCost", ("0", text));
			stringBuilder.AppendLine(localizedText);
			_uiManager.AttackerCost.SetManaCostSource(dictionary);
		}
		if (_request.Attackers.Any((Attacker x) => x.AlternativeGrpId == 261))
		{
			int num = 0;
			foreach (KeyValuePair<uint, Attacker> declaredAttacker in _declaredAttackers)
			{
				if (declaredAttacker.Value.AlternativeGrpId == 261)
				{
					num++;
				}
			}
			string localizedText2 = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/ClientPrompt/EnlistCount", ("count", num.ToString()));
			stringBuilder.AppendLine(localizedText2);
		}
		string result = stringBuilder.ToString();
		stringBuilder.Clear();
		_objPool.PushObject(stringBuilder);
		return result;
	}

	private static uint GetDamageRecipientId(DamageRecipient damageRecipient)
	{
		uint result = 0u;
		switch (damageRecipient.IdCase)
		{
		case DamageRecipient.IdOneofCase.TeamId:
			result = damageRecipient.TeamId;
			break;
		case DamageRecipient.IdOneofCase.PlayerSystemSeatId:
			result = damageRecipient.PlayerSystemSeatId;
			break;
		case DamageRecipient.IdOneofCase.PlaneswalkerInstanceId:
			result = damageRecipient.PlaneswalkerInstanceId;
			break;
		}
		return result;
	}

	private static DamageRecipient GetDamageRecipientForAttacker(Attacker attacker, uint entityId)
	{
		foreach (DamageRecipient legalDamageRecipient in attacker.LegalDamageRecipients)
		{
			if ((legalDamageRecipient.Type == DamageRecType.Player && legalDamageRecipient.PlayerSystemSeatId == entityId) || (legalDamageRecipient.Type == DamageRecType.PlanesWalker && legalDamageRecipient.PlaneswalkerInstanceId == entityId))
			{
				return legalDamageRecipient;
			}
		}
		return null;
	}

	private bool IsPendingAttacker(uint entityId)
	{
		return _pendingAttackers.ContainsKey(entityId);
	}

	private bool IsPendingDamageRecipient(uint entityId)
	{
		return _pendingAttackersCommonQuarryIds.Contains(entityId);
	}

	private List<Attacker> GetAttackersFromBattlefieldStack(uint parentId)
	{
		List<Attacker> list = new List<Attacker>();
		DuelScene_CDC cardView = _cardViewProvider.GetCardView(parentId);
		foreach (DuelScene_CDC allCard in _battlefield.GetStackForCard(cardView).AllCards)
		{
			if (!TryGetAttackersForId(allCard.InstanceId, out var attackers))
			{
				continue;
			}
			if (attackers.Count == 1)
			{
				list.Add(attackers[0]);
			}
			else if (attackers.Count > 1)
			{
				Attacker attacker = attackers.Find((Attacker x) => x.AlternativeGrpId == 0);
				if (attacker != null)
				{
					list.Add(attacker);
				}
				else
				{
					list.Add(attackers[0]);
				}
			}
		}
		return list;
	}

	public static void UpdatePendingAttackersCommonQuarryIds(HashSet<uint> commonQuarryIds, IReadOnlyDictionary<uint, Attacker> pendingAttackers, IEnumerable<AttackWarning> attackWarnings)
	{
		commonQuarryIds.Clear();
		foreach (KeyValuePair<uint, Attacker> pendingAttacker in pendingAttackers)
		{
			IEnumerable<uint> other = pendingAttacker.Value.LegalDamageRecipients.Select(GetDamageRecipientId);
			if (commonQuarryIds.Count == 0)
			{
				commonQuarryIds.UnionWith(other);
			}
			else
			{
				commonQuarryIds.IntersectWith(other);
			}
		}
		if (pendingAttackers.Count <= 1)
		{
			return;
		}
		foreach (AttackWarning item in attackWarnings ?? Array.Empty<AttackWarning>())
		{
			if (item.Type == AttackWarningType.CannotBeAttackedByMoreThanOne)
			{
				commonQuarryIds.Remove(item.InstanceId);
			}
		}
	}
}
