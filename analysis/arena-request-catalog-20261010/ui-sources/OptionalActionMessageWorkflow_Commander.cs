using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtgo.Gre.External.Messaging;

public class OptionalActionMessageWorkflow_Commander : WorkflowBase<OptionalActionMessageRequest>
{
	public OptionalActionMessageWorkflow_Commander(OptionalActionMessageRequest request)
		: base(request)
	{
	}

	protected override void ApplyInteractionInternal()
	{
		SetButtons();
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		ZoneType numberValue = (ZoneType)_request.Prompt.Parameters[1].NumberValue;
		bool flag = numberValue == ZoneType.Hand;
		ZoneType zoneType = (flag ? numberValue : ZoneType.Command);
		ZoneType zoneType2 = (flag ? ZoneType.Command : numberValue);
		OptionResponse primaryBtnResponse = ((!flag) ? OptionResponse.AllowYes : OptionResponse.CancelNo);
		OptionResponse secondaryBtnResponse = (flag ? OptionResponse.AllowYes : OptionResponse.CancelNo);
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = getZoneText(zoneType),
			Style = ButtonStyle.StyleType.Main,
			Tag = ButtonTag.Primary,
			ButtonCallback = () =>
			{
				_request.SubmitResponse(primaryBtnResponse);
			}
		});
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = getZoneText(zoneType2),
			Tag = ButtonTag.Secondary,
			Style = ButtonStyle.StyleType.Secondary,
			ButtonCallback = () =>
			{
				_request.SubmitResponse(secondaryBtnResponse);
			}
		});
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
		static string getZoneText(ZoneType zoneType3)
		{
			return zoneType3 switch
			{
				ZoneType.Graveyard => "Enum/ZoneType/ZoneType_Graveyard", 
				ZoneType.Exile => "Enum/ZoneType/ZoneType_Exile", 
				ZoneType.Hand => "Enum/ZoneType/ZoneType_Hand", 
				ZoneType.Library => "Enum/ZoneType/ZoneType_Library", 
				ZoneType.Battlefield => "Enum/ZoneType/ZoneType_Battlefield", 
				ZoneType.Stack => "Enum/ZoneType/ZoneType_Stack", 
				ZoneType.Command => "Enum/ZoneType/ZoneType_Command", 
				_ => "Enum/ZoneType/ZoneType_Limbo", 
			};
		}
	}
}
