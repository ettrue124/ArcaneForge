using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DecidingPlayerChangeEventTranslator : IEventTranslator
{
	private readonly IAvatarViewProvider _avatarProvider;

	private readonly ITurnController _turnController;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public DecidingPlayerChangeEventTranslator(IAvatarViewProvider avatarProvider, ITurnController turnController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_avatarProvider = avatarProvider ?? NullAvatarViewProvider.Default;
		_turnController = turnController ?? NullTurnController.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is DecidingPlayerChangeEvent decidingPlayerChangeEvent)
		{
			events.Add(new UXEventUpdateDecider(decidingPlayerChangeEvent.DecidingPlayer, _turnController, _avatarProvider, _vfxProvider, _assetLookupSystem));
		}
	}
}
