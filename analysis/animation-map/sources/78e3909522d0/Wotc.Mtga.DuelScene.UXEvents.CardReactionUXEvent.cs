using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardReactionUXEvent : UXEvent
{
	private readonly CardReactionEnum _reactionType;

	private DuelScene_CDC _cdc;

	private bool _isBlocking = true;

	public override bool IsBlocking => _isBlocking;

	public CardReactionUXEvent(ICardViewProvider cardViewProvider, CardReactionEnum type, uint instanceId)
	{
		_reactionType = type;
		if (cardViewProvider.TryGetCardView(instanceId, out var cardView))
		{
			_cdc = cardView;
		}
	}

	public override void Execute()
	{
		if ((bool)_cdc)
		{
			_cdc.PlayReactionAnimation(_reactionType);
			return;
		}
		_isBlocking = false;
		Complete();
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if ((bool)_cdc && _timeRunning > 0f && !_cdc.ReactionAnimation.isPlaying)
		{
			_isBlocking = false;
			Complete();
		}
	}
}
