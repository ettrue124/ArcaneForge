using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.UXEventData;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.Extensions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class MutationMergeUXEvent : UXEvent
{
	private const string FAKE_CARD_ID = "TEMPORARY_FAKE_MUTATION_SIBLING";

	public readonly uint CardId;

	public readonly MtgCardInstance CardInstance;

	public readonly bool IsOver;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly EntityViewManager _viewManager;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly MtgCardInstance _newCardInstance;

	private readonly MtgCardInstance _parentCardInstance;

	private readonly List<MtgCardInstance> _siblingCardInstances = new List<MtgCardInstance>();

	private readonly MutationMergeData _data;

	public MutationMergeUXEvent(uint cardId, MtgCardInstance cardInstance, IGameStateProvider gameStateProvider, IVfxProvider vfxProvider, EntityViewManager viewManager, ICardHolderProvider cardHolderManager, ICardDatabaseAdapter cardDatabase, ISplineMovementSystem splineMovementSystem, AssetLookupSystem assetLookupSystem)
	{
		assetLookupSystem.Blackboard.Clear();
		AssetLookupTree<MutationMergeData> assetLookupTree = assetLookupSystem.TreeLoader.LoadTree<MutationMergeData>();
		_data = assetLookupTree.GetPayload(assetLookupSystem.Blackboard);
		CardId = cardId;
		CardInstance = cardInstance;
		_gameStateProvider = gameStateProvider;
		_vfxProvider = vfxProvider;
		_viewManager = viewManager;
		_cardDatabase = cardDatabase;
		_splineMovementSystem = splineMovementSystem;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderManager);
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderManager);
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_newCardInstance = mtgGameState.VisibleCards[cardId].GetCopy();
		_newCardInstance.Zone = mtgGameState.Battlefield;
		_parentCardInstance = mtgGameState.VisibleCards[_newCardInstance.MutationParentId];
		IsOver = _parentCardInstance.LayeredEffects.Exists((LayeredEffectData x) => x.AffectorId == _newCardInstance.InstanceId && x.Type == "MutateOver");
		if (_parentCardInstance.OverlayGrpId.HasValue && _parentCardInstance.BaseGrpId != 3)
		{
			MtgCardInstance mtgCardInstance = _cardDatabase.CardDataProvider.GetCardPrintingById(_parentCardInstance.BaseGrpId).CreateInstance();
			mtgCardInstance.Zone = _parentCardInstance.Zone;
			mtgCardInstance.InstanceId = _parentCardInstance.InstanceId;
			_siblingCardInstances.Add(mtgCardInstance);
		}
		if (_parentCardInstance.MutationChildren.Count > 0)
		{
			_siblingCardInstances.AddRange(_parentCardInstance.MutationChildren);
		}
	}

	public override void Execute()
	{
		DuelScene_CDC cardView = _viewManager.GetCardView(CardId);
		DuelScene_CDC parentView = _viewManager.GetCardView(_parentCardInstance.InstanceId);
		_splineMovementSystem.MovementCompleted += onCardMovementComplete;
		CardData cardData = CardDataExtensions.CreateWithDatabase(_newCardInstance, _cardDatabase);
		LoopingAnimationManager.RemoveAllLoopingEffects(cardView.EffectsRoot);
		cardView.CurrentCardHolder?.RemoveCard(cardView);
		cardView.CurrentCardHolder = _battlefield.Get();
		cardView.SetModel(cardData, updateVisuals: true, CardHolderType.Battlefield);
		IdealPoint endPoint = new IdealPoint(parentView.Root);
		SplineMovementData splineMovementData = null;
		SplineEventData splineEventData = new SplineEventData();
		if (IsOver)
		{
			endPoint.Position += _data.OverPositionOffset;
			splineMovementData = AssetLoader.GetObjectData(_data.OverSplineRef);
			VfxPrefabData prefabData = _data.OverSplineBirthVfxData.PrefabData;
			if (prefabData != null && prefabData.AllPrefabs.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(_data.OverSplineBirthVfxData, cardData, cardView.EffectsRoot, _vfxProvider));
			}
			if (_data.OverSplineBirthSfxData.AudioEvents.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventAudio(0f, _data.OverSplineBirthSfxData.AudioEvents, cardView.EffectsRoot.gameObject));
			}
			VfxPrefabData prefabData2 = _data.OverSplineTrailVfxData.PrefabData;
			if (prefabData2 != null && prefabData2.AllPrefabs.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(_data.OverSplineTrailVfxData, cardData, cardView.EffectsRoot, _vfxProvider));
			}
			VfxPrefabData prefabData3 = _data.OverSplineHitVfxData.PrefabData;
			if (prefabData3 != null && prefabData3.AllPrefabs.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(_data.OverSplineHitVfxData, cardData, parentView.EffectsRoot, _vfxProvider));
			}
			if (_data.OverSplineHitSfxData.AudioEvents.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventAudio(1f, _data.OverSplineHitSfxData.AudioEvents, cardView.EffectsRoot.gameObject));
			}
		}
		else
		{
			endPoint.Position += _data.UnderPositionOffset;
			splineMovementData = AssetLoader.GetObjectData(_data.UnderSplineRef);
			splineEventData.Events.Add(new SplineEventCallback(0.8f, (float _) =>
			{
				_splineMovementSystem.RemoveTemporaryGoal(parentView.Root);
			}));
			VfxPrefabData prefabData4 = _data.UnderSplineBirthVfxData.PrefabData;
			if (prefabData4 != null && prefabData4.AllPrefabs.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(_data.UnderSplineBirthVfxData, cardData, cardView.EffectsRoot, _vfxProvider));
			}
			if (_data.UnderSplineBirthSfxData.AudioEvents.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventAudio(0f, _data.UnderSplineBirthSfxData.AudioEvents, cardView.EffectsRoot.gameObject));
			}
			VfxPrefabData prefabData5 = _data.UnderSplineTrailVfxData.PrefabData;
			if (prefabData5 != null && prefabData5.AllPrefabs.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(_data.UnderSplineTrailVfxData, cardData, cardView.EffectsRoot, _vfxProvider));
			}
			VfxPrefabData prefabData6 = _data.UnderSplineHitVfxData.PrefabData;
			if (prefabData6 != null && prefabData6.AllPrefabs.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(_data.UnderSplineHitVfxData, cardData, parentView.EffectsRoot, _vfxProvider));
			}
			if (_data.UnderSplineHitSfxData.AudioEvents.Count > 0)
			{
				splineEventData.Events.Add(new SplineEventAudio(1f, _data.UnderSplineHitSfxData.AudioEvents, cardView.EffectsRoot.gameObject));
			}
		}
		_splineMovementSystem.AddPermanentGoal(cardView.Root, endPoint, allowInteractions: false, splineMovementData, splineEventData);
		if (!IsOver)
		{
			IdealPoint endPoint2 = new IdealPoint(parentView.Root);
			endPoint2.Position += _data.UnderParentPositionOffset;
			endPoint2.Rotation *= Quaternion.Euler(_data.UnderParentRotationOffset);
			_splineMovementSystem.AddTemporaryGoal(parentView.Root, endPoint2);
			if (_siblingCardInstances.Count > 1)
			{
				MtgCardInstance instance = FindMostAppropriateSiblingForFakeUnderCard();
				DuelScene_CDC duelScene_CDC = _viewManager.CreateFakeCard("TEMPORARY_FAKE_MUTATION_SIBLING", CardDataExtensions.CreateWithDatabase(instance, _cardDatabase));
				duelScene_CDC.CurrentCardHolder = _battlefield.Get();
				duelScene_CDC.gameObject.SetLayer(_battlefield.Get().Layer);
				duelScene_CDC.UpdateVisibility(shouldBeVisible: true);
				duelScene_CDC.Root.SetParent(parentView.Root.parent);
				duelScene_CDC.Root.position = parentView.Root.position + _data.UnderSiblingPositionOffset;
				duelScene_CDC.Root.rotation = parentView.Root.rotation * Quaternion.Euler(_data.UnderSiblingRotationOffset);
				duelScene_CDC.Root.localScale = parentView.Root.localScale;
			}
		}
		void onCardMovementComplete(Transform transform)
		{
			if (!(cardView.Root != transform))
			{
				_splineMovementSystem.MovementCompleted -= onCardMovementComplete;
				cardView.CurrentCardHolder = _stack.Get();
				if (!IsOver)
				{
					_splineMovementSystem.RemoveTemporaryGoal(parentView.Root);
					_viewManager.DeleteFakeCard("TEMPORARY_FAKE_MUTATION_SIBLING");
				}
				Complete();
			}
		}
	}

	private MtgCardInstance FindMostAppropriateSiblingForFakeUnderCard()
	{
		for (int num = _siblingCardInstances.Count - 1; num >= 1; num--)
		{
			MtgCardInstance mtgCardInstance = _siblingCardInstances[num];
			if (mtgCardInstance.GrpId != _newCardInstance.GrpId && mtgCardInstance.GrpId != _parentCardInstance.GrpId)
			{
				return mtgCardInstance;
			}
		}
		return _siblingCardInstances[0];
	}

	protected override void Cleanup()
	{
		_stack.ClearCache();
		_battlefield.ClearCache();
		base.Cleanup();
	}

	public static bool IsZoneTransferDueToMutate(ZoneTransferUXEvent zte, GameManager gameManager)
	{
		if (zte.Reason == ZoneTransferReason.Delete && zte.OldInstance != null && zte.OldInstance.MutationParentId == 0 && gameManager.LatestGameState.VisibleCards.TryGetValue(zte.OldId, out var value) && value.MutationParentId != 0 && gameManager.LatestGameState.VisibleCards.TryGetValue(value.MutationParentId, out var value2))
		{
			return value2.MutationChildrenIds.Contains(zte.OldId);
		}
		return false;
	}
}
