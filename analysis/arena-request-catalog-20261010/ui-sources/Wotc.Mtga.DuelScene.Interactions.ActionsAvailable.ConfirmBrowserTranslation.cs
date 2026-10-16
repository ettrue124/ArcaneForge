using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ConfirmBrowserTranslation : SingleActionTranslation
{
	private readonly IContext _context;

	private readonly IPlayWarningProvider _playWarningProvider;

	public ConfirmBrowserTranslation(IContext context)
	{
		_context = context ?? NullContext.Default;
		_playWarningProvider = _context.Get<IPlayWarningProvider>() ?? NullPlayWarningProvider.Default;
	}

	protected override WorkflowVariant TranslateAction(DuelScene_CDC cardView, Action action)
	{
		if (MDNPlayerPrefs.GameplayWarningsEnabled && _playWarningProvider.ShouldDisplayPlayWarning(cardView, action))
		{
			return new ConfirmChoice_Browser(_context, cardView, action);
		}
		return null;
	}
}
