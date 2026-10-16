using System.Collections.Generic;
using System.Text;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class EffectCostWorkflow : WorkflowBase<EffectCostRequest>, IParentWorkflow, IAutoRespondWorkflow
{
	private readonly List<WorkflowBase> _childWorkflows = new List<WorkflowBase>();

	public IEnumerable<WorkflowBase> ChildWorkflows => _childWorkflows;

	public EffectCostWorkflow(EffectCostRequest decision, IWorkflowTranslator workflowTranslation)
		: base(decision)
	{
		foreach (BaseUserRequest childRequest in decision.ChildRequests)
		{
			if (childRequest != null)
			{
				WorkflowBase workflowBase = workflowTranslation.Translate(childRequest);
				if (workflowBase != null)
				{
					workflowBase.UpdateHighlights += OnUpdateChildHighlights;
					workflowBase.UpdateDimming += OnUpdateChildDimming;
					workflowBase.UpdateArrows += OnUpdateChildArrows;
					workflowBase.UpdateButtons += OnUpdateChildButtons;
					_childWorkflows.Add(workflowBase);
				}
			}
		}
		_highlightsGenerator = new HighlightGeneratorAggregate(_childWorkflows);
	}

	private void OnUpdateChildHighlights(Highlights highlights)
	{
		SetHighlights();
	}

	private void OnUpdateChildArrows(Arrows arrows)
	{
		SetArrows();
	}

	private void OnUpdateChildDimming(Dimming dimming)
	{
		SetDimming();
	}

	private void OnUpdateChildButtons(Buttons buttons)
	{
		SetButtons();
	}

	public override bool CanApply(List<UXEvent> events)
	{
		foreach (WorkflowBase childWorkflow in _childWorkflows)
		{
			if (!childWorkflow.CanApply(events))
			{
				return false;
			}
		}
		return true;
	}

	protected override void ApplyInteractionInternal()
	{
		for (int i = 0; i < _childWorkflows.Count; i++)
		{
			_childWorkflows[i].ApplyInteraction();
		}
	}

	public override void CleanUp()
	{
		while (_childWorkflows.Count > 0)
		{
			WorkflowBase workflowBase = _childWorkflows[0];
			workflowBase.CleanUp();
			workflowBase.UpdateHighlights -= OnUpdateChildHighlights;
			workflowBase.UpdateDimming -= OnUpdateChildDimming;
			workflowBase.UpdateArrows -= OnUpdateChildArrows;
			workflowBase.UpdateButtons -= OnUpdateChildButtons;
			_childWorkflows.RemoveAt(0);
		}
		base.CleanUp();
	}

	protected override void SetDimming()
	{
		Dimming = new Dimming();
		foreach (WorkflowBase childWorkflow in _childWorkflows)
		{
			Dimming.Merge(Dimming, childWorkflow.Dimming);
		}
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows = new Arrows();
		foreach (WorkflowBase childWorkflow in _childWorkflows)
		{
			Arrows.Merge(Arrows, childWorkflow.Arrows);
		}
		OnUpdateArrows(Arrows);
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		foreach (WorkflowBase childWorkflow in _childWorkflows)
		{
			Buttons.Merge(Buttons, childWorkflow.Buttons);
		}
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

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("ActionCostWorkflow");
		stringBuilder.AppendLine();
		stringBuilder.Append("-Children:");
		foreach (WorkflowBase childWorkflow in _childWorkflows)
		{
			stringBuilder.AppendLine();
			stringBuilder.Append("--");
			stringBuilder.Append(childWorkflow.ToString());
		}
		return stringBuilder.ToString();
	}

	public bool TryAutoRespond()
	{
		foreach (WorkflowBase childWorkflow in _childWorkflows)
		{
			if (childWorkflow is IAutoRespondWorkflow autoRespondWorkflow)
			{
				return autoRespondWorkflow.TryAutoRespond();
			}
		}
		return false;
	}
}
