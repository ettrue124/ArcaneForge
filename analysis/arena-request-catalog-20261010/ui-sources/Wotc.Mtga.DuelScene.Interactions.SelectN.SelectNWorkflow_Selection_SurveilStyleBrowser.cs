using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_SurveilStyleBrowser : SurveilStyleBrowserWorkflow<SelectNRequest>
{
	public SelectNWorkflow_Selection_SurveilStyleBrowser(SelectNRequest selectNRequest, IBrowserController browserController, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabaseAdapter, IClientLocProvider clientLocProvider, ICardViewProvider cardViewProvider)
		: base(selectNRequest, clientLocProvider, gameStateProvider, cardDatabaseAdapter, browserController, cardViewProvider)
	{
	}

	protected override IEnumerable<uint> GetCardIds()
	{
		return _request.Ids;
	}

	protected override void SetSubHeader()
	{
		_subHeader = _clientLocManager.GetLocalizedText("DuelScene/Browsers/BrowserSubheader_DragToGraveyard");
	}

	protected override bool IsDoneButtonEnabled(SurveilBrowser browser)
	{
		List<DuelScene_CDC> graveyardCards = browser.GetGraveyardCards();
		if (graveyardCards == null)
		{
			return false;
		}
		return graveyardCards.Count == 1;
	}

	protected override void OnDoneButtonPressed(SurveilBrowser browser)
	{
		List<DuelScene_CDC> graveyardCards = browser.GetGraveyardCards();
		_request.SubmitSelection(graveyardCards[0].InstanceId);
	}
}
