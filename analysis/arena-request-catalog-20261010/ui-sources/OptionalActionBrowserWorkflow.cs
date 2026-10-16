using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

public abstract class OptionalActionBrowserWorkflow : BrowserWorkflowBase<OptionalActionMessageRequest>
{
	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.OptionalAction;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "OptionalAction";
	}

	protected OptionalActionBrowserWorkflow(OptionalActionMessageRequest request)
		: base(request)
	{
	}

	protected void SetupButtons(string yesButtonText, string noButtonText)
	{
		SetupButtons(yesButtonText, noButtonText, yesOnRight: false, ButtonStyle.StyleType.Secondary, ButtonStyle.StyleType.Secondary);
	}

	protected void SetupButtons(string yesButtonText, string noButtonText, bool yesOnRight)
	{
		SetupButtons(yesButtonText, noButtonText, yesOnRight, ButtonStyle.StyleType.Secondary, ButtonStyle.StyleType.Secondary);
	}

	protected void SetupButtons(string yesButtonText, string noButtonText, ButtonStyle.StyleType yesButtonStyle, ButtonStyle.StyleType noButtonStyle)
	{
		SetupButtons(yesButtonText, noButtonText, yesOnRight: false, yesButtonStyle, noButtonStyle);
	}

	protected void SetupButtons(string yesButtonText, string noButtonText, bool yesOnRight, ButtonStyle.StyleType yesButtonStyle, ButtonStyle.StyleType noButtonStyle)
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData value = new ButtonStateData
		{
			LocalizedString = noButtonText,
			Enabled = true,
			BrowserElementKey = (yesOnRight ? "2Button_Left" : "2Button_Right"),
			StyleType = noButtonStyle
		};
		_buttonStateData.Add("NoButton", value);
		ButtonStateData value2 = new ButtonStateData
		{
			LocalizedString = yesButtonText,
			Enabled = true,
			BrowserElementKey = (yesOnRight ? "2Button_Right" : "2Button_Left"),
			StyleType = yesButtonStyle
		};
		_buttonStateData.Add("YesButton", value2);
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		OnBrowserActionSelected((!(buttonKey == "NoButton")) ? OptionResponse.AllowYes : OptionResponse.CancelNo);
	}

	protected virtual void OnBrowserActionSelected(OptionResponse response)
	{
		_request.SubmitResponse(response);
	}
}
