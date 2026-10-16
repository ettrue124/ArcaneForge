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

public class SelectNWorkflow_Selection_Weighted : WorkflowBase<SelectNRequest>, IClickableWorkflow, ICardStackWorkflow, ITargetCDCListProviderWorkflow, ISecondaryLayoutIdListProvider
{
	private readonly struct SelectCardBrowserParams(string header, string subHeader, MTGALocalizedString doneLocKey, string cancelLocKey, List<DuelScene_CDC> cardViews, HashSet<uint> selectableIds)
	{
		public readonly string Header = header;

		public readonly string SubHeader = subHeader;

		public readonly MTGALocalizedString DoneLocKey = doneLocKey;

		public readonly string CancelLocKey = cancelLocKey;

		public readonly List<DuelScene_CDC> CardViews = cardViews;

		public readonly HashSet<uint> SelectableIds = selectableIds;
	}

	private class SelectNWeightedHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _selectableIds;

		private readonly IReadOnlyCollection<uint> _selectedIds;

		private readonly IReadOnlyList<uint> _hotIds;

		public SelectNWeightedHighlightsGenerator(IReadOnlyCollection<uint> selectableIds, IReadOnlyCollection<uint> selectedIds, IReadOnlyList<uint> hotIds)
		{
			_selectableIds = selectableIds;
			_selectedIds = selectedIds;
			_hotIds = hotIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint selectedId in _selectedIds)
			{
				highlights.IdToHighlightType_Workflow[selectedId] = HighlightType.Selected;
			}
			bool flag = _hotIds.Count == 0;
			foreach (uint selectableId in _selectableIds)
			{
				if (!highlights.IdToHighlightType_Workflow.ContainsKey(selectableId))
				{
					bool flag2 = flag || _hotIds.Contains(selectableId);
					highlights.IdToHighlightType_Workflow[selectableId] = ((!flag2) ? HighlightType.Cold : HighlightType.Hot);
				}
			}
			return highlights;
		}
	}

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IBrowserManager _browserManager;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly PromptParameter _promptParamCardId;

	private readonly Dictionary<uint, int> _idsToWeights = new Dictionary<uint, int>();

	private readonly List<DuelScene_CDC> _targetCDCs = new List<DuelScene_CDC>();

	private SimpleSelectCardsBrowserProvider _simpleCardSelectBrowserProvider;

	private Dictionary<CardHolderType, List<ICardHolder>> _involvedCardHoldersByType = new Dictionary<CardHolderType, List<ICardHolder>>();

	public HashSet<uint> SelectableIds { get; } = new HashSet<uint>();

	public HashSet<uint> SelectedIds { get; } = new HashSet<uint>();

	public int CurrentSelectionWeight
	{
		get
		{
			int num = 0;
			foreach (uint selectedId in SelectedIds)
			{
				num += _idsToWeights[selectedId];
			}
			return num;
		}
	}

	private bool CanSubmit
	{
		get
		{
			int currentSelectionWeight = CurrentSelectionWeight;
			if (currentSelectionWeight >= _request.MinSel)
			{
				return currentSelectionWeight <= _request.MaxSel;
			}
			return false;
		}
	}

	public static bool IsForage(Prompt prompt)
	{
		if (prompt != null)
		{
			return IsForage(prompt.PromptId);
		}
		return false;
	}

	public static bool IsForage(uint promptId)
	{
		if (promptId != 13422)
		{
			return promptId == 13423;
		}
		return true;
	}

	public static bool IsBehold(Prompt prompt)
	{
		if (prompt != null)
		{
			return IsBehold(prompt.PromptId);
		}
		return false;
	}

	private static bool IsBehold(uint promptId)
	{
		if (promptId != 13974)
		{
			return promptId == 13975;
		}
		return true;
	}

	public SelectNWorkflow_Selection_Weighted(SelectNRequest request, IClientLocProvider clientLocProvider, IAbilityDataProvider abilityDataProvider, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, IBrowserManager browserManager, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_clientLocProvider = clientLocProvider;
		_abilityDataProvider = abilityDataProvider;
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_browserManager = browserManager;
		_cardViewProvider = cardViewProvider;
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_assetLookupSystem = assetLookupSystem;
		for (int i = 0; i < _request.Weights.Count; i++)
		{
			_idsToWeights[_request.Ids[i]] = _request.Weights[i];
		}
		_highlightsGenerator = new SelectNWeightedHighlightsGenerator(SelectableIds, SelectedIds, _request.HotIds);
		_promptParamCardId = _request?.Prompt?.Parameters.Find("CardId", (PromptParameter pp, string str) => pp.ParameterName == str);
	}

	private void GenerateSelectableIdsList(ref HashSet<uint> selectableIds)
	{
		selectableIds.Clear();
		foreach (uint id in _request.Ids)
		{
			if (AppliedState == InteractionAppliedState.Applied && CanWeightedSelect(id, _request.Ids, SelectedIds, _idsToWeights, CurrentSelectionWeight, _request.MinSel, _request.MaxSel))
			{
				selectableIds.Add(id);
			}
		}
	}

	protected override void ApplyInteractionInternal()
	{
		HashSet<uint> selectableIds = SelectableIds;
		GenerateSelectableIdsList(ref selectableIds);
		foreach (uint id in _request.Ids)
		{
			if (_cardViewProvider.TryGetCardView(id, out var cardView))
			{
				_targetCDCs.Add(cardView);
				ICardHolder currentCardHolder = cardView.CurrentCardHolder;
				CardHolderType cardHolderType = currentCardHolder.CardHolderType;
				if (_involvedCardHoldersByType.TryGetValue(cardHolderType, out var value))
				{
					if (!value.Contains(currentCardHolder))
					{
						value.Add(currentCardHolder);
					}
				}
				else
				{
					_involvedCardHoldersByType.Add(cardHolderType, new List<ICardHolder> { currentCardHolder });
				}
			}
			else if (!_involvedCardHoldersByType.ContainsKey(CardHolderType.None))
			{
				_involvedCardHoldersByType.Add(CardHolderType.None, new List<ICardHolder>());
			}
		}
		SetButtons();
		_battlefield.Get().LayoutNow();
		_stack.Get().TryAutoDock(_request.Ids);
	}

	private bool CanAutoSubmit()
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.Prompt = _request.Prompt;
		_assetLookupSystem.Blackboard.Request = _request;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<CanAutoSubmit> loadedTree) && loadedTree.GetPayload(_assetLookupSystem.Blackboard) != null)
		{
			if (_request.CanAutoSubmit() && _gameplaySettings.FullControlDisabled)
			{
				return CanSubmit;
			}
			return false;
		}
		return false;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (((MtgGameState)_gameStateProvider.LatestGameState).TryGetCard(entity.InstanceId, out var card) && card.Zone.Type == ZoneType.Graveyard && !_browserManager.IsAnyBrowserOpen)
		{
			return false;
		}
		return CanWeightedSelect(entity.InstanceId, _request.Ids, SelectedIds, _idsToWeights, CurrentSelectionWeight, _request.MinSel, _request.MaxSel);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (SelectedIds.Contains(entity.InstanceId))
		{
			if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: false, out var reroutedEntityView) && SelectedIds.Contains(reroutedEntityView.InstanceId))
			{
				entity = reroutedEntityView;
			}
			SelectedIds.Remove(entity.InstanceId);
		}
		else if (SelectableIds.Contains(entity.InstanceId))
		{
			if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: true, out var reroutedEntityView2) && SelectableIds.Contains(reroutedEntityView2.InstanceId))
			{
				entity = reroutedEntityView2;
			}
			SelectedIds.Add(entity.InstanceId);
		}
		HashSet<uint> selectableIds = SelectableIds;
		GenerateSelectableIdsList(ref selectableIds);
		if (CanAutoSubmit())
		{
			_request.SubmitSelection(SelectedIds);
			return;
		}
		_battlefield.Get().LayoutNow();
		Arrows.ClearLines();
		SetButtons();
		UpdateHighlightsAndDimming();
		SetArrows();
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

	public override void CleanUp()
	{
		_stack.Get().ResetAutoDock();
		_stack.ClearCache();
		_battlefield.ClearCache();
		base.CleanUp();
	}

	public static bool CanWeightedSelect(uint selection, List<uint> allIds, ICollection<uint> selectedIds, Dictionary<uint, int> idsToWeights, int currentWeight, int min, uint max)
	{
		if (selectedIds.Contains(selection))
		{
			return true;
		}
		List<uint> list = new List<uint>(allIds);
		IEnumerator<uint> enumerator = selectedIds.GetEnumerator();
		while (enumerator.MoveNext())
		{
			list.Remove(enumerator.Current);
		}
		if (idsToWeights.TryGetValue(selection, out var value))
		{
			if (value == 0)
			{
				return true;
			}
			int num = value + currentWeight;
			if (num >= min && num <= max)
			{
				return true;
			}
			if (min == int.MinValue && num < currentWeight)
			{
				return true;
			}
			if (max == uint.MaxValue && num > currentWeight)
			{
				return true;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] == selection)
				{
					continue;
				}
				int num2 = idsToWeights[list[i]];
				if (min <= num2 + num && num2 + num <= max)
				{
					return true;
				}
				int num3 = num2;
				for (int j = i + 1; j < list.Count; j++)
				{
					if (list[j] != selection)
					{
						int num4 = idsToWeights[list[j]];
						int num5 = num2 + num4 + currentWeight;
						if (min <= num5 + num && num5 + num <= max)
						{
							return true;
						}
						num3 += num4;
						if (min <= num3 + num && num3 + num <= max)
						{
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.Prompt = _request.Prompt;
		_assetLookupSystem.Blackboard.Request = _request;
		_assetLookupSystem.Blackboard.SelectionParams = new SelectionParams(_request.MinSel, _request.MaxSel, (uint)SelectedIds.Count, (uint)SelectableIds.Count);
		ButtonStyle.StyleType style = ButtonStyle.StyleType.Main;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonStylePayload> loadedTree))
		{
			SecondaryButtonStylePayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				style = payload.Style;
			}
		}
		PromptButtonData item = new PromptButtonData
		{
			ButtonText = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = ((IsForage(_request.Prompt) && SelectedIds.Count == 1 && CanSubmit) ? new Dictionary<string, string> { 
				{
					"submitCount",
					_clientLocProvider.GetLocalizedText("Events/DeckType/Food")
				} } : new Dictionary<string, string> { 
				{
					"submitCount",
					CurrentSelectionWeight.ToString()
				} })
			},
			Style = style,
			ButtonCallback = () =>
			{
				ConfirmSelectionIfNeeded(() =>
				{
					_request.SubmitSelection(SelectedIds);
				}, _gameStateProvider);
			},
			ButtonSFX = WwiseEvents.sfx_ui_submit.EventName,
			Enabled = CanSubmit
		};
		Buttons.WorkflowButtons.Add(item);
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = () =>
				{
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
					_request.Undo();
				}
			};
		}
		OnUpdateButtons(Buttons);
	}

	private void ConfirmSelectionIfNeeded(System.Action submitAction, IGameStateProvider gameStateProvider)
	{
		bool flag = false;
		if (_request.IsCostPaymentSelection && _request.HotIds.Count > 0)
		{
			foreach (uint selectedId in SelectedIds)
			{
				if (!_request.HotIds.Contains(selectedId))
				{
					flag = true;
					break;
				}
			}
		}
		if (flag)
		{
			YesNoProvider browserTypeProvider = new YesNoProvider(_clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), _clientLocProvider.GetLocalizedText(GetSubheaderKey(gameStateProvider.LatestGameState, SelectedIds)), YesNoProvider.CreateButtonMap("DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No"), YesNoProvider.CreateActionMap(() =>
			{
				submitAction();
			}, () =>
			{
				_browserManager.CurrentBrowser?.Close();
				ResetSelected();
			}));
			_browserManager.OpenBrowser(browserTypeProvider);
		}
		else
		{
			submitAction();
		}
	}

	private string GetSubheaderKey(MtgGameState gameState, IReadOnlyCollection<uint> selectedIds)
	{
		foreach (HighlightReason highlightReason in gameState.HighlightReasons)
		{
			if (highlightReason.Reason != HighlightReason.ReasonType.SacrificeResourceIsSourceOfCasualty)
			{
				continue;
			}
			foreach (uint selectedId in selectedIds)
			{
				if (selectedId == highlightReason.Affected)
				{
					return "DuelScene/ClientPrompt/Casualty_Sacrifice_Confirm";
				}
			}
		}
		if (gameState.GetTopCardOnStack().ObjectType != GameObjectType.Ability)
		{
			return "DuelScene/ClientPrompt/Sacrifice_Spell_Target_Confirm";
		}
		return "DuelScene/ClientPrompt/Sacrifice_Ability_Target_Confirm";
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed = new Dictionary<uint, bool>(_request.Ids.Count);
		foreach (uint id in _request.Ids)
		{
			Dimming.IdToIsDimmed[id] = false;
		}
		OnUpdateDimming(Dimming);
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		uint instanceId = lhs.InstanceId;
		uint instanceId2 = rhs.InstanceId;
		bool flag = _request.Ids.Contains(instanceId);
		bool flag2 = _request.Ids.Contains(instanceId2);
		if (flag != flag2)
		{
			return false;
		}
		bool flag3 = SelectedIds.Contains(instanceId);
		bool flag4 = SelectedIds.Contains(instanceId2);
		if (flag3 != flag4)
		{
			return false;
		}
		bool flag5 = SelectableIds.Contains(instanceId);
		bool flag6 = SelectableIds.Contains(instanceId2);
		if (flag5 != flag6)
		{
			return false;
		}
		bool num = _promptParamCardId?.NumberValue == instanceId;
		bool flag7 = _promptParamCardId?.NumberValue == instanceId2;
		if (num != flag7)
		{
			return false;
		}
		return true;
	}

	public List<DuelScene_CDC> GetTargetCDCs()
	{
		return _targetCDCs;
	}

	public IEnumerable<uint> GetSecondaryLayoutIds()
	{
		if (_browserManager.IsAnyBrowserOpen || !_stack.Get().TryGetTopCardOnStack(out var topCard) || topCard.Model == null || topCard.Model.ObjectType != GameObjectType.Ability || !_abilityDataProvider.TryGetAbilityPrintingById(topCard.Model.GrpId, out var ability) || (ability.BaseId != 305 && !ability.ReferencedAbilityTypes.Contains(AbilityType.Forage)))
		{
			yield break;
		}
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		if (ability.ReferencedAbilityTypes.Contains(AbilityType.Forage) && gameState.GetZone(ZoneType.Graveyard, gameState.ActivePlayer.ClientPlayerEnum).TotalCardCount < 3)
		{
			yield break;
		}
		foreach (uint id in _request.Ids)
		{
			if (gameState.TryGetCard(id, out var card))
			{
				MtgZone zone = card.Zone;
				if (zone != null && zone.Type == ZoneType.Graveyard)
				{
					yield return id;
				}
			}
		}
	}

	public void GraveyardSelectCardModalBrowser()
	{
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (DuelScene_CDC targetCDC in _targetCDCs)
		{
			ICardDataAdapter model = targetCDC.Model;
			if (model != null && model.Zone?.Type == ZoneType.Graveyard)
			{
				list.Add(targetCDC);
			}
		}
		SelectCardBrowserParams selectCardBrowserParams = new SelectCardBrowserParams(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/ChooseThree_Header"), _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Forage_Subheader"), new MTGALocalizedString
		{
			Key = "DuelScene/ClientPrompt/Submit_N",
			Parameters = ((IsForage(_request.Prompt) && SelectedIds.Count == 1 && CanSubmit) ? new Dictionary<string, string> { 
			{
				"submitCount",
				_clientLocProvider.GetLocalizedText("Events/DeckType/Food")
			} } : new Dictionary<string, string> { 
			{
				"submitCount",
				CurrentSelectionWeight.ToString()
			} })
		}, "DuelScene/Browsers/Browser_CancelText", list, SelectableIds);
		SimpleSelectCardsBrowserProvider simpleSelectCardsBrowserProvider = new SimpleSelectCardsBrowserProvider(selectCardBrowserParams.CardViews, selectCardBrowserParams.SelectableIds, selectCardBrowserParams.Header, selectCardBrowserParams.SubHeader, SimpleSelectCardsBrowserProvider.CreateButtonMap(selectCardBrowserParams.DoneLocKey, selectCardBrowserParams.CancelLocKey, CanSubmit), SimpleSelectCardsBrowserProvider.CreateActionMap(() =>
		{
			_browserManager.CurrentBrowser?.Close();
			_request.SubmitSelection(SelectedIds);
		}, () =>
		{
			_browserManager.CurrentBrowser?.Close();
			ResetSelected();
		}), ModalBrowserCardClicked);
		ICardBrowser openedBrowser = (ICardBrowser)_browserManager.OpenBrowser(simpleSelectCardsBrowserProvider);
		simpleSelectCardsBrowserProvider.SetOpenedBrowser(openedBrowser);
		_simpleCardSelectBrowserProvider = simpleSelectCardsBrowserProvider;
	}

	private void ModalBrowserCardClicked(DuelScene_CDC cardView)
	{
		if (CanClick(cardView, SimpleInteractionType.Primary))
		{
			OnClick(cardView, SimpleInteractionType.Primary);
			if (_simpleCardSelectBrowserProvider != null)
			{
				MTGALocalizedString submitLocString = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Submit_N",
					Parameters = new Dictionary<string, string> { 
					{
						"submitCount",
						CurrentSelectionWeight.ToString()
					} }
				};
				_simpleCardSelectBrowserProvider.UpdateSubmitButton(CanSubmit, submitLocString);
			}
		}
	}

	private void ResetSelected()
	{
		SelectedIds.Clear();
		HashSet<uint> selectableIds = SelectableIds;
		GenerateSelectableIdsList(ref selectableIds);
		SetButtons();
		UpdateHighlightsAndDimming();
	}
}
