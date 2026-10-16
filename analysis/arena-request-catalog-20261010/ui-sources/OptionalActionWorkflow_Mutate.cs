using System.Linq;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;

public class OptionalActionWorkflow_Mutate : OptionalActionBrowserWorkflow
{
	private const int MUTATE_ABILITY_ID = 203;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IBrowserController _browserController;

	public ICardDataAdapter SourceModel { get; private set; }

	public ICardDataAdapter RecipientModel { get; private set; }

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.MutateOptionalAction;
	}

	public OptionalActionWorkflow_Mutate(OptionalActionMessageRequest request, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabase, IBrowserController browserController)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_cardDatabase = cardDatabase;
		_browserController = browserController;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		uint cardId = 0u;
		for (int i = 0; i < _request.RecipientIds.Count; i++)
		{
			if (_request.RecipientIds[i] != _request.SourceId)
			{
				cardId = _request.RecipientIds[i];
				break;
			}
		}
		MtgCardInstance copy = mtgGameState.GetCardById(_request.SourceId).GetCopy();
		MtgCardInstance copy2 = mtgGameState.GetCardById(cardId).GetCopy();
		copy.InstanceId = 0u;
		copy2.InstanceId = 0u;
		SourceModel = CardDataExtensions.CreateWithDatabase(copy, _cardDatabase);
		RecipientModel = CardDataExtensions.CreateWithDatabase(copy2, _cardDatabase);
		_header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Mutate_BrowserHeader");
		_subHeader = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Mutate_BrowserSubHeader");
		SetupButtons("DuelScene/Browsers/Mutate_BrowserButton_Over", "DuelScene/Browsers/Mutate_BrowserButton_Under", yesOnRight: true);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	public override void SetFxBlackboardData(IBlackboard bb)
	{
		base.SetFxBlackboardData(bb);
		bb.SetCardDataExtensive(SourceModel);
		bb.Ability = SourceModel.Abilities.FirstOrDefault((AbilityPrintingData x) => x.BaseId == 203);
	}

	public override void SetFxBlackboardDataForCard(DuelScene_CDC cardView, IBlackboard bb)
	{
		base.SetFxBlackboardDataForCard(cardView, bb);
		bb.Ability = ((cardView.Model.GrpId == SourceModel.GrpId) ? SourceModel.Abilities.FirstOrDefault((AbilityPrintingData x) => x.BaseId == 203) : null);
	}
}
