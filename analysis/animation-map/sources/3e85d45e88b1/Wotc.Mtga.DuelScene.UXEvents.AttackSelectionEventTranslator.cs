using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using MovementSystem;
using Pooling;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AttackSelectionEventTranslator : IEventTranslator
{
	private readonly IUnityObjectPool _unityObjectPool;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly NPEDirector _npeDirector;

	public AttackSelectionEventTranslator(IContext context, AssetLookupSystem assetLookupSystem, NPEDirector npeDirector)
		: this(context.Get<IUnityObjectPool>(), context.Get<IEntityViewProvider>(), context.Get<ISplineMovementSystem>(), assetLookupSystem, npeDirector)
	{
	}

	public AttackSelectionEventTranslator(IUnityObjectPool unityObjectPool, IEntityViewProvider entityViewProvider, ISplineMovementSystem splineMovementSystem, AssetLookupSystem assetLookupSystem, NPEDirector npeDirector)
	{
		_unityObjectPool = unityObjectPool;
		_entityViewProvider = entityViewProvider;
		_splineMovementSystem = splineMovementSystem;
		_assetLookupSystem = assetLookupSystem;
		_npeDirector = npeDirector;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is AttackSelectionEvent attackSelectionEvent)
		{
			UXEvent item = (attackSelectionEvent.isSelected ? ((NPEUXEvent)new AttackLobUXEvent(GetNpeDirector, _entityViewProvider, _unityObjectPool, attackSelectionEvent.AttackerId, attackSelectionEvent.QuarryId, _assetLookupSystem, _splineMovementSystem)) : ((NPEUXEvent)new AttackDecrementUXEvent(GetNpeDirector, attackSelectionEvent.QuarryId)));
			events.Add(item);
		}
	}

	private NPEDirector GetNpeDirector()
	{
		return _npeDirector;
	}
}
