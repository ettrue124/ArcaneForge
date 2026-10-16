using System;
using System.Collections.Generic;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class CastingTimeOption_ModalRepeatWorkflow : WorkflowBase<CastingTimeOption_ModalRequest>, IBrowserHeaderProvider, ICardBrowserProvider, IDuelSceneBrowserProvider, IAutoRespondWorkflow
{
	private Dictionary<string, ButtonStateData> _buttonStateData;

	private string _header = string.Empty;

	private string _subheader = string.Empty;

	private DuelScene_CDC _sourceCDC;

	private readonly List<DuelScene_CDC> _nonselectableChoices = new List<DuelScene_CDC>();

	private readonly Dictionary<DuelScene_CDC, uint> _selectableChoices = new Dictionary<DuelScene_CDC, uint>();

	private readonly List<DuelScene_CDC> _selectedCopies = new List<DuelScene_CDC>();

	private Dictionary<uint, int> _cardGrpIdToPipCost = new Dictionary<uint, int>();

	private readonly List<DuelScene_CDC> _optionViews = new List<DuelScene_CDC>();

	private RepeatSelectionBrowser _openedBrowser;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IEntityViewProvider _viewManager;

	private readonly IBrowserController _browserController;

	private readonly IGameplaySettingsProvider _settingsProvider;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly List<uint> _seasonCycleCardTitleIds = new List<uint> { 811648u, 811497u, 811344u, 811182u, 811038u };

	public bool ApplyTargetOffset => false;

	public bool ApplySourceOffset => false;

	public bool ApplyControllerOffset => false;

	public CastingTimeOption_ModalRepeatWorkflow(CastingTimeOption_ModalRequest request, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, IEntityViewProvider viewManager, IBrowserController browserController, IGameplaySettingsProvider settingsProvider, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_cardBuilder = cardBuilder ?? NullCardBuilder<DuelScene_CDC>.Default;
		_viewManager = viewManager ?? NullEntityViewProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_settingsProvider = settingsProvider ?? NullGameplaySettingsProvider.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
	}

	public virtual string GetHeaderText()
	{
		return _header;
	}

	public virtual string GetSubHeaderText()
	{
		return _subheader;
	}

	public List<DuelScene_CDC> GetOptionViews()
	{
		return _optionViews;
	}

	public List<DuelScene_CDC> GetSelectionsViews()
	{
		return _selectedCopies;
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

	public bool IsSeasonCycleCard()
	{
		if (_sourceCDC == null || _sourceCDC.Model == null || _sourceCDC.Model.TitleId == 0 || _seasonCycleCardTitleIds.Count <= 0)
		{
			return false;
		}
		return _seasonCycleCardTitleIds.Contains(_sourceCDC.Model.TitleId);
	}

	public int ModalOptionTotal()
	{
		return _request.ModalOptions.Count + _request.ExcludedOptions.Count;
	}

	protected override void ApplyInteractionInternal()
	{
		_sourceCDC = _viewManager.GetCardView(_request.SourceId);
		foreach (uint modalOption in _request.ModalOptions)
		{
			DuelScene_CDC duelScene_CDC = CreateFakeCard(modalOption, _sourceCDC);
			_selectableChoices[duelScene_CDC] = modalOption;
			_optionViews.Add(duelScene_CDC);
		}
		foreach (uint excludedOption in _request.ExcludedOptions)
		{
			DuelScene_CDC item = CreateFakeCard(excludedOption, _sourceCDC);
			_nonselectableChoices.Add(item);
			_optionViews.Add(item);
		}
		HandleSeasonCycleCards();
		_optionViews.Sort(HiddenAbilityComparison);
		_buttonStateData = GenerateDefaultButtonStates();
		SetHeaderAndSubheader();
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.SetParams((int)_request.Min, _request.Max, _sourceCDC ? _sourceCDC.Model : null, this, _request, GetBrowserType());
		_header = _headerTextProvider.GetHeaderText();
		_subheader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
		UpdateSubheaderText();
	}

	private void HandleSeasonCycleCards()
	{
		foreach (AbilityPrintingData hiddenAbility in _sourceCDC.Model.Printing.HiddenAbilities)
		{
			ManaUtilities.ParsePawPrintPipCostQuantity(_cardDatabase.AbilityTextProvider.GetAbilityTextForCard(_sourceCDC.Model, hiddenAbility.Id), out var quantity);
			_cardGrpIdToPipCost.TryAdd(hiddenAbility.Id, quantity);
		}
	}

	private DuelScene_CDC CreateFakeCard(uint grpId, DuelScene_CDC source)
	{
		CardPrintingData printing = source.Model.Printing;
		CardPrintingRecord record = source.Model.Printing.Record;
		IReadOnlyList<uint> linkedFaceGrpIds = Array.Empty<uint>();
		uint? flavorTextId = 0u;
		IReadOnlyList<(uint, uint)> abilityIds = Array.Empty<(uint, uint)>();
		CardPrintingData printing2 = new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, flavorTextId, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds, null, linkedFaceGrpIds));
		CardData cardData = new CardData(source.Model.Instance.GetCopy(), printing2);
		cardData.Instance.InstanceId = 0u;
		cardData.Instance.GrpId = grpId;
		cardData.Instance.Abilities.Clear();
		cardData.RulesTextOverride = new AbilityTextOverride(_cardDatabase).AddAbility(grpId).AddSource(source.Model);
		return _cardBuilder.CreateCDC(cardData);
	}

	private void UpdateSubheaderText()
	{
		if (!IsSeasonCycleCard())
		{
			_subheader = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Repeat_Options_Subheader", ("options", (_request.Max - _selectedCopies.Count).ToString()));
		}
	}

	private void OnButtonPressed(string key)
	{
		if (key == "DoneButton")
		{
			SubmitResponse();
		}
		else if (key == "CancelButton")
		{
			CancelRequest();
		}
	}

	private void SetOpenedBrowser(IBrowser browser)
	{
		_openedBrowser = (RepeatSelectionBrowser)browser;
		_openedBrowser.CardViewSelectedHandlers += OnCardSelected;
		_openedBrowser.ClosedHandlers += OnBrowserClosed;
		_openedBrowser.ButtonPressedHandlers += OnButtonPressed;
	}

	private void OnBrowserClosed()
	{
		_openedBrowser.CardViewSelectedHandlers -= OnCardSelected;
		_openedBrowser.ClosedHandlers -= OnBrowserClosed;
		_openedBrowser.ButtonPressedHandlers -= OnButtonPressed;
	}

	private IEnumerable<uint> GetSelectedIds()
	{
		foreach (DuelScene_CDC selectedCopy in _selectedCopies)
		{
			yield return selectedCopy.Model.Instance.BaseGrpId;
		}
	}

	private void SubmitResponse()
	{
		_request.SubmitModal(GetSelectedIds());
	}

	private void OnCardSelected(DuelScene_CDC selectedCardView)
	{
		uint value;
		if (_selectedCopies.Remove(selectedCardView))
		{
			_openedBrowser.RemoveSelection(selectedCardView);
			_cardBuilder.DestroyCDC(selectedCardView);
		}
		else if ((_selectedCopies.Count < _request.Max) & _selectableChoices.TryGetValue(selectedCardView, out value))
		{
			if (selectedCardView.CurrentHighlight() == HighlightType.None)
			{
				return;
			}
			DuelScene_CDC duelScene_CDC = CreateFakeCard(value, selectedCardView);
			duelScene_CDC.transform.position = selectedCardView.transform.position;
			duelScene_CDC.PartsRoot.localScale = selectedCardView.PartsRoot.localScale;
			duelScene_CDC.transform.rotation = selectedCardView.transform.rotation;
			_selectedCopies.Add(duelScene_CDC);
			_selectedCopies.Sort(HiddenAbilityComparison);
			_openedBrowser.AddSelection(duelScene_CDC);
			if (_request.Max == _selectedCopies.Count && _settingsProvider.FullControlDisabled && !_openedBrowser.IsUsingCounters() && !IsSeasonCycleCard())
			{
				SubmitResponse();
				return;
			}
		}
		else
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid.EventName, selectedCardView.gameObject);
		}
		UpdateSubheaderText();
		_buttonStateData = GenerateDefaultButtonStates();
		_openedBrowser.Refresh();
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.RepeatSelection;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _buttonStateData;
	}

	public BrowserCardHeader.BrowserCardHeaderData GetCardHeaderData(DuelScene_CDC cardView)
	{
		if (TryGetHeaderData(cardView.Model, out var headerData))
		{
			string header = headerData.Header;
			string subHeader = headerData.SubHeader;
			return new BrowserCardHeader.BrowserCardHeaderData(header, subHeader);
		}
		return null;
	}

	public bool TryGetHeaderData(ICardDataAdapter cardModel, out ModalBrowserCardHeaderProvider.HeaderData headerData)
	{
		if (_cardGrpIdToPipCost.TryGetValue(cardModel.GrpId, out var value))
		{
			string subHeader = ManaUtilities.ConvertPawPrintSymbols(value);
			headerData = new ModalBrowserCardHeaderProvider.HeaderData(useActionTypeHeader: false, subHeader);
			return true;
		}
		headerData = ModalBrowserCardHeaderProvider.HeaderData.Null;
		return false;
	}

	private Dictionary<string, ButtonStateData> GenerateDefaultButtonStates()
	{
		Dictionary<string, ButtonStateData> dictionary = new Dictionary<string, ButtonStateData>();
		bool flag = _request.CancellationType != AllowCancel.No && _request.CancellationType != AllowCancel.None;
		uint min = _request.Min;
		int num = _selectedCopies.Count;
		if (IsSeasonCycleCard())
		{
			num = TotalSelectedPip();
		}
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.LocalizedString = (IsSeasonCycleCard() ? GetPipsForButton() : ((MTGALocalizedString)"DuelScene/KeywordSelection/KeywordSelection_Done"));
		buttonStateData.Enabled = _selectedCopies.Count >= min && num <= _request.Max;
		buttonStateData.BrowserElementKey = (flag ? "2Button_Left" : "SingleButton");
		buttonStateData.StyleType = ((!IsSeasonCycleCard() || num != 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
		dictionary.Add("DoneButton", buttonStateData);
		if (_request.CancellationType != AllowCancel.No && _request.CancellationType != AllowCancel.None)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData2.BrowserElementKey = ((dictionary.Count == 0) ? "SingleButton" : "2Button_Right");
			buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
			dictionary.Add("CancelButton", buttonStateData2);
		}
		return dictionary;
	}

	public string GetCardHolderLayoutKey()
	{
		return "Repeat_Selections";
	}

	public override void CleanUp()
	{
		_openedBrowser.Close();
		base.CleanUp();
	}

	private int RemainingPip()
	{
		if (_cardGrpIdToPipCost.Count > 0)
		{
			int num = TotalSelectedPip();
			return (int)_request.Max - num;
		}
		return 0;
	}

	public UnlocalizedMTGAString GetPipsForButton()
	{
		int num = 0;
		foreach (DuelScene_CDC selectedCopy in _selectedCopies)
		{
			if (_cardGrpIdToPipCost.TryGetValue(selectedCopy.Model.GrpId, out var value))
			{
				num += value;
			}
		}
		return new UnlocalizedMTGAString(ManaUtilities.FilledPawPrintSymbols(num, (int)_request.Max - num));
	}

	private int TotalSelectedPip()
	{
		int num = 0;
		if (_cardGrpIdToPipCost.Count > 0)
		{
			foreach (DuelScene_CDC selectedCopy in _selectedCopies)
			{
				if (_cardGrpIdToPipCost.TryGetValue(selectedCopy.Model.GrpId, out var value))
				{
					num += value;
				}
			}
		}
		return num;
	}

	public Dictionary<DuelScene_CDC, HighlightType> GetCardHighlights()
	{
		Dictionary<DuelScene_CDC, HighlightType> dictionary = new Dictionary<DuelScene_CDC, HighlightType>();
		if (IsSeasonCycleCard())
		{
			foreach (KeyValuePair<DuelScene_CDC, uint> selectableChoice in _selectableChoices)
			{
				dictionary[selectableChoice.Key] = ((_cardGrpIdToPipCost[selectableChoice.Key.Model.GrpId] <= RemainingPip()) ? HighlightType.Hot : HighlightType.None);
			}
		}
		else
		{
			foreach (KeyValuePair<DuelScene_CDC, uint> selectableChoice2 in _selectableChoices)
			{
				dictionary[selectableChoice2.Key] = ((_selectedCopies.Count < _request.Max) ? HighlightType.Hot : HighlightType.None);
			}
		}
		foreach (DuelScene_CDC selectedCopy in _selectedCopies)
		{
			dictionary[selectedCopy] = HighlightType.Selected;
		}
		foreach (DuelScene_CDC nonselectableChoice in _nonselectableChoices)
		{
			dictionary[nonselectableChoice] = HighlightType.None;
		}
		return dictionary;
	}

	public Dictionary<DuelScene_CDC, bool> GetCardDimming()
	{
		Dictionary<DuelScene_CDC, bool> dictionary = new Dictionary<DuelScene_CDC, bool>();
		if (IsSeasonCycleCard())
		{
			foreach (KeyValuePair<DuelScene_CDC, uint> selectableChoice in _selectableChoices)
			{
				dictionary[selectableChoice.Key] = _cardGrpIdToPipCost[selectableChoice.Key.Model.GrpId] > RemainingPip();
			}
		}
		else
		{
			foreach (KeyValuePair<DuelScene_CDC, uint> selectableChoice2 in _selectableChoices)
			{
				dictionary[selectableChoice2.Key] = _selectedCopies.Count >= _request.Max;
			}
		}
		foreach (DuelScene_CDC selectedCopy in _selectedCopies)
		{
			dictionary[selectedCopy] = false;
		}
		foreach (DuelScene_CDC nonselectableChoice in _nonselectableChoices)
		{
			dictionary[nonselectableChoice] = true;
		}
		return dictionary;
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
	}

	public void SetFxBlackboardDataForCard(DuelScene_CDC cardView, IBlackboard bb)
	{
	}

	private int HiddenAbilityComparison(DuelScene_CDC x, DuelScene_CDC y)
	{
		if (_sourceCDC == null)
		{
			return 0;
		}
		int num = _sourceCDC.Model.Printing.HiddenAbilities.FindIndex(x.Model.Instance.BaseGrpId, (AbilityPrintingData ability, uint id) => ability.Id == id);
		int value = _sourceCDC.Model.Printing.HiddenAbilities.FindIndex(y.Model.Instance.BaseGrpId, (AbilityPrintingData ability, uint id) => ability.Id == id);
		return num.CompareTo(value);
	}
}
