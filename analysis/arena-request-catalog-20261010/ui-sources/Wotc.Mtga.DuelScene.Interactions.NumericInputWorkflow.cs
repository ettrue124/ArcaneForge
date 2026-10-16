using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using UnityEngine;
using WorkflowVisuals;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class NumericInputWorkflow : WorkflowBase<NumericInputRequest>
{
	private const uint ENERGY_PROMPT_ID = 1071u;

	private const uint PROMPT_ID_TERRA = 14764u;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IGameStateProvider _gameStateProvider;

	private IChooseXInterfaceBuilder _interfaceBuilder;

	private IChooseXInterface _chooseXInterface = new NullChooseXInterface();

	private uint _current;

	private readonly uint _min;

	private readonly uint _max;

	private bool _displayingConfirmation;

	private bool _requiresConfirmation = true;

	public NumericInputType NumericInputType => _request.InputType;

	public NumericInputWorkflow(NumericInputRequest request, IChooseXInterfaceBuilder interfaceBuilder, IEntityViewProvider entityEntityViewProvider, AssetLookupSystem assetLookupSystem, IGameStateProvider gameStateProvider)
		: base(request)
	{
		_entityViewProvider = entityEntityViewProvider;
		_interfaceBuilder = interfaceBuilder ?? new NullChooseXBuilder();
		_assetLookupSystem = assetLookupSystem;
		_gameStateProvider = gameStateProvider;
		_min = request.Min;
		_max = request.Max;
		_current = _min;
	}

	protected override void ApplyInteractionInternal()
	{
		if (IsEnergyWorkflow())
		{
			_entityViewProvider.GetAvatarByPlayerSide(GREPlayerNum.LocalPlayer).SetCounterHighlights(CounterType.Energy);
		}
		_requiresConfirmation = SourceRequiresConfirmation(_entityViewProvider.GetCardView(_request.SourceId), _assetLookupSystem);
		_chooseXInterface = _interfaceBuilder.CreateInterface("NumericInputWorkflow");
		_chooseXInterface.Submit += OnSubmit;
		_chooseXInterface.ValueModified += ModifyValue;
		if (ItsTerraTime(_request.Prompt) && _request.SuggestedValues.Any())
		{
			_current = _request.SuggestedValues.Min();
		}
		OpenInterface();
	}

	private bool IsEnergyWorkflow()
	{
		if (_request == null)
		{
			return false;
		}
		if (_request.Prompt == null)
		{
			return false;
		}
		return _request.Prompt.PromptId == 1071;
	}

	private void OpenInterface()
	{
		_displayingConfirmation = false;
		_chooseXInterface.SetButtonText(InterfaceButtonText());
		_chooseXInterface.SetVisualState(NumericInputConversion.ToVisualState(_current, _request));
		_chooseXInterface.SetButtonStyle(SetButtonStyle());
		_chooseXInterface.Open();
		SetButtons();
	}

	private void OnSubmit()
	{
		if (_current == 0 && _requiresConfirmation && !_displayingConfirmation)
		{
			_displayingConfirmation = true;
			SetButtons();
		}
		else if (NumericInputValidation.CanSubmit(_current, _request))
		{
			_request.SubmitValue(_current);
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_submit, AudioManager.Default);
		}
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
		_current = (uint)Mathf.Clamp(_current + change, _min, _max);
		_chooseXInterface.SetButtonText(InterfaceButtonText());
		_chooseXInterface.SetVisualState(NumericInputConversion.ToVisualState(_current, _request));
		_chooseXInterface.SetButtonStyle(SetButtonStyle());
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_generic_click, AudioManager.Default);
	}

	private string InterfaceButtonText()
	{
		return new MTGALocalizedString
		{
			Key = GetButtonLocKey(GetSourceCDC(_request.SourceId, _gameStateProvider.LatestGameState, _entityViewProvider), _assetLookupSystem),
			Parameters = new Dictionary<string, string> { 
			{
				"quantity",
				_current.ToString()
			} }
		};
	}

	private static DuelScene_CDC GetSourceCDC(uint sourceId, MtgGameState gameState, ICardViewProvider cardProvider)
	{
		if (!gameState.TryGetCard(sourceId, out var card) || !cardProvider.TryGetCardView(card.InstanceId, out var cardView))
		{
			return null;
		}
		return cardView;
	}

	private ButtonStyle.StyleType SetButtonStyle()
	{
		if (ItsTerraTime(_request.Prompt))
		{
			if (_request.SuggestedValues.Any())
			{
				if (!_request.SuggestedValues.Contains(_current))
				{
					return ButtonStyle.StyleType.Secondary;
				}
				return ButtonStyle.StyleType.Main;
			}
			return ButtonStyle.StyleType.Secondary;
		}
		return ButtonStyle.StyleType.Main;
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		if (_requiresConfirmation && _displayingConfirmation)
		{
			_chooseXInterface.Close();
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ClearsInteractions = false,
				ButtonText = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Confirm_Quantity_Button_Text",
					Parameters = new Dictionary<string, string> { 
					{
						"quantity",
						_current.ToString()
					} }
				},
				Style = SetButtonStyle(),
				ButtonCallback = OnSubmit
			});
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ClearsInteractions = false,
				ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Back",
				Style = ButtonStyle.StyleType.Outlined,
				ButtonCallback = OpenInterface
			});
		}
		else if (_request.CanCancel)
		{
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel",
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = OnCancel
			});
		}
		else if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = TryUndo
			};
		}
		OnUpdateButtons(Buttons);
	}

	protected override void SetArrows()
	{
		Arrows.Reset();
		if (ItsTerraTime(Prompt))
		{
			MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
			uint sourceId = _request.SourceId;
			if (mtgGameState.TryGetCard(sourceId, out var card))
			{
				foreach (uint targetId in card.TargetIds)
				{
					Arrows.AddSuppressedLine(new Arrows.LineData(sourceId, targetId));
				}
			}
		}
		OnUpdateArrows(Arrows);
	}

	private bool ItsTerraTime(Prompt propmt)
	{
		if (propmt != null)
		{
			return propmt.PromptId == 14764;
		}
		return false;
	}

	private static bool SourceRequiresConfirmation(DuelScene_CDC sourceCardView, AssetLookupSystem assetLookupSystem)
	{
		assetLookupSystem.Blackboard.Clear();
		if (sourceCardView != null)
		{
			assetLookupSystem.Blackboard.SetCardDataExtensive(sourceCardView.Model);
		}
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NumericInputWorkflow_ConfirmZeroPrompt> loadedTree))
		{
			NumericInputWorkflow_ConfirmZeroPrompt payload = loadedTree.GetPayload(assetLookupSystem.Blackboard);
			if (payload != null)
			{
				return payload.ConfirmZero;
			}
		}
		return false;
	}

	private static string GetButtonLocKey(DuelScene_CDC sourceCardView, AssetLookupSystem assetLookupSystem)
	{
		assetLookupSystem.Blackboard.Clear();
		if (sourceCardView != null)
		{
			assetLookupSystem.Blackboard.SetCardDataExtensive(sourceCardView.Model);
		}
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NumericInputWorkflow_ButtonTextPayload> loadedTree))
		{
			NumericInputWorkflow_ButtonTextPayload payload = loadedTree.GetPayload(assetLookupSystem.Blackboard);
			if (payload != null)
			{
				return payload.Key;
			}
		}
		return string.Empty;
	}

	public override void CleanUp()
	{
		base.CleanUp();
		_chooseXInterface.Submit -= OnSubmit;
		_chooseXInterface.ValueModified -= ModifyValue;
		_interfaceBuilder.DestroyInterface(_chooseXInterface, "NumericInputWorkflow");
		if (_entityViewProvider != null && _entityViewProvider.TryGetAvatarByPlayerSide(GREPlayerNum.LocalPlayer, out var avatar))
		{
			avatar.SetCounterHighlights();
		}
	}
}
