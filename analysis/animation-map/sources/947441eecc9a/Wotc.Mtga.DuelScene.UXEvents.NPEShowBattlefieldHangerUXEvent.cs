using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEShowBattlefieldHangerUXEvent : NPEUXEventWithDuration
{
	public uint AnchorCard;

	public HangerSituation HangerType;

	public bool ShowLeftSide;

	public override bool IsBlocking => _duration - 0.5f > _timeRunning;

	public NPEShowBattlefieldHangerUXEvent(Func<NPEDirector> getNpeDirector, float duration, uint card, HangerSituation type, bool showLeftSide = false)
		: base(getNpeDirector, duration)
	{
		AnchorCard = card;
		HangerType = type;
		ShowLeftSide = showLeftSide;
	}

	public override void Execute()
	{
		NPEController nPEController = _getNpeDirector().NPEController;
		nPEController.ShowHangerOnBattlefield(AnchorCard, HangerType, ShowLeftSide);
		nPEController.CurrentHangerEvent = this;
	}

	protected override void Cleanup()
	{
		NPEController nPEController = _getNpeDirector().NPEController;
		nPEController.FadeOutBattlefieldAbilityHangers();
		nPEController.CurrentHangerEvent = null;
		base.Cleanup();
	}
}
