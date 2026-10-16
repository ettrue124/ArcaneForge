using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class OptionalActionMessageWorkflow_SurveilStyleBrowser : SurveilStyleBrowserWorkflow<OptionalActionMessageRequest>
{
	public OptionalActionMessageWorkflow_SurveilStyleBrowser(OptionalActionMessageRequest request, IClientLocProvider clientLocProvider, IBrowserController browserController, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabaseAdapter, ICardViewProvider cardViewProvider)
		: base(request, clientLocProvider, gameStateProvider, cardDatabaseAdapter, browserController, cardViewProvider)
	{
	}

	protected override IEnumerable<uint> GetCardIds()
	{
		return _request.RecipientIds;
	}

	protected override void OnDoneButtonPressed(SurveilBrowser browser)
	{
		List<DuelScene_CDC> graveyardCards = browser.GetGraveyardCards();
		if (graveyardCards != null && graveyardCards.Count > 0)
		{
			_request.SubmitResponse(OptionResponse.AllowYes);
		}
		else
		{
			_request.SubmitResponse(OptionResponse.CancelNo);
		}
	}
}
