using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ObjectDeselectedUXEvent : UXEvent
{
	private readonly ObjectDeselectedEvent _objectDeselectedEvent;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly string _loopingEffectKey;

	public ObjectDeselectedUXEvent(ObjectDeselectedEvent ode, ICardViewProvider cardViewProvider)
	{
		_objectDeselectedEvent = ode;
		_loopingEffectKey = $"ObjectSelection_{ode.AffectorId}_{ode.AffectedId}";
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
	}

	public override void Execute()
	{
		if (_cardViewProvider.TryGetCardView(_objectDeselectedEvent.AffectedId, out var cardView))
		{
			LoopingAnimationManager.RemoveLoopingEffect(cardView.EffectsRoot, _loopingEffectKey);
		}
		Complete();
	}
}
