using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using WorkflowVisuals;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SelectFromGroupsWorkflow : WorkflowBase<SelectFromGroupsRequest>, IClickableWorkflow, ICardStackWorkflow
{
	private class SelectFromGroupsHighlightsGenerator : IHighlightsGenerator
	{
		private readonly GroupNode _rootNode;

		public SelectFromGroupsHighlightsGenerator(GroupNode rootNode)
		{
			_rootNode = rootNode;
		}

		public Highlights GetHighlights()
		{
			List<uint> selectedIds = _rootNode.SelectedIds;
			List<uint> selectableIds = _rootNode.SelectableIds;
			List<uint> ids = _rootNode.Ids;
			Highlights highlights = new Highlights();
			foreach (uint item in ids)
			{
				highlights.IdToHighlightType_Workflow[item] = HighlightType.None;
			}
			if (selectedIds.Count < _rootNode.MaxCardSelections)
			{
				if (_rootNode.CanSubmit)
				{
					foreach (uint item2 in selectableIds)
					{
						highlights.IdToHighlightType_Workflow[item2] = HighlightType.Cold;
					}
				}
				else
				{
					foreach (uint item3 in selectableIds)
					{
						highlights.IdToHighlightType_Workflow[item3] = HighlightType.Hot;
					}
				}
			}
			foreach (uint item4 in selectedIds)
			{
				highlights.IdToHighlightType_Workflow[item4] = HighlightType.Selected;
			}
			return highlights;
		}
	}

	private readonly IObjectPool _objectPool;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly IBrowserProvider _browserProvider;

	private readonly Dictionary<string, string> _locParams;

	private readonly GroupNode _rootNode;

	private List<uint> _lockedIds = new List<uint>();

	public SelectFromGroupsWorkflow(SelectFromGroupsRequest request, IObjectPool objectPool, IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider, IBrowserProvider browserProvider)
		: base(request)
	{
		_request = request;
		_objectPool = objectPool;
		_gameStateProvider = gameStateProvider;
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_browserProvider = browserProvider;
		_locParams = _objectPool.PopObject<Dictionary<string, string>>();
		_rootNode = new GroupNode(_request);
		_highlightsGenerator = new SelectFromGroupsHighlightsGenerator(_rootNode);
	}

	protected override void ApplyInteractionInternal()
	{
		if (_rootNode.SelectionGroups == null)
		{
			throw new NotImplementedException("This request doesn't work. You're stuck like this forever. Are Dovin Baan and Winter Moon on the battlefield? If so, we know about that, and we're sorry for the bad time.");
		}
		_lockedIds = SelectFromGroupsRequest.GetUniqueGroupIds(_request.Groups);
		foreach (uint lockedId in _lockedIds)
		{
			_rootNode.Branch(lockedId);
		}
		_battlefield.Get().LayoutNow();
		SetButtons();
		_stack.Get().TryAutoDock(_rootNode.SelectableIds);
	}

	public override void CleanUp()
	{
		_stack.ClearCache();
		_battlefield.ClearCache();
		_locParams.Clear();
		_objectPool.PushObject(_locParams, tryClear: false);
		base.CleanUp();
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		List<uint> selectableIds = _rootNode.SelectableIds;
		bool flag = selectableIds.Contains(lhs.InstanceId);
		bool flag2 = selectableIds.Contains(rhs.InstanceId);
		if (flag != flag2)
		{
			return false;
		}
		List<uint> selectedIds = _rootNode.SelectedIds;
		bool flag3 = selectedIds.Contains(lhs.InstanceId);
		bool flag4 = selectedIds.Contains(rhs.InstanceId);
		if (flag3 != flag4)
		{
			return false;
		}
		return true;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (((MtgGameState)_gameStateProvider.LatestGameState).TryGetCard(entity.InstanceId, out var card) && card.Zone.Type == ZoneType.Graveyard && entity is DuelScene_CDC duelScene_CDC && duelScene_CDC.CurrentCardHolder.CardHolderType == CardHolderType.Graveyard && !_browserProvider.IsAnyBrowserOpen)
		{
			return false;
		}
		uint instanceId = entity.InstanceId;
		if (_lockedIds.Contains(instanceId))
		{
			return false;
		}
		if (_rootNode.SelectedIds.Contains(instanceId))
		{
			return true;
		}
		if (_rootNode.SelectableIds.Contains(instanceId) && _rootNode.SelectedIds.Count < _rootNode.MaxCardSelections)
		{
			return true;
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		List<uint> selectableIds = _rootNode.SelectableIds;
		List<uint> selectedIds = _rootNode.SelectedIds;
		if (selectedIds.Contains(entity.InstanceId))
		{
			if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: false, out var reroutedEntityView) && selectedIds.Contains(entity.InstanceId))
			{
				entity = reroutedEntityView;
			}
			_rootNode.Prune(entity.InstanceId);
		}
		else if (selectableIds.Contains(entity.InstanceId))
		{
			if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: true, out var reroutedEntityView2) && selectableIds.Contains(entity.InstanceId))
			{
				entity = reroutedEntityView2;
			}
			_rootNode.Branch(entity.InstanceId);
		}
		SetButtons();
		UpdateHighlightsAndDimming();
		if (entity is DuelScene_CDC duelScene_CDC)
		{
			duelScene_CDC.CurrentCardHolder.LayoutNow();
		}
	}

	public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
	{
		return false;
	}

	public void OnClickStack(CdcStackCounterView entity)
	{
	}

	public void OnBattlefieldClick()
	{
	}

	public bool CanSubmit()
	{
		return _rootNode.CanSubmit;
	}

	protected override void SetButtons()
	{
		_locParams["submitCount"] = _rootNode.SelectedIds.Count.ToString();
		Buttons = new Buttons();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = _locParams
			},
			Style = ((!_rootNode.CanSubmit) ? ButtonStyle.StyleType.Tepid : ((_rootNode.SelectedIds.Count < 1) ? ButtonStyle.StyleType.Tepid : (_rootNode.MaximizedSelection ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary))),
			ButtonCallback = () =>
			{
				_request.Submit(_rootNode.Submit());
			},
			ButtonSFX = WwiseEvents.sfx_ui_submit.EventName,
			Enabled = CanSubmit()
		});
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				Style = ButtonStyle.StyleType.Secondary,
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

	protected override void SetDimming()
	{
		List<uint> ids = _rootNode.Ids;
		Dimming.IdToIsDimmed = new Dictionary<uint, bool>(ids.Count);
		foreach (uint item in ids)
		{
			Dimming.IdToIsDimmed[item] = false;
		}
		OnUpdateDimming(Dimming);
	}
}
