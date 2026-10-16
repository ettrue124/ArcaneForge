using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class PlayerControllerChangedEventTranslator : IEventTranslator
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IAvatarViewProvider _avatarViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public PlayerControllerChangedEventTranslator(IGameStateProvider gameStateProvider, IAvatarViewProvider avatarViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_avatarViewProvider = avatarViewProvider ?? NullAvatarViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is PlayerControllerChanged playerControllerChanged)
		{
			events.Add(new PlayerControllerChangedUXEvent(playerControllerChanged.PlayerId, _gameStateProvider, _avatarViewProvider, _vfxProvider, _assetLookupSystem));
		}
	}
}
