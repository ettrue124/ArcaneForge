using GreClient.Rules;
using Pooling;

namespace Wotc.Mtga.DuelScene.Interactions.Distribution;

public class DistributionTranslation : IWorkflowTranslation<DistributionRequest>
{
	private readonly IObjectPool _objPool;

	private readonly ICardHolderProvider _cardHolderProvider;

	private SpinnerController _spinnerController;

	public DistributionTranslation(IContext context, SpinnerController spinnerController)
	{
		_objPool = context.Get<IObjectPool>() ?? NullObjectPool.Default;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
		_spinnerController = spinnerController;
	}

	public WorkflowBase Translate(DistributionRequest req)
	{
		if (req.ExistingDistributions.Count > 0)
		{
			return new DistributionWithExistingValuesWorkflow(req, _objPool, _cardHolderProvider, _spinnerController);
		}
		return new DistributionWorkflow(req, _cardHolderProvider, _spinnerController);
	}
}
