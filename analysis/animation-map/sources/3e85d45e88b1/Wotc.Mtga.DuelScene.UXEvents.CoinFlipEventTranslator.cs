using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Pooling;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CoinFlipEventTranslator : IEventTranslator
{
	private readonly IUnityObjectPool _unityObjectPool;

	private readonly AssetLookupSystem _assetLookupSystem;

	public CoinFlipEventTranslator(IUnityObjectPool unityObjectPool, AssetLookupSystem assetLookupSystem)
	{
		_unityObjectPool = unityObjectPool;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is CoinFlipEvent { AffectorId: var affectorId, AffectedId: var affectedId, Results: var results })
		{
			if (!(events[events.Count - 1] is ChooseRandomUXEvent_CoinFlip chooseRandomUXEvent_CoinFlip) || !chooseRandomUXEvent_CoinFlip.TryAddFlipResults(affectorId, affectedId, results))
			{
				events.Add(new ChooseRandomUXEvent_CoinFlip(affectorId, affectedId, results, _unityObjectPool, _assetLookupSystem));
			}
		}
	}
}
