using System;
using System.Collections.Generic;
using System.Linq;
using Core.Code.Input;
using GreClient.Rules;
using Pooling;
using UnityEngine.InputSystem;
using Wotc.Mtga.DuelScene.PlayerNameViews;
using Wotc.Mtga.DuelScene.ZoneCounts;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene;

public class PlayerFocusController : IPlayerFocusController, IUpdate, IDisposable
{
	private enum FocusMode
	{
		Single,
		All
	}

	private readonly IObjectPool _objectPool;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IAvatarViewProvider _avatarViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IZoneCountProvider _zoneCountProvider;

	private readonly IPlayerTimerProvider _playerTimerProvider;

	private readonly IPlayerNameViewProvider _playerNameViewProvider;

	private readonly ISignalListen<PlayerDeletedSignalArgs> _playerDeletedEvent;

	private uint _focusPlayerId;

	private FocusMode _focusMode;

	public PlayerFocusController(IObjectPool objectPool, IGameStateProvider gameStateProvider, IAvatarViewProvider avatarViewProvider, ICardHolderProvider cardHolderProvider, IZoneCountProvider zoneCountProvider, IPlayerTimerProvider playerTimerProvider, IPlayerNameViewProvider playerNameViewProvider, ISignalListen<PlayerDeletedSignalArgs> playerDeletedEvent)
	{
		_objectPool = objectPool ?? NullObjectPool.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_avatarViewProvider = avatarViewProvider ?? NullAvatarViewProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_zoneCountProvider = zoneCountProvider ?? NullZoneCountProvider.Default;
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
			while (player.IsLocalPlayer || player.Status != PlayerStatus.InGame)
			{
				player = mtgGameState.GetPlayerById(GetNextPlayerId(player.InstanceId));
			}
			if (_focusPlayerId != player.InstanceId)
			{
				_focusPlayerId = player.InstanceId;
				UpdatePlayersAndBattlefield();
			}
		}
	}

	private void UpdatePlayersAndBattlefield()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		if (_focusMode == FocusMode.All)
		{
			foreach (MtgPlayer player in mtgGameState.Players)
			{
				SetPlayerVisibility(player.InstanceId, player.Status == PlayerStatus.InGame, mtgGameState);
			}
			if (_cardHolderProvider.TryGetCardHolder(GREPlayerNum.Invalid, CardHolderType.Battlefield, out IBattlefieldCardHolder result))
			{
				result.SetOpponentFocus((from player in mtgGameState.Players
					where player.Status == PlayerStatus.InGame
					select player.InstanceId).ToArray());
			}
		}
		else
		{
			if (_focusMode != FocusMode.Single)
			{
				return;
			}
			foreach (MtgPlayer player2 in mtgGameState.Players)
			{
				SetPlayerVisibility(player2.InstanceId, player2.Status == PlayerStatus.InGame && (player2.IsLocalPlayer || player2.InstanceId == _focusPlayerId), mtgGameState);
			}
			if (_cardHolderProvider.TryGetCardHolder(GREPlayerNum.Invalid, CardHolderType.Battlefield, out IBattlefieldCardHolder result2))
			{
				result2.SetOpponentFocus(_focusPlayerId);
			}
		}
	}

	private void SetPlayerVisibility(uint playerId, bool isVisible, MtgGameState gameState)
	{
		if (!_avatarViewProvider.TryGetAvatarById(playerId, out var avatar))
		{
			return;
		}
		avatar.gameObject.UpdateActive(isVisible);
		foreach (CardHolderBase item in GetAssociatedCardHoldersFor(playerId, gameState))
		{
			item.SetVisibility(isVisible);
		}
		if (_zoneCountProvider.TryGetForPlayer(playerId, out var zoneCountView))
		{
			zoneCountView.gameObject.UpdateActive(isVisible);
		}
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

	private IEnumerable<CardHolderBase> GetAssociatedCardHoldersFor(uint playerId, MtgGameState gameState)
	{
		if (gameState != null)
		{
			MtgZone zoneForPlayer = gameState.GetZoneForPlayer(playerId, ZoneType.Hand);
			if (_cardHolderProvider.TryGetCardHolder<CardHolderBase>(zoneForPlayer.Id, out var result))
			{
				yield return result;
			}
			MtgZone zoneForPlayer2 = gameState.GetZoneForPlayer(playerId, ZoneType.Library);
			if (_cardHolderProvider.TryGetCardHolder<CardHolderBase>(zoneForPlayer2.Id, out var result2))
			{
				yield return result2;
			}
			MtgZone zoneForPlayer3 = gameState.GetZoneForPlayer(playerId, ZoneType.Graveyard);
			if (_cardHolderProvider.TryGetCardHolder<CardHolderBase>(zoneForPlayer3.Id, out var result3))
			{
				yield return result3;
			}
			if (_cardHolderProvider.TryGetCardHolder<IExileCardHolder>(gameState.Exile.Id, out var result4))
			{
				yield return result4.GetSubCardHolderForPlayer(playerId) as CardHolderBase;
			}
			if (_cardHolderProvider.TryGetCardHolder<ICommandCardHolder>(gameState.Command.Id, out var result5))
			{
				yield return result5.GetSubCardHolderForPlayer(playerId) as CardHolderBase;
			}
		}
	}

	public void OnUpdate(float time)
	{
		if (InputHelper.KeyWasReleasedThisFrame(Key.LeftArrow))
		{
			if (_focusMode == FocusMode.All)
			{
				_focusMode = FocusMode.Single;
				UpdatePlayersAndBattlefield();
			}
			else
			{
				FocusPlayer(GetPrevPlayerId(_focusPlayerId));
			}
		}
		else if (InputHelper.KeyWasReleasedThisFrame(Key.RightArrow))
		{
			if (_focusMode == FocusMode.All)
			{
				_focusMode = FocusMode.Single;
				UpdatePlayersAndBattlefield();
			}
			else
			{
				FocusPlayer(GetNextPlayerId(_focusPlayerId));
			}
		}
		else if (InputHelper.KeyWasReleasedThisFrame(Key.UpArrow))
		{
			if (_focusMode == FocusMode.Single)
			{
				_focusMode = FocusMode.All;
				UpdatePlayersAndBattlefield();
			}
		}
		else if (InputHelper.KeyWasReleasedThisFrame(Key.DownArrow) && _focusMode == FocusMode.All)
		{
			_focusMode = FocusMode.Single;
			UpdatePlayersAndBattlefield();
		}
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
		while (playerById.ClientPlayerEnum != GREPlayerNum.Opponent || playerById.Status != PlayerStatus.InGame);
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
		while (playerById.ClientPlayerEnum != GREPlayerNum.Opponent || playerById.Status != PlayerStatus.InGame);
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
