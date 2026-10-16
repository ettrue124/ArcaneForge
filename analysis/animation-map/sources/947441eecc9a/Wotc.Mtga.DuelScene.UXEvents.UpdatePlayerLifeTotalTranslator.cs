using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdatePlayerLifeTotalTranslator : IEventTranslator, IDisposable
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCache;

	private readonly ILifeTotalController _lifeTotalController;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private MtgGameState _previousGameState;

	private MtgGameState _currentGameState;

	public UpdatePlayerLifeTotalTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<ISplineMovementSystem>(), context.Get<IAssetCacheProvider<SplineMovementData>>(), context.Get<ILifeTotalController>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	public UpdatePlayerLifeTotalTranslator(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCache, ILifeTotalController lifeTotalController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_entityViewProvider = entityViewProvider;
		_splineMovementSystem = splineMovementSystem;
		_lifeTotalController = lifeTotalController;
		_vfxProvider = vfxProvider;
		_splineCache = splineCache;
		_assetLookupSystem = assetLookupSystem;
		_gameStateProvider.CurrentGameState.ValueUpdated += OnCurrentGameStateUpdated;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UpdatePlayerLifeTotal updatePlayerLifeTotal)
		{
			MtgCardInstance affector = GetAffector(updatePlayerLifeTotal.Affector, _currentGameState, _previousGameState);
			events.Add(new LifeTotalUpdateUXEvent(affector, updatePlayerLifeTotal.Player.InstanceId, updatePlayerLifeTotal.LifeChange, _cardDatabase, _entityViewProvider, _splineMovementSystem, _splineCache, _lifeTotalController, _vfxProvider, _assetLookupSystem));
		}
	}

	private static MtgCardInstance GetAffector(uint id, MtgGameState newState, MtgGameState oldState)
	{
		if (newState.TryGetCard(id, out var card))
		{
			return card;
		}
		if (oldState.TryGetCard(id, out var card2))
		{
			return card2;
		}
		Debug.LogWarningFormat("Couldn't find affector id {0} in either new or old GameState for UpdatePlayerLifeTotal change event.", id);
		return new MtgCardInstance
		{
			InstanceId = id
		};
	}

	private void OnCurrentGameStateUpdated(MtgGameState gameState)
	{
		_previousGameState = _currentGameState;
		_currentGameState = gameState;
	}

	public void Dispose()
	{
		_gameStateProvider.CurrentGameState.ValueUpdated -= OnCurrentGameStateUpdated;
		_previousGameState = null;
		_currentGameState = null;
	}
}
