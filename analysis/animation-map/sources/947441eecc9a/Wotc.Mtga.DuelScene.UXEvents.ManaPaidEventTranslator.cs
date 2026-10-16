using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ManaPaidEventTranslator : IEventTranslator
{
	private readonly IManaProducedAnimationPlayer _manaProducedAnimationPlayer;

	public ManaPaidEventTranslator(IManaProducedAnimationPlayer manaProducedAnimationPlayer)
	{
		_manaProducedAnimationPlayer = manaProducedAnimationPlayer;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ManaPaidEvent manaPaidEvent)
		{
			events.Add(new ManaProducedUXEvent(manaPaidEvent.ProducerId, manaPaidEvent.ConsumerId, manaPaidEvent.Mana, _manaProducedAnimationPlayer));
		}
	}
}
