using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene.Logging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class ChooseXWorkflow : WorkflowBase<CastingTimeOption_NumericInputRequest>
{
	private readonly IChooseXInterfaceBuilder _interfaceBuilder;

	private readonly IConfirmZeroLogger _confirmZeroLogger;

	private readonly string _buttonLocKey;

	private readonly uint _min;

	private readonly uint _max;

	private IChooseXInterface _chooseXInterface = NullChooseXInterface.Default;

	private uint _current;

	private bool _confirmZero;

	public ChooseXWorkflow(CastingTimeOption_NumericInputRequest request, IChooseXInterfaceBuilder interfaceBuilder, IConfirmZeroLogger confirmZeroLogger, string buttonLocKey)
		: base(request)
	{
		_interfaceBuilder = interfaceBuilder ?? NullChooseXBuilder.Default;
		_confirmZeroLogger = confirmZeroLogger ?? NullConfirmZeroLogger.Default;
		_buttonLocKey = buttonLocKey ?? string.Empty;
		_min = request.Min;
		_max = request.Max;
		_current = _min;
	}

	protected override void ApplyInteractionInternal()
	{
		_chooseXInterface = _interfaceBuilder.CreateInterface("ChooseXWorkflow");
		_chooseXInterface.Submit += OnSubmit;
		_chooseXInterface.ValueModified += ModifyValue;
		OpenInterface();
	}

	public override void TryUndo()
	{
		if (_confirmZero && _request.AllowUndo)
		{
			_confirmZeroLogger.UndoSelected();
		}
		base.TryUndo();
	}

	private void OpenInterface()
	{
		_confirmZero = false;
		_chooseXInterface.SetButtonText(InterfaceButtonText(_current));
		_chooseXInterface.SetVisualState(NumericInputConversion.ToVisualState(_current, _request));
		_chooseXInterface.SetButtonStyle(ButtonStyle.StyleType.Main);
		_chooseXInterface.Open();
		SetButtons();
	}

	private string InterfaceButtonText(uint current)
	{
		return new MTGALocalizedString
		{
			Key = _buttonLocKey,
			Parameters = new Dictionary<string, string>
			{
				{
					"quantity",
					current.ToString()
				},
				{
					"count",
					current.ToString()
				}
			}
		};
	}

	private void ModifyValue(int change)
	{
		_current = (uint)Mathf.Clamp(_current + change, _min, _max);
		_chooseXInterface.SetButtonText(InterfaceButtonText(_current));
		_chooseXInterface.SetVisualState(NumericInputConversion.ToVisualState(_current, _request));
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_generic_click, AudioManager.Default);
	}

	private void OnSubmitConfirmZero()
	{
		_confirmZeroLogger.ConfirmZeroSelected();
		OnSubmit();
	}

	private void OnBackConfirmZero()
	{
		_confirmZeroLogger.BackSelected();
		OpenInterface();
	}

	private void OnSubmit()
	{
		if (!NumericInputValidation.CanSubmit(_current, _request))
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid, AudioManager.Default);
		}
		else if (_current == 0 && !_confirmZero)
		{
			_confirmZero = true;
			SetButtons();
		}
		else
		{
			_request.SubmitX(_current);
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_submit, AudioManager.Default);
		}
	}

	private void OnCancel()
	{
		if (_request.AllowUndo)
		{
			_request.Undo();
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_cancel, AudioManager.Default);
		}
		else if (_request.CanCancel)
		{
			_request.Cancel();
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_cancel, AudioManager.Default);
		}
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		Buttons.DisplayUndo = false;
		if (_confirmZero)
		{
			_confirmZeroLogger.ConfirmZeroDisplayed(_request.SourceId);
			_chooseXInterface.Close();
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Confirm_Quantity_Button_Text",
					Parameters = new Dictionary<string, string> { 
					{
						"quantity",
						_current.ToString()
					} }
				},
				Style = ButtonStyle.StyleType.Main,
				ButtonCallback = OnSubmitConfirmZero
			});
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Back",
				Style = ButtonStyle.StyleType.Outlined,
				ClearsInteractions = false,
				ButtonCallback = OnBackConfirmZero
			});
		}
		else if (_request.CanCancel)
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

	public override void CleanUp()
	{
		base.CleanUp();
		_chooseXInterface.Submit -= OnSubmit;
		_chooseXInterface.ValueModified -= ModifyValue;
		_interfaceBuilder.DestroyInterface(_chooseXInterface, "ChooseXWorkflow");
		_confirmZeroLogger.WorkflowCleanup();
	}
}
