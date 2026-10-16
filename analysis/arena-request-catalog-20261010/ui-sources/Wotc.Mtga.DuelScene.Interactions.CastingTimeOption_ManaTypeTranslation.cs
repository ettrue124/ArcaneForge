using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class CastingTimeOption_ManaTypeTranslation : IWorkflowTranslation<CastingTimeOption_ManaTypeRequest>
{
	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IBrowserController _browserController;

	private readonly IClientLocProvider _locProvider;

	public CastingTimeOption_ManaTypeTranslation(IBrowserController browserController, IClientLocProvider locProvider, AssetLookupSystem assetLookupSystem)
	{
		_browserController = browserController;
		_locProvider = locProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(CastingTimeOption_ManaTypeRequest req)
	{
		return new CastingTimeOption_ManaTypeWorkflow(req, _browserController, _locProvider, _assetLookupSystem);
	}
}
