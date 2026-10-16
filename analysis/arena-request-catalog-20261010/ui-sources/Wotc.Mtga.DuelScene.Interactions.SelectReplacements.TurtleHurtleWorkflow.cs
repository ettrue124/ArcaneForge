using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using WorkflowVisuals;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectReplacements;

public class TurtleHurtleWorkflow : WorkflowBase<SelectReplacementRequest>, IClickableWorkflow, ICardStackWorkflow
{
	private class HighlightGeneration : IHighlightsGenerator
	{
		private readonly IReadOnlyList<ReplacementSelectionByZcid> _replacementEffects;

		private readonly Highlights _highlights = new Highlights();

		public HighlightGeneration(IReadOnlyList<ReplacementSelectionByZcid> replacementEffects)
		{
			_replacementEffects = replacementEffects ?? Array.Empty<ReplacementSelectionByZcid>();
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			foreach (ReplacementSelectionByZcid replacementEffect in _replacementEffects)
			{
				_highlights.IdToHighlightType_Workflow[replacementEffect.SelectableZcid] = HighlightType.Hot;
			}
			return _highlights;
		}
	}

	private readonly IObjectPool _objectPool;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly List<uint> _instanceIds;

	private readonly Dictionary<DuelScene_CDC, ReplacementEffect> _cardToReplacementMap;

	public TurtleHurtleWorkflow(SelectReplacementRequest req, IObjectPool objectPool, ICardViewProvider cardViewProvider)
		: base(req)
	{
		_objectPool = objectPool ?? NullObjectPool.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_highlightsGenerator = new HighlightGeneration(req.ObjectSelections);
		_instanceIds = _objectPool.PopObject<List<uint>>();
		_cardToReplacementMap = _objectPool.PopObject<Dictionary<DuelScene_CDC, ReplacementEffect>>();
	}

	protected override void ApplyInteractionInternal()
	{
		for (int i = 0; i < _request.ObjectSelections.Count; i++)
		{
			ReplacementSelectionByZcid replacementSelectionByZcid = _request.ObjectSelections[i];
			ReplacementEffect value = _request.Replacements[i];
			uint selectableZcid = replacementSelectionByZcid.SelectableZcid;
			if (_cardViewProvider.TryGetCardView(selectableZcid, out var cardView))
			{
				_instanceIds.Add(selectableZcid);
				_cardToReplacementMap[cardView] = value;
			}
		}
	}

	public override void CleanUp()
	{
		base.CleanUp();
		_instanceIds.Clear();
		_objectPool.PushObject(_instanceIds, tryClear: false);
		_cardToReplacementMap.Clear();
		_objectPool.PushObject(_cardToReplacementMap, tryClear: false);
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (entity is DuelScene_CDC key)
		{
			return _cardToReplacementMap.ContainsKey(key);
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (entity is DuelScene_CDC key && _cardToReplacementMap.TryGetValue(key, out var value))
		{
			_request.SubmitReplacement(value);
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

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		if (lhs != null && rhs != null)
		{
			return _instanceIds.Contains(lhs.InstanceId) == _instanceIds.Contains(rhs.InstanceId);
		}
		return false;
	}
}
