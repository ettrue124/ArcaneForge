using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceWithContextWorkflow : BrowserWorkflowBase<SelectNRequest>
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly IChoiceOptionsTranslation _choiceTranslation;

	private ChoiceWithContextBrowser _spyglassBrowser;

	private bool _showingAll;

	private ChoiceOptions _choiceOptions;

	private List<string> _selections;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.KeywordSelectionWithContext;
	}

	public ChoiceWithContextWorkflow(SelectNRequest request, IGameStateProvider gameStateProvider, IPromptTextProvider promptTextProvider, IBrowserController browserController, IChoiceOptionsTranslation choiceTranslation)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_promptTextProvider = promptTextProvider;
		_browserController = browserController;
		_choiceTranslation = choiceTranslation;
		_selections = new List<string>();
	}

	protected override void OnBrowserOpened()
	{
		_choiceOptions = _choiceTranslation.Translate(_request);
		List<uint> cardIds = ((_request.StaticList == StaticList.CardTypes) ? _gameStateProvider.LatestGameState.Value.OpponentHand.CardIds : _request.UnfilteredIds);
		_spyglassBrowser = (ChoiceWithContextBrowser)_openedBrowser;
		_spyglassBrowser.Init(_promptTextProvider.GetPromptText(_request.Prompt), string.Empty, cardIds, _choiceOptions.SortedChoices, _choiceOptions.HintingOptions, (uint)_request.MinSel, _request.MaxSel, Languages.ActiveLocProvider.GetLocalizedText("DuelScene/KeywordSelection/KeywordSelection_Filter", ("description", string.Empty)));
		ConfigureButtonState();
		_spyglassBrowser.ButtonData = _buttonStateData;
		_spyglassBrowser.SelectionsChangedHandlers += OnSelectionsChanged;
	}

	public override void CleanUp()
	{
		if (_spyglassBrowser != null)
		{
			_spyglassBrowser.SelectionsChangedHandlers -= OnSelectionsChanged;
			_spyglassBrowser = null;
		}
		base.CleanUp();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		switch (buttonKey)
		{
		case "ShowAllButton":
			OnShowAllPressed();
			break;
		case "CancelButton":
			OnCancelPressed();
			break;
		case "DoneButton":
			OnDonePressed();
			break;
		}
	}

	protected override void ApplyInteractionInternal()
	{
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void OnSelectionsChanged(IEnumerable<string> selections)
	{
		_selections.Clear();
		_selections.AddRange(selections);
		ConfigureButtonState();
		_spyglassBrowser.ButtonData = _buttonStateData;
	}

	private void OnShowAllPressed()
	{
		if (!_showingAll)
		{
			_showingAll = true;
			ConfigureButtonState();
			_spyglassBrowser.ButtonData = _buttonStateData;
			_spyglassBrowser.ShowAllChoices = true;
		}
	}

	private void OnCancelPressed()
	{
		_request.Cancel();
		_openedBrowser.Close();
	}

	private void OnDonePressed()
	{
		List<uint> list = new List<uint>();
		foreach (string selection in _selections)
		{
			list.Add(_choiceOptions.ChoiceToIdMap[selection]);
		}
		_request.SubmitSelection(list);
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_done, AudioManager.Default);
		_openedBrowser.Close();
	}

	private void ConfigureButtonState()
	{
		if (_buttonStateData == null)
		{
			_buttonStateData = new Dictionary<string, ButtonStateData>();
		}
		if (!_buttonStateData.ContainsKey("ShowAllButton"))
		{
			ButtonStateData buttonStateData = new ButtonStateData
			{
				BrowserElementKey = "ShowAllButton",
				LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_ShowAll",
				StyleType = ButtonStyle.StyleType.OutlinedSmall
			};
			_buttonStateData.Add(buttonStateData.BrowserElementKey, buttonStateData);
		}
		_buttonStateData["ShowAllButton"].IsActive = _choiceOptions.HintingOptions.Count > 0 && _choiceOptions.SortedChoices.Count > 15 && !_showingAll;
		if (!_buttonStateData.ContainsKey("CancelButton"))
		{
			ButtonStateData buttonStateData2 = new ButtonStateData
			{
				BrowserElementKey = "CancelButton",
				LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Cancel",
				StyleType = ButtonStyle.StyleType.Secondary
			};
			_buttonStateData.Add(buttonStateData2.BrowserElementKey, buttonStateData2);
		}
		_buttonStateData["CancelButton"].IsActive = _request.CanCancel;
		if (!_buttonStateData.ContainsKey("DoneButton"))
		{
			ButtonStateData buttonStateData3 = new ButtonStateData
			{
				BrowserElementKey = "DoneButton",
				LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done",
				StyleType = ButtonStyle.StyleType.Secondary
			};
			_buttonStateData.Add(buttonStateData3.BrowserElementKey, buttonStateData3);
		}
		_buttonStateData["DoneButton"].Enabled = _request.MinSel <= _selections.Count && _selections.Count <= _request.MaxSel;
	}
}
