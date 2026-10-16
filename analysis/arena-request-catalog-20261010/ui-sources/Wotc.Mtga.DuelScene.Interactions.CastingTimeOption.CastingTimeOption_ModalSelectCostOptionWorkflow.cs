using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Google.Protobuf.Collections;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Pooling;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class CastingTimeOption_ModalSelectCostOptionWorkflow : SelectCardsWorkflow<CastingTimeOption_ModalRequest>
{
	private class HighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly AffordabilityCalculator _affordabilityCalculator;

		private readonly AutoTapActionsProvider _autoTapProvider;

		private readonly Dictionary<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.ModalOption> _cardToOptionMap;

		private readonly List<Wotc.Mtgo.Gre.External.Messaging.ModalOption> _selections;

		public HighlightsGenerator(Dictionary<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.ModalOption> cardToOptionMap, List<Wotc.Mtgo.Gre.External.Messaging.ModalOption> selections, AffordabilityCalculator affordabilityCalculator, AutoTapActionsProvider autoTapProvider)
		{
			_cardToOptionMap = cardToOptionMap;
			_selections = selections;
			_affordabilityCalculator = affordabilityCalculator;
			_autoTapProvider = autoTapProvider;
		}

		public virtual Highlights GetHighlights()
		{
			_highlights.Clear();
			foreach (KeyValuePair<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.ModalOption> item in _cardToOptionMap)
			{
				_highlights.EntityHighlights[item.Key] = GetHighlightForOption(item.Value);
			}
			foreach (AutoTapAction autoTapAction in _autoTapProvider.GetAutoTapActions(GetHoveredOption(), _selections))
			{
				if (autoTapAction.ManaId != 0)
				{
					_highlights.ManaHighlights[autoTapAction.ManaId] = new ManaHighlight(HighlightType.AutoPay);
				}
				if (autoTapAction.InstanceId != 0)
				{
					_highlights.IdToHighlightType_User[autoTapAction.InstanceId] = HighlightType.AutoPay;
				}
			}
			return _highlights;
		}

		private Wotc.Mtgo.Gre.External.Messaging.ModalOption GetHoveredOption()
		{
			DuelScene_CDC hoveredCard = CardHoverController.HoveredCard;
			if (hoveredCard == null)
			{
				return null;
			}
			if (!_cardToOptionMap.TryGetValue(hoveredCard, out var value))
			{
				return null;
			}
			return value;
		}

		private HighlightType GetHighlightForOption(Wotc.Mtgo.Gre.External.Messaging.ModalOption option)
		{
			if (_selections.Contains(option))
			{
				return HighlightType.Selected;
			}
			if (!_affordabilityCalculator.SelectionIsAffordable(option, _selections))
			{
				return HighlightType.None;
			}
			return HighlightType.Hot;
		}
	}

	public class AffordabilityCalculator : IDisposable
	{
		private readonly Dictionary<int, List<List<uint>>> _affordableModeCombos;

		private readonly IObjectPool _objPool;

		public AffordabilityCalculator(IObjectPool objectPool, IReadOnlyList<ModeCostSolution> modeCostSolutions)
		{
			_objPool = objectPool ?? NullObjectPool.Default;
			_affordableModeCombos = _objPool.PopObject<Dictionary<int, List<List<uint>>>>();
			foreach (ModeCostSolution modeCostSolution in modeCostSolutions)
			{
				int count = modeCostSolution.ModeGrpIds.Count;
				if (_affordableModeCombos.TryGetValue(count, out var value))
				{
					List<uint> list = _objPool.PopObject<List<uint>>();
					list.AddRange(modeCostSolution.ModeGrpIds);
					value.Add(list);
				}
				else
				{
					List<List<uint>> list2 = _objPool.PopObject<List<List<uint>>>();
					List<uint> list3 = _objPool.PopObject<List<uint>>();
					list3.AddRange(modeCostSolution.ModeGrpIds);
					list2.Add(list3);
					_affordableModeCombos[count] = list2;
				}
			}
		}

		public bool SelectionIsAffordable(Wotc.Mtgo.Gre.External.Messaging.ModalOption selection, IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.ModalOption> currentSelections)
		{
			if (HasZeroCost(selection))
			{
				return true;
			}
			List<Wotc.Mtgo.Gre.External.Messaging.ModalOption> list = _objPool.PopObject<List<Wotc.Mtgo.Gre.External.Messaging.ModalOption>>();
			list.AddRange(currentSelections);
			list.Add(selection);
			bool result = SelectionsAreAffordable(list);
			list.Clear();
			_objPool.PushObject(list, tryClear: false);
			return result;
		}

		public bool SelectionsAreAffordable(IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.ModalOption> selections)
		{
			if (_affordableModeCombos.TryGetValue(selections.Count, out var value))
			{
				return value.Exists(selections, (List<uint> grpIdList, IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.ModalOption> selectionList) => selectionList.TrueForAll(grpIdList, (Wotc.Mtgo.Gre.External.Messaging.ModalOption selection, List<uint> grpIds) => HasZeroCost(selection) || grpIds.Contains(selection.GrpId)));
			}
			return false;
		}

		private static bool HasZeroCost(Wotc.Mtgo.Gre.External.Messaging.ModalOption modalOption)
		{
			if (modalOption.ModeCost.Count != 1)
			{
				return false;
			}
			ManaCost manaCost = modalOption.ModeCost[0].ManaCost;
			if (manaCost.Count == 0 && manaCost.Color.Count == 1)
			{
				return manaCost.Color[0] == ManaColor.Generic;
			}
			return false;
		}

		public void Dispose()
		{
			foreach (KeyValuePair<int, List<List<uint>>> affordableModeCombo in _affordableModeCombos)
			{
				List<List<uint>> value = affordableModeCombo.Value;
				while (value.Count > 0)
				{
					List<uint> list = value[0];
					value.RemoveAt(0);
					list.Clear();
					_objPool.PushObject(list, tryClear: false);
				}
				_objPool.PushObject(value, tryClear: false);
			}
			_affordableModeCombos.Clear();
			_objPool.PushObject(_affordableModeCombos, tryClear: false);
		}
	}

	private class AutoTapActionsProvider
	{
		private readonly IObjectPool _objectPool;

		private readonly IReadOnlyList<ModeCostSolution> _modeSolutions;

		public AutoTapActionsProvider(IObjectPool objectPool, IReadOnlyList<ModeCostSolution> modeSolutions)
		{
			_objectPool = objectPool;
			_modeSolutions = modeSolutions;
		}

		public IEnumerable<AutoTapAction> GetAutoTapActions(Wotc.Mtgo.Gre.External.Messaging.ModalOption additionalOption, IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.ModalOption> currentSelections)
		{
			List<Wotc.Mtgo.Gre.External.Messaging.ModalOption> list = _objectPool.PopObject<List<Wotc.Mtgo.Gre.External.Messaging.ModalOption>>();
			list.AddRange(currentSelections);
			if (additionalOption != null && !list.Contains(additionalOption))
			{
				list.Add(additionalOption);
			}
			AutoTapSolution autoTapSolution = null;
			foreach (ModeCostSolution modeSolution in _modeSolutions)
			{
				if (modeSolution.ModeGrpIds.Count == list.Count && list.TrueForAll(modeSolution.ModeGrpIds, (Wotc.Mtgo.Gre.External.Messaging.ModalOption option, RepeatedField<uint> grpIds) => grpIds.Contains(option.GrpId)))
				{
					autoTapSolution = modeSolution.AutoTapSolution;
					break;
				}
			}
			list.Clear();
			_objectPool.PushObject(list, tryClear: false);
			if (autoTapSolution == null)
			{
				return Array.Empty<AutoTapAction>();
			}
			return autoTapSolution.AutoTapActions;
		}
	}

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IObjectPool _genericPool;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IClientLocProvider _locProvider;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IAbilityTextProvider _abilityTextProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly Dictionary<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.ModalOption> _cardsToOptionMap = new Dictionary<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.ModalOption>();

	private readonly Dictionary<Wotc.Mtgo.Gre.External.Messaging.ModalOption, DuelScene_CDC> _optionToCardMap = new Dictionary<Wotc.Mtgo.Gre.External.Messaging.ModalOption, DuelScene_CDC>();

	private readonly List<Wotc.Mtgo.Gre.External.Messaging.ModalOption> _selections = new List<Wotc.Mtgo.Gre.External.Messaging.ModalOption>();

	private readonly Dictionary<DuelScene_CDC, string> _cardToManaText = new Dictionary<DuelScene_CDC, string>();

	private readonly Dictionary<DuelScene_CDC, string> _cardToModeWord = new Dictionary<DuelScene_CDC, string>();

	private readonly AffordabilityCalculator _affordabilityCalculator;

	private readonly bool _isTieredWorkflow;

	private readonly string _modeWordBeforeNoBrRegexPattern = "^.+?(?=<nobr>)";

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public CastingTimeOption_ModalSelectCostOptionWorkflow(CastingTimeOption_ModalRequest request, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, IObjectPool genericPool, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_abilityDataProvider = cardDatabase.AbilityDataProvider;
		_abilityTextProvider = cardDatabase.AbilityTextProvider;
		_locProvider = cardDatabase.ClientLocProvider;
		_cardBuilder = cardBuilder;
		_genericPool = genericPool;
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_isTieredWorkflow = _abilityDataProvider.TryGetAbilityPrintingById(_request.AbilityGrpId, out var ability) && ability.BaseId == 365;
		_affordabilityCalculator = new AffordabilityCalculator(_genericPool, _request.ModalRequest.ModeCostSolutions);
		_highlightsGenerator = new HighlightsGenerator(_cardsToOptionMap, _selections, _affordabilityCalculator, new AutoTapActionsProvider(_genericPool, _request.ModalRequest.ModeCostSolutions));
		CardHoverController.OnHoveredCardUpdated += OnHoveredCardUpdated;
	}

	protected override void ApplyInteractionInternal()
	{
		currentSelections.Clear();
		selectable.Clear();
		nonSelectable.Clear();
		_cardsToDisplay.Clear();
		_cardsToOptionMap.Clear();
		_optionToCardMap.Clear();
		_cardToModeWord.Clear();
		_cardToManaText.Clear();
		_cardToModeWord.Clear();
		DuelScene_CDC cardView = _cardViewProvider.GetCardView(_request.SourceId);
		ICardDataAdapter cardDataAdapter2;
		if (!(cardView != null))
		{
			ICardDataAdapter cardDataAdapter = CardDataExtensions.CreateBlank();
			cardDataAdapter2 = cardDataAdapter;
		}
		else
		{
			cardDataAdapter2 = cardView.Model;
		}
		ICardDataAdapter cardDataAdapter3 = cardDataAdapter2;
		ModalReq modalRequest = _request.ModalRequest;
		foreach (Wotc.Mtgo.Gre.External.Messaging.ModalOption modalOption in modalRequest.ModalOptions)
		{
			DuelScene_CDC duelScene_CDC = CreateFakeCard(cardDataAdapter3, modalOption);
			_cardsToOptionMap[duelScene_CDC] = modalOption;
			_optionToCardMap[modalOption] = duelScene_CDC;
			selectable.Add(duelScene_CDC);
			_cardsToDisplay.Add(duelScene_CDC);
		}
		foreach (Wotc.Mtgo.Gre.External.Messaging.ModalOption excludedOption in modalRequest.ExcludedOptions)
		{
			DuelScene_CDC item = CreateFakeCard(cardDataAdapter3, excludedOption);
			nonSelectable.Add(item);
			_cardsToDisplay.Add(item);
		}
		_cardsToDisplay.Sort((DuelScene_CDC lhs, DuelScene_CDC rhs) => lhs.Model.GrpId.CompareTo(rhs.Model.GrpId));
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, (int)_request.Min, (int)_request.Max, _request.CancellationType);
		SetHeaderAndSubheader(cardDataAdapter3);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader(ICardDataAdapter sourceModel)
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax((int)_request.Min, _request.Max);
		_headerTextProvider.SetSourceModel(sourceModel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	private DuelScene_CDC CreateFakeCard(ICardDataAdapter sourceModel, Wotc.Mtgo.Gre.External.Messaging.ModalOption modalOption)
	{
		uint grpId = modalOption.GrpId;
		DuelScene_CDC duelScene_CDC = _cardBuilder.CreateCDC(FakeCardData(sourceModel, grpId));
		_cardToManaText[duelScene_CDC] = ConvertToManaText(modalOption.ModeCost);
		if (_isTieredWorkflow)
		{
			Match match = Regex.Match(_abilityTextProvider.GetAbilityTextForCard(sourceModel, grpId), _modeWordBeforeNoBrRegexPattern);
			_cardToModeWord[duelScene_CDC] = match.Value;
		}
		return duelScene_CDC;
	}

	private string ConvertToManaText(IEnumerable<Cost> costs)
	{
		List<ManaQuantity> list = _genericPool.PopObject<List<ManaQuantity>>();
		foreach (Cost cost in costs)
		{
			ManaQuantity item = new ManaQuantity((uint)cost.ManaCost.Count, cost.ManaCost.Color);
			list.Add(item);
		}
		string result = ManaUtilities.ConvertManaSymbols(ManaUtilities.ConvertToOldSchoolManaText(list));
		list.Clear();
		_genericPool.PushObject(list, tryClear: false);
		return result;
	}

	public override BrowserCardHeader.BrowserCardHeaderData GetCardHeaderData(DuelScene_CDC cardView)
	{
		if (TryGetHeaderData(cardView, out var headerData))
		{
			string header = headerData.Header;
			string subHeader = headerData.SubHeader;
			return new BrowserCardHeader.BrowserCardHeaderData(header, subHeader);
		}
		return base.GetCardHeaderData(cardView);
	}

	private bool TryGetHeaderData(DuelScene_CDC card, out ModalBrowserCardHeaderProvider.HeaderData headerData)
	{
		string header = string.Empty;
		string empty = string.Empty;
		if (_cardToModeWord.TryGetValue(card, out var value))
		{
			header = value;
		}
		if (_cardToManaText.TryGetValue(card, out var value2))
		{
			empty = _locProvider.GetLocalizedText("DuelScene/Browsers/Spree_Browser_CardHeader_Plus", ("manaCost", value2));
			headerData = new ModalBrowserCardHeaderProvider.HeaderData(header, empty);
			return true;
		}
		headerData = ModalBrowserCardHeaderProvider.HeaderData.Null;
		return false;
	}

	private ICardDataAdapter FakeCardData(ICardDataAdapter sourceModel, uint grpId)
	{
		CardPrintingData printing = sourceModel.Printing;
		CardPrintingRecord record = sourceModel.Printing.Record;
		uint? flavorTextId = 0u;
		IReadOnlyList<(uint, uint)> abilityIds = Array.Empty<(uint, uint)>();
		IReadOnlyDictionary<uint, IReadOnlyList<uint>> abilityIdToLinkedTokenGrpId = DictionaryExtensions.Empty<uint, IReadOnlyList<uint>>();
		CardPrintingData printing2 = new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, flavorTextId, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds, null, null, null, abilityIdToLinkedTokenGrpId));
		CardData cardData = new CardData(sourceModel.Instance.GetCopy(), printing2);
		cardData.Instance.Owner = null;
		cardData.Instance.InstanceId = 0u;
		cardData.Instance.GrpId = grpId;
		cardData.Instance.Abilities.Clear();
		if (_abilityDataProvider.TryGetAbilityPrintingById(grpId, out var ability))
		{
			cardData.Instance.Abilities.Add(ability);
		}
		cardData.RulesTextOverride = new AbilityTextOverride(_cardDatabase).AddAbility(grpId).AddSource(sourceModel);
		return cardData;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitResponse();
		}
		else if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
	}

	private void OnHoveredCardUpdated(DuelScene_CDC hoveredCard)
	{
		if (_cardsToDisplay.Count != 0)
		{
			SetHighlights();
		}
	}

	private void SubmitResponse()
	{
		List<uint> list = _genericPool.PopObject<List<uint>>();
		foreach (Wotc.Mtgo.Gre.External.Messaging.ModalOption selection in _selections)
		{
			list.Add(selection.GrpId);
		}
		_request.SubmitModal(list);
		list.Clear();
		_genericPool.PushObject(list, tryClear: false);
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_cardsToOptionMap.TryGetValue(cardView, out var value))
		{
			if (!_selections.Remove(value))
			{
				_selections.Add(value);
			}
			if (_request.Min == 1 && _request.Max == 1 && _selections.Count == 1)
			{
				SubmitResponse();
				return;
			}
			base.CardBrowser_OnCardViewSelected(cardView);
			_buttonStateData = GenerateDefaultButtonStates(_selections.Count, (int)_request.Min, (int)_request.Max, _request.CancellationType);
			_openedBrowser.UpdateButtons();
		}
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		Dictionary<string, ButtonStateData> dictionary = new Dictionary<string, ButtonStateData>();
		bool flag = cancelType != AllowCancel.No && cancelType != AllowCancel.None;
		bool enabled = currentSelectionCount >= minSelections && currentSelectionCount <= maxSelections;
		if (maxSelections > 1)
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.LocalizedString = "DuelScene/Browsers/Spree_BrowserButton_PayMana";
			buttonStateData.LocalizedString.Parameters = new Dictionary<string, string>();
			buttonStateData.LocalizedString.Parameters.Add("manaCost", GetManaCostButtonText());
			buttonStateData.Enabled = enabled;
			buttonStateData.BrowserElementKey = (flag ? "2Button_Left" : "SingleButton");
			buttonStateData.StyleType = (_affordabilityCalculator.SelectionsAreAffordable(_selections) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Tepid);
			dictionary.Add("DoneButton", buttonStateData);
		}
		if (flag)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData2.BrowserElementKey = ((dictionary.Count == 0) ? "SingleButton" : "2Button_Right");
			buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
			dictionary.Add("CancelButton", buttonStateData2);
		}
		return dictionary;
	}

	private string GetManaCostButtonText()
	{
		List<ManaQuantity> list = _genericPool.PopObject<List<ManaQuantity>>();
		Dictionary<ManaColor, uint> dictionary = _genericPool.PopObject<Dictionary<ManaColor, uint>>();
		DuelScene_CDC cardView = _cardViewProvider.GetCardView(_request.SourceId);
		ICardDataAdapter cardDataAdapter2;
		if (!(cardView != null))
		{
			ICardDataAdapter cardDataAdapter = CardDataExtensions.CreateBlank();
			cardDataAdapter2 = cardDataAdapter;
		}
		else
		{
			cardDataAdapter2 = cardView.Model;
		}
		foreach (ManaQuantity item2 in ManaUtilities.ConvertToManaQuantity(cardDataAdapter2.OldSchoolManaText))
		{
			if (!dictionary.TryAdd(item2.Color, item2.Count))
			{
				dictionary[item2.Color] += item2.Count;
			}
		}
		foreach (Wotc.Mtgo.Gre.External.Messaging.ModalOption selection in _selections)
		{
			foreach (Cost item3 in selection.ModeCost)
			{
				ManaCost manaCost = item3.ManaCost;
				if (manaCost.Count == 0)
				{
					continue;
				}
				for (int i = 0; i < manaCost.Count; i++)
				{
					foreach (ManaColor item4 in manaCost.Color)
					{
						if (!dictionary.TryAdd(item4, 1u))
						{
							dictionary[item4]++;
						}
					}
				}
			}
		}
		foreach (KeyValuePair<ManaColor, uint> item5 in dictionary)
		{
			ManaQuantity item = new ManaQuantity(item5.Value, item5.Key);
			list.Add(item);
		}
		list.Sort(ManaQuantity.SortComparison);
		string result = ManaUtilities.ConvertManaSymbols(ManaUtilities.ConvertToOldSchoolManaText(list));
		list.Clear();
		_genericPool.PushObject(list, tryClear: false);
		dictionary.Clear();
		_genericPool.PushObject(dictionary, tryClear: false);
		return result;
	}

	public override void CleanUp()
	{
		base.CleanUp();
		_affordabilityCalculator.Dispose();
		CardHoverController.OnHoveredCardUpdated -= OnHoveredCardUpdated;
	}
}
