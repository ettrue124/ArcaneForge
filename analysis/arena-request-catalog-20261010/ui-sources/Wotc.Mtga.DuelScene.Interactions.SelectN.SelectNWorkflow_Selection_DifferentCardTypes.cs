using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_DifferentCardTypes : WorkflowBase<SelectNRequest>, IClickableWorkflow, ICardStackWorkflow
{
	private class SelectNWorkflow_Selection_DifferentCardTypes_HighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _selectableIds;

		private readonly IReadOnlyCollection<uint> _selectedIds;

		private readonly IReadOnlyDictionary<uint, List<CardType>> _idsToCardTypes;

		private readonly HashSet<CardType> _cardTypeCache = new HashSet<CardType>();

		public SelectNWorkflow_Selection_DifferentCardTypes_HighlightsGenerator(IReadOnlyCollection<uint> selectableIds, IReadOnlyCollection<uint> selectedIds, IReadOnlyDictionary<uint, List<CardType>> idToCardType)
		{
			_selectableIds = selectableIds;
			_selectedIds = selectedIds;
			_idsToCardTypes = idToCardType;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint selectedId in _selectedIds)
			{
				highlights.IdToHighlightType_Workflow[selectedId] = HighlightType.Selected;
				foreach (CardType item in _idsToCardTypes[selectedId])
				{
					_cardTypeCache.Add(item);
				}
			}
			bool flag = _selectedIds.Count == 0;
			foreach (uint selectableId in _selectableIds)
			{
				if (!highlights.IdToHighlightType_Workflow.ContainsKey(selectableId))
				{
					bool flag2 = flag || CheckCardTypeIsHot(selectableId, _cardTypeCache);
					highlights.IdToHighlightType_Workflow[selectableId] = ((!flag2) ? HighlightType.Cold : HighlightType.Hot);
				}
			}
			_cardTypeCache.Clear();
			return highlights;
		}

		private bool CheckCardTypeIsHot(uint id, HashSet<CardType> selectedCardTypes)
		{
			if (_idsToCardTypes.TryGetValue(id, out var value))
			{
				foreach (CardType item in value)
				{
					if (!selectedCardTypes.Contains(item))
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	private readonly IGameStateProvider _gameStateProvider;

	private Dictionary<uint, List<CardType>> _idsToCardTypes = new Dictionary<uint, List<CardType>>();

	public HashSet<uint> SelectableIds { get; } = new HashSet<uint>();

	public HashSet<uint> SelectedIds { get; } = new HashSet<uint>();

	public int CurrentSelection => SelectedIds.Count;

	private bool CanSubmit
	{
		get
		{
			if (CurrentSelection >= _request.MinSel)
			{
				return CurrentSelection <= _request.MaxSel;
			}
			return false;
		}
	}

	public SelectNWorkflow_Selection_DifferentCardTypes(SelectNRequest request, IGameStateProvider gameStateProvider)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_highlightsGenerator = new SelectNWorkflow_Selection_DifferentCardTypes_HighlightsGenerator(SelectableIds, SelectedIds, _idsToCardTypes);
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		foreach (uint id in _request.Ids)
		{
			SelectableIds.Add(id);
			if (mtgGameState.TryGetCard(id, out var card))
			{
				_idsToCardTypes.Add(id, card.CardTypes);
			}
		}
		SetButtons();
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		PromptButtonData item = new PromptButtonData
		{
			ButtonText = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					CurrentSelection.ToString()
				} }
			},
			Style = HighlightForButton(),
			ButtonCallback = () =>
			{
				_request.SubmitSelection(SelectedIds);
			},
			ButtonSFX = WwiseEvents.sfx_ui_submit.EventName,
			Enabled = CanSubmit
		};
		Buttons.WorkflowButtons.Add(item);
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

	private ButtonStyle.StyleType HighlightForButton()
	{
		if (SelectedCardsDifferentTypes())
		{
			return ButtonStyle.StyleType.Main;
		}
		return ButtonStyle.StyleType.Secondary;
	}

	private bool SelectedCardsDifferentTypes()
	{
		HashSet<CardType> hashSet = new HashSet<CardType>();
		foreach (uint selectedId in SelectedIds)
		{
			foreach (CardType item in _idsToCardTypes[selectedId])
			{
				hashSet.Add(item);
			}
		}
		if (hashSet.Count >= _request.MaxSel)
		{
			return true;
		}
		return false;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType == SimpleInteractionType.Primary && SelectableIds.Contains(entity.InstanceId))
		{
			if (SelectedIds.Count >= _request.MaxSel)
			{
				return SelectedIds.Contains(entity.InstanceId);
			}
			return true;
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (!SelectedIds.Add(entity.InstanceId))
		{
			SelectedIds.Remove(entity.InstanceId);
		}
		SetButtons();
		UpdateHighlightsAndDimming();
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

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		uint instanceId = lhs.InstanceId;
		uint instanceId2 = rhs.InstanceId;
		bool flag = _request.Ids.Contains(instanceId);
		bool flag2 = _request.Ids.Contains(instanceId2);
		if (flag != flag2)
		{
			return false;
		}
		bool flag3 = SelectedIds.Contains(instanceId);
		bool flag4 = SelectedIds.Contains(instanceId2);
		if (flag3 != flag4)
		{
			return false;
		}
		bool flag5 = SelectableIds.Contains(instanceId);
		bool flag6 = SelectableIds.Contains(instanceId2);
		if (flag5 != flag6)
		{
			return false;
		}
		return true;
	}
}
