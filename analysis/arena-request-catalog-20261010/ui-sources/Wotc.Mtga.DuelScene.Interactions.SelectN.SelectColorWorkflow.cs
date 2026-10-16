using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.UI.DuelScene;
using Core.Code.Input;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectColorWorkflow : WorkflowBase<SelectNRequest>, IButtonSelectionBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider, IAutoRespondWorkflow
{
	private ColorSelectionBrowser _colorSelectionBrowser;

	private readonly ManaColorSelector _colorSelector;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IGreLocProvider _greLocProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly bool _canHoverCards = true;

	private StackCardHolder _stackCache;

	private HandCardHolder _localHandCache;

	private readonly AssetLoader.AssetTracker<ManaSymbolTable> _manaSymbolTracker = new AssetLoader.AssetTracker<ManaSymbolTable>("SelectNWorkflow_Color ManaSymbolTracker");

	private readonly ManaSymbolTable _manaSymbolTable;

	private readonly List<CardColor> _selectable = new List<CardColor>();

	private readonly List<CardColor> _selected = new List<CardColor>();

	private string _header;

	private string _subHeader;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _scrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _selectedScrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private IBrowser _browser;

	private static readonly List<CardColor> _staticDefaultWithoutColorless = new List<CardColor>(5)
	{
		CardColor.White,
		CardColor.Blue,
		CardColor.Black,
		CardColor.Red,
		CardColor.Green
	};

	private static readonly List<CardColor> _staticCardColorList = new List<CardColor>(6)
	{
		CardColor.Colorless,
		CardColor.White,
		CardColor.Blue,
		CardColor.Black,
		CardColor.Red,
		CardColor.Green
	};

	private StackCardHolder Stack => _stackCache ?? (_stackCache = _cardHolderProvider.GetCardHolder<StackCardHolder>(GREPlayerNum.Invalid, CardHolderType.Stack));

	private HandCardHolder LocalHand => _localHandCache ?? (_localHandCache = _cardHolderProvider.GetCardHolder<HandCardHolder>(GREPlayerNum.LocalPlayer, CardHolderType.Hand));

	public SelectColorWorkflow(SelectNRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, IPromptTextProvider promptTextProvider, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, IPlayWarningManager playWarningManager, ManaColorSelector manaColorSelector, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_promptTextProvider = promptTextProvider;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_greLocProvider = cardDatabase.GreLocProvider;
		_clientLocProvider = cardDatabase.ClientLocProvider;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
		if (_request.IsManaColorSelection)
		{
			_colorSelector = manaColorSelector;
			_cardViewProvider = cardViewProvider;
		}
		else if (_request.IsCardColorSelection)
		{
			assetLookupSystem.Blackboard.Clear();
			ManaSymbolTablePayload payload = assetLookupSystem.TreeLoader.LoadTree<ManaSymbolTablePayload>().GetPayload(assetLookupSystem.Blackboard);
			if (payload != null)
			{
				_manaSymbolTable = _manaSymbolTracker.Acquire(payload.PrefabPath);
			}
		}
	}

	public static List<CardColor> CardColorOptionsForRequest(SelectionListType listType, IReadOnlyList<uint> ids)
	{
		if (!ids.Any())
		{
			return new List<CardColor>(_staticDefaultWithoutColorless);
		}
		List<CardColor> list = new List<CardColor>(_staticCardColorList);
		if (listType == SelectionListType.StaticSubset)
		{
			list.RemoveAll((CardColor x) => !ids.Contains((uint)x));
		}
		return list;
	}

	protected override void ApplyInteractionInternal()
	{
		_selectable.AddRange(CardColorOptionsForRequest(_request.ListType, _request.Ids));
		if (_request.IsManaColorSelection)
		{
			List<ManaColor> list = new List<ManaColor>();
			if (_request.ListType == SelectionListType.Static && (_request.StaticList == StaticList.Colors || _request.StaticList == StaticList.ManaColors))
			{
				for (int i = 1; i < 6; i++)
				{
					list.Add((ManaColor)i);
				}
			}
			else if (_request.Context == SelectionContext.ManaFromAbility || (_request.ListType == SelectionListType.StaticSubset && _request.StaticList == StaticList.ManaColors))
			{
				foreach (uint id in _request.Ids)
				{
					list.Add((ManaColor)id);
				}
			}
			if (list.Count == 0)
			{
				throw new ArgumentException("Unhandled SelectNReq (ManaColor) case.");
			}
			DuelScene_CDC sourceCard;
			DuelScene_CDC topCard2;
			if (_request.MaxSel > 5)
			{
				ColorSelectionBrowserProvider browserTypeProvider = new ColorSelectionBrowserProvider(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/ColorSelectHeader"), list, _request.MaxSel, _request.CanCancel, _request.SourceId);
				_colorSelectionBrowser = _browserController.OpenBrowser(browserTypeProvider) as ColorSelectionBrowser;
				_colorSelectionBrowser.ManaSelectionsMadeEvent += ColorsSelected;
			}
			else if (TryGetSourceCard(_request.SourceId, _gameStateProvider.LatestGameState, out sourceCard))
			{
				CardHolderType cardHolderType = ((sourceCard.CurrentCardHolder != null) ? sourceCard.CurrentCardHolder.CardHolderType : CardHolderType.None);
				if (Stack.TryGetTopCardOnStack(out var topCard) && (sourceCard.IsParentOf(topCard) || topCard.Model.InstanceId == sourceCard.InstanceId))
				{
					_colorSelector.OpenSelector(list, _request.MaxSel, _request.ValidationType, topCard, new ManaColorSelector.ManaColorSelectorConfig(sourceCard.Model, _request.CanCancel, PromptText(_request.Prompt), Stack, _canHoverCards), ColorsSelected);
				}
				else if (cardHolderType == CardHolderType.Battlefield)
				{
					_colorSelector.OpenSelector(list, _request.MaxSel, _request.ValidationType, sourceCard.Root, new ManaColorSelector.ManaColorSelectorConfig(sourceCard.Model, _request.CanCancel, PromptText(_request.Prompt), Stack, _canHoverCards), ColorsSelected);
					Stack.TryAutoDock(new uint[1] { _request.SourceId });
				}
				else
				{
					OpenChooseColorBrowser();
				}
			}
			else if (Stack.TryGetTopCardOnStack(out topCard2) && topCard2.IsChildOf(_request.SourceId))
			{
				_colorSelector.OpenSelector(list, _request.MaxSel, _request.ValidationType, topCard2, new ManaColorSelector.ManaColorSelectorConfig(_request.CanCancel, PromptText(_request.Prompt))
				{
					Stack = Stack,
					CanHoverCards = _canHoverCards
				}, ColorsSelected);
			}
			else
			{
				_colorSelector.OpenSelector(list, _request.MaxSel, _request.ValidationType, InputHelper.GetPointerPosition(), new ManaColorSelector.ManaColorSelectorConfig(_request.CanCancel, PromptText(_request.Prompt))
				{
					Stack = Stack,
					CanHoverCards = _canHoverCards
				}, ColorsSelected);
			}
			LocalHand.SetHandCollapse(collapsed: true);
		}
		else if (_request.IsCardColorSelection)
		{
			OpenChooseColorBrowser();
		}
		SetButtons();
	}

	private void OpenChooseColorBrowser()
	{
		SetHeaderAndSubheader();
		UpdateBrowserButtons();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(_request.MinSel, _request.MaxSel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.ButtonSelection);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt, "DuelScene/Browsers/Choose_Color");
		_headerTextProvider.ClearParams();
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = _request.Undo
			};
		}
		OnUpdateButtons(Buttons);
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
	}

	private string PromptText(Prompt prompt)
	{
		if (prompt == null)
		{
			return _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Color");
		}
		return _promptTextProvider.GetPromptText(prompt);
	}

	private bool TryGetSourceCard(uint sourceId, MtgGameState gameState, out DuelScene_CDC sourceCard)
	{
		if (gameState.TryGetCard(sourceId, out var card) && _cardViewProvider.TryGetCardView(card.InstanceId, out sourceCard))
		{
			return sourceCard != null;
		}
		sourceCard = null;
		return false;
	}

	public override void CleanUp()
	{
		_manaSymbolTracker.Cleanup();
		if (_colorSelectionBrowser != null)
		{
			_colorSelectionBrowser.ManaSelectionsMadeEvent -= ColorsSelected;
			_colorSelectionBrowser.Close();
		}
		if (_browser != null)
		{
			_browser.Close();
			_browser = null;
		}
		if ((bool)_colorSelector && _colorSelector.IsOpen)
		{
			_colorSelector.CloseSelector();
		}
		_localHandCache = null;
		_stackCache = null;
		base.CleanUp();
	}

	private void SubmitSelections()
	{
		_request.SubmitSelection(_selected.Select((CardColor x) => (uint)x));
	}

	private void Cancel()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	private void MakeSelection(CardColor color)
	{
		if (!_selectable.Contains(color))
		{
			return;
		}
		_selected.Add(color);
		_selected.Sort();
		if (_request.ValidationType == SelectionValidationType.NonRepeatable)
		{
			_selectable.Remove(color);
		}
		if (_selected.Count == _request.MaxSel && _gameplaySettings.FullControlDisabled)
		{
			SubmitSelections();
			return;
		}
		UpdateBrowserButtons();
		if (_browser is IRefreshable refreshable)
		{
			refreshable.Refresh();
		}
	}

	private void RemoveSelection(CardColor color)
	{
		if (_selected.Remove(color))
		{
			if (!_selectable.Contains(color))
			{
				_selectable.Add(color);
				_selectable.Sort();
			}
			UpdateBrowserButtons();
			if (_browser is IRefreshable refreshable)
			{
				refreshable.Refresh();
			}
		}
	}

	private ButtonStateData CreateButtonStateData(CardColor color)
	{
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.BrowserElementKey = "ButtonDefault";
		buttonStateData.LocalizedString = new UnlocalizedMTGAString(_greLocProvider.GetLocalizedTextForEnumValue("CardColor", (int)color));
		buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
		if (_manaSymbolTable != null)
		{
			buttonStateData.Sprite = _manaSymbolTable.GetSprite(color);
		}
		return buttonStateData;
	}

	private void UpdateBrowserButtons()
	{
		_browserButtonStateData.Clear();
		bool flag = _request.CancellationType != AllowCancel.No && _request.CancellationType != AllowCancel.None;
		if (_gameplaySettings.FullControlEnabled || _request.MinSel != _request.MaxSel)
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			MTGALocalizedString localizedString = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					_selected.Count.ToString()
				} }
			};
			buttonStateData.LocalizedString = localizedString;
			buttonStateData.BrowserElementKey = (flag ? "2Button_Left" : "SingleButton");
			buttonStateData.Enabled = _request.MinSel <= _selected.Count && _selected.Count <= _request.MaxSel;
			buttonStateData.StyleType = ((_selected.Count > 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			_browserButtonStateData.Add("DoneButton", buttonStateData);
		}
		if (flag)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData2.BrowserElementKey = ((_browserButtonStateData.Count > 0) ? "2Button_Right" : "SingleButton");
			buttonStateData2.Enabled = true;
			buttonStateData2.StyleType = ((_browserButtonStateData.Count <= 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			_browserButtonStateData.Add("CancelButton", buttonStateData2);
		}
		_scrollListButtonStateData.Clear();
		foreach (CardColor item in _selectable)
		{
			string key = $"Color_{(uint)item}";
			ButtonStateData buttonStateData3 = CreateButtonStateData(item);
			buttonStateData3.Enabled = _selectable.Contains(item) && _selected.Count < _request.MaxSel;
			_scrollListButtonStateData.Add(key, buttonStateData3);
		}
		_selectedScrollListButtonStateData.Clear();
		foreach (CardColor item2 in _selected)
		{
			string key2 = $"Selection_{(uint)item2}";
			ButtonStateData value = CreateButtonStateData(item2);
			_selectedScrollListButtonStateData.Add(key2, value);
		}
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonSelection;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _browserButtonStateData;
	}

	private void Browser_OnButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			if (ShouldShowConfirmationBrowser(_gameStateProvider.LatestGameState, out var header, out var subheader, out var yesLocKey, out var noLocKey))
			{
				YesNoProvider browserTypeProvider = new YesNoProvider(header, subheader, YesNoProvider.CreateButtonMap(yesLocKey, noLocKey), YesNoProvider.CreateActionMap(() =>
				{
					_playWarningManager.IncrementPlayWarningCount();
					SubmitSelections();
				}, () =>
				{
					SetOpenedBrowser(_browserController.OpenBrowser(this));
				}));
				_browserController.OpenBrowser(browserTypeProvider);
			}
			else
			{
				SubmitSelections();
			}
		}
		else if (buttonKey == "CancelButton")
		{
			Cancel();
		}
		else if (buttonKey.StartsWith("Color_"))
		{
			CardColor color = (CardColor)uint.Parse(buttonKey.Split('_')[1]);
			MakeSelection(color);
		}
		else if (buttonKey.StartsWith("Selection_"))
		{
			CardColor color2 = (CardColor)uint.Parse(buttonKey.Split('_')[1]);
			RemoveSelection(color2);
		}
	}

	private void Browser_OnClosed()
	{
		_browser.ClosedHandlers -= Browser_OnClosed;
		_browser.ButtonPressedHandlers -= Browser_OnButtonPressed;
		_browser = null;
	}

	private void SetOpenedBrowser(IBrowser browser)
	{
		_browser = browser;
		_browser.ButtonPressedHandlers += Browser_OnButtonPressed;
		_browser.ClosedHandlers += Browser_OnClosed;
	}

	public string GetHeaderText()
	{
		return _header;
	}

	public string GetSubHeaderText()
	{
		return _subHeader;
	}

	public Dictionary<string, ButtonStateData> GetScrollListButtonStateData()
	{
		return _scrollListButtonStateData;
	}

	public Dictionary<string, ButtonStateData> GetSelectedScrollListButtonStateData()
	{
		return _selectedScrollListButtonStateData;
	}

	public bool SortButtonsByKey()
	{
		return true;
	}

	private bool ShouldShowConfirmationBrowser(MtgGameState gameState, out string header, out string subheader, out string yesLocKey, out string noLocKey)
	{
		if (_playWarningManager.HasReachedWarningLimit())
		{
			header = (subheader = (yesLocKey = (noLocKey = string.Empty)));
			return false;
		}
		if (_selected.Count < _request.MaxSel)
		{
			PromptParameter promptParameter = Prompt.Parameters.FirstOrDefault();
			if (promptParameter.ParameterName == "CardId" && promptParameter.Type == ParameterType.Number && gameState.TryGetCard((uint)promptParameter.NumberValue, out var card) && card.GrpId == 97070 && Prompt.PromptId == 118)
			{
				header = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title");
				subheader = _header;
				yesLocKey = "DuelScene/ClientPrompt/ClientPrompt_Button_Yes";
				noLocKey = "DuelScene/ClientPrompt/ClientPrompt_Button_No";
				return true;
			}
		}
		header = (subheader = (yesLocKey = (noLocKey = string.Empty)));
		return false;
	}

	private void ColorsSelected(IReadOnlyCollection<ManaColor> colors)
	{
		if ((colors == null || colors.Count == 0) && _request.CanCancel)
		{
			_request.Cancel();
			return;
		}
		_request.SubmitSelection(colors.Select((ManaColor x) => Convert.ToUInt32(x)));
	}

	public bool TryAutoRespond()
	{
		if (_request.IsManaColorSelection && _request.MinSel == 0 && _request.MaxSel == 0)
		{
			SubmitSelections();
			return true;
		}
		return false;
	}
}
