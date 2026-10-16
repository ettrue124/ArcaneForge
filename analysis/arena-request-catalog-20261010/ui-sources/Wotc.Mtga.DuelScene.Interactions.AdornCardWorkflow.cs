using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions;

public class AdornCardWorkflow : WorkflowBase<StringInputRequest>
{
	private static IReadOnlyCollection<string> ALL_SKIN_CODES;

	private const string HEADER_TEXT = "Select card skin code";

	private const string RESET_OPTION_TEXT = "Reset";

	private readonly IObjectPool _objectPool;

	private readonly IDatabaseUtilities _cardDatabaseUtils;

	private readonly IBrowserController _browserController;

	private string _selectedSkinCode = string.Empty;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStates;

	private ChoiceSelectionBrowser _choiceSelectionBrowser;

	public AdornCardWorkflow(StringInputRequest req, IObjectPool objectPool, IBrowserController browserController, IDatabaseUtilities cardDataProvider)
		: base(req)
	{
		_objectPool = objectPool ?? NullObjectPool.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_cardDatabaseUtils = cardDataProvider ?? new NullDatabaseUtilities();
		_browserButtonStates = _objectPool.PopObject<Dictionary<string, ButtonStateData>>();
	}

	protected override void ApplyInteractionInternal()
	{
		OpenBrowser();
	}

	private void OpenBrowser()
	{
		if (_choiceSelectionBrowser == null && _browserController.OpenBrowser(new ChoiceSelectionBrowserProvider(SetUpButtons(_browserButtonStates))) is ChoiceSelectionBrowser choiceSelectionBrowser)
		{
			choiceSelectionBrowser.InitChoiceFilter(GetAllSkinCodes());
			choiceSelectionBrowser.SetHeader("Select card skin code");
			choiceSelectionBrowser.SubmitClicked += OnSubmitClicked;
			choiceSelectionBrowser.CancelClicked += OnCancelClicked;
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
			Enabled = !string.IsNullOrEmpty(_selectedSkinCode)
		};
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
		_request.SubmitValue(_selectedSkinCode);
	}

	private void OnCancelClicked()
	{
		_request.Cancel();
	}

	private void OnSelectionsUpdated(IEnumerable<string> selections)
	{
		_selectedSkinCode = string.Empty;
		using (IEnumerator<string> enumerator = selections.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				string current = enumerator.Current;
				_selectedSkinCode = current;
			}
		}
		_browserButtonStates["DoneButton"].Enabled = !string.IsNullOrEmpty(_selectedSkinCode);
		_choiceSelectionBrowser.SetButtons(_browserButtonStates);
	}

	private void CloseBrowser()
	{
		if (_choiceSelectionBrowser != null)
		{
			_choiceSelectionBrowser.SetHeader(string.Empty);
			_choiceSelectionBrowser.SubmitClicked -= OnSubmitClicked;
			_choiceSelectionBrowser.CancelClicked -= OnCancelClicked;
			_choiceSelectionBrowser.SelectionsUpdated -= OnSelectionsUpdated;
			_choiceSelectionBrowser.Close();
			_choiceSelectionBrowser = null;
		}
	}

	public override void CleanUp()
	{
		CloseBrowser();
		_selectedSkinCode = string.Empty;
		_browserButtonStates.Clear();
		_objectPool.PushObject(_browserButtonStates, tryClear: false);
		base.CleanUp();
	}

	private IReadOnlyCollection<string> GetAllSkinCodes()
	{
		return ALL_SKIN_CODES ?? (ALL_SKIN_CODES = LoadAllSkinCodes());
	}

	private IReadOnlyCollection<string> LoadAllSkinCodes()
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (KeyValuePair<uint, CardPrintingData> allPrinting in _cardDatabaseUtils.GetAllPrintings())
		{
			hashSet.UnionWith(allPrinting.Value.KnownSupportedStyles);
		}
		return hashSet.OrderBy((string x) => x).Prepend("Reset").ToArray();
	}
}
