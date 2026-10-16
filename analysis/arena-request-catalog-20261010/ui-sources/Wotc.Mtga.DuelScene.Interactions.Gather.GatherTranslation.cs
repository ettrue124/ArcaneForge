using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.Gather;

public class GatherTranslation : IWorkflowTranslation<GatherRequest>
{
	private readonly GameManager _gameManager;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	public GatherTranslation(IContext context, GameManager gameManager)
		: this(context.Get<ICardHolderProvider>(), context.Get<IEntityViewProvider>(), gameManager)
	{
	}

	public GatherTranslation(ICardHolderProvider cardHolderProvider, IEntityViewProvider entityViewProvider, GameManager gameManager)
	{
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_entityViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
		_gameManager = gameManager;
	}

	public WorkflowBase Translate(GatherRequest req)
	{
		if (req.AmountToGather != 0)
		{
			return new GatherWorkflow_RemoveExact(req, _cardHolderProvider, _gameManager.SpinnerController);
		}
		return new GatherWorkflow_AnyNumber(req, _cardHolderProvider, _entityViewProvider, _gameManager.SpinnerController);
	}
}
