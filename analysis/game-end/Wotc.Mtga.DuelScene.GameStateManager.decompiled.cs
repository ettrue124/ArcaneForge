using System;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene;

public class GameStateManager : IGameStateManager, IGameStateController, IGameStateProvider, IDisposable
{
	public ObservableReference<MtgGameState> CurrentGameState { get; } = new ObservableReference<MtgGameState>();

	public ObservableReference<MtgGameState> LatestGameState { get; } = new ObservableReference<MtgGameState>();

	public void SetCurrentGameState(MtgGameState gameState)
	{
		CurrentGameState.Value = gameState;
	}

	public void SetLatestGameState(MtgGameState gameState)
	{
		LatestGameState.Value = gameState;
	}

	public void Dispose()
	{
		CurrentGameState?.Dispose();
		LatestGameState?.Dispose();
	}
}
