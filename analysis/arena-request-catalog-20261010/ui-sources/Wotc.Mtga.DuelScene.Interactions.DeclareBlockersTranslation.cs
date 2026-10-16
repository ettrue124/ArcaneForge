using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class DeclareBlockersTranslation : IWorkflowTranslation<DeclareBlockersRequest>
{
	private readonly IObjectPool _objPool;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IPromptEngine _promptEngine;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly UIManager _uiManager;

	public DeclareBlockersTranslation(IContext context, UIManager uiManager)
		: this(context.Get<IObjectPool>(), context.Get<IClientLocProvider>(), context.Get<IPromptEngine>(), context.Get<IGameStateProvider>(), context.Get<ICardHolderProvider>(), context.Get<ICardViewProvider>(), uiManager)
	{
	}

	private DeclareBlockersTranslation(IObjectPool objectPool, IClientLocProvider clientLocProvider, IPromptEngine promptEngine, IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider, ICardViewProvider cardViewProvider, UIManager uiManager)
	{
		_objPool = objectPool ?? NullObjectPool.Default;
		_clientLocProvider = clientLocProvider ?? NullLocProvider.Default;
		_promptEngine = promptEngine ?? NullPromptEngine.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderManager.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewManager.Default;
		_uiManager = uiManager;
	}

	public WorkflowBase Translate(DeclareBlockersRequest req)
	{
		return new DeclareBlockersWorkflow(req, _objPool, _clientLocProvider, _promptEngine, _gameStateProvider, _cardViewProvider, GetBattlefield(), _uiManager);
	}

	private IBattlefieldCardHolder GetBattlefield()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		if (mtgGameState == null)
		{
			return null;
		}
		if (mtgGameState.Battlefield == null)
		{
			return null;
		}
		return _cardHolderProvider.GetCardHolder(GREPlayerNum.Invalid, CardHolderType.Battlefield) as IBattlefieldCardHolder;
	}
}
