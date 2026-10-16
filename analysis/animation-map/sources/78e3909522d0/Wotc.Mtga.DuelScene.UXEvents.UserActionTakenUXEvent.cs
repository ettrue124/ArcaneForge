using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.GeneralEffect;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UserActionTakenUXEvent : UXEvent
{
	public readonly uint AffectorId;

	public readonly List<uint> AffectedIds;

	private readonly uint _abilityGrpId;

	public readonly ActionType ActionType;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public UserActionTakenUXEvent(uint affector, List<uint> affected, uint abilityGrpId, ActionType actionType, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: this(affector, affected, abilityGrpId, actionType)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_entityViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public UserActionTakenUXEvent(uint affector, List<uint> affected, uint abilityGrpId, ActionType actionType)
	{
		AffectorId = affector;
		AffectedIds = affected;
		_abilityGrpId = abilityGrpId;
		ActionType = actionType;
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		_assetLookupSystem.Blackboard.GreActionType = ActionType;
		ICardDataAdapter cardDataAdapter = null;
		if (mtgGameState.TryGetEntity(AffectorId, out var mtgEntity))
		{
			if (mtgEntity is MtgCardInstance instance)
			{
				ICardDataAdapter cardDataAdapter2 = CardDataExtensions.CreateWithDatabase(instance, _cardDatabase);
				if (_entityViewProvider.TryGetCardView(AffectorId, out var cardView))
				{
					_assetLookupSystem.Blackboard.SetCardDataExtensive(cardDataAdapter2);
					_assetLookupSystem.Blackboard.CardHolderType = cardView.CurrentCardHolder.CardHolderType;
					if (cardView.PreviousCardHolder is ZoneCardHolderBase zoneCardHolderBase)
					{
						_assetLookupSystem.Blackboard.FromZone = zoneCardHolderBase.GetZone;
					}
					if (cardView.CurrentCardHolder is ZoneCardHolderBase zoneCardHolderBase2)
					{
						_assetLookupSystem.Blackboard.ToZone = zoneCardHolderBase2.GetZone;
					}
				}
				cardDataAdapter = cardDataAdapter2;
			}
			else if (mtgEntity is MtgPlayer mtgPlayer)
			{
				_assetLookupSystem.Blackboard.GREPlayerNum = mtgPlayer.ClientPlayerEnum;
			}
		}
		List<MtgEntity> list = new List<MtgEntity>();
		foreach (uint affectedId in AffectedIds)
		{
			if (!mtgGameState.TryGetEntity(affectedId, out var mtgEntity2))
			{
				continue;
			}
			list.Add(mtgEntity2);
			if (cardDataAdapter == null && mtgEntity2 is MtgCardInstance instance2 && _entityViewProvider.TryGetCardView(affectedId, out var cardView2))
			{
				ICardDataAdapter cardDataAdapter3 = CardDataExtensions.CreateWithDatabase(instance2, _cardDatabase);
				_assetLookupSystem.Blackboard.SetCardDataExtensive(cardDataAdapter3);
				_assetLookupSystem.Blackboard.CardHolderType = cardView2.CurrentCardHolder.CardHolderType;
				if (cardView2.PreviousCardHolder is ZoneCardHolderBase zoneCardHolderBase3)
				{
					_assetLookupSystem.Blackboard.FromZone = zoneCardHolderBase3.GetZone;
				}
				if (cardView2.CurrentCardHolder is ZoneCardHolderBase zoneCardHolderBase4)
				{
					_assetLookupSystem.Blackboard.ToZone = zoneCardHolderBase4.GetZone;
				}
				cardDataAdapter = cardDataAdapter3;
			}
			else if (mtgEntity2 is MtgPlayer mtgPlayer2 && _assetLookupSystem.Blackboard.GREPlayerNum == GREPlayerNum.Invalid)
			{
				_assetLookupSystem.Blackboard.GREPlayerNum = mtgPlayer2.ClientPlayerEnum;
			}
		}
		if (_abilityGrpId != 0)
		{
			AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(_abilityGrpId);
			if (abilityPrintingById != null)
			{
				_assetLookupSystem.Blackboard.Ability = abilityPrintingById;
			}
		}
		UserActionTakenVfx payload = _assetLookupSystem.TreeLoader.LoadTree<UserActionTakenVfx>().GetPayload(_assetLookupSystem.Blackboard);
		UserActionTakenSfx payload2 = _assetLookupSystem.TreeLoader.LoadTree<UserActionTakenSfx>().GetPayload(_assetLookupSystem.Blackboard);
		if (payload != null)
		{
			foreach (VfxData vfxData in payload.VfxDatas)
			{
				foreach (MtgEntity item in list)
				{
					if (_entityViewProvider.TryGetEntity(item.InstanceId, out var entityView))
					{
						_vfxProvider.PlayVFX(vfxData, cardDataAdapter, item, entityView.EffectsRoot);
					}
				}
			}
		}
		if (payload2 != null)
		{
			AudioManager.PlayAudio(payload2.SfxData.AudioEvents, _entityViewProvider.TryGetCardView(AffectorId, out var cardView3) ? cardView3.gameObject : AudioManager.Default);
		}
		Complete();
	}
}
