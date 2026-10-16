using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class LifeTotalUpdateUXEventBase : UXEvent
{
	public readonly uint AffectorId;

	protected readonly ICardDataAdapter _affectorCard;

	protected float _lifeChangeDelay;

	protected LifeTotalUpdateUXEventBase(MtgCardInstance affectorCardInstance, ICardDatabaseAdapter cardDatabase)
	{
		AffectorId = affectorCardInstance?.InstanceId ?? 0;
		_affectorCard = affectorCardInstance.ToCardData(cardDatabase);
	}

	protected LifeTotalUpdateUXEventBase(uint affector)
	{
		AffectorId = affector;
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_timeRunning > _lifeChangeDelay && !IsComplete)
		{
			ApplyLifeChange();
			Complete();
		}
	}

	protected abstract void ApplyLifeChange();
}
