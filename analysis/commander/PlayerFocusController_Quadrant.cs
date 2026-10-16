using System;
using System.Collections.Generic;
using Core.Code.Input;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using Wotc.Mtga.DuelScene.PlayerNameViews;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene;

public class PlayerFocusController_Quadrant : IPlayerFocusController, IUpdate, IDisposable
{
	private enum FocusMode
	{
		Single,
		All
	}

	private enum NavigationDirection
	{
		None,
		Clockwise,
		CounterClockwise
	}

	private static Vector3 VIEW_ALL_POSITION = new Vector3(-33f, 122f, 30f);

	private static readonly List<Key> NavigationKeys = new List<Key>
	{
		Key.UpArrow,
		Key.DownArrow,
		Key.LeftArrow,
		Key.RightArrow
	};

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IRelativePlayerIdCalculator _relativePlayerIdCalculator;

	private readonly IPlayerOffsetCalculator _playerOffsetCalculator;

	private readonly IPlayerTimerProvider _playerTimerProvider;

	private readonly IPlayerNameViewProvider _playerNameViewProvider;

	private readonly ISignalListen<PlayerDeletedSignalArgs> _playerDeletedEvent;

	private readonly Transform _cameraTransform;

	private readonly Vector3 _originalCameraPosition;

	private uint _focusPlayerId;

	private FocusMode _focusMode;

	public PlayerFocusController_Quadrant(Transform cameraTransform, ISplineMovementSystem splineMovementSystem, IRelativePlayerIdCalculator relativePlayerIdCalculator, IPlayerOffsetCalculator playerOffsetCalculator, IGameStateProvider gameStateProvider, IPlayerTimerProvider playerTimerProvider, IPlayerNameViewProvider playerNameViewProvider, ISignalListen<PlayerDeletedSignalArgs> playerDeletedEvent)
	{
		_cameraTransform = cameraTransform;
		_originalCameraPosition = _cameraTransform.position;
		_splineMovementSystem = splineMovementSystem;
		_relativePlayerIdCalculator = relativePlayerIdCalculator;
		_playerOffsetCalculator = playerOffsetCalculator;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_playerTimerProvider = playerTimerProvider ?? NullPlayerTimerProvider.Default;
		_playerNameViewProvider = playerNameViewProvider ?? NullPlayerNameViewProvider.Default;
		_playerDeletedEvent = playerDeletedEvent;
		_playerDeletedEvent.Listeners += OnPlayerDeleted;
	}

	public void FocusPlayer(uint playerId)
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		if (mtgGameState.TryGetPlayer(playerId, out var player))
		{
			while (player.Status != PlayerStatus.InGame)
			{
				player = mtgGameState.GetPlayerById(GetNextPlayerId(player.InstanceId));
			}
			if (_focusPlayerId != player.InstanceId)
			{
				_focusPlayerId = player.InstanceId;
				UpdatePlayersAndBattlefield();
			}
			if (_focusMode == FocusMode.Single)
			{
				FocusPlayer_Internal(_focusPlayerId);
			}
		}
	}

	private void FocusPlayer_Internal(uint playerId)
	{
		Vector3 originalCameraPosition = _originalCameraPosition;
		Vector3 offset = _playerOffsetCalculator.GetOffset(playerId);
		Vector3 position = originalCameraPosition + offset;
		_splineMovementSystem.AddPermanentGoal(_cameraTransform, new IdealPoint(_cameraTransform)
		{
			Position = position
		});
	}

	private void UpdatePlayersAndBattlefield()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		if (_focusMode == FocusMode.All)
		{
			foreach (MtgPlayer player in mtgGameState.Players)
			{
				SetPlayerVisibility(player.InstanceId, player.Status == PlayerStatus.InGame);
			}
			return;
		}
		if (_focusMode != FocusMode.Single)
		{
			return;
		}
		foreach (MtgPlayer player2 in mtgGameState.Players)
		{
			SetPlayerVisibility(player2.InstanceId, player2.Status == PlayerStatus.InGame && player2.InstanceId == _focusPlayerId);
		}
	}

	private void SetPlayerVisibility(uint playerId, bool isVisible)
	{
		if (_playerTimerProvider.TryGetPlayerTimer(playerId, out var playerTimer))
		{
			playerTimer.TimeoutDisplay.SetVisibility(isVisible);
			playerTimer.MatchTimer.SetVisibility(isVisible);
		}
		if (_playerNameViewProvider.TryGetPlayerNameById(playerId, out var playerName))
		{
			playerName.SetVisibility(isVisible);
		}
	}

	public void OnUpdate(float time)
	{
		if (TryGetNavigationDirection(_focusPlayerId, out var navigationDirection))
		{
			if (_focusMode == FocusMode.All)
			{
				_focusMode = FocusMode.Single;
				FocusPlayer_Internal(_focusPlayerId);
				UpdatePlayersAndBattlefield();
				return;
			}
			switch (navigationDirection)
			{
			case NavigationDirection.Clockwise:
				FocusPlayer(GetNextPlayerId(_focusPlayerId));
				break;
			case NavigationDirection.CounterClockwise:
				FocusPlayer(GetPrevPlayerId(_focusPlayerId));
				break;
			}
		}
		else if (InputHelper.KeyWasReleasedThisFrame(Key.Equals) && _focusMode == FocusMode.All)
		{
			_focusMode = FocusMode.Single;
			FocusPlayer_Internal(_focusPlayerId);
			UpdatePlayersAndBattlefield();
		}
		else if (InputHelper.KeyWasReleasedThisFrame(Key.Minus) && _focusMode == FocusMode.Single)
		{
			_focusMode = FocusMode.All;
			_splineMovementSystem.AddPermanentGoal(_cameraTransform, new IdealPoint(_cameraTransform)
			{
				Position = VIEW_ALL_POSITION
			});
			UpdatePlayersAndBattlefield();
		}
	}

	private bool TryGetNavigationDirection(uint focusedId, out NavigationDirection navigationDirection)
	{
		navigationDirection = NavigationDirection.None;
		foreach (Key navigationKey in NavigationKeys)
		{
			if (InputHelper.KeyWasReleasedThisFrame(navigationKey))
			{
				navigationDirection = GetNavigationDirection(_relativePlayerIdCalculator.GetRelativeId(focusedId), navigationKey);
				break;
			}
		}
		return navigationDirection != NavigationDirection.None;
	}

	private static NavigationDirection GetNavigationDirection(uint relativeID, Key key)
	{
		return relativeID switch
		{
			1u => key switch
			{
				Key.UpArrow => NavigationDirection.Clockwise, 
				Key.RightArrow => NavigationDirection.CounterClockwise, 
				_ => NavigationDirection.None, 
			}, 
			2u => key switch
			{
				Key.RightArrow => NavigationDirection.Clockwise, 
				Key.DownArrow => NavigationDirection.CounterClockwise, 
				_ => NavigationDirection.None, 
			}, 
			3u => key switch
			{
				Key.DownArrow => NavigationDirection.Clockwise, 
				Key.LeftArrow => NavigationDirection.CounterClockwise, 
				_ => NavigationDirection.None, 
			}, 
			4u => key switch
			{
				Key.LeftArrow => NavigationDirection.Clockwise, 
				Key.UpArrow => NavigationDirection.CounterClockwise, 
				_ => NavigationDirection.None, 
			}, 
			_ => NavigationDirection.None, 
		};
	}

	private uint GetNextPlayerId(uint current)
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		MtgPlayer playerById = mtgGameState.GetPlayerById(current);
		int num = mtgGameState.Players.IndexOf(playerById);
		do
		{
			playerById = mtgGameState.Players[++num % mtgGameState.Players.Count];
		}
		while (playerById.Status != PlayerStatus.InGame);
		return playerById.InstanceId;
	}

	private uint GetPrevPlayerId(uint current)
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		MtgPlayer playerById = mtgGameState.GetPlayerById(current);
		int num = mtgGameState.Players.IndexOf(playerById);
		do
		{
			playerById = mtgGameState.Players[(--num + mtgGameState.Players.Count) % mtgGameState.Players.Count];
		}
		while (playerById.Status != PlayerStatus.InGame);
		return playerById.InstanceId;
	}

	private void OnPlayerDeleted(PlayerDeletedSignalArgs args)
	{
		DuelScene_AvatarView player = args.Player;
		if (!(player == null))
		{
			uint instanceId = player.InstanceId;
			if (_focusPlayerId == instanceId)
			{
				FocusPlayer(GetNextPlayerId(instanceId));
			}
		}
	}

	public void Dispose()
	{
		_playerDeletedEvent.Listeners -= OnPlayerDeleted;
	}
}
