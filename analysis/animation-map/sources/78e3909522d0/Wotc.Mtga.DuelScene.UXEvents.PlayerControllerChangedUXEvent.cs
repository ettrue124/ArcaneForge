using AssetLookupTree;
using AssetLookupTree.Payloads.Player;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class PlayerControllerChangedUXEvent : UXEvent
{
	private readonly uint _playerId;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IAvatarViewProvider _avatarViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public PlayerControllerChangedUXEvent(uint playerId, IGameStateProvider gameStateProvider, IAvatarViewProvider avatarViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_playerId = playerId;
		_gameStateProvider = gameStateProvider;
		_avatarViewProvider = avatarViewProvider;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		if (((MtgGameState)_gameStateProvider.CurrentGameState).TryGetPlayer(_playerId, out var player))
		{
			PlayVFX(player);
		}
		if (_avatarViewProvider.TryGetAvatarById(_playerId, out var avatar))
		{
			avatar.PlayerControllerChanged();
		}
		Complete();
	}

	private void PlayVFX(MtgPlayer player)
	{
		if (!_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ControllerChangedVFX> loadedTree))
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.Player = player;
		ControllerChangedVFX payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null)
		{
			return;
		}
		foreach (VfxData vfxData in payload.VfxDatas)
		{
			_vfxProvider.PlayVFX(vfxData, null);
		}
	}
}
