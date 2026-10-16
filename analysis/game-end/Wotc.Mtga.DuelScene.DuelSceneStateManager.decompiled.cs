using System;

namespace Wotc.Mtga.DuelScene;

public class DuelSceneStateManager : IDuelSceneStateManager, IDuelSceneStateProvider, IDuelSceneStateController, IDisposable
{
	private readonly IMatchSceneStateManager _matchSceneStateManager;

	public ObservableValue<DuelSceneState> CurrentState { get; } = new ObservableValue<DuelSceneState>();

	public DuelSceneStateManager(IMatchSceneStateManager matchSceneStateManager)
	{
		CurrentState.Value = new DuelSceneState(matchSceneStateManager.CurrentState, CurrentState);
		_matchSceneStateManager = matchSceneStateManager;
		_matchSceneStateManager.CurrentState.ValueUpdated += OnMatchSceneStateUpdated;
	}

	public void SetState(DuelSceneState state)
	{
		CurrentState.Value = state;
		IMatchSceneStateManager matchSceneStateManager = _matchSceneStateManager;
		bool? lockSceneTransitions = state.MatchScene_LockSceneTransitions;
		matchSceneStateManager.SetState(null, null, lockSceneTransitions);
	}

	public void SetState(bool? lockSceneTransitions = null, bool? firstGameStatePlaybackComplete = null, bool? gameComplete = null)
	{
		SetState(new DuelSceneState(CurrentState, lockSceneTransitions, firstGameStatePlaybackComplete, gameComplete));
	}

	private void OnMatchSceneStateUpdated(MatchSceneState matchSceneState)
	{
		CurrentState.Value = new DuelSceneState(matchSceneState, CurrentState);
	}

	public void Dispose()
	{
		_matchSceneStateManager.CurrentState.ValueUpdated -= OnMatchSceneStateUpdated;
	}
}
