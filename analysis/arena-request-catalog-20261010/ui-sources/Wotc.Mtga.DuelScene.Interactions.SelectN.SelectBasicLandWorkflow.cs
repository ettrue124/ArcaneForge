using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.UI.DuelScene;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectBasicLandWorkflow : WorkflowBase<SelectNRequest>, IButtonSelectionBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider
{
	private static IReadOnlyList<ManaColor> ORDERED_COLORS = new List<ManaColor>
	{
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green
	};

	private ColorSelectionBrowser _colorSelectionBrowser;

	private readonly ManaColorSelector _colorSelector;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IPromptEngine _promptEngine;

	private readonly ICardDatabaseAdapter _cardDatabaseAdapter;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGreLocProvider _localizationManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private static readonly List<Color> _staticColorList = new List<Color>(5)
	{
		Color.White,
		Color.Blue,
		Color.Black,
		Color.Red,
		Color.Green
	};

	private string _header;

	private string _subHeader;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _scrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _selectedScrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private ButtonSelectionBrowser _buttonSelectionBrowser;

	private readonly List<Color> _selectable = new List<Color>();

	private readonly List<Color> _selected = new List<Color>();

	private readonly AssetLoader.AssetTracker<ManaSymbolTable> _manaSymbolTracker = new AssetLoader.AssetTracker<ManaSymbolTable>("SelectNWorkflow_Color ManaSymbolTracker");

	private readonly ManaSymbolTable _manaSymbolTable;

	public SelectBasicLandWorkflow(SelectNRequest request, ManaColorSelector manaColorSelector, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IClientLocProvider clientLocProvider, IBrowserController browserController, IGameplaySettingsProvider gameplaySettings, IPromptEngine promptEngine, ICardDatabaseAdapter cardDatabaseAdapter, IGameStateProvider gameStateProvider, IBrowserHeaderTextProvider headerTextProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_colorSelector = manaColorSelector;
		_cardViewProvider = cardViewProvider;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_clientLocProvider = clientLocProvider;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_gameplaySettings = gameplaySettings;
		_promptEngine = promptEngine;
		_cardDatabaseAdapter = cardDatabaseAdapter;
		_localizationManager = cardDatabaseAdapter.GreLocProvider;
		_gameStateProvider = gameStateProvider;
		assetLookupSystem.Blackboard.Clear();
		ManaSymbolTablePayload payload = assetLookupSystem.TreeLoader.LoadTree<ManaSymbolTablePayload>().GetPayload(assetLookupSystem.Blackboard);
		if (payload != null)
		{
			_manaSymbolTable = _manaSymbolTracker.Acquire(payload.PrefabPath);
		}
		_selectable.AddRange(ColorOptionsForRequest(_request.ListType, _request.Ids));
	}

	public static List<Color> ColorOptionsForRequest(SelectionListType listType, IReadOnlyList<uint> ids)
	{
		List<Color> list = new List<Color>(_staticColorList);
		if (listType == SelectionListType.StaticSubset)
		{
			list.RemoveAll((Color x) => !ids.Contains((uint)x));
		}
		return list;
	}

	protected override void ApplyInteractionInternal()
	{
		DuelScene_CDC sourceCard;
		DuelScene_CDC topCard2;
		if (_request.MaxSel > 2)
		{
			ColorSelectionBrowserProvider browserTypeProvider = new ColorSelectionBrowserProvider(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/ColorSelectHeader"), ORDERED_COLORS, _request.MaxSel, _request.CanCancel);
			_colorSelectionBrowser = _browserController.OpenBrowser(browserTypeProvider) as ColorSelectionBrowser;
			_colorSelectionBrowser.ManaSelectionsMadeEvent += ColorsSelected;
		}
		else if (TryGetSourceCard(_request.SourceId, out sourceCard))
		{
			DuelScene_CDC topCard;
			if (((sourceCard.CurrentCardHolder != null) ? sourceCard.CurrentCardHolder.CardHolderType : CardHolderType.None) == CardHolderType.Battlefield)
			{
				_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, sourceCard.Root, new ManaColorSelector.ManaColorSelectorConfig(sourceCard.Model, _request.CanCancel, PromptText(_request.Prompt), _stack.Get()), ColorsSelected);
				_stack.Get().TryAutoDock(new uint[1] { _request.SourceId });
			}
			else if (_stack.Get().TryGetTopCardOnStack(out topCard) && (sourceCard.IsParentOf(topCard) || topCard.Model.InstanceId == sourceCard.InstanceId))
			{
				_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, topCard, new ManaColorSelector.ManaColorSelectorConfig(sourceCard.Model, _request.CanCancel, PromptText(_request.Prompt), _stack.Get()), ColorsSelected);
			}
			else
			{
				OpenChooseColorBrowser();
			}
		}
		else if (_stack.Get().TryGetTopCardOnStack(out topCard2) && topCard2.IsChildOf(_request.SourceId))
		{
			_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, topCard2, new ManaColorSelector.ManaColorSelectorConfig(_request.CanCancel, PromptText(_request.Prompt)), ColorsSelected);
		}
		else
		{
			OpenChooseColorBrowser();
		}
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
		foreach (Color item in _selectable)
		{
			string key = $"Color_{(uint)item}";
			ButtonStateData buttonStateData3 = CreateButtonStateData(item);
			buttonStateData3.Enabled = _selectable.Contains(item) && _selected.Count < _request.MaxSel;
			_scrollListButtonStateData.Add(key, buttonStateData3);
		}
		_selectedScrollListButtonStateData.Clear();
		foreach (Color item2 in _selected)
		{
			string key2 = $"Selection_{(uint)item2}";
			ButtonStateData value = CreateButtonStateData(item2);
			_selectedScrollListButtonStateData.Add(key2, value);
		}
	}

	private ButtonStateData CreateButtonStateData(Color color)
	{
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.BrowserElementKey = "ButtonDefault";
		buttonStateData.LocalizedString = new UnlocalizedMTGAString(_localizationManager.GetLocalizedTextForEnumValue("Color", (int)color));
		buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
		if (_manaSymbolTable != null)
		{
			buttonStateData.Sprite = _manaSymbolTable.GetSprite(color);
		}
		return buttonStateData;
	}

	private void SetOpenedBrowser(IBrowser browser)
	{
		_buttonSelectionBrowser = (ButtonSelectionBrowser)browser;
		_buttonSelectionBrowser.ButtonPressedHandlers += Browser_OnButtonPressed;
		_buttonSelectionBrowser.ClosedHandlers += Browser_OnClosed;
	}

	private void Browser_OnClosed()
	{
		_buttonSelectionBrowser.ClosedHandlers -= Browser_OnClosed;
		_buttonSelectionBrowser.ButtonPressedHandlers -= Browser_OnButtonPressed;
		_buttonSelectionBrowser = null;
	}

	private void Browser_OnButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitSelections();
		}
		else if (buttonKey == "CancelButton")
		{
			Cancel();
		}
		else if (buttonKey.StartsWith("Color_"))
		{
			Color color = (Color)uint.Parse(buttonKey.Split('_')[1]);
			MakeSelection(color);
		}
		else if (buttonKey.StartsWith("Selection_"))
		{
			Color color2 = (Color)uint.Parse(buttonKey.Split('_')[1]);
			RemoveSelection(color2);
		}
	}

	private void MakeSelection(Color color)
	{
		if (_selectable.Contains(color))
		{
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
			_buttonSelectionBrowser.Refresh();
		}
	}

	private void RemoveSelection(Color color)
	{
		if (_selected.Remove(color))
		{
			if (!_selectable.Contains(color))
			{
				_selectable.Add(color);
				_selectable.Sort();
			}
			UpdateBrowserButtons();
			_buttonSelectionBrowser.Refresh();
		}
	}

	private void Cancel()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	private void SubmitSelections()
	{
		_request.SubmitSelection(_selected.Select((Color x) => (uint)x));
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

	private string PromptText(Prompt prompt)
	{
		if (prompt != null)
		{
			return _promptEngine.GetPromptText(_request.Prompt, _gameStateProvider.LatestGameState, _cardDatabaseAdapter);
		}
		return Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Land_Type");
	}

	private bool TryGetSourceCard(uint sourceId, out DuelScene_CDC sourceCard)
	{
		if (_gameStateProvider.LatestGameState.Value.TryGetCard(sourceId, out var card) && _cardViewProvider.TryGetCardView(card.InstanceId, out sourceCard))
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
		_buttonSelectionBrowser?.Close();
		if ((bool)_colorSelector && _colorSelector.IsOpen)
		{
			_colorSelector.CloseSelector();
		}
		_stack.ClearCache();
		base.CleanUp();
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonSelection;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _browserButtonStateData;
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
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
}
