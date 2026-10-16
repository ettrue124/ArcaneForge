using System.Collections.Generic;
using System.Text;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class CastingTimeOptionWorkflow : WorkflowBase<CastingTimeOptionRequest>, IParentWorkflow, IAutoRespondWorkflow
{
	private readonly IAbilityDataProvider _abilityProvider;

	private readonly List<WorkflowBase> _children = new List<WorkflowBase>();

	public IEnumerable<WorkflowBase> ChildWorkflows => _children;

	public CastingTimeOptionWorkflow(CastingTimeOptionRequest request, IAbilityDataProvider abilityDataProvider, IWorkflowTranslation<CastingTimeOptionRequest> childTranslation)
		: base(request)
	{
		_abilityProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
		WorkflowBase workflowBase = childTranslation.Translate(request);
		if (workflowBase != null)
		{
			workflowBase.UpdateHighlights += OnUpdateChildHighlights;
			workflowBase.UpdateDimming += OnUpdateChildDimming;
			workflowBase.UpdateArrows += OnUpdateChildArrows;
			workflowBase.UpdateButtons += OnUpdateChildButtons;
			_prompt = workflowBase.Prompt;
			_children.AddIfNotNull(workflowBase);
		}
		_highlightsGenerator = new HighlightGeneratorAggregate(_children);
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
		foreach (WorkflowBase child in _children)
		{
			if (!child.CanApply(events))
			{
				return false;
			}
		}
		return true;
	}

	public bool TryAutoRespond()
	{
		foreach (BaseUserRequest childRequest in _request.ChildRequests)
		{
			if (childRequest is CastingTimeOption_SelectNRequest castingTimeOption_SelectNRequest && _abilityProvider.TryGetAbilityPrintingById(castingTimeOption_SelectNRequest.GrpId, out var ability) && ability.BaseId == 147)
			{
				return false;
			}
		}
		foreach (WorkflowBase childWorkflow in ChildWorkflows)
		{
			if (childWorkflow is IAutoRespondWorkflow autoRespondWorkflow && autoRespondWorkflow.TryAutoRespond())
			{
				return true;
			}
		}
		return false;
	}

	protected override void ApplyInteractionInternal()
	{
		_children.ForEach((WorkflowBase x) =>
		{
			x.ApplyInteraction();
		});
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed.Clear();
		foreach (WorkflowBase child in _children)
		{
			Dimming.Merge(Dimming, child.Dimming);
		}
		OnUpdateDimming(Dimming);
	}

	protected override void SetArrows()
	{
		Arrows.ClearLines();
		Arrows.ClearCtMLines();
		Arrows = new Arrows();
		foreach (WorkflowBase child in _children)
		{
			Arrows.Merge(Arrows, child.Arrows);
		}
		OnUpdateArrows(Arrows);
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		foreach (WorkflowBase child in _children)
		{
			Buttons.Merge(Buttons, child.Buttons);
		}
		if (_request.CanCancel && Buttons.CancelData == null)
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
		if (_request.AllowUndo && Buttons.UndoData == null && Buttons.DisplayUndo)
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
		stringBuilder.Append("CastTimeOptionWorkflow");
		stringBuilder.AppendLine();
		stringBuilder.Append("-Children:");
		foreach (WorkflowBase child in _children)
		{
			stringBuilder.AppendLine();
			stringBuilder.Append("--");
			stringBuilder.Append(child.ToString());
		}
		return stringBuilder.ToString();
	}

	public override void CleanUp()
	{
		while (_children.Count > 0)
		{
			WorkflowBase workflowBase = _children[0];
			workflowBase.CleanUp();
			workflowBase.UpdateHighlights -= OnUpdateChildHighlights;
			workflowBase.UpdateDimming -= OnUpdateChildDimming;
			workflowBase.UpdateArrows -= OnUpdateChildArrows;
			workflowBase.UpdateButtons -= OnUpdateChildButtons;
			_children.RemoveAt(0);
		}
		base.CleanUp();
	}
}
