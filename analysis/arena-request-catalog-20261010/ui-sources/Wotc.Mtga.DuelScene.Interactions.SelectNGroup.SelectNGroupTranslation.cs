using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectNGroup;

public class SelectNGroupTranslation : IWorkflowTranslation<SelectNGroupRequest>
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _browserHeaderTextProvider;

	private readonly GameManager _gameManager;

	private const uint HOSTILE_NEGOTIATIONS_TITLEID = 618299u;

	public SelectNGroupTranslation(IContext context, GameManager gameManager)
		: this(context.Get<ICardViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<IBrowserController>(), context.Get<IBrowserHeaderTextProvider>(), gameManager)
	{
	}

	public SelectNGroupTranslation(ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IBrowserController browserController, IBrowserHeaderTextProvider browserHeaderTextProvider, GameManager gameManager)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_browserHeaderTextProvider = browserHeaderTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_gameManager = gameManager;
	}

	public WorkflowBase Translate(SelectNGroupRequest req)
	{
		if (IsLilianaOfTheVeilUltimate(_gameManager.LatestGameState))
		{
			return new SelectNPileGroupWorkflow(req, _cardViewProvider, _browserController, _browserHeaderTextProvider);
		}
		if (IsHostileNegotiationsChoosePileFaceUp(_gameManager.LatestGameState, req))
		{
			return new SelectNFaceUpPileGroupWorkflow(req, _cardViewProvider, _gameManager.SplineMovementSystem, _browserController, _cardHolderProvider, _browserHeaderTextProvider);
		}
		return new SelectNGroupWorkflow(req, _cardViewProvider, _browserController, _browserHeaderTextProvider);
	}

	private static bool IsLilianaOfTheVeilUltimate(MtgGameState state)
	{
		if (state.ResolvingCardInstance != null && state.ResolvingCardInstance.GrpId != 0)
		{
			return state.ResolvingCardInstance.GrpId == 99470;
		}
		return false;
	}

	private static bool IsHostileNegotiationsChoosePileFaceUp(MtgGameState state, SelectNGroupRequest request)
	{
		if (state.TryGetCard(request.SourceId, out var card) && card.TitleId == 618299)
		{
			foreach (Group group in request.Groups)
			{
				if (!group.IsFacedown)
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}
}
