using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CounterProducedUXEvent : UXEvent
{
	private readonly uint _sourceId;

	private readonly uint _sinkId;

	private readonly CounterType _counterType;

	private readonly ICounterProducedAnimationPlayer _counterProducedAnimationPlayer;

	public override bool IsBlocking => true;

	public CounterProducedUXEvent(uint sourceId, uint sinkId, CounterType counterType, ICounterProducedAnimationPlayer counterProducedAnimationPlayer)
	{
		_sourceId = sourceId;
		_sinkId = sinkId;
		_counterType = counterType;
		_counterProducedAnimationPlayer = counterProducedAnimationPlayer;
	}

	public override void Execute()
	{
		_counterProducedAnimationPlayer.PlayAnimation(_sourceId, _sinkId, _counterType, Complete);
	}
}
