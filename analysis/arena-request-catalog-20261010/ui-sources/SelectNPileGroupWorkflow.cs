using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;

internal class SelectNPileGroupWorkflow : GroupedBrowserWorkflow<SelectNGroupRequest>, ICardStackWorkflow
{
	private class SelectNPileGroupWorkflow_HighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _pile1;

		private readonly IReadOnlyCollection<uint> _pile2;

		private bool _showConfirmationPrompt;

		private int _selectedPile;

		public SelectNPileGroupWorkflow_HighlightsGenerator(IReadOnlyCollection<uint> pile1, IReadOnlyCollection<uint> pile2)
		{
			_pile1 = pile1;
			_pile2 = pile2;
		}

		public void SetShowingConfirmationPrompt(bool showPromptHighlights, int selectedPile)
		{
			_showConfirmationPrompt = showPromptHighlights;
			_selectedPile = selectedPile;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			if (_showConfirmationPrompt)
			{
				foreach (uint item in _pile1)
				{
					highlights.IdToHighlightType_Workflow[item] = ((_selectedPile == 0) ? HighlightType.Selected : HighlightType.None);
				}
				foreach (uint item2 in _pile2)
				{
					highlights.IdToHighlightType_Workflow[item2] = ((_selectedPile == 1) ? HighlightType.Selected : HighlightType.None);
				}
			}
			else
			{
				foreach (uint item3 in _pile1)
				{
					highlights.IdToHighlightType_Workflow[item3] = HighlightType.Cold;
				}
				foreach (uint item4 in _pile2)
				{
					highlights.IdToHighlightType_Workflow[item4] = HighlightType.Hot;
				}
			}
			return highlights;
		}
	}

	private const string PILE_SACRIFICE_NUMBER_STRING = "pileNum";

	private int sacrificePileNumber = 1;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _browserHeaderTextProvider;

	private Dictionary<int, List<uint>> _piles = new Dictionary<int, List<uint>>();

	public SelectNPileGroupWorkflow(SelectNGroupRequest request, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider browserHeaderTextProvider)
		: base(request)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_browserHeaderTextProvider = browserHeaderTextProvider ?? NullBrowserHeaderTextProvider.Default;
		for (int i = 0; i < _request.Groups.Count; i++)
		{
			List<uint> list = new List<uint>();
			foreach (uint id in _request.Groups[i].Ids)
			{
				list.Add(id);
			}
			_piles.Add(i, list);
		}
		if (_piles.Count > 1)
		{
			_highlightsGenerator = new SelectNPileGroupWorkflow_HighlightsGenerator(_piles[0], _piles[1]);
		}
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay = new List<List<DuelScene_CDC>>();
		InitializeButtonStateData();
		for (int i = 0; i < _piles.Count; i++)
		{
			_cardsToDisplay.Add(_cardViewProvider.GetCardViews(_piles[i]));
		}
		SetHeaderAndSubheader();
		SetOpenedBrowser(OpenBrowser());
	}

	private IBrowser OpenBrowser()
	{
		IBrowser browser = _browserController.OpenBrowser(this);
		if (browser is SelectGroupBrowser selectGroupBrowser)
		{
			selectGroupBrowser.SetHeader(_header);
			selectGroupBrowser.SetSubheader(_subHeader);
		}
		return browser;
	}

	private void SetHeaderAndSubheader()
	{
		_browserHeaderTextProvider.ClearParams();
		_header = _browserHeaderTextProvider.GetHeaderText("DuelScene/ClientPrompt/Select_Pile_Sacrifice");
		_subHeader = _browserHeaderTextProvider.GetSubHeaderText(_request.Prompt);
	}

	private void InitializeButtonStateData()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.Enabled = true;
		buttonStateData.LocalizedString = "DuelScene/Browsers/Select_Title";
		buttonStateData.BrowserElementKey = "GroupAButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("GroupAButton", buttonStateData);
		ButtonStateData buttonStateData2 = new ButtonStateData();
		buttonStateData2.Enabled = true;
		buttonStateData2.LocalizedString = "DuelScene/Browsers/Select_Title";
		buttonStateData2.BrowserElementKey = "GroupBButton";
		buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("GroupBButton", buttonStateData2);
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/Interaction/OptionalAction/Option_Sacrifice",
			Style = ButtonStyle.StyleType.Main,
			ButtonCallback = () =>
			{
				Submit();
			}
		});
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Back",
			Style = ButtonStyle.StyleType.Outlined,
			ButtonCallback = () =>
			{
				BackToBrowser();
			}
		});
		OnUpdateButtons(Buttons);
	}

	private void BackToBrowser()
	{
		if (_highlightsGenerator is SelectNPileGroupWorkflow_HighlightsGenerator selectNPileGroupWorkflow_HighlightsGenerator)
		{
			selectNPileGroupWorkflow_HighlightsGenerator.SetShowingConfirmationPrompt(showPromptHighlights: false, sacrificePileNumber);
		}
		ApplyInteractionInternal();
		UpdateHighlightsAndDimming();
	}

	private void OnPileSelected(int groupIndex)
	{
		if (_piles[groupIndex].Count > 0)
		{
			sacrificePileNumber = groupIndex;
			_browserController.CloseCurrentBrowser();
			_workflowPrompt.Reset();
			_workflowPrompt.LocKey = "DuelScene/ClientPrompt/Sacrifice_Pile_Number_Confirmation";
			_workflowPrompt.LocParams = new (string, string)[1] { ("pileNum", (sacrificePileNumber + 1).ToString()) };
			OnUpdatePrompt(_workflowPrompt);
			SetButtons();
			if (_highlightsGenerator is SelectNPileGroupWorkflow_HighlightsGenerator selectNPileGroupWorkflow_HighlightsGenerator)
			{
				selectNPileGroupWorkflow_HighlightsGenerator.SetShowingConfirmationPrompt(showPromptHighlights: true, sacrificePileNumber);
			}
			UpdateHighlightsAndDimming();
		}
		else
		{
			_request.SubmitGroupSelection((uint)_request.Groups[groupIndex].GroupId);
		}
	}

	private void Submit()
	{
		_request.SubmitGroupSelection((uint)_request.Groups[sacrificePileNumber].GroupId);
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectGroup;
	}

	public override void Browser_OnButtonPressed(string buttonKey)
	{
		OnPileSelected((!(buttonKey == "GroupAButton")) ? 1 : 0);
	}

	public override string GetCardHolderLayoutKey()
	{
		return "SelectGroup";
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		List<uint> list = _piles[0];
		List<uint> list2 = _piles[1];
		uint instanceId = lhs.InstanceId;
		uint instanceId2 = rhs.InstanceId;
		if ((list.Contains(instanceId) && list.Contains(instanceId2)) || (list2.Contains(instanceId) && list2.Contains(instanceId2)))
		{
			return true;
		}
		return false;
	}
}
