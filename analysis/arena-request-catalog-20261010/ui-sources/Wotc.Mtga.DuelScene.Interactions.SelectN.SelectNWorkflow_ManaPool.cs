using System.Collections.Generic;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_ManaPool : WorkflowBase<SelectNRequest>
{
	private class SelectNManaPoolHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly IReadOnlyCollection<uint> _requestIds;

		public SelectNManaPoolHighlightsGenerator(IReadOnlyCollection<uint> requestIds)
		{
			_requestIds = requestIds;
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			foreach (uint requestId in _requestIds)
			{
				_highlights.ManaHighlights[requestId] = new ManaHighlight(HighlightType.Hot);
			}
			return _highlights;
		}
	}

	private readonly IAvatarViewProvider _avatarViewProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private DuelScene_AvatarView _avatarView;

	private bool _submitted;

	public SelectNWorkflow_ManaPool(SelectNRequest request, IAvatarViewProvider avatarViewProvider, IGameStateProvider gameStateProvider)
		: base(request)
	{
		_avatarViewProvider = avatarViewProvider ?? NullAvatarViewProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_highlightsGenerator = new SelectNManaPoolHighlightsGenerator(_request.Ids);
	}

	public override bool CanApply(List<UXEvent> events)
	{
		foreach (UXEvent @event in events)
		{
			if (@event is ZoneTransferGroup || @event is ZoneTransferUXEvent || @event is ResolutionEffectUXEventBase || @event is UpdateManaPoolUXEvent)
			{
				return false;
			}
		}
		return true;
	}

	protected override void ApplyInteractionInternal()
	{
		uint valueOrDefault = (((MtgGameState)_gameStateProvider.LatestGameState)?.DecidingPlayer?.InstanceId).GetValueOrDefault();
		if (_avatarViewProvider.TryGetAvatarById(valueOrDefault, out _avatarView))
		{
			_avatarView.ManaSelected += OnManaSelected;
		}
		SetButtons();
	}

	private void OnManaSelected(uint manaId)
	{
		if (!_submitted && _request.Ids.Contains(manaId))
		{
			_submitted = true;
			_request.SubmitSelection(manaId);
		}
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				Style = ButtonStyle.StyleType.Main,
				ButtonCallback = () =>
				{
					_submitted = true;
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
					_submitted = true;
					_request.Undo();
				}
			};
		}
		OnUpdateButtons(Buttons);
	}

	public override void CleanUp()
	{
		if (_avatarView != null)
		{
			_avatarView.ManaSelected -= OnManaSelected;
		}
		base.CleanUp();
	}
}
