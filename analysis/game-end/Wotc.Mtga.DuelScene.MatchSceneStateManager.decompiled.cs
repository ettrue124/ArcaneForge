using System;

namespace Wotc.Mtga.DuelScene;

public class MatchSceneStateManager : IMatchSceneStateManager, IMatchSceneStateProvider, IMatchSceneStateController, IDisposable
{
	public ObservableValue<MatchSceneState> CurrentState { get; } = new ObservableValue<MatchSceneState>();

	public void SetState(MatchSceneState state)
	{
		CurrentState.Value = state;
	}

	public void Dispose()
	{
		CurrentState?.Dispose();
	}
}
