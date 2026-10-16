using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class CastingTimeOptionTranslation : IWorkflowTranslation<CastingTimeOptionRequest>
{
	private readonly IAbilityDataProvider _abilityProvider;

	private readonly IWorkflowTranslation<CastingTimeOptionRequest> _childTranslation;

	public CastingTimeOptionTranslation(IContext context, IWorkflowTranslation<SelectNRequest> selectNTranslation, AssetLookupSystem assetLookupSystem)
	{
		_abilityProvider = context.Get<IAbilityDataProvider>() ?? NullAbilityDataProvider.Default;
		_childTranslation = new ChildWorkflowTranslator(context, selectNTranslation, assetLookupSystem, context.Get<IUnityObjectPool>(), context.Get<ICanvasRootProvider>());
	}

	public WorkflowBase Translate(CastingTimeOptionRequest req)
	{
		return new CastingTimeOptionWorkflow(req, _abilityProvider, _childTranslation);
	}
}
