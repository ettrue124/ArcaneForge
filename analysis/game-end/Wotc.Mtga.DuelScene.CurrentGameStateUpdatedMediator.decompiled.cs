using System;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene;

public class CurrentGameStateUpdatedMediator : IDisposable
{
	private readonly IAvatarViewProvider _avatarProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ISignalListen<GameStatePlaybackStartedSignalArgs> _playbackStartedEvent;

	public CurrentGameStateUpdatedMediator(IContext context)
		: this(context.Get<IGameStateProvider>(), context.Get<IAvatarViewProvider>())
	{
	}

	public CurrentGameStateUpdatedMediator(IGameStateProvider gameStateProvider, IAvatarViewProvider avatarProvider)
	{
		_gameStateProvider = gameStateProvider;
		_avatarProvider = avatarProvider;
		_gameStateProvider.CurrentGameState.ValueUpdated += OnCurrentGameStateChanged;
	}

	private void OnCurrentGameStateChanged(MtgGameState gameState)
	{
		foreach (DuelScene_AvatarView allAvatar in _avatarProvider.GetAllAvatars())
		{
			if (gameState.TryGetPlayer(allAvatar.InstanceId, out var player))
			{
				allAvatar.SetModel(player);
			}
			allAvatar.UpdateTurnFrameVisuals(gameState);
			allAvatar.SetConcessionIndicator();
		}
	}

	public void Dispose()
	{
		_gameStateProvider.CurrentGameState.ValueUpdated -= OnCurrentGameStateChanged;
	}
}
