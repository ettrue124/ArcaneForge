using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPETooltipBumperUXEvent : NPEUXEventWithDuration
{
	protected PromptType Position;

	protected MTGALocalizedString Text;

	private bool _isBlocking;

	public override bool IsBlocking => _isBlocking;

	public NPETooltipBumperUXEvent(Func<NPEDirector> getNpeDirector, MTGALocalizedString text, float duration, PromptType pos, bool isBlocking = false)
		: base(getNpeDirector, duration)
	{
		Position = pos;
		Text = text;
		_isBlocking = isBlocking;
	}

	public override void Execute()
	{
		_getNpeDirector().NPEController.ShowPrompt(Text, Position);
	}

	protected override void Cleanup()
	{
		_getNpeDirector().NPEController.HidePrompt(Position);
		base.Cleanup();
	}
}
