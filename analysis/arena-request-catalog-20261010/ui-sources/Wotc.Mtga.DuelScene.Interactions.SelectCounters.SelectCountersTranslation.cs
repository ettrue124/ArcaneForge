using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectCounters;

public class SelectCountersTranslation : IWorkflowTranslation<SelectCountersRequest>
{
	private readonly IObjectPool _pool;

	private readonly IUnityObjectPool _unityPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly CounterDistribution _counterDistribution;

	private readonly CounterSelection _counterSelection;

	public SelectCountersTranslation(IContext context, AssetLookupSystem assetLookupSystem, SpinnerController spinnerController)
	{
		_assetLookupSystem = assetLookupSystem;
		_pool = context.Get<IObjectPool>() ?? NullObjectPool.Default;
		_unityPool = context.Get<IUnityObjectPool>() ?? NullUnityObjectPool.Default;
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_browserController = context.Get<IBrowserController>() ?? NullBrowserController.Default;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_counterDistribution = new CounterDistribution(spinnerController, _cardHolderProvider, _pool);
		IButtonStateBuilder buttonStateBuilder = new ButtonStateBuilder(_unityPool, _cardDatabase.GreLocProvider, assetLookupSystem);
		IBrowserTextProvider browserTextProvider = new BrowserTextProvider(context.Get<IEntityNameProvider<uint>>(), _cardDatabase.ClientLocProvider);
		_counterSelection = new CounterSelection(_browserController, buttonStateBuilder, browserTextProvider, _pool);
	}

	public WorkflowBase Translate(SelectCountersRequest req)
	{
		if (req.MinSelect == 1 && req.MaxSelect == 1)
		{
			return new SelectCounterWorkflow(req, _unityPool, _cardDatabase, _gameStateProvider, _cardHolderProvider, _browserController, _headerTextProvider, _assetLookupSystem);
		}
		return new SelectCountersWorkflow(req, _pool, _counterDistribution, _counterSelection);
	}
}
