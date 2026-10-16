using System.Collections.Generic;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceSelectionWorkflow : WorkflowBase<SelectNRequest>
{
	private const int MinOptionsToEnableHinting = 15;

	private readonly IObjectPool _objectPool;

	private readonly IBrowserManager _browserManager;

	private readonly ChoiceOptions _choiceOptions;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly List<uint> _selectedIds;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStates;

	private ChoiceSelectionBrowser _choiceSelectionBrowser;

	public ChoiceSelectionWorkflow(SelectNRequest request, IObjectPool objectPool, IBrowserManager browserManager, ChoiceOptions choiceOptions, IPromptTextProvider promptTextProvider)
		: base(request)
	{
		_choiceOptions = choiceOptions;
		_objectPool = objectPool;
		_browserManager = browserManager;
		_promptTextProvider = promptTextProvider;
		_selectedIds = _objectPool.PopObject<List<uint>>();
		_browserButtonStates = _objectPool.PopObject<Dictionary<string, ButtonStateData>>();
	}

	public override bool CanApply(List<UXEvent> events)
	{
		if (events.Count == 0)
		{
			return !_browserManager.IsAnyBrowserOpen;
		}
		return false;
	}

	protected override void ApplyInteractionInternal()
	{
		OpenBrowser();
	}

	private void OpenBrowser()
	{
		if (_choiceSelectionBrowser == null && _browserManager.OpenBrowser(new ChoiceSelectionBrowserProvider(SetUpButtons(_browserButtonStates))) is ChoiceSelectionBrowser choiceSelectionBrowser)
		{
			choiceSelectionBrowser.InitChoiceFilter(_choiceOptions.SortedChoices, _choiceOptions.HintingOptions, (uint)_request.MinSel, _request.MaxSel);
			choiceSelectionBrowser.SetHeader(_promptTextProvider.GetPromptText(_request.Prompt));
			choiceSelectionBrowser.SubmitClicked += OnSubmitClicked;
			choiceSelectionBrowser.CancelClicked += OnCancelClicked;
			choiceSelectionBrowser.ShowAllClicked += ShowAllClicked;
			choiceSelectionBrowser.SelectionsUpdated += OnSelectionsUpdated;
			_choiceSelectionBrowser = choiceSelectionBrowser;
		}
	}

	private Dictionary<string, ButtonStateData> SetUpButtons(Dictionary<string, ButtonStateData> buttonStates)
	{
		buttonStates.Clear();
		buttonStates["DoneButton"] = new ButtonStateData
		{
			BrowserElementKey = "DoneButton",
			LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done",
			StyleType = ButtonStyle.StyleType.Secondary,
			Enabled = CanSubmit(_selectedIds.Count)
		};
		if (_choiceOptions.HintingOptions.Count > 0 && _choiceOptions.SortedChoices.Count > 15)
		{
			buttonStates["ShowAllButton"] = new ButtonStateData
			{
				BrowserElementKey = "ShowAllButton",
				LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_ShowAll",
				StyleType = ButtonStyle.StyleType.OutlinedSmall
			};
		}
		if (_request.CanCancel)
		{
			buttonStates["CancelButton"] = new ButtonStateData
			{
				BrowserElementKey = "CancelButton",
				LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Cancel",
				StyleType = ButtonStyle.StyleType.Secondary
			};
		}
		return buttonStates;
	}

	private void OnSubmitClicked()
	{
		_request.SubmitSelection(_selectedIds);
	}

	private void OnCancelClicked()
	{
		_request.Cancel();
	}

	private void ShowAllClicked()
	{
		if (_browserButtonStates.TryGetValue("ShowAllButton", out var value))
		{
			value.IsActive = false;
			_choiceSelectionBrowser.SetButtons(_browserButtonStates);
			_choiceSelectionBrowser.ShowAllOptions();
		}
	}

	private void OnSelectionsUpdated(IEnumerable<string> selections)
	{
		_selectedIds.Clear();
		foreach (string selection in selections)
		{
			_selectedIds.Add(_choiceOptions.ChoiceToIdMap[selection]);
		}
		_browserButtonStates["DoneButton"].Enabled = CanSubmit(_selectedIds.Count);
		_choiceSelectionBrowser.SetButtons(_browserButtonStates);
	}

	private bool CanSubmit(int selectionCount)
	{
		if (selectionCount >= _request.MinSel)
		{
			return selectionCount <= _request.MaxSel;
		}
		return false;
	}

	private void CloseBrowser()
	{
		if (_choiceSelectionBrowser != null)
		{
			_choiceSelectionBrowser.SetHeader(string.Empty);
			_choiceSelectionBrowser.SubmitClicked -= OnSubmitClicked;
			_choiceSelectionBrowser.CancelClicked -= OnCancelClicked;
			_choiceSelectionBrowser.ShowAllClicked -= ShowAllClicked;
			_choiceSelectionBrowser.SelectionsUpdated -= OnSelectionsUpdated;
			_choiceSelectionBrowser.Close();
			_choiceSelectionBrowser = null;
		}
	}

	public override void CleanUp()
	{
		CloseBrowser();
		_selectedIds.Clear();
		_objectPool.PushObject(_selectedIds, tryClear: false);
		_browserButtonStates.Clear();
		_objectPool.PushObject(_browserButtonStates, tryClear: false);
		base.CleanUp();
	}
}
