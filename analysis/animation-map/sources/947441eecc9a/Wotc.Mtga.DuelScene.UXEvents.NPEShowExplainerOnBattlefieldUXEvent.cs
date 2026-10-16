using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEShowExplainerOnBattlefieldUXEvent : NPEUXEventWithDuration
{
	public uint AnchorCard;

	public bool ShowPower;

	private NPE_Card_Augment _theAugment;

	public override bool IsBlocking => _duration - 0.5f > _timeRunning;

	public NPEShowExplainerOnBattlefieldUXEvent(Func<NPEDirector> getNpeDirector, float duration, uint card, bool showPower)
		: base(getNpeDirector, duration)
	{
		AnchorCard = card;
		ShowPower = showPower;
	}

	public override void Execute()
	{
		_theAugment = _getNpeDirector().NPEController.ShowExplainerOnBattlefield(AnchorCard, ShowPower);
	}

	protected override void Cleanup()
	{
		_theAugment.FadeOut_OnBattlefield();
		base.Cleanup();
	}
}
