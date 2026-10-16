using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions;

public class AdornCardsTranslation : IWorkflowTranslation<StringInputRequest>
{
	private readonly IObjectPool _objectPool;

	private readonly IDatabaseUtilities _databaseUtilities;

	private readonly IBrowserController _browserController;

	public AdornCardsTranslation(IContext context)
		: this(context.Get<IObjectPool>(), context.Get<IDatabaseUtilities>(), context.Get<IBrowserController>())
	{
	}

	private AdornCardsTranslation(IObjectPool objectPool, IDatabaseUtilities databaseUtilities, IBrowserController browserController)
	{
		_objectPool = objectPool;
		_databaseUtilities = databaseUtilities;
		_browserController = browserController;
	}

	public WorkflowBase Translate(StringInputRequest req)
	{
		return new AdornCardWorkflow(req, _objectPool, _browserController, _databaseUtilities);
	}
}
