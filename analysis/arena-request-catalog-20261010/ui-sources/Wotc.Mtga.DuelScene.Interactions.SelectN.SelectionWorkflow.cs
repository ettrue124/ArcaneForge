using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectionWorkflow : WorkflowBase<SelectNRequest>, IClickableWorkflow, ICardStackWorkflow, IAutoRespondWorkflow, ITargetCDCListProviderWorkflow, ISecondaryLayoutIdListProvider
{
	private class SelectNHighlightsGenerator : IHighlightsGenerator
	{
		private const uint ABILITY_ID_BIRTHING_RITUAL = 172351u;

		private readonly IReadOnlyCollection<uint> _selectableIds;

		private readonly IReadOnlyCollection<uint> _hotIds;

		private readonly IReadOnlyCollection<uint> _selectedIds;

		private readonly IReadOnlyDictionary<uint, List<uint>> _relatedIdsBySelectedId;

		private readonly IResolutionEffectProvider _resolutionEffectProvider;

		public SelectNHighlightsGenerator(IReadOnlyCollection<uint> selectableIds, IReadOnlyCollection<uint> hotIds, IReadOnlyCollection<uint> selectedIds, IReadOnlyDictionary<uint, List<uint>> relatedIdsBySelectedId, IResolutionEffectProvider resolutionEffectProvider)
		{
			_selectableIds = selectableIds;
			_hotIds = hotIds;
			_selectedIds = selectedIds;
			_relatedIdsBySelectedId = relatedIdsBySelectedId;
			_resolutionEffectProvider = resolutionEffectProvider;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			HighlightType value = ((_hotIds.Count != 0 || ShouldUseColdHighlightsWithNoHotIds()) ? HighlightType.Cold : HighlightType.Hot);
			foreach (uint selectableId in _selectableIds)
			{
				highlights.IdToHighlightType_Workflow[selectableId] = value;
				if (!_relatedIdsBySelectedId.TryGetValue(selectableId, out var value2))
				{
					continue;
				}
				foreach (uint item in value2)
				{
					highlights.IdToHighlightType_Workflow[item] = value;
				}
			}
			foreach (uint hotId in _hotIds)
			{
				highlights.IdToHighlightType_Workflow[hotId] = HighlightType.Hot;
			}
			foreach (uint selectedId in _selectedIds)
			{
				highlights.IdToHighlightType_Workflow[selectedId] = HighlightType.Selected;
				if (!_relatedIdsBySelectedId.TryGetValue(selectedId, out var value3))
				{
					continue;
				}
				foreach (uint item2 in value3)
				{
					highlights.IdToHighlightType_Workflow[item2] = HighlightType.Selected;
				}
			}
			return highlights;
		}

		private bool ShouldUseColdHighlightsWithNoHotIds()
		{
			if (_hotIds.Count == 0)
			{
				return IsResolvingAbilityBirthingRitual(_resolutionEffectProvider.ResolutionEffect);
			}
			return false;
		}

		private static bool IsResolvingAbilityBirthingRitual(ResolutionEffectModel activeResolutionData)
		{
			if (activeResolutionData == null)
			{
				return false;
			}
			return activeResolutionData.AbilityPrinting?.Id == 172351;
		}
	}

	private const uint ROTTENMOUTH_VIPER_ABILITY_GRPID = 173967u;

	private const string SUBMIT_COUNT_PARAM = "submitCount";

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IGameplaySettingsProvider _gameplaySettingsProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserManager _browserManager;

	private readonly ISelectionConfirmation _selectionConfirmation;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly Dictionary<uint, uint> _relatedSelectionToSelectableId = new Dictionary<uint, uint>();

	private readonly Dictionary<uint, List<uint>> _selectableIdToRelatedSelections = new Dictionary<uint, List<uint>>();

	private readonly List<uint> _selectableIds = new List<uint>();

	private readonly List<uint> _selectedIds = new List<uint>();

	private readonly List<DuelScene_CDC> _targetCDCs = new List<DuelScene_CDC>();

	private IBattlefieldCardHolder _battlefieldCache;

	private StackCardHolder _stackCache;

	private const uint BIRTHING_RITUAL_ABILITY = 172351u;

	private IBattlefieldCardHolder Battlefield => _battlefieldCache ?? (_battlefieldCache = _cardHolderProvider.GetCardHolder<IBattlefieldCardHolder>(GREPlayerNum.Invalid, CardHolderType.Battlefield));

	private StackCardHolder Stack => _stackCache ?? (_stackCache = _cardHolderProvider.GetCardHolder<StackCardHolder>(GREPlayerNum.Invalid, CardHolderType.Stack));

	public SelectionWorkflow(SelectNRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IResolutionEffectProvider resolutionEffectProvider, IGameplaySettingsProvider gameplaySettingsProvider, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IBrowserManager browserManager, ISelectionConfirmation selectionConfirmation, IPlayWarningManager playWarningManager, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_prompt = _request.GetPrompt();
		_selectableIds = new List<uint>(_request.Ids);
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_resolutionEffectProvider = resolutionEffectProvider ?? NullResolutionEffectProvider.Default;
		_gameplaySettingsProvider = gameplaySettingsProvider ?? NullGameplaySettingsProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_selectionConfirmation = selectionConfirmation ?? NullSelectionConfirmation.Default;
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
		_assetLookupSystem = assetLookupSystem;
		_highlightsGenerator = new SelectNHighlightsGenerator(_selectableIds, _request.HotIds, _selectedIds, _selectableIdToRelatedSelections, _resolutionEffectProvider);
	}

	protected override void ApplyInteractionInternal()
	{
		_relatedSelectionToSelectableId.Clear();
		_selectableIdToRelatedSelections.Clear();
		_targetCDCs.Clear();
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		foreach (uint selectableId in _selectableIds)
		{
			if (_cardViewProvider.TryGetCardView(selectableId, out var cardView))
			{
				_targetCDCs.Add(cardView);
			}
			MtgCardInstance cardById = mtgGameState.GetCardById(selectableId);
			if (cardById == null || cardById.Zone.Type != ZoneType.Limbo)
			{
				continue;
			}
			foreach (MtgCardInstance child in cardById.Children)
			{
				uint instanceId = child.InstanceId;
				if (!_relatedSelectionToSelectableId.ContainsKey(instanceId))
				{
					_relatedSelectionToSelectableId.Add(instanceId, selectableId);
				}
				if (_selectableIdToRelatedSelections.TryGetValue(selectableId, out var value))
				{
					value.Add(instanceId);
					continue;
				}
				_selectableIdToRelatedSelections.Add(selectableId, new List<uint> { instanceId });
			}
		}
		_selectedIds.AddRange(GetAutoSelectIds(_request, _gameStateProvider.LatestGameState, _cardDatabase.AbilityDataProvider));
		SetButtons();
		Stack.TryAutoDock((_request.HotIds.Count > 0) ? _request.HotIds : _request.Ids);
	}

	public static IReadOnlyCollection<uint> GetAutoSelectIds(SelectNRequest req, MtgGameState gameState, IAbilityDataProvider abilityDataProvider)
	{
		int count = req.HotIds.Count;
		if (count == 0 || count < req.MinSel || count > req.MaxSel)
		{
			return Array.Empty<uint>();
		}
		AbilityPrintingData sourceAbility = GetSourceAbility(req, gameState, abilityDataProvider);
		if (sourceAbility == null || !sourceAbility.ReferencedAbilityTypes.Contains(AbilityType.Proliferate))
		{
			return Array.Empty<uint>();
		}
		return req.HotIds;
	}

	public static AbilityPrintingData GetSourceAbility(SelectNRequest req, MtgGameState gameState, IAbilityDataProvider abilityDataProvider)
	{
		if (!gameState.TryGetCard(req.SourceId, out var card) || card == null)
		{
			return null;
		}
		return GetSourceAbility(card, abilityDataProvider);
	}

	public static AbilityPrintingData GetSourceAbility(MtgCardInstance sourceCard, IAbilityDataProvider abilityDataProvider)
	{
		return GetAbilityFromObjectType(sourceCard, abilityDataProvider) ?? GetAbilityByZone(sourceCard) ?? GetAbilityFromChildren(sourceCard.Children, abilityDataProvider);
	}

	public static AbilityPrintingData GetAbilityFromObjectType(MtgCardInstance sourceCard, IAbilityDataProvider abilityDataProvider)
	{
		return sourceCard.ObjectType switch
		{
			GameObjectType.Ability => abilityDataProvider.GetAbilityPrintingById(sourceCard.GrpId), 
			GameObjectType.Emblem => sourceCard.Abilities.Find(IsStaticProliferateAbility), 
			_ => null, 
		};
	}

	public static AbilityPrintingData GetAbilityByZone(MtgCardInstance sourceCard)
	{
		return sourceCard.Zone.Type switch
		{
			ZoneType.Stack => sourceCard.Abilities.Find((AbilityPrintingData x) => x.Category == AbilityCategory.Spell), 
			ZoneType.Battlefield => sourceCard.Abilities.Find(IsStaticProliferateAbility), 
			_ => null, 
		};
	}

	private static bool IsStaticProliferateAbility(AbilityPrintingData ability)
	{
		if (ability != null && ability.Category == AbilityCategory.Static)
		{
			return ability.ReferencedAbilityTypes.Contains(AbilityType.Proliferate);
		}
		return false;
	}

	public static AbilityPrintingData GetAbilityFromChildren(IEnumerable<MtgCardInstance> children, IAbilityDataProvider abilityProvider)
	{
		foreach (MtgCardInstance item in children ?? Array.Empty<MtgCardInstance>())
		{
			if (item.ObjectType == GameObjectType.Ability && item.Zone.Type == ZoneType.Stack && abilityProvider.TryGetAbilityPrintingById(item.GrpId, out var ability))
			{
				return ability;
			}
		}
		return null;
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		bool flag = _selectableIds.Contains(lhs.InstanceId);
		bool flag2 = _selectableIds.Contains(rhs.InstanceId);
		if (flag != flag2)
		{
			return false;
		}
		bool flag3 = _selectedIds.Contains(lhs.InstanceId);
		bool flag4 = _selectedIds.Contains(rhs.InstanceId);
		if (flag3 != flag4)
		{
			return false;
		}
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		foreach (ChoosingAttachmentsInfo choosingAttachmentsInfo in mtgGameState.ChoosingAttachmentsInfos)
		{
			if (choosingAttachmentsInfo.SelectedIds.Contains(lhs.InstanceId) || choosingAttachmentsInfo.SelectedIds.Contains(rhs.InstanceId))
			{
				return false;
			}
		}
		MtgCardInstance resolvingCardInstance = mtgGameState.ResolvingCardInstance;
		if (resolvingCardInstance != null)
		{
			_ = resolvingCardInstance.GrpId;
			if (true && _request.Ids.Contains(lhs.InstanceId))
			{
				AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(mtgGameState.ResolvingCardInstance.GrpId);
				if (abilityPrintingById != null && abilityPrintingById.Tags?.Contains(MetaDataTag.UnstackPotentialTargets) == true)
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (!_browserManager.IsAnyBrowserOpen && entity is DuelScene_CDC duelScene_CDC)
		{
			CardHolderType cardHolderType = duelScene_CDC.CurrentCardHolder.CardHolderType;
			if (cardHolderType == CardHolderType.Graveyard || cardHolderType == CardHolderType.Library)
			{
				return false;
			}
		}
		return IsSelectable(entity.InstanceId);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (!ShowConfirmationIfRequired(entity))
		{
			ApplyTheSelection(entity);
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

	private bool ShowConfirmationIfRequired(IEntityView entity)
	{
		if (!MDNPlayerPrefs.GameplayWarningsEnabled)
		{
			return false;
		}
		if (_playWarningManager.HasReachedWarningLimit())
		{
			return false;
		}
		if (_selectedIds.Contains(entity.InstanceId))
		{
			return false;
		}
		if (!_selectableIds.Contains(entity.InstanceId))
		{
			return false;
		}
		if (!Highlights.IdToHighlightType_Workflow.TryGetValue(entity.InstanceId, out var value))
		{
			return false;
		}
		string confirmationText = _selectionConfirmation.GetConfirmationText(value, entity, _request);
		if (string.IsNullOrWhiteSpace(confirmationText))
		{
			return false;
		}
		_browserManager.OpenBrowser(new YesNoProvider(_cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), confirmationText, YesNoProvider.CreateButtonMap("DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No"), YesNoProvider.CreateActionMap(() =>
		{
			ApplyTheSelection(entity);
			_playWarningManager.IncrementPlayWarningCount();
		})));
		return true;
	}

	protected virtual void ApplyTheSelection(IEntityView entity)
	{
		uint num = entity.InstanceId;
		IEntityView reroutedEntityView2;
		if (_selectedIds.Contains(num))
		{
			if (WorkflowBase.TryRerouteClick(num, Battlefield, isSelecting: false, out var reroutedEntityView) && _selectedIds.Contains(reroutedEntityView.InstanceId))
			{
				num = reroutedEntityView.InstanceId;
			}
		}
		else if (_selectableIds.Contains(num) && WorkflowBase.TryRerouteClick(num, Battlefield, isSelecting: true, out reroutedEntityView2) && _selectableIds.Contains(reroutedEntityView2.InstanceId))
		{
			num = reroutedEntityView2.InstanceId;
		}
		if (_relatedSelectionToSelectableId.ContainsKey(num))
		{
			num = _relatedSelectionToSelectableId[num];
		}
		if (_selectedIds.Contains(num))
		{
			_selectedIds.Remove(num);
		}
		else
		{
			_selectedIds.Add(num);
			Arrows.ClearLines();
		}
		if (CanAutoSubmit(_gameStateProvider.LatestGameState))
		{
			_request.SubmitSelection(_selectedIds);
			Battlefield.LayoutNow();
			return;
		}
		Battlefield.LayoutNow();
		SetButtons();
		UpdateHighlightsAndDimming();
		SetArrows();
	}

	private bool CanAutoSubmit(MtgGameState gameState)
	{
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<DisableAutoSubmit> loadedTree) && loadedTree.GetPayload(_assetLookupSystem.Blackboard) != null)
		{
			return false;
		}
		List<uint> handIds = gameState?.LocalHand?.CardIds ?? new List<uint>();
		if (_request.CanAutoSubmit() && _gameplaySettingsProvider.FullControlDisabled && CanSubmitSelection(_selectedIds.Count, _request.MinSel, _request.MaxSel))
		{
			if (!CanAutoSubmitIds((_request.UnfilteredIds.Count > 0) ? _request.UnfilteredIds : _request.Ids, handIds))
			{
				return CanAutoSubmitPrompt(_prompt);
			}
			return true;
		}
		return false;
	}

	public static bool CanSubmitSelection(int selectionCount, int min, uint max)
	{
		bool num = selectionCount >= min && selectionCount <= max;
		bool flag = selectionCount == max;
		return num & flag;
	}

	private static bool CanAutoSubmitPrompt(Prompt prompt)
	{
		if (prompt != null)
		{
			return CanAutoSubmitPromptId(prompt.PromptId);
		}
		return false;
	}

	private static bool CanAutoSubmitPromptId(uint promptId)
	{
		return promptId switch
		{
			13974u => true, 
			13975u => true, 
			5771u => true, 
			4984u => true, 
			_ => false, 
		};
	}

	public static bool CanAutoSubmitIds(List<uint> ids, List<uint> handIds)
	{
		if (ids.Count != 0)
		{
			return ids.Exists((uint x) => !handIds.Contains(x));
		}
		return true;
	}

	public override void CleanUp()
	{
		if (_stackCache != null)
		{
			_stackCache.ResetAutoDock();
			_stackCache = null;
		}
		_battlefieldCache = null;
		base.CleanUp();
	}

	protected override void SetButtons()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		ResolutionEffectModel resolutionEffectModel = _resolutionEffectProvider.ResolutionEffect;
		Buttons.Cleanup();
		_assetLookupSystem.Blackboard.Request = _request;
		_assetLookupSystem.Blackboard.Prompt = _request.Prompt;
		_assetLookupSystem.Blackboard.SelectionParams = new SelectionParams(_request.MinSel, _request.MaxSel, (uint)_selectedIds.Count, (uint)_selectableIds.Count);
		ButtonStyle.StyleType style;
		if (_request.ShouldCancel)
		{
			style = ButtonStyle.StyleType.Tepid_NoGlow;
		}
		else
		{
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonStylePayload> loadedTree))
			{
				SecondaryButtonStylePayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					style = payload.Style;
					goto IL_0106;
				}
			}
			style = ((_request.MinSel != 0 || _selectedIds.Count != 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
		}
		goto IL_0106;
		IL_03ad:
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
		_assetLookupSystem.Blackboard.Clear();
		OnUpdateButtons(Buttons);
		return;
		IL_0367:
		string text;
		Buttons.CancelData = new PromptButtonData
		{
			ButtonText = text,
			Style = ButtonStyle.StyleType.Secondary,
			ButtonCallback = () =>
			{
				_request.Cancel();
			},
			ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
		};
		goto IL_03ad;
		IL_0106:
		string key = "DuelScene/ClientPrompt/Submit_N";
		MTGALocalizedString buttonText = new MTGALocalizedString
		{
			Key = key,
			Parameters = new Dictionary<string, string> { 
			{
				"submitCount",
				_selectedIds.Count.ToString()
			} }
		};
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ButtonTextPayload> loadedTree2))
		{
			ButtonTextPayload payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
			if (payload2 != null)
			{
				key = payload2.LocKey.Key;
				if (resolutionEffectModel == null || resolutionEffectModel.GrpId != 173967)
				{
					buttonText = ((!(key == "DuelScene/ClientPrompt/Submit_N")) ? new MTGALocalizedString
					{
						Key = key
					} : new MTGALocalizedString
					{
						Key = key,
						Parameters = new Dictionary<string, string> { 
						{
							"submitCount",
							_selectedIds.Count.ToString()
						} }
					});
				}
				else
				{
					long num = ((_request.MaxSel > _selectedIds.Count) ? (4 * (_request.MaxSel - _selectedIds.Count)) : 0);
					style = ((num >= mtgGameState.LocalPlayer.LifeTotal && _selectedIds.Count < _selectableIds.Count) ? ButtonStyle.StyleType.Tepid : ButtonStyle.StyleType.Secondary);
					buttonText = new MTGALocalizedString
					{
						Key = key,
						Parameters = new Dictionary<string, string> { 
						{
							"lifeToLose",
							num.ToString()
						} }
					};
				}
			}
		}
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = buttonText,
			Style = style,
			ButtonCallback = () =>
			{
				_request.SubmitSelection(_selectedIds);
			},
			ButtonSFX = WwiseEvents.sfx_ui_submit.EventName,
			Enabled = (_selectedIds.Count >= _request.MinSel && _selectedIds.Count <= _request.MaxSel)
		});
		if (_request.CanCancel)
		{
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonTextPayload> loadedTree3))
			{
				SecondaryButtonTextPayload payload3 = loadedTree3.GetPayload(_assetLookupSystem.Blackboard);
				if (payload3 != null)
				{
					text = payload3.LocKey.Key;
					goto IL_0367;
				}
			}
			text = Utils.GetCancelLocKey(_request.CancellationType);
			goto IL_0367;
		}
		goto IL_03ad;
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed = new Dictionary<uint, bool>(_selectableIds.Count);
		foreach (uint item in AllSelectableIds())
		{
			Dimming.IdToIsDimmed[item] = false;
		}
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows.ClearLines();
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		foreach (ChoosingAttachmentsInfo choosingAttachmentsInfo in mtgGameState.ChoosingAttachmentsInfos)
		{
			if ((bool)CardHoverController.HoveredCard && choosingAttachmentsInfo.AffectorId == mtgGameState.LocalPlayer.InstanceId && choosingAttachmentsInfo.AffectedIds.Contains(CardHoverController.HoveredCard.InstanceId))
			{
				int index = choosingAttachmentsInfo.AffectedIds.IndexOf(CardHoverController.HoveredCard.InstanceId);
				if (choosingAttachmentsInfo.SelectedIds[index] != 0)
				{
					Arrows.AddLine(new Arrows.LineData(CardHoverController.HoveredCard.InstanceId, choosingAttachmentsInfo.SelectedIds[index]));
				}
			}
		}
		OnUpdateArrows(Arrows);
	}

	private IEnumerable<uint> AllSelectableIds()
	{
		foreach (uint selectableId in _selectableIds)
		{
			yield return selectableId;
		}
		foreach (uint key in _relatedSelectionToSelectableId.Keys)
		{
			yield return key;
		}
	}

	private bool IsSelectable(uint instanceId)
	{
		if (!_selectableIds.Contains(instanceId))
		{
			return _relatedSelectionToSelectableId.ContainsKey(instanceId);
		}
		return true;
	}

	public bool TryAutoRespond()
	{
		if (_gameplaySettingsProvider.FullControlEnabled)
		{
			return false;
		}
		int count = _request.Ids.Count;
		if (count != _request.MinSel || count != _request.MaxSel)
		{
			return false;
		}
		if (_request.CancellationType == AllowCancel.Continue)
		{
			return false;
		}
		_request.SubmitSelection(_request.Ids);
		return true;
	}

	public List<DuelScene_CDC> GetTargetCDCs()
	{
		return _targetCDCs;
	}

	public IEnumerable<uint> GetSecondaryLayoutIds()
	{
		foreach (uint item in BirthingRitualIds(_gameStateProvider.LatestGameState, _resolutionEffectProvider.ResolutionEffect, _request.UnfilteredIds))
		{
			yield return item;
		}
	}

	private IEnumerable<uint> BirthingRitualIds(MtgGameState gameState, ResolutionEffectModel resolutionEffect, IEnumerable<uint> cardIds)
	{
		if (resolutionEffect == null || gameState == null)
		{
			yield break;
		}
		AbilityPrintingData abilityPrinting = resolutionEffect.AbilityPrinting;
		if (abilityPrinting == null || abilityPrinting.Id != 172351)
		{
			yield break;
		}
		foreach (uint cardId in cardIds)
		{
			if (gameState.TryGetCard(cardId, out var card) && card.CardTypes.Contains(CardType.Creature))
			{
				yield return cardId;
			}
		}
	}
}
