using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class DefaultTranslation : IWorkflowTranslation<OptionalActionMessageRequest>
{
	private readonly IObjectPool _pool;

	private readonly IGreLocProvider _greLocProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserProvider _browserProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public DefaultTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<IGreLocProvider>(), context.Get<IClientLocProvider>(), context.Get<IGameStateProvider>(), context.Get<IBrowserProvider>(), assetLookupSystem)
	{
	}

	private DefaultTranslation(IObjectPool objectPool, IGreLocProvider greLocProvider, IClientLocProvider clientLocProvider, IGameStateProvider gameStateProvider, IBrowserProvider browserProvider, AssetLookupSystem assetLookupSystem)
	{
		_pool = objectPool ?? NullObjectPool.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_greLocProvider = greLocProvider ?? NullGreLocManager.Default;
		_clientLocProvider = clientLocProvider ?? NullLocProvider.Default;
		_browserProvider = browserProvider ?? NullBrowserProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(OptionalActionMessageRequest req)
	{
		return new OptionalActionMessageWorkflow(req, _pool, _greLocProvider, _clientLocProvider, _gameStateProvider, _browserProvider, _assetLookupSystem);
	}
}
