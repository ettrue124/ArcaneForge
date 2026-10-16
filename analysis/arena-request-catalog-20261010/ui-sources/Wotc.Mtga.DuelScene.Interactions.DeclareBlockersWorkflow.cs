using System.Collections.Generic;
using System.Text;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using UnityEngine;
using Wizards.Mtga.Platforms;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class DeclareBlockersWorkflow : WorkflowBase<DeclareBlockersRequest>, IRoundTripWorkflow, IClickableWorkflow, IDraggableWorkflow, ICardStackWorkflow, IYieldWorkflow
{
	private class AssignBlockersHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyDictionary<uint, Blocker> _allBlockers;

		private readonly IReadOnlyCollection<uint> _pendingBlockerIds;

		private readonly IReadOnlyCollection<uint> _pendingBlockerCommonAttackerIds;

		public AssignBlockersHighlightsGenerator(IReadOnlyDictionary<uint, Blocker> allBlockers, IReadOnlyCollection<uint> pendingBlockerIds, IReadOnlyCollection<uint> pendingBlockerCommonAttackerIds)
		{
			_allBlockers = allBlockers;
			_pendingBlockerIds = pendingBlockerIds;
			_pendingBlockerCommonAttackerIds = pendingBlockerCommonAttackerIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint pendingBlockerCommonAttackerId in _pendingBlockerCommonAttackerIds)
			{
				highlights.IdToHighlightType_Workflow[pendingBlockerCommonAttackerId] = HighlightType.Hot;
			}
			foreach (KeyValuePair<uint, Blocker> allBlocker in _allBlockers)
			{
				uint key = allBlocker.Key;
				if (allBlocker.Value.SelectedAttackerInstanceIds.Count > 0)
				{
					highlights.IdToHighlightType_Workflow[key] = HighlightType.Selected;
					continue;
				}
				if (_pendingBlockerIds.Contains(key))
				{
					highlights.IdToHighlightType_Workflow[key] = HighlightType.Selected;
					continue;
				}
				highlights.IdToHighlightType_Workflow[key] = HighlightType.Hot;
				if (PlatformUtils.GetCurrentDeviceType() == DeviceType.Handheld && _pendingBlockerIds.Count > 0)
				{
					highlights.IdToHighlightType_Workflow[key] = HighlightType.None;
				}
			}
			return highlights;
		}
	}

	private static readonly IEqualityComparer<BlockWarning> BlockWarningTypeComparer = new LambdaEqualityComparer<BlockWarning>((BlockWarning warningA, BlockWarning warningB) => warningA.Type.Equals(warningB.Type), (BlockWarning warning) => (int)warning.Type);

	private readonly IObjectPool _objPool;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IPromptEngine _promptEngine;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBattlefieldCardHolder _battlefieldCardHolder;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly UIManager _uiManager;

	private readonly Dictionary<uint, List<BlockWarning>> _blockWarnings;

	private readonly Dictionary<uint, Blocker> _allBlockers;

	private readonly HashSet<uint> _pendingBlockerIds;

	private readonly HashSet<uint> _pendingBlockersCommonAttackerIds;

	private bool _submitted;

	public IReadOnlyDictionary<uint, Blocker> AllBlockers => _allBlockers;

	public IReadOnlyCollection<uint> PendingBlockerIds => _pendingBlockerIds;

	public DeclareBlockersWorkflow(DeclareBlockersRequest request, IObjectPool objectPool, IClientLocProvider clientLocProvider, IPromptEngine promptEngine, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBattlefieldCardHolder battlefieldCardHolder, UIManager uiManager)
		: base(request)
	{
		_objPool = objectPool ?? NullObjectPool.Default;
		_clientLocProvider = clientLocProvider ?? NullLocProvider.Default;
		_promptEngine = promptEngine ?? NullPromptEngine.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewManager.Default;
		_battlefieldCardHolder = battlefieldCardHolder;
		_uiManager = uiManager;
		_blockWarnings = _objPool.PopObject<Dictionary<uint, List<BlockWarning>>>();
		_allBlockers = _objPool.PopObject<Dictionary<uint, Blocker>>();
		_pendingBlockerIds = _objPool.PopObject<HashSet<uint>>();
		_pendingBlockersCommonAttackerIds = _objPool.PopObject<HashSet<uint>>();
		SetUpBlockers();
		_highlightsGenerator = new AssignBlockersHighlightsGenerator(_allBlockers, _pendingBlockerIds, _pendingBlockersCommonAttackerIds);
	}

	public bool IsWaitingForRoundTrip()
	{
		return _submitted;
	}

	public bool CanHandleRequest(BaseUserRequest req)
	{
		return req is DeclareBlockersRequest;
	}

	public void OnRoundTrip(BaseUserRequest req)
	{
		CleanupBlockWarningTooltips();
		_request = req as DeclareBlockersRequest;
		_submitted = false;
		SetUpBlockers();
		ApplyInteraction();
	}

	public bool CanCleanupAfterOutboundMessage(ClientToGREMessage messsage)
	{
		return messsage.Type == ClientMessageType.SubmitBlockersReq;
	}

	private void SetUpBlockers()
	{
		_allBlockers.Clear();
		foreach (Blocker allBlocker in _request.AllBlockers)
		{
			_allBlockers[allBlocker.BlockerInstanceId] = allBlocker;
		}
		UpdatePendingBlockersCommonAttackerIds();
	}

	private uint OnBlockerClicked(uint blockerId)
	{
		if (_submitted)
		{
			return blockerId;
		}
		if (!_allBlockers.ContainsKey(blockerId))
		{
			return blockerId;
		}
		if (_allBlockers[blockerId].SelectedAttackerInstanceIds.Count > 0)
		{
			if (WorkflowBase.TryRerouteClick(blockerId, _battlefieldCardHolder, isSelecting: false, out var reroutedEntityView) && _allBlockers.TryGetValue(reroutedEntityView.InstanceId, out var value) && value.SelectedAttackerInstanceIds.Count > 0)
			{
				blockerId = reroutedEntityView.InstanceId;
			}
			if (_pendingBlockerIds.Remove(blockerId))
			{
				UpdatePendingBlockersCommonAttackerIds();
			}
			Blocker blocker = _allBlockers[blockerId];
			blocker.SelectedAttackerInstanceIds.Clear();
			_request.UpdateBlockers(blocker);
			_submitted = true;
			AudioManager.PlayAudio(WwiseEvents.sfx_combat_declare_no_defenders.EventName, AudioManager.Default);
			if (_pendingBlockerIds.Count > 0 && _pendingBlockerIds.Add(blockerId))
			{
				UpdatePendingBlockersCommonAttackerIds();
			}
		}
		else if (_pendingBlockerIds.Contains(blockerId))
		{
			if (WorkflowBase.TryRerouteClick(blockerId, _battlefieldCardHolder, isSelecting: false, out var reroutedEntityView2) && _pendingBlockerIds.Contains(reroutedEntityView2.InstanceId))
			{
				blockerId = reroutedEntityView2.InstanceId;
			}
			if (_pendingBlockerIds.Remove(blockerId))
			{
				UpdatePendingBlockersCommonAttackerIds();
			}
		}
		else
		{
			if (WorkflowBase.TryRerouteClick(blockerId, _battlefieldCardHolder, isSelecting: true, out var reroutedEntityView3) && _allBlockers.TryGetValue(reroutedEntityView3.InstanceId, out var value2) && value2.SelectedAttackerInstanceIds.Count == 0)
			{
				blockerId = reroutedEntityView3.InstanceId;
			}
			if (_pendingBlockerIds.Add(blockerId))
			{
				UpdatePendingBlockersCommonAttackerIds();
			}
			AudioManager.PlayAudio(WwiseEvents.sfx_combat_declare_defenders.EventName, AudioManager.Default);
		}
		UpdateHighlightsAndDimming();
		SetArrows();
		return blockerId;
	}

	private uint OnAttackerClicked(uint attackerId)
	{
		if (_submitted)
		{
			return attackerId;
		}
		if (WorkflowBase.TryRerouteClick(attackerId, _battlefieldCardHolder, isSelecting: true, out var reroutedEntityView))
		{
			attackerId = reroutedEntityView.InstanceId;
		}
		List<Blocker> list = _objPool.PopObject<List<Blocker>>();
		List<uint> list2 = _objPool.PopObject<List<uint>>();
		foreach (uint pendingBlockerId in _pendingBlockerIds)
		{
			Blocker blocker = _allBlockers[pendingBlockerId];
			if (blocker.AttackerInstanceIds.Contains(attackerId))
			{
				blocker.SelectedAttackerInstanceIds.Add(attackerId);
			}
			list.Add(blocker);
			if (blocker.SelectedAttackerInstanceIds.Count == blocker.MaxAttackers)
			{
				list2.Add(blocker.BlockerInstanceId);
			}
		}
		foreach (uint item in list2)
		{
			_pendingBlockerIds.Remove(item);
		}
		if (list.Count > 0)
		{
			_request.UpdateBlockers(list.ToArray());
			_submitted = true;
		}
		UpdatePendingBlockersCommonAttackerIds();
		UpdateHighlightsAndDimming();
		SetArrows();
		list.Clear();
		_objPool.PushObject(list, tryClear: false);
		list2.Clear();
		_objPool.PushObject(list2, tryClear: false);
		return attackerId;
	}

	private void ClearPendingBlockers()
	{
		_pendingBlockerIds.Clear();
		UpdatePendingBlockersCommonAttackerIds();
		UpdateHighlightsAndDimming();
		SetArrows();
	}

	public override void CleanUp()
	{
		CleanupBlockWarningTooltips();
		_blockWarnings.Clear();
		_allBlockers.Clear();
		_pendingBlockerIds.Clear();
		_pendingBlockersCommonAttackerIds.Clear();
		_objPool.PushObject(_blockWarnings, tryClear: false);
		_objPool.PushObject(_allBlockers, tryClear: false);
		_objPool.PushObject(_pendingBlockerIds, tryClear: false);
		_objPool.PushObject(_pendingBlockersCommonAttackerIds, tryClear: false);
		base.CleanUp();
	}

	protected virtual PromptButtonData SubmitBlockersButton(MTGALocalizedString buttonText, string buttonSFX, ButtonStyle.StyleType buttonStyle)
	{
		PromptTooltipData promptTooltipData = null;
		if (_request.BlockWarnings.Count > 0)
		{
			BlockWarning blockWarning = _request.BlockWarnings[0];
			string promptText = _promptEngine.GetPromptText((int)blockWarning.WarningPromptId);
			promptTooltipData = new PromptTooltipData(_promptEngine, blockWarning.WarningPromptId)
			{
				TooltipStyle = TooltipSystem.TooltipStyle.Prompt,
				RelativePosition = TooltipSystem.TooltipPositionAnchor.MiddleRight,
				Text = promptText
			};
		}
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		return new PromptButtonData
		{
			ButtonText = buttonText,
			ButtonCallback = OnSubmitBlockersButtonPressed,
			ButtonSFX = buttonSFX,
			Style = buttonStyle,
			Enabled = (promptTooltipData == null),
			ShowWarningIcon = (promptTooltipData != null),
			TooltipData = promptTooltipData,
			NextPhase = mtgGameState.NextPhase,
			NextStep = mtgGameState.NextStep,
			Tag = ButtonTag.Primary
		};
	}

	protected virtual PromptButtonData CancelBlocksButton(string buttonText, string buttonSfx)
	{
		return new PromptButtonData
		{
			ButtonText = buttonText,
			ButtonCallback = OnCancelBlocksButtonPressed,
			ButtonSFX = buttonSfx,
			ClearsInteractions = false,
			Style = ButtonStyle.StyleType.Outlined,
			Tag = ButtonTag.Secondary
		};
	}

	public override void TryUndo()
	{
	}

	protected override void ApplyInteractionInternal()
	{
		_blockWarnings.Clear();
		foreach (BlockWarning blockWarning in _request.BlockWarnings)
		{
			if (!_blockWarnings.TryGetValue(blockWarning.InstanceId, out var value))
			{
				value = (_blockWarnings[blockWarning.InstanceId] = new List<BlockWarning>(1));
			}
			value.Add(blockWarning);
		}
		SetButtons();
		foreach (BlockWarning blockWarning2 in _request.BlockWarnings)
		{
			if (_cardViewProvider.TryGetCardView(blockWarning2.InstanceId, out var cardView))
			{
				cardView.UpdateCombatWarningIcon(enabled: true);
				string promptText = _promptEngine.GetPromptText((int)blockWarning2.WarningPromptId);
				_uiManager.TooltipSystem.AddDynamicTooltip(cardView.gameObject, new PromptTooltipData(_promptEngine, blockWarning2.WarningPromptId)
				{
					TooltipStyle = TooltipSystem.TooltipStyle.Prompt,
					RelativePosition = TooltipSystem.TooltipPositionAnchor.MiddleRight,
					Text = promptText
				}, new TooltipProperties
				{
					HoverDurationUntilShow = 0f,
					FontSize = 21f,
					Padding = new Vector2(40f, 5f)
				});
			}
		}
		SetupManaCostUI();
	}

	private void OnCancelBlocksButtonPressed()
	{
		if (_submitted)
		{
			return;
		}
		_submitted = true;
		Arrows.ClearLines();
		_pendingBlockerIds.Clear();
		UpdatePendingBlockersCommonAttackerIds();
		List<Blocker> list = _objPool.PopObject<List<Blocker>>();
		foreach (KeyValuePair<uint, Blocker> allBlocker in _allBlockers)
		{
			if (allBlocker.Value.SelectedAttackerInstanceIds.Count > 0)
			{
				allBlocker.Value.SelectedAttackerInstanceIds.Clear();
				list.Add(allBlocker.Value);
			}
		}
		_request.UpdateBlockers(list.ToArray());
		list.Clear();
		_objPool.PushObject(list, tryClear: false);
	}

	private void OnSubmitBlockersButtonPressed()
	{
		if (!_submitted)
		{
			_submitted = true;
			CleanupBlockWarningTooltips();
			_request.SubmitBlockers();
		}
	}

	private void CleanupBlockWarningTooltips()
	{
		foreach (BlockWarning blockWarning in _request.BlockWarnings)
		{
			if (_cardViewProvider.TryGetCardView(blockWarning.InstanceId, out var cardView))
			{
				cardView.UpdateCombatWarningIcon(enabled: false);
				_uiManager.TooltipSystem.RemoveDynamicTooltip(cardView.gameObject);
			}
		}
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_submitted)
		{
			return false;
		}
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (!_allBlockers.ContainsKey(entity.InstanceId))
		{
			return _pendingBlockersCommonAttackerIds.Contains(entity.InstanceId);
		}
		return true;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		_ = entity.InstanceId;
		if (_allBlockers.ContainsKey(entity.InstanceId))
		{
			OnBlockerClicked(entity.InstanceId);
		}
		else if (_pendingBlockerIds.Count > 0)
		{
			OnAttackerClicked(entity.InstanceId);
		}
		UpdateHighlightsAndDimming();
		SetArrows();
	}

	public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
	{
		if (_submitted)
		{
			return false;
		}
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		IBattlefieldStack stackForInstanceId = _battlefieldCardHolder.GetStackForInstanceId(entity.parentInstanceId);
		if (stackForInstanceId == null)
		{
			return false;
		}
		if (stackForInstanceId.HasAttachmentOrExile)
		{
			return false;
		}
		if (!_allBlockers.ContainsKey(entity.parentInstanceId))
		{
			return _pendingBlockersCommonAttackerIds.Contains(entity.parentInstanceId);
		}
		return true;
	}

	public void OnClickStack(CdcStackCounterView entity)
	{
		IBattlefieldStack stackForInstanceId = _battlefieldCardHolder.GetStackForInstanceId(entity.parentInstanceId);
		if (stackForInstanceId.StackParent.Model.ControllerNum == GREPlayerNum.LocalPlayer)
		{
			if (_allBlockers.TryGetValue(entity.parentInstanceId, out var value) && value.SelectedAttackerInstanceIds.Count > 0)
			{
				List<Blocker> list = _objPool.PopObject<List<Blocker>>();
				foreach (DuelScene_CDC allCard in stackForInstanceId.AllCards)
				{
					Blocker blocker = _allBlockers[allCard.InstanceId];
					blocker.SelectedAttackerInstanceIds.Clear();
					list.Add(blocker);
				}
				_request.UpdateBlockers(list.ToArray());
				_submitted = true;
				AudioManager.PlayAudio(WwiseEvents.sfx_combat_declare_no_defenders.EventName, AudioManager.Default);
				list.Clear();
				_objPool.PushObject(list, tryClear: false);
				if (_pendingBlockerIds.Count > 0)
				{
					foreach (DuelScene_CDC allCard2 in stackForInstanceId.AllCards)
					{
						_pendingBlockerIds.Add(allCard2.InstanceId);
					}
					UpdatePendingBlockersCommonAttackerIds();
				}
			}
			else if (_pendingBlockerIds.Contains(entity.parentInstanceId))
			{
				foreach (DuelScene_CDC allCard3 in stackForInstanceId.AllCards)
				{
					_pendingBlockerIds.Remove(allCard3.InstanceId);
				}
			}
			else
			{
				foreach (DuelScene_CDC allCard4 in stackForInstanceId.AllCards)
				{
					_pendingBlockerIds.Add(allCard4.InstanceId);
				}
				AudioManager.PlayAudio(WwiseEvents.sfx_combat_declare_defenders.EventName, AudioManager.Default);
			}
		}
		else
		{
			int num = ((stackForInstanceId.AllCards.Count < _pendingBlockerIds.Count) ? stackForInstanceId.AllCards.Count : _pendingBlockerIds.Count);
			List<Blocker> list2 = _objPool.PopObject<List<Blocker>>();
			List<uint> list3 = _objPool.PopObject<List<uint>>();
			List<uint> list4 = _objPool.PopObject<List<uint>>();
			list3.AddRange(_pendingBlockerIds);
			for (int i = 0; i < num; i++)
			{
				Blocker blocker2 = _allBlockers[list3[i]];
				uint instanceId = stackForInstanceId.AllCards[i].InstanceId;
				if (blocker2.AttackerInstanceIds.Contains(instanceId))
				{
					blocker2.SelectedAttackerInstanceIds.Add(instanceId);
				}
				else
				{
					blocker2.SelectedAttackerInstanceIds.Remove(instanceId);
				}
				list2.Add(blocker2);
				if (blocker2.SelectedAttackerInstanceIds.Count == blocker2.MaxAttackers)
				{
					list4.Add(blocker2.BlockerInstanceId);
				}
			}
			foreach (uint item in list4)
			{
				_pendingBlockerIds.Remove(item);
			}
			if (list2.Count > 0)
			{
				_request.UpdateBlockers(list2.ToArray());
				_submitted = true;
			}
			list2.Clear();
			list3.Clear();
			list4.Clear();
			_objPool.PushObject(list2, tryClear: false);
			_objPool.PushObject(list3, tryClear: false);
			_objPool.PushObject(list4, tryClear: false);
		}
		UpdatePendingBlockersCommonAttackerIds();
		UpdateHighlightsAndDimming();
		SetArrows();
	}

	public void OnBattlefieldClick()
	{
		ClearPendingBlockers();
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
		if (_allBlockers.ContainsKey(beginningEntityView.InstanceId))
		{
			return !_pendingBlockerIds.Contains(beginningEntityView.InstanceId);
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
		return _pendingBlockersCommonAttackerIds.Contains(endingEntityView.InstanceId);
	}

	public void OnDragCompleted(IEntityView beginningEntityView, IEntityView endingEntityView)
	{
		if (endingEntityView != null && CanClick(endingEntityView, SimpleInteractionType.Primary))
		{
			OnClick(endingEntityView, SimpleInteractionType.Primary);
		}
		else
		{
			ClearPendingBlockers();
		}
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		int num = 0;
		foreach (KeyValuePair<uint, Blocker> allBlocker in _allBlockers)
		{
			if (allBlocker.Value.SelectedAttackerInstanceIds.Count > 0)
			{
				num++;
			}
		}
		if (num > 0)
		{
			MTGALocalizedString buttonText = new MTGALocalizedString
			{
				Key = ((num == 1) ? "DuelScene/ClientPrompt/ClientPrompt_Button_Single_Blocker" : "DuelScene/ClientPrompt/ClientPrompt_Button_Multiple_Blockers"),
				Parameters = new Dictionary<string, string> { 
				{
					"numBlockers",
					num.ToString()
				} }
			};
			Buttons.WorkflowButtons.Add(SubmitBlockersButton(buttonText, WwiseEvents.sfx_ui_submit.EventName, ButtonStyle.StyleType.Escalated));
			Buttons.WorkflowButtons.Add(CancelBlocksButton("DuelScene/ClientPrompt/ClientPrompt_Button_CancelBlock", WwiseEvents.sfx_ui_cancel.EventName));
		}
		else
		{
			PromptButtonData item = SubmitBlockersButton("DuelScene/ClientPrompt/ClientPrompt_Button_NoBlockers", WwiseEvents.sfx_ui_phasebutton_combatphase_block.EventName, ButtonStyle.StyleType.Secondary);
			Buttons.WorkflowButtons.Add(item);
		}
		OnUpdateButtons(Buttons);
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed.Clear();
		foreach (uint pendingBlockersCommonAttackerId in _pendingBlockersCommonAttackerIds)
		{
			Dimming.IdToIsDimmed[pendingBlockersCommonAttackerId] = false;
		}
		foreach (KeyValuePair<uint, Blocker> allBlocker in _allBlockers)
		{
			Blocker value = allBlocker.Value;
			Dimming.IdToIsDimmed[value.BlockerInstanceId] = false;
		}
		Dimming.WorkflowActive = true;
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows.ClearLines();
		Arrows.ClearCtMLines();
		if (!PlatformUtils.IsHandheld())
		{
			foreach (uint pendingBlockerId in _pendingBlockerIds)
			{
				Arrows.AddCtMLine(new Arrows.LineData(pendingBlockerId));
			}
		}
		OnUpdateArrows(Arrows);
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		if (_blockWarnings.TryGetValue(lhs.InstanceId, out var value) != _blockWarnings.TryGetValue(rhs.InstanceId, out var value2))
		{
			return false;
		}
		if (!value.ContainSame(value2, BlockWarningTypeComparer))
		{
			return false;
		}
		bool flag = _pendingBlockerIds.Contains(lhs.InstanceId);
		bool flag2 = _pendingBlockerIds.Contains(rhs.InstanceId);
		if (flag != flag2)
		{
			return false;
		}
		if ((flag & flag2) && _allBlockers.TryGetValue(lhs.InstanceId, out var value3) && _allBlockers.TryGetValue(rhs.InstanceId, out var value4) && (value3.MustBlock != value4.MustBlock || value3.MinAttackers != value4.MinAttackers || value3.MaxAttackers != value4.MaxAttackers || !value3.AttackerInstanceIds.ContainSame(value4.AttackerInstanceIds, _objPool) || !value3.SelectedAttackerInstanceIds.ContainSame(value4.SelectedAttackerInstanceIds, _objPool)))
		{
			return false;
		}
		return true;
	}

	public void OnAutoYieldEnabled()
	{
		if (!_submitted && AppliedState == InteractionAppliedState.Applied && _request.BlockWarnings.Count <= 0)
		{
			OnSubmitBlockersButtonPressed();
		}
	}

	private void UpdatePendingBlockersCommonAttackerIds()
	{
		_pendingBlockersCommonAttackerIds.Clear();
		foreach (uint pendingBlockerId in _pendingBlockerIds)
		{
			Blocker blocker = _allBlockers[pendingBlockerId];
			if (_pendingBlockersCommonAttackerIds.Count == 0)
			{
				_pendingBlockersCommonAttackerIds.UnionWith(blocker.AttackerInstanceIds);
			}
			else
			{
				_pendingBlockersCommonAttackerIds.IntersectWith(blocker.AttackerInstanceIds);
			}
		}
	}

	private void SetupManaCostUI()
	{
		string manaCostText = GetManaCostText(_request.ManaCost, _objPool, _clientLocProvider);
		if (string.IsNullOrEmpty(manaCostText))
		{
			_uiManager.AttackerCost.Disable();
			_uiManager.AttackerCost.SetManaCostSource(DictionaryExtensions.Empty<uint, List<uint>>());
		}
		else
		{
			_uiManager.AttackerCost.SetCostText(manaCostText);
			_uiManager.AttackerCost.SetManaCostSource(GetManaCostSource(_request.ManaCost));
		}
	}

	private static string GetManaCostText(IReadOnlyList<ManaRequirement> manaRequirements, IObjectPool objPool, IClientLocProvider locProvider)
	{
		if (manaRequirements.Count == 0)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = objPool.PopObject<StringBuilder>();
		string text = ManaUtilities.ConvertToOldSchoolManaText(ManaUtilities.ConvertManaCostsToList(manaRequirements));
		text = ManaUtilities.ConvertManaSymbols(text);
		string localizedText = locProvider.GetLocalizedText("DuelScene/ClientPrompt/AttackerManaCost", ("0", text));
		stringBuilder.AppendLine(localizedText);
		string result = stringBuilder.ToString();
		stringBuilder.Clear();
		objPool.PushObject(stringBuilder);
		return result;
	}

	private static IReadOnlyDictionary<uint, List<uint>> GetManaCostSource(IReadOnlyList<ManaRequirement> manaRequirements)
	{
		if (manaRequirements.Count == 0)
		{
			return DictionaryExtensions.Empty<uint, List<uint>>();
		}
		Dictionary<uint, List<uint>> dictionary = new Dictionary<uint, List<uint>>();
		foreach (ManaRequirement manaRequirement in manaRequirements)
		{
			uint abilityGrpId = manaRequirement.AbilityGrpId;
			uint objectId = manaRequirement.ObjectId;
			if (dictionary.TryGetValue(abilityGrpId, out var value))
			{
				value.Add(objectId);
				continue;
			}
			dictionary[abilityGrpId] = new List<uint> { objectId };
		}
		return dictionary;
	}
}
