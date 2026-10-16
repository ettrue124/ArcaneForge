using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ObjectSelectedEventTranslator : IEventTranslator
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ObjectSelectedEventTranslator(ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ObjectSelectedEvent ose)
		{
			events.Add(new ObjectSelectedUXEvent(ose, _cardViewProvider, _vfxProvider, _assetLookupSystem));
		}
	}
}
