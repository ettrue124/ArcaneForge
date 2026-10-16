using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddPendingEffectUXEvent : PendingEffectUXEvent
{
	private MtgEntity Affected;

	private MtgCardInstance Affector;

	public AddPendingEffectUXEvent(MtgCardInstance affector, MtgEntity affected, PendingEffectData data, IPendingEffectController pendingEffectController)
		: base(data, pendingEffectController)
	{
		Affector = affector;
		Affected = affected;
	}

	public override void Execute()
	{
		_pendingEffectController.AddPendingEffect(Data, Affector, Affected as MtgPlayer);
		Complete();
	}
}
