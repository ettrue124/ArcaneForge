using System.Collections.Generic;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ParityWorkflow : WorkflowBase<SelectNRequest>
{
	private class HighlightGenerator : IHighlightsGenerator
	{
		private const uint EVEN_ID = 0u;

		private const uint ODD_ID = 1u;

		private readonly Highlights _highlights = new Highlights();

		private readonly Dictionary<uint, HashSet<uint>> _affectedIds;

		private uint? _highlightId;

		public HighlightGenerator(Dictionary<uint, HashSet<uint>> affectedIds)
		{
			_affectedIds = affectedIds ?? new Dictionary<uint, HashSet<uint>>();
		}

		public void SetEven()
		{
			_highlightId = 0u;
		}

		public void SetOdd()
		{
			_highlightId = 1u;
		}

		public void Clear()
		{
			_highlightId = null;
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			if (_highlightId.HasValue && _affectedIds.TryGetValue(_highlightId.Value, out var value))
			{
				foreach (uint item in value)
				{
					_highlights.IdToHighlightType_Workflow[item] = HighlightType.Selected;
				}
			}
			return _highlights;
		}
	}

	private readonly HighlightGenerator _highlights;

	public ParityWorkflow(SelectNRequest request)
		: base(request)
	{
		_highlightsGenerator = (_highlights = new HighlightGenerator(request.AffectedObjects));
	}

	protected override void ApplyInteractionInternal()
	{
		SetButtons();
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ParityChoice_Even",
			ButtonCallback = () =>
			{
				SubmitSelection(Parity.Even);
			},
			PointerEnter = SetEvenHighlights,
			PointerExit = ClearHighlights,
			Style = ButtonStyle.StyleType.Secondary,
			Tag = ButtonTag.Secondary
		});
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ParityChoice_Odd",
			ButtonCallback = () =>
			{
				SubmitSelection(Parity.Odd);
			},
			PointerEnter = SetOddHighlights,
			PointerExit = ClearHighlights,
			Style = ButtonStyle.StyleType.Secondary,
			Tag = ButtonTag.Secondary
		});
		base.SetButtons();
	}

	private void SetEvenHighlights()
	{
		_highlights.SetEven();
		SetHighlights();
	}

	private void SetOddHighlights()
	{
		_highlights.SetOdd();
		SetHighlights();
	}

	private void ClearHighlights()
	{
		_highlights.Clear();
		SetHighlights();
	}

	private void SubmitSelection(Parity parity)
	{
		_request.SubmitSelection((uint)parity);
	}
}
