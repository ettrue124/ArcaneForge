using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class ExploreWorkflow : OptionalActionMessageWorkflow_SurveilStyleBrowser
{
	public ExploreWorkflow(OptionalActionMessageRequest request, IClientLocProvider clientLocProvider, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabaseAdapter, IBrowserController browserController, ICardViewProvider cardViewProvider)
		: base(request, clientLocProvider, browserController, gameStateProvider, cardDatabaseAdapter, cardViewProvider)
	{
	}

	protected override void SetHeader()
	{
		_header = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Explore_Title");
	}
}
