using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ChoiceResultUXEvent_Random : UXEvent
{
	private readonly ChoiceResultEvent _choiceResult;

	private const float CHOOSE_EFFECT_DURATION = 1.2f;

	private const float OPTIONS_CHOSEN_DURATION = 0.3f;

	private const float _timeSpentOnEachOption = 0.15f;

	private float _optionTimer;

	private int _optionIndex;

	private readonly ICardViewProvider _cardViewProvider;

	private DuelScene_CDC _affectorCdc;

	private List<DuelScene_CDC> _optionCdcs = new List<DuelScene_CDC>();

	private List<DuelScene_CDC> _valueCdcs = new List<DuelScene_CDC>();

	public override bool IsBlocking => true;

	public ChoiceResultUXEvent_Random(ChoiceResultEvent cre, ICardViewProvider cardViewProvider)
	{
		_choiceResult = cre;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		foreach (uint choiceValue in _choiceResult.ChoiceValues)
		{
			if (_cardViewProvider.TryGetCardView(choiceValue, out var cardView))
			{
				_valueCdcs.Add(cardView);
			}
		}
		foreach (uint choiceOption in _choiceResult.ChoiceOptions)
		{
			if (_cardViewProvider.TryGetCardView(choiceOption, out var cardView2))
			{
				_optionCdcs.Add(cardView2);
			}
		}
		if (_optionCdcs.Count > 1)
		{
			_optionCdcs.Sort((DuelScene_CDC lhs, DuelScene_CDC rhs) =>
			{
				float x = lhs.Root.position.x;
				float x2 = rhs.Root.position.x;
				return x2.CompareTo(x);
			});
		}
		if (_valueCdcs.Count == 0)
		{
			Complete();
		}
		else if (_cardViewProvider.TryGetCardView(_choiceResult.AffectorId, out _affectorCdc))
		{
			_affectorCdc.UpdateHighlight(HighlightType.Cold);
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_timeRunning >= 1.2f)
		{
			_optionCdcs.FindAll((DuelScene_CDC x) => !_valueCdcs.Contains(x)).ForEach((DuelScene_CDC x) =>
			{
				x.UpdateHighlight(HighlightType.None);
			});
			_valueCdcs.ForEach((DuelScene_CDC x) =>
			{
				x.UpdateHighlight(HighlightType.Selected);
			});
			if (_affectorCdc != null)
			{
				_affectorCdc.UpdateHighlight(HighlightType.Hot);
			}
			if (_timeRunning >= 1.5f)
			{
				_valueCdcs.ForEach((DuelScene_CDC x) =>
				{
					x.UpdateHighlight(HighlightType.None);
				});
				if (_affectorCdc != null)
				{
					_affectorCdc.UpdateHighlight(HighlightType.None);
				}
				Complete();
			}
			return;
		}
		_optionTimer += dt;
		if (_optionTimer > 0.15f)
		{
			_optionTimer -= 0.15f;
			for (int num = 0; num < _optionCdcs.Count; num++)
			{
				_optionCdcs[num].UpdateHighlight((num == _optionIndex) ? HighlightType.Selected : HighlightType.Hot);
			}
			if (_optionIndex == _optionCdcs.Count - 1)
			{
				_optionIndex = 0;
			}
			else
			{
				_optionIndex++;
			}
		}
	}
}
