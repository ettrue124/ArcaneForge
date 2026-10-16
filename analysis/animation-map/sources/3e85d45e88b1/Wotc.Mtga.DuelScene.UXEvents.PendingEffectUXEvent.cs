using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class PendingEffectUXEvent : UXEvent
{
	protected IPendingEffectController _pendingEffectController;

	protected PendingEffectData Data { get; private set; }

	public PendingEffectUXEvent(PendingEffectData data, IPendingEffectController pendingEffectController)
	{
		Data = data;
		_pendingEffectController = pendingEffectController ?? NullPendingEffectController.Default;
	}
}
