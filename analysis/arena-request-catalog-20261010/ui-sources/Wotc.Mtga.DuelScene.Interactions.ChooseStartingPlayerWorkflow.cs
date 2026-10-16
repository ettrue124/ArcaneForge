using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class ChooseStartingPlayerWorkflow : WorkflowBase<ChooseStartingPlayerRequest>, IAutoRespondWorkflow
{
	private readonly IClientLocProvider _locProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

	public ChooseStartingPlayerWorkflow(ChooseStartingPlayerRequest request, IClientLocProvider locProvider, IGameStateProvider gameStateProvider, IBrowserController browserController)
		: base(request)
	{
		_locProvider = locProvider;
		_gameStateProvider = gameStateProvider;
		_browserController = browserController;
	}

	public bool TryAutoRespond()
	{
		GameInfo gameInfo = ((MtgGameState)_gameStateProvider.LatestGameState).GameInfo;
		if (gameInfo == null)
		{
			return false;
		}
		if (gameInfo.MatchWinCondition == MatchWinCondition.SingleElimination || gameInfo.Type == GameType.MultiPlayer)
		{
			SubmitPlayChoice();
			return true;
		}
		return false;
	}

	protected override void ApplyInteractionInternal()
	{
		YesNoProvider browserTypeProvider = new YesNoProvider(_locProvider.GetLocalizedText("DuelScene/Browsers/StartingPlayer_Header"), _locProvider.GetLocalizedText("DuelScene/Browsers/StartingPlayer_SubHeader"), new Dictionary<string, ButtonStateData>
		{
			["YesButton"] = new ButtonStateData
			{
				LocalizedString = "DuelScene/Browsers/StartingPlayer_DrawFirst",
				BrowserElementKey = "2Button_Left",
				StyleType = ButtonStyle.StyleType.Secondary
			},
			["NoButton"] = new ButtonStateData
			{
				LocalizedString = "DuelScene/Browsers/StartingPlayer_PlayFirst",
				BrowserElementKey = "2Button_Right",
				StyleType = ButtonStyle.StyleType.Main
			}
		}, YesNoProvider.CreateActionMap(SubmitDrawChoice, SubmitPlayChoice));
		_browserController.OpenBrowser(browserTypeProvider);
	}

	private void SubmitPlayChoice()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_request.ChooseStartingPlayer(mtgGameState.LocalPlayer.InstanceId);
	}

	private void SubmitDrawChoice()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_request.ChooseStartingPlayer(mtgGameState.Opponent.InstanceId);
	}
}
