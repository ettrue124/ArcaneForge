using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.Interactions;

public class IntermissionTranslation : IWorkflowTranslation<IntermissionRequest>
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IDuelSceneStateController _duelSceneStateController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public IntermissionTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IEntityViewProvider>(), context.Get<IGameStateProvider>(), context.Get<IDuelSceneStateController>(), context.Get<ICardHolderProvider>(), context.Get<IBrowserController>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	public IntermissionTranslation(IEntityViewProvider entityViewProvider, IGameStateProvider gameStateProvider, IDuelSceneStateController duelSceneStateController, ICardHolderProvider cardHolderProvider, IBrowserController browserController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_entityViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_duelSceneStateController = duelSceneStateController ?? NullDuelSceneStateController.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(IntermissionRequest req)
	{
		return new IntermissionWorkflow(req, _entityViewProvider, _gameStateProvider, _duelSceneStateController, _cardHolderProvider, _browserController, _vfxProvider, _assetLookupSystem);
	}
}
