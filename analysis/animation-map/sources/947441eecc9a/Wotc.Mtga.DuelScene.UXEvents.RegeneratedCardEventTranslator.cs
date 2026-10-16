using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RegeneratedCardEventTranslator : IEventTranslator
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public RegeneratedCardEventTranslator(ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookup)
	{
		_cardViewProvider = cardViewProvider;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookup;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is RegeneratedCardEvent regeneratedCardEvent)
		{
			events.Add(new RegeneratedCardUXEvent(_cardViewProvider, _vfxProvider, _assetLookupSystem, regeneratedCardEvent.InstanceId));
		}
	}
}
