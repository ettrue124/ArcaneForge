using System;
using GreClient.Rules;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class ReplicateWorkflow : WorkflowBase<CastingTimeOptionRequest>
{
	private const string BUTTON_PARAM = "quantity";

	private readonly IChooseXInterfaceBuilder _interfaceBuilder;

	private readonly IClientLocProvider _locProvider;

	private readonly CastingTimeOption_Replicate _replicate;

	private readonly CastingTimeOption_DoneRequest _done;

	private IChooseXInterface _chooseXInterface;

	private uint _current;

	public ReplicateWorkflow(CastingTimeOptionRequest req, IChooseXInterfaceBuilder interfaceBuilder, IClientLocProvider locProvider)
		: base(req)
	{
		_interfaceBuilder = interfaceBuilder ?? NullChooseXBuilder.Default;
		_locProvider = locProvider;
		_replicate = (CastingTimeOption_Replicate)req.ChildRequests.Find((BaseUserRequest x) => x is CastingTimeOption_Replicate);
		_done = (CastingTimeOption_DoneRequest)req.ChildRequests.Find((BaseUserRequest x) => x is CastingTimeOption_DoneRequest);
	}

	protected override void ApplyInteractionInternal()
	{
		DestroyInterface();
		_chooseXInterface = _interfaceBuilder.CreateInterface("ReplicateWorkflow");
		_chooseXInterface.Submit += OnSubmit;
		_chooseXInterface.ValueModified += ModifyValue;
		OpenInterface();
		SetButtons();
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		if (_request.CanCancel && Buttons.CancelData == null)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel",
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = OnCancel
			};
		}
		OnUpdateButtons(Buttons);
	}

	private void OnCancel()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_cancel, AudioManager.Default);
		}
	}

	private void ModifyValue(int change)
	{
		_current = (uint)Math.Clamp(_current + change, 0L, _replicate.Max);
		_chooseXInterface.SetButtonText(ButtonText());
		_chooseXInterface.SetVisualState(GetVisualState(_current));
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_generic_click, AudioManager.Default);
	}

	private void OnSubmit()
	{
		if (_current == 0)
		{
			_done.SubmitDone();
		}
		else
		{
			_replicate.SubmitValue(_current);
		}
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_submit, AudioManager.Default);
	}

	private void OpenInterface()
	{
		_chooseXInterface.SetButtonText(ButtonText());
		_chooseXInterface.SetVisualState(GetVisualState(_current));
		_chooseXInterface.SetButtonStyle(ButtonStyle.StyleType.Main);
		_chooseXInterface.Open();
		SetButtons();
	}

	private string ButtonText()
	{
		return _locProvider.GetLocalizedText("DuelScene/ClientPrompt/ChooseX_Replicate", ("quantity", _current.ToString()));
	}

	public override void CleanUp()
	{
		DestroyInterface();
		base.CleanUp();
	}

	private void DestroyInterface()
	{
		if (_chooseXInterface != null)
		{
			_chooseXInterface.Submit -= OnSubmit;
			_chooseXInterface.ValueModified -= ModifyValue;
			_interfaceBuilder.DestroyInterface(_chooseXInterface, "ReplicateWorkflow");
		}
	}

	private static NumericInputVisualState GetVisualState(uint current)
	{
		NumericInputVisualState numericInputVisualState = NumericInputVisualState.CanSubmit;
		numericInputVisualState |= NumericInputVisualState.IncrementEnabled;
		numericInputVisualState |= NumericInputVisualState.IncrementManyEnabled;
		if (current != 0)
		{
			numericInputVisualState |= NumericInputVisualState.DecrementEnabled;
			if (current > 4)
			{
				numericInputVisualState |= NumericInputVisualState.DecrementManyEnabled;
			}
		}
		return numericInputVisualState;
	}
}
