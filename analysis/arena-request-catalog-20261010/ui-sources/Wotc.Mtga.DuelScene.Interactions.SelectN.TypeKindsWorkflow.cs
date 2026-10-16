using System;
using System.Linq;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class TypeKindsWorkflow : WorkflowBase<SelectNRequest>
{
	public TypeKindsWorkflow(SelectNRequest req)
		: base(req)
	{
	}

	protected override void ApplyInteractionInternal()
	{
		SetButtons();
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		foreach (TypeKind typeKind in (_request.ListType == SelectionListType.Dynamic || _request.ListType == SelectionListType.StaticSubset) ? _request.Ids.Select((uint x) => (TypeKind)x) : (from TypeKind x in Enum.GetValues(typeof(TypeKind))
			where x != TypeKind.None
			select x))
		{
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = $"Enum/TypeKind/TypeKind_{typeKind}",
				ButtonCallback = () =>
				{
					SubmitSelection(typeKind);
				},
				Style = ButtonStyle.StyleType.Secondary,
				Tag = ButtonTag.Secondary
			});
		}
		Buttons.WorkflowButtons.Reverse();
		base.SetButtons();
	}

	private void SubmitSelection(TypeKind typeKind)
	{
		_request.SubmitSelection((uint)typeKind);
	}
}
