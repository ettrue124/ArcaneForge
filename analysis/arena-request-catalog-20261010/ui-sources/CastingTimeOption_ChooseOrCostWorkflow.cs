using System;
using System.Collections.Generic;
using AssetLookupTree.Blackboard;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class CastingTimeOption_ChooseOrCostWorkflow : WorkflowBase<CastingTimeOption_ChooseOrCostRequest>, IDuelSceneBrowserProvider, IBrowserHeaderProvider, IButtonScrollListBrowserProvider
{
	private readonly struct ButtonDataOverride(ButtonStyle.StyleType? primary, ButtonStyle.StyleType? secondary, bool? reverse)
	{
		public readonly ButtonStyle.StyleType PrimaryStyle = primary.GetValueOrDefault();

		public readonly ButtonStyle.StyleType SecondaryStyle = secondary.GetValueOrDefault();

		public readonly bool ReverseButtons = reverse == true;
	}

	private const string PROMPT_ERROR = "PROMPT_ERROR";

	private static Dictionary<uint, ButtonDataOverride> _grpIdButtonOverrides = new Dictionary<uint, ButtonDataOverride> { 
	{
		90948u,
		new ButtonDataOverride(ButtonStyle.StyleType.Main, ButtonStyle.StyleType.Secondary, true)
	} };

	private readonly IPromptEngine _promptEngine;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IClientLocProvider _locProvider;

	private ButtonScrollListBrowser _openedBrowser;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _scrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, uint> _keywordToIdMapping = new Dictionary<string, uint>();

	public CastingTimeOption_ChooseOrCostWorkflow(CastingTimeOption_ChooseOrCostRequest request, ICardViewProvider viewManager, IPromptEngine promptEngine, IBrowserController browserController, IClientLocProvider locProvider)
		: base(request)
	{
		_request = request;
		_promptEngine = promptEngine;
		_cardViewProvider = viewManager;
		_browserController = browserController;
		_locProvider = locProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		OldWorkflow();
	}

	private void OldWorkflow()
	{
		if (_request.Options.Count > 0)
		{
			List<DuelScene_CDC> list = new List<DuelScene_CDC>();
			foreach (KeyValuePair<uint, uint> option in _request.Options)
			{
				uint value = option.Value;
				if (_cardViewProvider.TryGetCardView(value, out var cardView) && cardView.Model.Zone.Type == ZoneType.Hand && cardView.Model.Zone.Owner.ClientPlayerEnum != GREPlayerNum.LocalPlayer)
				{
					list.Add(cardView);
				}
			}
			if (list.Count > 0)
			{
				ViewDismissBrowserProvider viewDismissBrowserProvider = new ViewDismissBrowserProvider(list, SetupPrompt);
				IBrowser openedBrowser = _browserController.OpenBrowser(viewDismissBrowserProvider);
				viewDismissBrowserProvider.SetOpenedBrowser(openedBrowser);
			}
			else
			{
				SetupPrompt();
			}
		}
		else
		{
			SetupPrompt();
		}
	}

	private void SetupPrompt()
	{
		if (_request.Options.Count > 2)
		{
			if (_request.Max != 1 || _request.Min != 1)
			{
				throw new NotImplementedException("Currently only support 1 selection for CastingTimeOption_ChooseOrCostWorkflow with more than 2 options.");
			}
			foreach (KeyValuePair<uint, uint> option in _request.Options)
			{
				string promptText = _promptEngine.GetPromptText((int)option.Key);
				uint value = option.Value;
				_keywordToIdMapping[promptText] = value;
				_scrollListButtonStateData[promptText] = new ButtonStateData
				{
					LocalizedString = new UnlocalizedMTGAString(promptText),
					StyleType = ButtonStyle.StyleType.Secondary
				};
			}
			if (_request.CanCancel)
			{
				_browserButtonStateData["CancelButton"] = new ButtonStateData
				{
					BrowserElementKey = "SingleButton",
					LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel",
					StyleType = ButtonStyle.StyleType.Main
				};
			}
			_openedBrowser = (ButtonScrollListBrowser)_browserController.OpenBrowser(this);
			_openedBrowser.ButtonPressedHandlers += Browser_OnButtonPressed;
			_openedBrowser.ClosedHandlers += Browser_OnClosed;
		}
		else
		{
			SetButtons();
		}
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		foreach (KeyValuePair<uint, uint> kvp in _request.Options)
		{
			string promptText = _promptEngine.GetPromptText((int)kvp.Key);
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = new UnlocalizedMTGAString
				{
					Key = (string.IsNullOrEmpty(promptText) ? "PROMPT_ERROR" : promptText)
				},
				Style = ((Buttons.WorkflowButtons.Count == 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary),
				ButtonCallback = () =>
				{
					_request.SubmitChoice(kvp.Value);
				}
			});
		}
		ApplyButtonOverrides(Buttons.WorkflowButtons);
		if (_request.CanCancel)
		{
			ButtonStyle.StyleType style = ((Buttons.WorkflowButtons.Count == 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				Style = style,
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

	private void ApplyButtonOverrides(List<PromptButtonData> buttons)
	{
		if (_grpIdButtonOverrides.TryGetValue(_request.GrpId, out var value))
		{
			if (value.ReverseButtons)
			{
				buttons.Reverse();
			}
			for (int i = 0; i < buttons.Count; i++)
			{
				ButtonStyle.StyleType style = ((i == 0) ? value.PrimaryStyle : value.SecondaryStyle);
				buttons[i].Style = style;
			}
		}
		else if (buttons.Count == 2 && !buttons.Exists((PromptButtonData x) => x.ButtonText.Key.Equals("Decline")))
		{
			buttons.ForEach((PromptButtonData x) =>
			{
				x.Style = ButtonStyle.StyleType.Secondary;
			});
		}
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonScrollList;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _browserButtonStateData;
	}

	public string GetHeaderText()
	{
		return _locProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_One");
	}

	public string GetSubHeaderText()
	{
		return _promptEngine.GetPromptText((int)_request.Prompt.PromptId);
	}

	public Dictionary<string, ButtonStateData> GetScrollListButtonDataByKey()
	{
		return _scrollListButtonStateData;
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
	}

	private void Browser_OnButtonPressed(string buttonKey)
	{
		uint value;
		if (buttonKey == "CancelButton")
		{
			_request.Cancel();
		}
		else if (_keywordToIdMapping.TryGetValue(buttonKey, out value))
		{
			_request.SubmitChoice(value);
		}
	}

	private void Browser_OnClosed()
	{
		_openedBrowser.ButtonPressedHandlers -= Browser_OnButtonPressed;
		_openedBrowser.ClosedHandlers -= Browser_OnClosed;
		_openedBrowser = null;
	}
}
