namespace Wotc.Mtga.DuelScene.UXEvents;

public class UnlockTutorialButtonUXEvent : UXEvent
{
	private readonly NPEState _npeState;

	public UnlockTutorialButtonUXEvent(NPEState npeState)
	{
		_npeState = npeState;
	}

	public override void Execute()
	{
		_npeState.UnlockSkipTutorialButton();
		Complete();
	}
}
