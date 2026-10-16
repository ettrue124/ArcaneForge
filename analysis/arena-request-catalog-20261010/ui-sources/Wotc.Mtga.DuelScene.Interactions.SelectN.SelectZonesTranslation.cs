using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectZonesTranslation : IWorkflowTranslation<SelectNRequest>
{
	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserController _browserController;

	private readonly AssetLookupSystem _assetLookupSystem;

	public SelectZonesTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IClientLocProvider>(), context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<IBrowserController>(), assetLookupSystem)
	{
	}

	public SelectZonesTranslation(IClientLocProvider clientLocProvider, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, ICardHolderProvider cardHolderProvider, IBrowserController browserController, AssetLookupSystem assetLookupSystem)
	{
		_clientLocProvider = clientLocProvider;
		_gameStateProvider = gameStateProvider;
		_entityViewProvider = entityViewProvider;
		_cardHolderProvider = cardHolderProvider;
		_browserController = browserController;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(SelectNRequest req)
	{
		if (IsSingleZoneSelection(req))
		{
			return new SelectZoneWorkflow(req, _gameStateProvider, _cardHolderProvider, _entityViewProvider, _assetLookupSystem);
		}
		return new SelectZonesWorkflow(req, _gameStateProvider, _clientLocProvider, _browserController);
	}

	private static bool IsSingleZoneSelection(SelectNRequest req)
	{
		if (req.MinSel == 1 && req.MaxSel == 1 && req.Ids.Count == 2)
		{
			return !req.CanCancel;
		}
		return false;
	}
}
