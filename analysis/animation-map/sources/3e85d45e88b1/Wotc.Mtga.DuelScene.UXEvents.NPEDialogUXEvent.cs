using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEDialogUXEvent : NPEUXEventWithDuration
{
	public readonly MTGALocalizedString Line;

	public readonly NPEController.Actor Character;

	public readonly string WwiseEvent;

	public bool FollowCard;

	public bool SpeakingStarted;

	private float _timeSinceSpeakingStarted;

	public override bool IsBlocking => 1.2f < _timeSinceSpeakingStarted;

	public NPEDialogUXEvent(Func<NPEDirector> getNpeDirector, NPEController.Actor character, MTGALocalizedString displayText, string wwiseEvent, float duration, bool followCard = false)
		: base(getNpeDirector, duration)
	{
		Character = character;
		Line = displayText;
		WwiseEvent = wwiseEvent;
		FollowCard = followCard;
	}

	public override void Execute()
	{
		_getNpeDirector().NPEController.ShowDialog(this);
	}

	protected override void Cleanup()
	{
		NPEController nPEController = _getNpeDirector().NPEController;
		nPEController.HideDialog(Character);
		nPEController.CurrentDialog = null;
		base.Cleanup();
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (SpeakingStarted)
		{
			_timeSinceSpeakingStarted += dt;
		}
	}
}
