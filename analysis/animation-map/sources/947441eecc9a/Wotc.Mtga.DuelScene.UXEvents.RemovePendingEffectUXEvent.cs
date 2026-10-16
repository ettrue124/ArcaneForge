using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemovePendingEffectUXEvent : PendingEffectUXEvent
{
	public RemovePendingEffectUXEvent(PendingEffectData data, IPendingEffectController pendingEffectController)
		: base(data, pendingEffectController)
	{
	}

	public override void Execute()
	{
		_pendingEffectController.RemovePendingEffect(Data);
		Complete();
	}
}
