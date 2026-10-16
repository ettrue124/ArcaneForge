using System;
using System.Collections.Generic;
using Pooling;
using UnityEngine.SceneManagement;
using Wotc.Mtga.Battlefield;

namespace Wotc.Mtga.DuelScene;

public class BattlefieldEventMediator : IDisposable
{
	private readonly IObjectPool _objectPool;

	private readonly List<GameStateUpdatedEvent> _gameStateUpdatedEvents;

	private readonly List<GameStatePlaybackCompleteEvent> _playbackCompleteEvents;

	private readonly List<IncrementPlayerLifeEvent> _incrementPlayerLifeEvents;

	public BattlefieldEventMediator(IObjectPool objectPool, IGameStateProvider gameStateProvider, ISignalListen<GameStatePlaybackCompleteSignalArgs> playbackCompleteSignal, ISignalListen<IncrementPlayerLifeSignalArgs> incrementPlayerLife)
	{
		_objectPool = objectPool;
		_gameStateUpdatedEvents = _objectPool.PopObject<List<GameStateUpdatedEvent>>();
		_playbackCompleteEvents = _objectPool.PopObject<List<GameStatePlaybackCompleteEvent>>();
		_incrementPlayerLifeEvents = _objectPool.PopObject<List<IncrementPlayerLifeEvent>>();
		Scene sceneByName = SceneManager.GetSceneByName(BattlefieldUtil.BattlefieldSceneName);
		if (!sceneByName.IsValid())
		{
			return;
		}
		foreach (GameStateUpdatedEvent sceneComponent in sceneByName.GetSceneComponents<GameStateUpdatedEvent>())
		{
			_gameStateUpdatedEvents.Add(sceneComponent);
			sceneComponent.SetGameStateProvider(gameStateProvider);
		}
		foreach (GameStatePlaybackCompleteEvent sceneComponent2 in sceneByName.GetSceneComponents<GameStatePlaybackCompleteEvent>())
		{
			_playbackCompleteEvents.Add(sceneComponent2);
			sceneComponent2.SetListener(playbackCompleteSignal);
		}
		foreach (IncrementPlayerLifeEvent sceneComponent3 in sceneByName.GetSceneComponents<IncrementPlayerLifeEvent>())
		{
			_incrementPlayerLifeEvents.Add(sceneComponent3);
			sceneComponent3.SetListener(incrementPlayerLife);
		}
	}

	public void Dispose()
	{
		while (_gameStateUpdatedEvents.Count > 0)
		{
			_gameStateUpdatedEvents[0].SetGameStateProvider(NullGameStateProvider.Default);
			_gameStateUpdatedEvents.RemoveAt(0);
		}
		_objectPool.PushObject(_gameStateUpdatedEvents, tryClear: false);
		while (_playbackCompleteEvents.Count > 0)
		{
			_playbackCompleteEvents[0].SetListener(null);
			_playbackCompleteEvents.RemoveAt(0);
		}
		_objectPool.PushObject(_playbackCompleteEvents, tryClear: false);
		while (_incrementPlayerLifeEvents.Count > 0)
		{
			_incrementPlayerLifeEvents[0].SetListener(null);
			_incrementPlayerLifeEvents.RemoveAt(0);
		}
		_objectPool.PushObject(_incrementPlayerLifeEvents, tryClear: false);
	}
}
