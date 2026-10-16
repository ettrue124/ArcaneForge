using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class CostKeywordTranslation
{
	private readonly IComparer<BaseUserRequest> _defaultComparer = new CostKeywordRequestComparer();

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IReadOnlyDictionary<CastingTimeOptionType, IComparer<BaseUserRequest>> _requestComparers;

	public CostKeywordTranslation(IContext context)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<IFakeCardViewController>(), context.Get<IBrowserController>(), context.Get<IBrowserHeaderTextProvider>())
	{
	}

	private CostKeywordTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IFakeCardViewController fakeCardController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_fakeCardController = fakeCardController;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_requestComparers = new Dictionary<CastingTimeOptionType, IComparer<BaseUserRequest>> { 
		{
			CastingTimeOptionType.Casualty,
			new CasualtyRequestComparer(_cardDatabase.AbilityDataProvider)
		} };
	}

	public WorkflowBase Translate(CastingTimeOptionRequest req, CastingTimeOption_CostKeywordRequest childRequest)
	{
		return new CostKeywordWorkflow(req, childRequest.GrpId, GetComparer(childRequest.OptionType), _cardDatabase, _gameStateProvider, _cardViewProvider, _fakeCardController, _browserController, _headerTextProvider);
	}

	private IComparer<BaseUserRequest> GetComparer(CastingTimeOptionType ctoType)
	{
		if (!_requestComparers.TryGetValue(ctoType, out var value))
		{
			return _defaultComparer;
		}
		return value;
	}
}
