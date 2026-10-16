using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class ModalWorkflow : SelectCardsWorkflow<CastingTimeOption_ModalRequest>, IAutoRespondWorkflow
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IEntityViewManager _viewManager;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IBrowserController _browserController;

	private readonly IClientLocProvider _locProvider;

	private readonly IPromptEngine _promptEngine;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IGameplaySettingsProvider _gameplaySettingsProvider;

	private List<ModalOption> _modalOptions = new List<ModalOption>();

	private Dictionary<uint, ModalChoiceAdvisability> _grpIdToAdvisability = new Dictionary<uint, ModalChoiceAdvisability>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public ModalWorkflow(CastingTimeOption_ModalRequest request, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, IEntityViewManager viewManager, IAbilityDataProvider abilityDataProvider, IBrowserController browserController, IClientLocProvider locProvider, IPromptEngine promptEngine, IBrowserHeaderTextProvider headerTextProvider, IGameplaySettingsProvider gameplaySettingsProvider, AssetLookupSystem assetlookupSystem)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_cardBuilder = cardBuilder ?? NullCardBuilder<DuelScene_CDC>.Default;
		_viewManager = viewManager ?? NullEntityViewManager.Default;
		_abilityDataProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
		_assetLookupSystem = assetlookupSystem;
		_browserController = browserController ?? NullBrowserController.Default;
		_locProvider = locProvider ?? NullLocProvider.Default;
		_promptEngine = promptEngine ?? NullPromptEngine.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_gameplaySettingsProvider = gameplaySettingsProvider ?? NullGameplaySettingsProvider.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		currentSelections.Clear();
		_grpIdToAdvisability.Clear();
		SetHeaderAndSubheader();
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, (int)_request.Min, (int)_request.Max, _request.CancellationType);
		DuelScene_CDC cardView = _viewManager.GetCardView(_request.SourceId);
		List<ModalOption> list = new List<ModalOption>();
		foreach (Wotc.Mtgo.Gre.External.Messaging.ModalOption modalOption in _request.ModalRequest.ModalOptions)
		{
			_grpIdToAdvisability.Add(modalOption.GrpId, modalOption.Advisability);
			list.Add(ModalOption.SelectableOption(modalOption.GrpId, modalOption.Advisability, modalOption.AdvisabilityPromptId));
		}
		foreach (uint excludedOption in _request.ExcludedOptions)
		{
			list.Add(ModalOption.InactvieOption(excludedOption));
		}
		list = list.OrderBy((ModalOption x) => x, new ModalOptionComparer(SourceModalAbilities(_request.AbilityGrpId))).ToList();
		selectable.Clear();
		nonSelectable.Clear();
		_cardsToDisplay.Clear();
		foreach (ModalOption item2 in list)
		{
			ICardDataAdapter cardData = FakeCardData(cardView.Model, item2.GrpId);
			DuelScene_CDC item = _cardBuilder.CreateCDC(cardData);
			(item2.Selectable ? selectable : nonSelectable).Add(item);
			_cardsToDisplay.Add(item);
		}
		_modalOptions = list;
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.SetParams((int)_request.Min, _request.Max, null, this, _request, GetBrowserType());
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	private IReadOnlyList<AbilityPrintingData> SourceModalAbilities(uint abilityId)
	{
		AbilityPrintingData abilityPrintingById = _abilityDataProvider.GetAbilityPrintingById(abilityId);
		if (abilityPrintingById != null)
		{
			return abilityPrintingById.ModalAbilityChildren;
		}
		return Array.Empty<AbilityPrintingData>();
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

	public override Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		browserHighlights.Clear();
		foreach (DuelScene_CDC item in selectable)
		{
			if (_grpIdToAdvisability.TryGetValue(item.Model.GrpId, out var value) && value == ModalChoiceAdvisability.Discourage)
			{
				browserHighlights[item] = HighlightType.Cold;
			}
			else
			{
				browserHighlights[item] = HighlightType.Hot;
			}
		}
		foreach (DuelScene_CDC item2 in nonSelectable)
		{
			browserHighlights[item2] = HighlightType.None;
		}
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			browserHighlights[currentSelection] = HighlightType.Selected;
		}
		return browserHighlights;
	}

	public void SubmitResponse()
	{
		List<uint> list = new List<uint>();
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			list.Add(currentSelection.Model.Instance.BaseGrpId);
		}
		SubmitResponse(list);
	}

	public void SubmitResponse(List<uint> selectedGrpIds)
	{
		_request.SubmitModal(selectedGrpIds);
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		base.CardBrowser_OnCardViewSelected(cardView);
		uint grpId = cardView.Model.GrpId;
		if (_grpIdToAdvisability.TryGetValue(grpId, out var value) && value == ModalChoiceAdvisability.Discourage && selectable.Count > 1)
		{
			ModalOption modalOption = _modalOptions.Find(grpId, (ModalOption x, uint y) => x.GrpId == y);
			string promptText = _promptEngine.GetPromptText((int)modalOption.AdvisabilityPromptId);
			List<uint> selectedIds = new List<uint> { cardView.Model.Instance.BaseGrpId };
			YesNoProvider browserTypeProvider = new YesNoProvider(_locProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), promptText, YesNoProvider.CreateButtonMap("DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No"), YesNoProvider.CreateActionMap(() =>
			{
				SubmitResponse(selectedIds);
			}, () =>
			{
				ResetWorkflow();
			}));
			_browserController.OpenBrowser(browserTypeProvider);
		}
		else if (_request.Min == 1 && _request.Max == 1 && currentSelections.Count == 1 && _request.OtherSelection.Count == 0)
		{
			SubmitResponse();
		}
		else if (_request.Max == currentSelections.Count && _gameplaySettingsProvider.FullControlDisabled && _request.OtherSelection.Count == 0)
		{
			SubmitResponse();
		}
		else
		{
			_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, (int)_request.Min, (int)_request.Max, _request.CancellationType);
			_openedBrowser.UpdateButtons();
		}
	}

	private void ResetWorkflow()
	{
		_browserController.CloseCurrentBrowser();
		ApplyInteractionInternal();
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
		if (minSelections != 1 || maxSelections != 1 || _request.OtherSelection.Count != 0)
		{
			int num = maxSelections;
			if (_request.OtherSelection.Count > 0)
			{
				num = (int)_request.OtherSelection[0];
			}
			bool enabled = (currentSelectionCount >= minSelections && (currentSelectionCount <= maxSelections || currentSelectionCount == num)) || (currentSelectionCount == 0 && !_request.IsRequired);
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done";
			buttonStateData.Enabled = enabled;
			buttonStateData.BrowserElementKey = (flag ? "2Button_Left" : "SingleButton");
			buttonStateData.StyleType = SetupButtonStyle(currentSelectionCount, minSelections, maxSelections);
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

	private ButtonStyle.StyleType SetupButtonStyle(int currentSelectionCount, int minSelections, int maxSelections)
	{
		IBlackboard blackboard = _assetLookupSystem.Blackboard;
		blackboard.Clear();
		blackboard.Prompt = _prompt;
		blackboard.Request = _request;
		blackboard.Interaction = this;
		blackboard.SelectCardBrowserCurrentSelectionCount = currentSelectionCount;
		blackboard.SelectCardBrowserMinMax = (minSelections, (uint)maxSelections);
		if (_abilityDataProvider.TryGetAbilityPrintingById(_request.AbilityGrpId, out var ability))
		{
			blackboard.Ability = ability;
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ButtonStylePayload> loadedTree))
			{
				ButtonStylePayload payload = loadedTree.GetPayload(blackboard);
				if (payload != null)
				{
					return payload.Style;
				}
			}
		}
		if ((minSelections == 0 && currentSelectionCount == 0) || !_request.IsRequired)
		{
			return ButtonStyle.StyleType.Secondary;
		}
		return ButtonStyle.StyleType.Main;
	}

	public bool TryAutoRespond()
	{
		if (_request.Min == 0 && _request.Max == 0)
		{
			_request.SubmitModal(Array.Empty<uint>());
			return true;
		}
		return false;
	}
}
