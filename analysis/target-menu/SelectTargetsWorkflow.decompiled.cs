using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizards.Mtga.Platforms;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.DuelScene.Interactions.SelectTargets;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class SelectTargetsWorkflow : SelectCardsWorkflow<SelectTargetsRequest>, IRoundTripWorkflow, IClickableWorkflow, ICardStackWorkflow, ITargetCDCListProviderWorkflow, ISecondaryLayoutIdListProvider, IKeybindingWorkflow
{
	private readonly StartingZoneIdCalculator _startingZoneIdCalculator = new StartingZoneIdCalculator();

	private readonly IObjectPool _objPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardTitleProvider _cardTitleProvider;

	private readonly IAbilityDataProvider _abilityProvider;

	private readonly IGameplaySettingsProvider _gameplaySettingsProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly IAutoTargetingSolution _autoTargetingSolution;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<ICardHolder> _browserCardHolder;

	private bool _submitted;

	private bool _undoSent;

	private int _targetSelectionIndex;

	private readonly Stack<int> _undoIndexStack;

	private uint _currentZoneId = uint.MaxValue;

	private List<MtgZone> _involvedZones = new List<MtgZone>();

	private Dictionary<uint, List<DuelScene_CDC>> _cardViewsByZoneId = new Dictionary<uint, List<DuelScene_CDC>>();

	private readonly List<KeyValuePair<int, int>> _promptIndexNofCount = new List<KeyValuePair<int, int>>();

	private DuelScene_CDC _lastBrowserSelection;

	private bool _coldHighlightSelectionConfirmed;

	private string _promptOverrideKey;

	private (string, string)[] _promptOverrideParams;

	private List<uint> _initialTargetIdList = new List<uint>();

	private IReadOnlyList<TargetSelection> _initialTargetSelections;

	private SelectCardsBrowser _stackBrowser;

	private SelectCardsBrowser_MultiZone _multiZoneBrowser;

	private BrowserType _currentBrowserType;

	private string _browserCardHolderLayoutKey = string.Empty;

	private const string _multiZoneLayoutKey = "MultiZone";

	private const string _stackLayoutKey = "Stack";

	private readonly List<uint> _hotTargetIds = new List<uint>();

	private readonly List<DuelScene_CDC> _targetCDCs = new List<DuelScene_CDC>();

	private TargetSelection _currentTargetSelection => _request.TargetSelections[_targetSelectionIndex];

	public SelectCardsBrowser StackBrowser => _stackBrowser;

	public SelectTargetsWorkflow(SelectTargetsRequest request, IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettingsProvider, ICardHolderProvider cardHolderProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider, ICardViewProvider cardViewProvider, IPromptTextProvider promptTextProvider, IPlayWarningManager playWarningManager, IAutoTargetingSolution autoTargetingProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_objPool = objectPool ?? NullObjectPool.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_cardTitleProvider = _cardDatabase.CardTitleProvider;
		_abilityProvider = _cardDatabase.AbilityDataProvider;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_gameplaySettingsProvider = gameplaySettingsProvider ?? NullGameplaySettingsProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_headerTextProvider = headerTextProvider;
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_browserCardHolder = CardHolderReference<ICardHolder>.DefaultBrowser(cardHolderProvider);
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_autoTargetingSolution = autoTargetingProvider ?? new NullAutoTargetingSolution();
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
		_assetLookupSystem = assetLookupSystem;
		_initialTargetSelections = new List<TargetSelection>(request.TargetSelections);
		_undoIndexStack = _objPool.PopObject<Stack<int>>();
		InitPromptIndexNofCountList();
		SetTargetSelectionIndex(0);
		_highlightsGenerator = new SelectTargetsHighlightsGenerator(() => _targetSelectionIndex, () => TargetSelections(), _gameStateProvider);
	}

	private void InitPromptIndexNofCountList()
	{
		List<List<int>> list = new List<List<int>>();
		for (int i = 0; i < _request.TargetSelections.Count; i++)
		{
			if (list.Count == 0)
			{
				list.Add(new List<int> { i });
				continue;
			}
			TargetSelection targetSelection = _request.TargetSelections[list[list.Count - 1][0]];
			TargetSelection targetSelection2 = _request.TargetSelections[i];
			if (targetSelection2.Prompt.PromptId == targetSelection.Prompt.PromptId && targetSelection2.MinTargets == targetSelection.MinTargets && targetSelection2.MaxTargets == targetSelection.MaxTargets && targetSelection2.Targets.ContainSame(targetSelection.Targets))
			{
				list[list.Count - 1].Add(i);
				continue;
			}
			list.Add(new List<int> { i });
		}
		foreach (List<int> item in list)
		{
			for (int j = 0; j < item.Count; j++)
			{
				_promptIndexNofCount.Add(new KeyValuePair<int, int>(j + 1, item.Count));
			}
		}
	}

	protected override void SetPrompt()
	{
		_workflowPrompt.Reset();
		_prompt = _currentTargetSelection.Prompt;
		if (!_submitted)
		{
			if (_promptIndexNofCount[_targetSelectionIndex].Value > 1)
			{
				string empty = string.Empty;
				empty = (string.IsNullOrEmpty(_promptOverrideKey) ? _promptTextProvider.GetPromptText(Prompt) : _cardDatabase.ClientLocProvider.GetLocalizedText(_promptOverrideKey, _promptOverrideParams));
				_workflowPrompt.LocKey = "DuelScene/Prompt/PromptNofCount";
				_workflowPrompt.LocParams = new (string, string)[3]
				{
					("prompt", empty),
					("n", _promptIndexNofCount[_targetSelectionIndex].Key.ToString()),
					("count", _promptIndexNofCount[_targetSelectionIndex].Value.ToString())
				};
			}
			else if (!string.IsNullOrEmpty(_promptOverrideKey))
			{
				_workflowPrompt.LocKey = _promptOverrideKey;
				_workflowPrompt.LocParams = _promptOverrideParams;
			}
			else
			{
				_workflowPrompt.GrePrompt = Prompt;
			}
		}
		OnUpdatePrompt(_workflowPrompt);
	}

	public override void CleanUp()
	{
		LayoutTargetedCardHolders();
		_stack.Get().ResetAutoDock();
		_stack.Get().TargetingSourceId = 0u;
		_stack.ClearCache();
		_battlefield.ClearCache();
		_browserCardHolder.ClearCache();
		_initialTargetSelections = null;
		_undoIndexStack.Clear();
		_objPool.PushObject(_undoIndexStack, tryClear: false);
		base.CleanUp();
	}

	public override void TryUndo()
	{
		base.TryUndo();
		SelectTargetsRequest request = _request;
		if (request != null && request.AllowUndo)
		{
			_submitted = true;
			_undoSent = true;
			int num = ((_undoIndexStack.Count > 0) ? _undoIndexStack.Pop() : 0);
			if (num != _targetSelectionIndex)
			{
				SetTargetSelectionIndex(num);
			}
		}
	}

	private IEnumerable<uint> SelectedIds()
	{
		HashSet<uint> selectedIds = _objPool.PopObject<HashSet<uint>>();
		for (int i = 0; i <= _targetSelectionIndex; i++)
		{
			foreach (Target target in _request.TargetSelections[i].Targets)
			{
				uint targetInstanceId = target.TargetInstanceId;
				if (target.LegalAction == SelectAction.Unselect && selectedIds.Add(target.TargetInstanceId))
				{
					yield return targetInstanceId;
				}
			}
		}
		selectedIds.Clear();
		_objPool.PushObject(selectedIds);
	}

	public IEnumerable<uint> GetSecondaryLayoutIds()
	{
		return SelectedIds();
	}

	public bool CanHandleRequest(BaseUserRequest req)
	{
		return req is SelectTargetsRequest;
	}

	public void OnRoundTrip(BaseUserRequest req)
	{
		_request = req as SelectTargetsRequest;
		_submitted = false;
		ApplyInteraction();
	}

	public bool IsWaitingForRoundTrip()
	{
		return _submitted;
	}

	public bool CanCleanupAfterOutboundMessage(ClientToGREMessage message)
	{
		return message.Type == ClientMessageType.SubmitTargetsReq;
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
		if (_openedBrowser != null && !_openedBrowser.IsVisible)
		{
			return false;
		}
		Target targetForId = GetTargetForId(entity.InstanceId);
		if (targetForId == null)
		{
			return false;
		}
		if (EntityRequiresBrowserSelection(entity) && !_browserManager.IsBrowserVisible)
		{
			return false;
		}
		return targetForId.LegalAction switch
		{
			SelectAction.Select => !_currentTargetSelection.IsAtCapacity(), 
			SelectAction.Unselect => true, 
			_ => false, 
		};
	}

	private static bool EntityRequiresBrowserSelection(IEntityView entity)
	{
		if (entity is DuelScene_CDC duelScene_CDC)
		{
			return CardHolderRequiresBrowserSelection(duelScene_CDC.CurrentCardHolder);
		}
		return false;
	}

	private static bool CardHolderRequiresBrowserSelection(ICardHolder cardHolder)
	{
		if (cardHolder != null)
		{
			return CardHolderRequiresBrowserSelection(cardHolder.CardHolderType);
		}
		return false;
	}

	private static bool CardHolderRequiresBrowserSelection(CardHolderType cardHolderType)
	{
		return cardHolderType switch
		{
			CardHolderType.Graveyard => true, 
			CardHolderType.Exile => true, 
			_ => false, 
		};
	}

	private Target GetTargetForId(uint id)
	{
		Target result = null;
		foreach (Target target in _currentTargetSelection.Targets)
		{
			if (target.TargetInstanceId == id)
			{
				result = target;
				break;
			}
		}
		return result;
	}

	private int GetCurrentTargetSelectionSelectedTargetsCount()
	{
		int num = 0;
		foreach (Target target in _currentTargetSelection.Targets)
		{
			if (target.LegalAction == SelectAction.Unselect)
			{
				num++;
			}
		}
		return num;
	}

	private bool AreAllAvailableSelectionsCold()
	{
		bool result = true;
		if (_currentTargetSelection.Targets.Count == 0)
		{
			result = false;
		}
		else
		{
			foreach (Target target in _currentTargetSelection.Targets)
			{
				if (target.Highlight != Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold && target.Highlight != Wotc.Mtgo.Gre.External.Messaging.HighlightType.ReplaceRole)
				{
					result = false;
					break;
				}
			}
		}
		return result;
	}

	private bool CanSubmitCurrentTargetSelection()
	{
		if (!_submitted)
		{
			return _currentTargetSelection.CanSubmit();
		}
		return false;
	}

	private bool SubmitAtThisIndex()
	{
		return TargetSubmission.SubmitAtThisIndex(_targetSelectionIndex, _request.TargetSelections, _gameStateProvider.LatestGameState);
	}

	private bool CanAutoAdvanceTargetSelectionIndex(MtgGameState gameState)
	{
		if (!DiaochanAutoAdvanceIndex(gameState))
		{
			if (_gameplaySettingsProvider.FullControlDisabled && !_submitted && !_undoSent && TargetSubmission.CanAdvanceTargetSelection(_targetSelectionIndex, gameState, _request.TargetSelections) && _currentTargetSelection.Targets.Count != 0)
			{
				return CanAutoSubmitBasedOnInitialTargetList();
			}
			return false;
		}
		return true;
	}

	private bool DiaochanAutoAdvanceIndex(MtgGameState gameState)
	{
		return TargetSubmission.DiaochanAutoAdvanceIndex(_targetSelectionIndex, _request.TargetSelections, gameState);
	}

	private bool CanAutoSubmitBasedOnInitialTargetList()
	{
		if (_currentTargetSelection.SelectedTargets >= _currentTargetSelection.MaxTargets)
		{
			return true;
		}
		List<uint> list = _objPool.PopObject<List<uint>>();
		foreach (Target target in _currentTargetSelection.Targets)
		{
			list.Add(target.TargetInstanceId);
		}
		bool result = list.ContainSame(_initialTargetIdList);
		list.Clear();
		_objPool.PushObject(list, tryClear: false);
		return result;
	}

	private bool ShouldShowConfirmationBasedOffBrowserType(Target target, MtgGameState gameState)
	{
		BrowserType currentBrowserType = _currentBrowserType;
		if ((uint)currentBrowserType > 1u && (uint)(currentBrowserType - 2) <= 1u)
		{
			return ShouldShowConfirmation(target, gameState);
		}
		return ShouldShowConfirmation_NonBrowser(target, gameState);
	}

	private bool ShouldShowConfirmation_NonBrowser(Target target, MtgGameState gameState)
	{
		if (_openedBrowser != null && _openedBrowser.IsVisible)
		{
			return false;
		}
		return ShouldShowConfirmation(target, gameState);
	}

	private bool ShouldShowConfirmation(Target target, MtgGameState gameState)
	{
		if (_playWarningManager.HasReachedWarningLimit())
		{
			return false;
		}
		if (_coldHighlightSelectionConfirmed)
		{
			return false;
		}
		if (target.LegalAction != SelectAction.Select)
		{
			return false;
		}
		if (gameState.TryGetCard(_request.SourceId, out var card) && gameState.TryGetCard(target.TargetInstanceId, out var card2))
		{
			if (!MDNPlayerPrefs.GameplayWarningsEnabled)
			{
				return false;
			}
			if (targetControlledByOpponent(card2) && sourceIsCounterable(card) && targetIsOnBattlefield(card2) && (ShowWardWarning(card2, card, gameState) || warnForKiraAbility(card2)))
			{
				return true;
			}
			if (card.TitleId == 703215 && card.Parent != null && targetHasSameDecorator(card2, card.Parent))
			{
				return true;
			}
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.Request = _request;
			_assetLookupSystem.Blackboard.TargetSelectionParams = new TargetSelectionParams(card, card2, _request.AbilityGrpId, target.Highlight, SelectedIds());
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ShouldShowConfirmationPayload> loadedTree))
			{
				ShouldShowConfirmationPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					return payload.ShowConfirmation;
				}
			}
		}
		if (target.Highlight != Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold && target.Highlight != Wotc.Mtgo.Gre.External.Messaging.HighlightType.ReplaceRole)
		{
			return false;
		}
		if (gameState.TryGetCard(target.TargetInstanceId, out var card3) && card3.Zone != null && (card3.Zone.Type == ZoneType.Graveyard || card3.Zone.Type == ZoneType.Exile))
		{
			return false;
		}
		if (!_request.CanCancel && _currentTargetSelection.MinTargets != 0 && AreAllAvailableSelectionsCold())
		{
			return false;
		}
		return MDNPlayerPrefs.GameplayWarningsEnabled;
		static bool sourceIsCounterable(MtgCardInstance sourceCardInternal)
		{
			return !sourceCardInternal.AffectedByQualifications.Exists((QualificationData x) => x.Type == QualificationType.CantBeCountered);
		}
		static bool targetControlledByOpponent(MtgCardInstance targetCardInternal)
		{
			return targetCardInternal.Controller.ClientPlayerEnum == GREPlayerNum.Opponent;
		}
		static bool targetHasSameDecorator(MtgCardInstance targetCardInternal, MtgCardInstance sourceCardInternal)
		{
			int num = 0;
			foreach (HashSet<DecoratorType> value in targetCardInternal.Decorators.Values)
			{
				foreach (DecoratorType targetDec in value)
				{
					if (!sourceCardInternal.Decorators.Values.Exists((HashSet<DecoratorType> x) => x.Contains(targetDec)))
					{
						num++;
					}
				}
			}
			return num == 0;
		}
		static bool targetIsOnBattlefield(MtgCardInstance targetCardInternal)
		{
			return targetCardInternal.Zone.Type == ZoneType.Battlefield;
		}
		static bool warnForKiraAbility(MtgCardInstance targetCardInternal)
		{
			return targetCardInternal.AbilityInstances.Exists((MtgAbilityInstance x) => x.AbilityId == 7884 && !x.IsExhausted);
		}
	}

	public static bool ShowWardWarning(MtgCardInstance targetCard, MtgCardInstance sourceCard, MtgGameState gameState)
	{
		if (!targetCard.Abilities.Exists((AbilityPrintingData x) => x.BaseId == 211))
		{
			return false;
		}
		MtgPlayer decidingPlayer = gameState.DecidingPlayer;
		MtgPlayer controller = sourceCard.Controller;
		uint num = decidingPlayer?.InstanceId ?? 0;
		uint num2 = controller?.InstanceId ?? 0;
		bool flag = num == num2;
		if (gameState.BattlefieldPermanentsByControllerId.TryGetValue(num, out var value))
		{
			flag &= !value.Exists((MtgCardInstance x) => x.Abilities.Exists((AbilityPrintingData a) => a.Id == 174396));
		}
		return flag;
	}

	private void ConfirmAction(IEntityView entity, MtgGameState gameState, System.Action action)
	{
		string empty = string.Empty;
		MtgCardInstance cardById = gameState.GetCardById(_request.SourceId);
		empty = ((cardById.ObjectType != GameObjectType.Ability) ? _cardTitleProvider.GetCardTitle(cardById) : _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/SelectTargets_Ability"));
		string text = string.Empty;
		bool flag = false;
		Target targetForId = GetTargetForId(entity.InstanceId);
		MtgCardInstance card;
		if (targetForId.Highlight == Wotc.Mtgo.Gre.External.Messaging.HighlightType.ReplaceRole)
		{
			text = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/SelectColdRoleTargets");
		}
		else if (_request.AbilityGrpId == 171987)
		{
			text = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/SelectColdOkoRingleaderTriggeredAbility");
		}
		else if (gameState.TryGetCard(targetForId.TargetInstanceId, out card) && IsGiftedCoilingRebirthTargetingLegendaryCard(cardById, card))
		{
			text = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/GiftedCoilingRebirthTargetingLegendaryCard");
		}
		else if ((_request.AbilityGrpId == 204319 || _request.AbilityGrpId == 102664 || _request.AbilityGrpId == 69623) && SelectedIds().Contains(targetForId.TargetInstanceId))
		{
			text = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/CardAlreadyTargetOfSpell");
		}
		else if (entity is DuelScene_CDC duelScene_CDC)
		{
			if (duelScene_CDC.Model.Abilities.Exists((AbilityPrintingData ability) => ability.BaseId == 211) && card.Controller.ClientPlayerEnum == GREPlayerNum.Opponent)
			{
				text = _cardDatabase.ClientLocProvider.GetLocalizedText((cardById.ObjectType == GameObjectType.Ability) ? "DuelScene/Warning/SelectTargets_Ward_Ability" : "DuelScene/Warning/SelectTargets_Ward_Spell");
			}
			else
			{
				flag = duelScene_CDC.Model.ObjectType == GameObjectType.Ability;
				string cardTitle = _cardTitleProvider.GetCardTitle(duelScene_CDC.Model);
				text = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Warning/SelectTargets_ColdHighlightWarning", ("target", cardTitle), ("source", empty));
			}
		}
		else if (entity is DuelScene_AvatarView duelScene_AvatarView)
		{
			if (duelScene_AvatarView.IsLocalPlayer)
			{
				text = ((!cardById.Subtypes.Contains(SubType.Curse)) ? _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/SelectTargets_LocalPlayer", ("source", empty)) : _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/SelectTargets_LocalPlayer_Curse"));
			}
			else
			{
				text = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/SelectTargets_Opponent", ("source", empty));
			}
		}
		YesNoProvider browserTypeProvider = new YesNoProvider(_cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), flag ? string.Empty : text, YesNoProvider.CreateButtonMap("DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No"), YesNoProvider.CreateActionMap(() =>
		{
			_playWarningManager.IncrementPlayWarningCount();
			_coldHighlightSelectionConfirmed = true;
			action?.Invoke();
		}, (_openedBrowser != null && _openedBrowser.IsVisible) ? new System.Action(ApplyInteractionInternal) : null));
		_browserManager.OpenBrowser(browserTypeProvider);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (entity is DuelScene_CDC)
		{
			Target targetForId = GetTargetForId(entity.InstanceId);
			if (targetForId != null)
			{
				switch (targetForId.LegalAction)
				{
				case SelectAction.Select:
				{
					if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: true, out var reroutedEntityView2))
					{
						Target targetForId3 = GetTargetForId(reroutedEntityView2.InstanceId);
						if (targetForId3 != null && targetForId3.LegalAction == SelectAction.Select)
						{
							entity = reroutedEntityView2;
						}
					}
					break;
				}
				case SelectAction.Unselect:
				{
					if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: false, out var reroutedEntityView))
					{
						Target targetForId2 = GetTargetForId(reroutedEntityView.InstanceId);
						if (targetForId2 != null && targetForId2.LegalAction == SelectAction.Unselect)
						{
							entity = reroutedEntityView;
						}
					}
					break;
				}
				}
			}
		}
		Target target = GetTargetForId(entity.InstanceId);
		System.Action action = () =>
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_basicloc_hightlight_on_selection, AudioManager.Default);
			ApplyTheSelection(target);
		};
		if (ShouldShowConfirmationBasedOffBrowserType(target, _gameStateProvider.LatestGameState))
		{
			ConfirmAction(entity, _gameStateProvider.LatestGameState, action);
			return;
		}
		action?.Invoke();
		if (entity is DuelScene_CDC lastBrowserSelection && _openedBrowser != null && _openedBrowser.IsVisible)
		{
			_lastBrowserSelection = lastBrowserSelection;
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

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		for (int i = 0; i <= _targetSelectionIndex; i++)
		{
			Target target = null;
			Target target2 = null;
			foreach (Target target3 in _request.TargetSelections[i].Targets)
			{
				target = target ?? ((target3.TargetInstanceId == lhs.InstanceId) ? target3 : null);
				target2 = target2 ?? ((target3.TargetInstanceId == rhs.InstanceId) ? target3 : null);
				if (target != null && target2 != null)
				{
					break;
				}
			}
			if ((target == null && target2 != null) || (target != null && target2 == null))
			{
				return false;
			}
			if (target != null && target2 != null && (target.LegalAction != target2.LegalAction || target.Highlight != target2.Highlight))
			{
				return false;
			}
		}
		return true;
	}

	public bool CanKeyUp(Key key)
	{
		return key == Key.Z;
	}

	public void OnKeyUp(Key key)
	{
		if (key == Key.Z)
		{
			TryUndo();
		}
	}

	public bool CanKeyDown(Key key)
	{
		return false;
	}

	public void OnKeyDown(Key key)
	{
	}

	public bool CanKeyHeld(Key key, float holdDuration)
	{
		return false;
	}

	public void OnKeyHeld(Key key, float holdDuration)
	{
	}

	protected virtual void ApplyTheSelection(Target target)
	{
		UpdateTarget(target);
		UpdateHighlightsAndDimming();
		SetArrows();
		if (!_browserManager.IsAnyBrowserOpen)
		{
			_battlefield.Get().LayoutNow();
		}
		ShowMultiZoneBrowserRefresh(visible: false);
	}

	protected override void ApplyInteractionInternal()
	{
		_stack.Get().TargetingSourceId = _request.SourceId;
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		while (CanAutoAdvanceTargetSelectionIndex(gameState))
		{
			SetTargetSelectionIndex(_targetSelectionIndex + 1);
		}
		if (CanAutoSubmitTargets())
		{
			SubmitTargets();
			return;
		}
		if (TryAutoTarget(out var target))
		{
			UpdateTarget(target);
			return;
		}
		SetTargetSelection();
		SetPromptOverrides();
	}

	private void SetTargetSelectionIndex(int idx)
	{
		_targetSelectionIndex = idx;
		_initialTargetIdList.Clear();
		foreach (Target target in _currentTargetSelection.Targets)
		{
			_initialTargetIdList.Add(target.TargetInstanceId);
		}
	}

	private void OnSubmitClicked()
	{
		if (CanSubmitCurrentTargetSelection())
		{
			if (SubmitAtThisIndex())
			{
				SubmitTargets();
				return;
			}
			SetTargetSelectionIndex(_targetSelectionIndex + 1);
			ApplyInteractionInternal();
			SetPrompt();
			UpdateHighlightsAndDimming();
			SetArrows();
		}
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		int currentTargetSelectionSelectedTargetsCount = GetCurrentTargetSelectionSelectedTargetsCount();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					currentTargetSelectionSelectedTargetsCount.ToString()
				} }
			},
			Style = ((_currentTargetSelection.MinTargets != 0 || currentTargetSelectionSelectedTargetsCount != 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary),
			ButtonCallback = OnSubmitClicked,
			ButtonSFX = WwiseEvents.sfx_ui_submit.EventName,
			Enabled = CanSubmitCurrentTargetSelection(),
			ClearsInteractions = false
		});
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = Cancel,
				ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
			};
		}
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = TryUndo
			};
		}
		OnUpdateButtons(Buttons);
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed = new Dictionary<uint, bool>();
		foreach (uint item in SelectedIds())
		{
			Dimming.IdToIsDimmed[item] = false;
		}
		foreach (Target target in _currentTargetSelection.Targets)
		{
			Dimming.IdToIsDimmed[target.TargetInstanceId] = false;
		}
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows.ClearLines();
		Arrows.ClearCtMLines();
		DuelScene_CDC hoveredCard = CardHoverController.HoveredCard;
		ZoneType zoneType = ((hoveredCard != null && hoveredCard.Model != null) ? hoveredCard.Model.ZoneType : ZoneType.None);
		if (_cardViewProvider.TryGetCardView(_request.SourceId, out var cardView))
		{
			if (hoveredCard == null || zoneType != ZoneType.Stack || hoveredCard == cardView)
			{
				for (int i = 0; i < _targetSelectionIndex; i++)
				{
					TargetSelection targetSelection = _request.TargetSelections[i];
					for (int j = 0; j < targetSelection.Targets.Count; j++)
					{
						Target target = targetSelection.Targets[j];
						if (target.LegalAction == SelectAction.Unselect)
						{
							Arrows.AddLine(new Arrows.LineData(cardView.InstanceId, target.TargetInstanceId, (uint)i, (uint)_request.TargetSelections.Count));
						}
					}
				}
				foreach (Target target2 in _currentTargetSelection.Targets)
				{
					if (target2.LegalAction == SelectAction.Unselect)
					{
						Arrows.AddLine(new Arrows.LineData(cardView.InstanceId, target2.TargetInstanceId, (uint)_targetSelectionIndex, (uint)_request.TargetSelections.Count));
					}
				}
			}
			uint num = _currentTargetSelection.MaxTargets - (uint)GetCurrentTargetSelectionSelectedTargetsCount();
			if (!_submitted && num != 0 && !PlatformUtils.IsHandheld())
			{
				Arrows.AddCtMLine(new Arrows.LineData(cardView.InstanceId, 0u, (uint)_targetSelectionIndex, (uint)_request.TargetSelections.Count));
			}
		}
		OnUpdateArrows(Arrows);
	}

	private void Cancel()
	{
		if (!_submitted)
		{
			_submitted = true;
			_undoSent = false;
			_request.Cancel();
		}
	}

	private void UpdateTarget(Target target)
	{
		if (!_submitted)
		{
			_submitted = true;
			_undoSent = false;
			_undoIndexStack.Push(_targetSelectionIndex);
			_request.UpdateTarget(target, _currentTargetSelection.TargetIdx);
		}
	}

	private void SubmitTargets()
	{
		if (!_submitted)
		{
			_submitted = true;
			_undoSent = false;
			_request.SubmitTargets();
		}
	}

	private void SetTargetSelection()
	{
		_targetCDCs.Clear();
		Arrows.ClearLines();
		Highlights.Clear();
		_coldHighlightSelectionConfirmed = false;
		SetArrows();
		UpdateHighlightsAndDimming();
		if (_submitted)
		{
			return;
		}
		TargetSelection targetSelection = _initialTargetSelections[_targetSelectionIndex];
		_involvedZones = GetInvolvedZones(targetSelection.Targets, targetSelection.TargetSourceZoneId);
		HashSet<ZoneType> hashSet = _objPool.PopObject<HashSet<ZoneType>>();
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		foreach (MtgZone involvedZone in _involvedZones)
		{
			hashSet.Add(involvedZone.Type);
		}
		switch (TargetingSetup.BrowserTypeForTargetSelection(targetSelection, hashSet, mtgGameState.Stack))
		{
		case BrowserType.MultiZone:
			OpenMultiZoneBrowser(_involvedZones);
			break;
		case BrowserType.Stack:
			OpenStackBrowser();
			break;
		default:
			SetUpNonBrowserTargetSelection();
			break;
		}
		hashSet.Clear();
		_objPool.PushObject(hashSet);
	}

	private void SetPromptOverrides()
	{
		_promptOverrideKey = null;
		_promptOverrideParams = null;
		if (_prompt.PromptId == 1041)
		{
			if (_currentTargetSelection.MinTargets == _currentTargetSelection.MaxTargets)
			{
				_promptOverrideKey = "DuelScene/ClientPrompt/Select_N_Targets";
				_promptOverrideParams = new (string, string)[1] { ("targets", _currentTargetSelection.MaxTargets.ToString()) };
			}
			else if (_currentTargetSelection.MaxTargets < uint.MaxValue)
			{
				_promptOverrideKey = "DuelScene/ClientPrompt/Select_Up_To_N_Targets";
				_promptOverrideParams = new (string, string)[1] { ("targets", _currentTargetSelection.MaxTargets.ToString()) };
			}
		}
	}

	private void LayoutTargetedCardHolders()
	{
		HashSet<ICardHolder> hashSet = _objPool.PopObject<HashSet<ICardHolder>>();
		List<uint> list = _objPool.PopObject<List<uint>>();
		list.AddRange(SelectedIds());
		foreach (uint item in list)
		{
			if (_cardViewProvider.TryGetCardView(item, out var cardView))
			{
				hashSet.Add(cardView.CurrentCardHolder);
			}
		}
		foreach (ICardHolder item2 in hashSet)
		{
			item2.LayoutNow();
		}
		hashSet.Clear();
		list.Clear();
		_objPool.PushObject(hashSet);
		_objPool.PushObject(list);
	}

	private MtgCardInstance SourceInstance(MtgGameState gameState)
	{
		return gameState.GetCardById(_request.SourceId);
	}

	private TargetSource TargetSource()
	{
		MtgCardInstance instance = SourceInstance(_gameStateProvider.LatestGameState);
		AbilityPrintingData abilityPrintingById = _abilityProvider.GetAbilityPrintingById(_request.AbilityGrpId);
		return new TargetSource(instance, abilityPrintingById);
	}

	private bool CanAutoSubmitTargets()
	{
		if (!_undoSent && _gameplaySettingsProvider.FullControlDisabled && CanAutoSubmitBasedOnInitialTargetList())
		{
			return TargetSubmission.CanAutoSubmitTargets(TargetSource(), new MtgTargetSelection(_currentTargetSelection.Targets.Count, _currentTargetSelection.CanSubmit(), _currentTargetSelection.IsAtCapacity(), submitAtThisIndex: SubmitAtThisIndex(), targetsContainAttachments: TargetsContainAttachments(_currentTargetSelection.Targets, _gameStateProvider.LatestGameState)));
		}
		return false;
	}

	private bool TryAutoTarget(out Target target)
	{
		if (_submitted || _undoSent)
		{
			target = null;
			return false;
		}
		return _autoTargetingSolution.TryAutoTarget(_currentTargetSelection, TargetSource(), out target);
	}

	private static IEnumerable<Target> PreviousAndCurrentTargets(IObjectPool objPool, IEnumerable<TargetSelection> history, IEnumerable<Target> currentTargets)
	{
		Dictionary<uint, Target> targetComposite = objPool.PopObject<Dictionary<uint, Target>>();
		foreach (Target item in history.SelectMany((TargetSelection x) => x.Targets))
		{
			targetComposite[item.TargetInstanceId] = item;
		}
		foreach (Target currentTarget in currentTargets)
		{
			targetComposite[currentTarget.TargetInstanceId] = currentTarget;
		}
		foreach (KeyValuePair<uint, Target> item2 in targetComposite)
		{
			yield return item2.Value;
		}
		targetComposite.Clear();
		objPool.PushObject(targetComposite);
	}

	private static bool TargetsContainAttachments(IEnumerable<Target> targets, MtgGameState gameState)
	{
		foreach (Target target in targets)
		{
			if (target.LegalAction != SelectAction.Unselect || !gameState.TryGetCard(target.TargetInstanceId, out var card) || (card.AttachedToId == 0 && card.AttachedWithIds.Count <= 0))
			{
				continue;
			}
			foreach (Target target2 in targets)
			{
				if (target != target2)
				{
					uint targetInstanceId = target2.TargetInstanceId;
					if (card.AttachedToId == targetInstanceId || card.AttachedWithIds.Contains(targetInstanceId))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private static bool IsGiftedCoilingRebirthTargetingLegendaryCard(MtgCardInstance sourceCard, MtgCardInstance targetCard)
	{
		if (sourceCard.Abilities.Exists((AbilityPrintingData a) => a.Id == 173946) && targetCard.Supertypes.Contains(SuperType.Legendary))
		{
			return sourceCard.CastingTimeOptions.Exists((CastingTimeOption x) => x.AbilityId == 173832);
		}
		return false;
	}

	public List<DuelScene_CDC> GetTargetCDCs()
	{
		return _targetCDCs;
	}

	private IReadOnlyList<TargetSelection> TargetSelections()
	{
		if (_request == null)
		{
			return Array.Empty<TargetSelection>();
		}
		if (_request.TargetSelections == null)
		{
			return Array.Empty<TargetSelection>();
		}
		return _request.TargetSelections;
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		if (_currentBrowserType == BrowserType.Stack)
		{
			return DuelSceneBrowserType.SelectCards;
		}
		return DuelSceneBrowserType.SelectCardsMultiZone;
	}

	public override string GetCardHolderLayoutKey()
	{
		return _browserCardHolderLayoutKey;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (CanClick(cardView, SimpleInteractionType.Primary))
		{
			OnClick(cardView, SimpleInteractionType.Primary);
		}
		else
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid.EventName, cardView.gameObject);
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (!_submitted)
		{
			if (buttonKey.StartsWith("ZoneButton"))
			{
				uint currentZoneId = (uint)Convert.ToInt32(buttonKey.Replace("ZoneButton", string.Empty));
				Debug.Log("Pressed Zone Button with Id " + currentZoneId);
				_currentZoneId = currentZoneId;
				_cardsToDisplay.Clear();
				_cardsToDisplay.AddRange(GetCurrentZoneCardViews(_currentZoneId));
				UpdateMultiZoneButtonStateData();
				_multiZoneBrowser.OnZoneUpdated();
			}
			if (buttonKey == "DoneButton")
			{
				OnSubmitClicked();
			}
			else if (buttonKey == "CancelButton")
			{
				Cancel();
			}
		}
	}

	private void OpenStackBrowser()
	{
		_currentBrowserType = BrowserType.Stack;
		_browserCardHolderLayoutKey = "Stack";
		ApplyTargetOffset = false;
		ApplyControllerOffset = true;
		SetupBrowserHeader();
		selectable.Clear();
		foreach (Target target in _currentTargetSelection.Targets)
		{
			if (_cardViewProvider.TryGetCardView(target.TargetInstanceId, out var cardView))
			{
				selectable.Add(cardView);
				if (target.Highlight == Wotc.Mtgo.Gre.External.Messaging.HighlightType.Hot)
				{
					_hotTargetIds.Add(target.TargetInstanceId);
				}
			}
		}
		currentSelections.Clear();
		foreach (uint item in SelectedIds())
		{
			if (_cardViewProvider.TryGetCardView(item, out var cardView2))
			{
				currentSelections.Add(cardView2);
			}
		}
		_cardsToDisplay.Clear();
		nonSelectable.Clear();
		foreach (uint cardId in ((MtgGameState)_gameStateProvider.LatestGameState).Stack.CardIds)
		{
			if (_cardViewProvider.TryGetCardView(cardId, out var cardView3))
			{
				if (!selectable.Contains(cardView3))
				{
					nonSelectable.Add(cardView3);
				}
				_cardsToDisplay.Add(cardView3);
			}
		}
		UpdateStackButtonStateData();
		if (_stackBrowser != null)
		{
			RefreshOpenedBrowser(_stackBrowser);
			return;
		}
		IBrowser openedBrowser = _browserManager.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private void OnStackBrowserClose()
	{
		CardHoverController.OnHoveredCardUpdated -= OnStackBrowserCardHovered;
		_stackBrowser.ClosedHandlers -= OnStackBrowserClose;
		_stackBrowser = null;
	}

	private void OnStackBrowserCardHovered(DuelScene_CDC duelScene_CDC)
	{
		if (_stackBrowser.IsVisible)
		{
			_stackBrowser.UpdateHighlightsAndDimming();
		}
	}

	public override Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		if (_stackBrowser != null && _stackBrowser.IsVisible && CardHoverController.HoveredCard != null)
		{
			browserHighlights.Clear();
			if (currentSelections.Contains(CardHoverController.HoveredCard))
			{
				browserHighlights.Add(CardHoverController.HoveredCard, HighlightType.Selected);
			}
			else if (selectable.Contains(CardHoverController.HoveredCard))
			{
				HighlightType value = HighlightType.Cold;
				if (_hotTargetIds.Contains(CardHoverController.HoveredCard.InstanceId))
				{
					value = HighlightType.Hot;
				}
				browserHighlights.Add(CardHoverController.HoveredCard, value);
			}
			return browserHighlights;
		}
		return base.GetBrowserHighlights();
	}

	protected override void OnBrowserOpened()
	{
		if (_openedBrowser is SelectCardsBrowser_MultiZone multiZoneBrowser)
		{
			_multiZoneBrowser = multiZoneBrowser;
			_multiZoneBrowser.ClosedHandlers += OnMultiZoneBrowserClose;
		}
		else if (_openedBrowser is SelectCardsBrowser stackBrowser)
		{
			_stackBrowser = stackBrowser;
			_stackBrowser.ClosedHandlers += OnStackBrowserClose;
			CardHoverController.OnHoveredCardUpdated += OnStackBrowserCardHovered;
		}
	}

	private IEnumerable<DuelScene_CDC> GetCurrentZoneCardViews(uint currentZoneId)
	{
		if (_cardViewsByZoneId.TryGetValue(currentZoneId, out var value))
		{
			return value;
		}
		return Array.Empty<DuelScene_CDC>();
	}

	private void OpenMultiZoneBrowser(List<MtgZone> involvedZones)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_currentBrowserType = BrowserType.MultiZone;
		_browserCardHolderLayoutKey = "MultiZone";
		ApplyControllerOffset = false;
		_cardViewsByZoneId.Clear();
		SetupBrowserHeader();
		_cardsToDisplay.Clear();
		selectable.Clear();
		foreach (Target target in _currentTargetSelection.Targets)
		{
			if (_cardViewProvider.TryGetCardView(target.TargetInstanceId, out var cardView))
			{
				if (!_cardViewsByZoneId.ContainsKey(cardView.Model.Zone.Id))
				{
					_cardViewsByZoneId.Add(cardView.Model.Zone.Id, new List<DuelScene_CDC>());
				}
				_cardViewsByZoneId[cardView.Model.Zone.Id].Add(cardView);
				selectable.Add(cardView);
				if (SelectTargetsHighlightsGenerator.GreHighlightToClientHighlight(_currentTargetSelection.Targets.IndexOf(target), _currentTargetSelection, mtgGameState) == HighlightType.Hot)
				{
					_hotTargetIds.Add(target.TargetInstanceId);
				}
			}
		}
		if (_currentZoneId == uint.MaxValue || !_cardViewsByZoneId.Keys.Contains(_currentZoneId))
		{
			_currentZoneId = _startingZoneIdCalculator.GetStartingZoneId(_request.SourceId, _gameStateProvider.LatestGameState, _cardDatabase, _cardViewsByZoneId.Keys);
		}
		bool applySourceOffset = (ApplyTargetOffset = mtgGameState.Zones.TryGetValue(_currentZoneId, out var value) && value.Type == ZoneType.Graveyard);
		ApplySourceOffset = applySourceOffset;
		currentSelections.Clear();
		foreach (uint item in SelectedIds())
		{
			if (_cardViewProvider.TryGetCardView(item, out var cardView2))
			{
				currentSelections.Add(cardView2);
			}
		}
		nonSelectable.Clear();
		if (ShowNonSelectableCardsInMultiZoneBrowser(involvedZones))
		{
			foreach (MtgZone involvedZone in _involvedZones)
			{
				if (involvedZone.Type == ZoneType.None)
				{
					continue;
				}
				foreach (uint cardId in involvedZone.CardIds)
				{
					if (_cardViewProvider.TryGetCardView(cardId, out var cardView3) && !selectable.Contains(cardView3))
					{
						if (_cardViewsByZoneId.TryGetValue(involvedZone.Id, out var value2))
						{
							value2.Add(cardView3);
						}
						else
						{
							_cardViewsByZoneId[involvedZone.Id] = new List<DuelScene_CDC> { cardView3 };
						}
						nonSelectable.Add(cardView3);
					}
				}
			}
		}
		foreach (List<DuelScene_CDC> value3 in _cardViewsByZoneId.Values)
		{
			value3.Sort(CompareCardViewsForSelectTargetsBrowser);
		}
		UpdateMultiZoneButtonStateData();
		_cardsToDisplay.Clear();
		_cardsToDisplay.AddRange(GetCurrentZoneCardViews(_currentZoneId));
		if (_multiZoneBrowser != null)
		{
			ShowMultiZoneBrowserRefresh(visible: true);
			RefreshOpenedBrowser(_multiZoneBrowser);
		}
		else
		{
			IBrowser openedBrowser = _browserManager.OpenBrowser(this);
			SetOpenedBrowser(openedBrowser);
		}
	}

	private void ShowMultiZoneBrowserRefresh(bool visible)
	{
		if (_gameplaySettingsProvider.FullControlDisabled && _multiZoneBrowser != null && (_request.AbilityGrpId == 102664 || _request.AbilityGrpId == 204319 || _request.AbilityGrpId == 69623))
		{
			_multiZoneBrowser.SetVisibility(visible);
		}
	}

	public static bool ShowNonSelectableCardsInMultiZoneBrowser(IReadOnlyList<MtgZone> associatedZones)
	{
		if (associatedZones.Count == 1)
		{
			return !associatedZones.Exists((MtgZone x) => x.Type == ZoneType.Exile && x.OwnerNum != GREPlayerNum.Invalid);
		}
		return true;
	}

	private void SetUpNonBrowserTargetSelection()
	{
		if (_openedBrowser != null)
		{
			_openedBrowser.Close();
			_openedBrowser = null;
		}
		SetButtons();
		_battlefield.Get().LayoutNow();
		List<uint> list = _objPool.PopObject<List<uint>>();
		foreach (Target target in _currentTargetSelection.Targets)
		{
			if (target.Highlight != Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold)
			{
				list.Add(target.TargetInstanceId);
			}
			DuelScene_CDC cardView = _cardViewProvider.GetCardView(target.TargetInstanceId);
			if ((object)cardView != null)
			{
				_targetCDCs.Add(cardView);
			}
		}
		_stack.Get().TryAutoDock(list);
		list.Clear();
		_objPool.PushObject(list, tryClear: false);
		LayoutTargetedCardHolders();
	}

	private int CompareCardViewsForSelectTargetsBrowser(DuelScene_CDC a, DuelScene_CDC b)
	{
		bool value = selectable.Contains(a);
		int num = selectable.Contains(b).CompareTo(value);
		if (num != 0)
		{
			return num;
		}
		if (a.Model != null && b.Model != null && a.Model.ZoneType == b.Model.ZoneType && a.Model.ZoneType != ZoneType.None && a.Model.Zone != null && b.Model.Zone != null)
		{
			int num2 = a.Model.Zone.CardIds.IndexOf(a.InstanceId);
			int value2 = b.Model.Zone.CardIds.IndexOf(b.InstanceId);
			return num2.CompareTo(value2);
		}
		return b.InstanceId.CompareTo(a.InstanceId);
	}

	private void OnMultiZoneBrowserClose()
	{
		_multiZoneBrowser.ClosedHandlers -= OnMultiZoneBrowserClose;
		_multiZoneBrowser = null;
	}

	protected override bool IsHotSelectable(DuelScene_CDC cdc)
	{
		return _hotTargetIds.Contains(cdc.InstanceId);
	}

	private void SetupBrowserHeader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax((int)_currentTargetSelection.MinTargets, _currentTargetSelection.MaxTargets);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(GetBrowserType());
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_currentTargetSelection.Prompt);
		if (_promptIndexNofCount[_targetSelectionIndex].Value > 1)
		{
			_subHeader = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Prompt/PromptNofCount", ("prompt", _subHeader), ("n", _promptIndexNofCount[_targetSelectionIndex].Key.ToString()), ("count", _promptIndexNofCount[_targetSelectionIndex].Value.ToString()));
		}
		_headerTextProvider.ClearParams();
	}

	private void RefreshOpenedBrowser(SelectCardsBrowser browser)
	{
		int num = _cardsToDisplay.IndexOf(_lastBrowserSelection);
		if (num > 0)
		{
			num = _cardsToDisplay.Count - 1 - num;
		}
		float scrollPos = 1f;
		if (num >= 0)
		{
			CardLayout_ScrollableBrowser cardLayout_ScrollableBrowser = _browserCardHolder.Get().Layout as CardLayout_ScrollableBrowser;
			int num2 = Mathf.Clamp(_cardsToDisplay.Count, 1, cardLayout_ScrollableBrowser.FrontCount);
			int num3 = Mathf.RoundToInt(cardLayout_ScrollableBrowser.ScrollPosition * (float)(_cardsToDisplay.Count - num2));
			scrollPos = ((num < num3 || num >= num3 + cardLayout_ScrollableBrowser.FrontCount) ? (1f - (float)num / (float)_cardsToDisplay.Count) : cardLayout_ScrollableBrowser.ScrollPosition);
		}
		browser.Refresh(scrollPos);
	}

	private List<MtgZone> GetInvolvedZones(IEnumerable<Target> targets, uint optionalZoneId)
	{
		IEnumerable<uint> entityIds = targets.Select((Target target) => target.TargetInstanceId);
		return GetZonesFromIds(_gameStateProvider.LatestGameState, entityIds, optionalZoneId);
	}

	public List<MtgZone> GetZonesFromIds(MtgGameState currentState, IEnumerable<uint> entityIds, uint optionalZoneId = 0u)
	{
		List<MtgZone> list = new List<MtgZone>();
		foreach (uint entityId in entityIds)
		{
			if (currentState.TryGetCard(entityId, out var card) && !list.Contains(card.Zone))
			{
				list.Add(card.Zone);
			}
		}
		if (optionalZoneId != 0)
		{
			MtgZone zoneById = currentState.GetZoneById(optionalZoneId);
			if (zoneById != null && !list.Contains(zoneById))
			{
				list.Add(zoneById);
			}
		}
		return list;
	}

	private void UpdateStackButtonStateData()
	{
		_buttonStateData = GenerateDefaultButtonStates((int)_currentTargetSelection.SelectedTargets, (int)_currentTargetSelection.MinTargets, (int)_currentTargetSelection.MaxTargets, _request.CancellationType);
		ModifyDoneButtonText();
	}

	private void UpdateMultiZoneButtonStateData()
	{
		List<uint> list = new List<uint>(_cardViewsByZoneId.Keys);
		list.Sort();
		if (_currentZoneId == uint.MaxValue && list.Count > 0)
		{
			_currentZoneId = list[0];
		}
		_buttonStateData = SelectCardsWorkflow<SelectTargetsRequest>.GenerateMultiZoneButtonStates((int)_currentTargetSelection.SelectedTargets, (int)_currentTargetSelection.MinTargets, (int)_currentTargetSelection.MaxTargets, _request.CancellationType, list, _currentZoneId, _gameStateProvider.LatestGameState, _cardDatabase.ClientLocProvider);
		ModifyDoneButtonText();
	}

	private void ModifyDoneButtonText()
	{
		ButtonStateData value = null;
		if (_buttonStateData.TryGetValue("DoneButton", out value))
		{
			value.LocalizedString = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					GetCurrentTargetSelectionSelectedTargetsCount().ToString()
				} }
			};
		}
	}
}
