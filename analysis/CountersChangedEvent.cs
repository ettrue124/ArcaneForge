using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CountersChangedEvent : IncrementalModelUpdateEvent
{
	public int Change;

	public int OldCount;

	public CounterType Type;

	public CountersChangedEvent(MtgEntity oldInst, MtgEntity newInst, uint affectorId, int change, CounterType counterType)
		: base(oldInst, newInst, affectorId)
	{
		Change = change;
		Type = counterType;
		oldInst?.Counters.TryGetValue(Type, out OldCount);
	}

	public override string ToString()
	{
		return string.Format("CountersChangedEvent [{0} gives {1} {2} counters ({3})]", (AffectorId != 0) ? AffectorId.ToString() : "?", NewInstance.InstanceId, Change, Type);
	}
}
