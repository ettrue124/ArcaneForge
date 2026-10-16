using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Ability;
using AssetLookupTree.Payloads.Card;
using AssetLookupTree.Payloads.ZoneTransfer;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Duel;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

public class ZoneTransferUXEvent : UXEvent
{
	private class ZoneTransferEffect
	{
		private readonly DuelScene_CDC _source;

		private readonly DuelScene_CDC _target;

		private readonly ZoneTransferReason _reason;

		private readonly ZonePair _zonePair;

		public ZoneTransferEffect(DuelScene_CDC source, DuelScene_CDC target, ZoneTransferReason reason, ZonePair zonePair)
		{
			_source = source;
			_target = target;
			_reason = reason;
			_zonePair = zonePair;
		}

		public void Execute(CombatAnimationPlayer combatAnimationPlayer, System.Action onComplete)
		{
			combatAnimationPlayer.PlayZoneTransferEffect(_source, _target.EffectsRoot, _reason, _zonePair, onComplete);
			if (_target?.Model != null)
			{
				_target.UpdateAbilityVisuals<PersistVFX, PersistSfx>(display: true, animate: true, 0f);
			}
		}
	}

	private float _timeSpentAtDestination;

	public float RequiredTimeSpentAtDestination;

	private ZonePair _zonePair;

	private ZoneTransferEffect _zoneTransferEffect;

	private bool _movementComplete;

	private bool _isCreateEvent;

	private bool _isDestroyEvent;

	private readonly IContext _context;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly GameManager _gameManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly ICardViewManager _cardViewManager;

	private readonly ICardDissolveController _cardDissolveController;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IRevealedCardsController _revealedCardsController;

	private readonly ICardMovementController _cardMovementController;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly IVfxProvider _vfxProvider;

	private readonly CardData _cachedNewInstanceData;

	private readonly CardData _cachedOldIsntanceData;

	private DuelScene_CDC _cardView;

	private float _pendingZoneTransferTimer;

	public readonly bool IsAbilityBeingCountered;

	public readonly bool IsMergeBreakup;

	private const string TOSTRING_FORMAT = "{0} -> {1} | {2} -> {3} | Reason: {4}";

	public ZoneTransferReason Reason { get; set; }

	public uint OldId { get; private set; }

	public MtgCardInstance OldInstance { get; private set; }

	public uint NewId { get; private set; }

	public MtgCardInstance NewInstance { get; private set; }

	public MtgCardInstance Instigator { get; private set; }

	public MtgZone FromZone { get; private set; }

	public ZoneType FromZoneType { get; private set; }

	public MtgZone ToZone { get; private set; }

	public ZoneType ToZoneType => ToZone?.Type ?? ZoneType.None;

	public List<CardRevealedEvent> RevealEvents { get; private set; } = new List<CardRevealedEvent>();

	public bool HasIdChange => OldId != NewId;

	public override bool IsBlocking => true;

	public static event Action<MtgCardInstance, ZoneTransferReason> ZoneTransferExecuted;

	private bool IsUnknownToKnownDestroy()
	{
		if (_isDestroyEvent && HasIdChange && (OldInstance == null || OldInstance.FaceDownState.IsFaceDown) && NewInstance != null)
		{
			return !NewInstance.FaceDownState.IsFaceDown;
		}
		return false;
	}

	public ZoneTransferUXEvent(uint oldId, MtgCardInstance oldInstance, uint newId, MtgCardInstance newInstance, MtgCardInstance instigator, MtgZone fromZone, MtgZone toZone, ZoneTransferReason reason, List<CardRevealedEvent> revealEvents = null)
	{
		OldId = oldId;
		OldInstance = oldInstance;
		NewId = newId;
		NewInstance = newInstance;
		Instigator = instigator;
		FromZone = fromZone ?? new MtgZone
		{
			Type = ZoneType.None
		};
		FromZoneType = FromZone.Type;
		ToZone = toZone ?? new MtgZone
		{
			Type = ZoneType.None
		};
		Reason = reason;
		if (revealEvents != null)
		{
			RevealEvents.AddRange(revealEvents);
		}
		_zonePair = new ZonePair(FromZone, ToZone);
	}

	public ZoneTransferUXEvent(IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager, uint oldId, MtgCardInstance oldInstance, uint newId, MtgCardInstance newInstance, MtgCardInstance instigator, MtgZone fromZone, MtgZone toZone, ZoneTransferReason reason, List<CardRevealedEvent> revealEvents = null)
		: this(oldId, oldInstance, newId, newInstance, instigator, fromZone, toZone, reason, revealEvents)
	{
		_context = context ?? (context = NullContext.Default);
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_cardViewManager = context.Get<ICardViewManager>() ?? NullCardViewManager.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
		_cardMovementController = context.Get<ICardMovementController>() ?? NullCardMovementController.Default;
		_revealedCardsController = context.Get<IRevealedCardsController>() ?? NullRevealedCardsController.Default;
		_vfxProvider = context.Get<IVfxProvider>() ?? NullVfxProvider.Default;
		_splineMovementSystem = context.Get<ISplineMovementSystem>();
		_cardDissolveController = context.Get<ICardDissolveController>() ?? NullCardDissolveController.Default;
		_assetLookupSystem = assetLookupSystem;
		_gameManager = gameManager;
		_stack = CardHolderReference<StackCardHolder>.Stack(_cardHolderProvider);
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(_cardHolderProvider);
		IsAbilityBeingCountered = Reason == ZoneTransferReason.Countered && NewInstance == null && OldInstance != null && OldInstance.ObjectType == GameObjectType.Ability;
		IsMergeBreakup = HasIdChange && FromZoneType == ZoneType.Suppressed && OldInstance != null && NewInstance != null;
		if (NewInstance != null)
		{
			_cachedNewInstanceData = CardDataExtensions.CreateWithDatabase(NewInstance, _cardDatabase);
		}
		if (OldInstance != null)
		{
			_cachedOldIsntanceData = CardDataExtensions.CreateWithDatabase(OldInstance, _cardDatabase);
		}
	}

	public ZoneTransferUXEvent Clone(uint oldId, MtgCardInstance oldInstance, uint newId, MtgCardInstance newInstance, MtgCardInstance instigator, MtgZone fromZone, MtgZone toZone, ZoneTransferReason reason, List<CardRevealedEvent> revealEvents = null)
	{
		return new ZoneTransferUXEvent(_context, _assetLookupSystem, _gameManager, oldId, oldInstance, newId, newInstance, instigator, fromZone, toZone, reason, revealEvents);
	}

	private void CheckIsCreateDestroyEvents()
	{
		if (NewInstance == null && OldInstance == null)
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.ZoneTransferReason = Reason;
		_assetLookupSystem.Blackboard.ZonePair = new ZonePair(FromZone, ToZone);
		_assetLookupSystem.Blackboard.SetCardDataExtensive((NewInstance != null) ? _cachedNewInstanceData : _cachedOldIsntanceData);
		if (OldInstance != null)
		{
			_assetLookupSystem.Blackboard.SupplementalCardData[SupplementalKey.OldInstance] = _cachedOldIsntanceData;
		}
		_isCreateEvent = _assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<IsCreateEvent> loadedTree) && loadedTree.GetPayload(_assetLookupSystem.Blackboard) != null;
		int isDestroyEvent;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<IsDestroyEvent> loadedTree2))
		{
			IsDestroyEvent payload = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				isDestroyEvent = (payload.IsDestroy ? 1 : 0);
				goto IL_0108;
			}
		}
		isDestroyEvent = 0;
		goto IL_0108;
		IL_0108:
		_isDestroyEvent = (byte)isDestroyEvent != 0;
	}

	public override void Execute()
	{
		SetToZoneOverride(_gameStateProvider.CurrentGameState);
		CheckIsCreateDestroyEvents();
		if (RevealEvents.Count == 1 && RevealEvents[0].CreateInstance != null)
		{
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(RevealEvents[0].CreateInstance.GrpId);
			if (cardPrintingById != null && cardPrintingById.IsToken)
			{
				NewId = OldId;
				Reason = ZoneTransferReason.Delete;
			}
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.ZonePair = _zonePair;
		_assetLookupSystem.Blackboard.ZoneTransferReason = Reason;
		if (OldInstance != null)
		{
			_assetLookupSystem.Blackboard.SupplementalCardData[SupplementalKey.OldInstance] = _cachedOldIsntanceData;
		}
		if (NewInstance != null)
		{
			_assetLookupSystem.Blackboard.SetCardDataExtensive(_cachedNewInstanceData);
			_assetLookupSystem.Blackboard.CardHolderType = _cachedNewInstanceData.ZoneType.ToCardHolderType();
		}
		DelayPayload payload = _assetLookupSystem.TreeLoader.LoadTree<DelayPayload>().GetPayload(_assetLookupSystem.Blackboard);
		if (payload != null)
		{
			RequiredTimeSpentAtDestination = payload.EndDelay;
		}
		if (Reason == ZoneTransferReason.Delete || IsAbilityBeingCountered)
		{
			_cardView = _cardViewManager.GetCardView(OldId);
			DoPreDeleteMoveCard();
			PlayZoneTransferEffect();
			return;
		}
		if (_isCreateEvent)
		{
			_cardView = CreateCard(NewInstance);
			_zoneTransferEffect = new ZoneTransferEffect(_cardView, _cardView, Reason, _zonePair);
			PlayZoneTransferEffect();
			return;
		}
		if (HasIdChange)
		{
			DuelScene_CDC cardView2;
			if (Instigator != null)
			{
				if ((_cardViewManager.TryGetCardView(NewId, out var cardView) || _cardViewManager.TryGetCardView(OldId, out cardView)) && ((_stack.Get().TryGetTopCardOnStack(out var topCard) && topCard.Model?.Instance?.ParentId == Instigator.InstanceId) || _cardViewManager.TryGetCardView(Instigator.InstanceId, out topCard)))
				{
					_zoneTransferEffect = new ZoneTransferEffect(topCard, cardView, Reason, _zonePair);
				}
			}
			else if (_cardViewManager.TryGetCardView(OldId, out cardView2))
			{
				_zoneTransferEffect = new ZoneTransferEffect(cardView2, cardView2, Reason, _zonePair);
			}
		}
		uint num = (HasIdChange ? OldId : NewId);
		MtgCardInstance mtgCardInstance = (HasIdChange ? OldInstance : NewInstance);
		if (IsMergeBreakup)
		{
			DuelScene_CDC cardView3 = _cardViewManager.GetCardView(OldId);
			_cardView = CreateCard(NewInstance);
			_cardView.UpdateVisibility(shouldBeVisible: true);
			_cardView.CurrentCardHolder = _battlefield.Get();
			_cardView.Root.position = (cardView3 ? cardView3.Root.position : _battlefield.Get().CardRoot.position) + Vector3.down;
			if (ToZoneType == ZoneType.Graveyard)
			{
				PlayCardDestroyedEffect();
			}
			else
			{
				UpdateInstanceId();
			}
			return;
		}
		if (!_cardViewManager.TryGetCardView(num, out _cardView))
		{
			Debug.LogWarningFormat("ZoneTransferUXEvent told to perform an operation on a card not found in the game: {0} -> {1}", num, mtgCardInstance);
			_cardView = CreateCard(mtgCardInstance);
		}
		if (IsUnknownToKnownDestroy())
		{
			MtgZone fromZone = FromZone;
			if (fromZone == null || fromZone.Type != ZoneType.Hand)
			{
				MtgZone fromZone2 = FromZone;
				if (fromZone2 == null || fromZone2.Type != ZoneType.Library)
				{
					goto IL_0440;
				}
			}
			AddFaceDownPendingZoneTransferDelay(0.5f);
			return;
		}
		goto IL_0440;
		IL_0440:
		MtgCardInstance newInstance = NewInstance;
		if (newInstance != null && newInstance.FaceDownState.IsFaceDown)
		{
			MtgZone fromZone3 = FromZone;
			if (fromZone3 != null && fromZone3.Type == ZoneType.Graveyard)
			{
				AddFaceDownPendingZoneTransferDelay(0.4f);
				return;
			}
		}
		PlayZoneTransferEffect();
	}

	private void SetToZoneOverride(MtgGameState gameState)
	{
		if (Reason == ZoneTransferReason.Delete || NewInstance == null)
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(_cachedNewInstanceData);
		_assetLookupSystem.Blackboard.ZonePair = _zonePair;
		_assetLookupSystem.Blackboard.ZoneTransferReason = Reason;
		if (OldInstance != null)
		{
			_assetLookupSystem.Blackboard.SupplementalCardData[SupplementalKey.OldInstance] = _cachedOldIsntanceData;
		}
		ToZoneOverridePayload payload = _assetLookupSystem.TreeLoader.LoadTree<ToZoneOverridePayload>().GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null)
		{
			return;
		}
		foreach (KeyValuePair<uint, MtgZone> zone in gameState.Zones)
		{
			MtgZone value = zone.Value;
			if (value.Type == payload.ZoneType)
			{
				ToZone = value;
				break;
			}
		}
	}

	private void AddFaceDownPendingZoneTransferDelay(float delay)
	{
		MtgCardInstance copy = NewInstance.GetCopy();
		copy.Zone = FromZone;
		UpdateCardModel(copy);
		_pendingZoneTransferTimer = delay;
	}

	private void PlayZoneTransferEffect()
	{
		if ((bool)_cardView)
		{
			LoopingAnimationManager.RemoveAllLoopingEffectsDuringZoneTransfer(_cardView.EffectsRoot);
		}
		if (_zoneTransferEffect != null)
		{
			if (_gameManager.ActiveResolutionEffect != null && _gameManager.ActiveResolutionEffect.IgnoreDestroyEffects && _isDestroyEvent)
			{
				PlayCardDestroyedEffect();
			}
			else
			{
				_zoneTransferEffect.Execute(_gameManager.CombatAnimationPlayer, PlayCardDestroyedEffect);
			}
		}
		else if (_isDestroyEvent)
		{
			PlayCardDestroyedEffect();
		}
		else
		{
			UpdateInstanceId();
		}
	}

	private void PlayCardDestroyedEffect()
	{
		if (_isDestroyEvent)
		{
			CardData responsibleCard = ((Instigator != null) ? CardDataExtensions.CreateWithDatabase(Instigator, _cardDatabase) : CardDataExtensions.CreateBlank());
			if (!_cardView.TargetVisibility || !_cardView.gameObject.activeSelf)
			{
				UpdateInstanceId();
			}
			else if (FromZoneType == ZoneType.Suppressed || (FromZoneType == ZoneType.Graveyard && ToZoneType == ZoneType.Exile))
			{
				UpdateInstanceId();
			}
			else
			{
				_cardDissolveController.DissolveCard(_cardView, UpdateInstanceId, Reason, responsibleCard, FromZone, ToZone);
			}
		}
		else
		{
			UpdateInstanceId();
		}
	}

	private void UpdateInstanceId()
	{
		if (NewInstance == null)
		{
			_cardViewManager.DeleteCard(OldId);
			ApplyRevealEvents(null);
			Complete();
			return;
		}
		if (NewInstance.Zone == null || (NewInstance.Zone.Type == ZoneType.Limbo && NewInstance.Zone.Type != ToZoneType))
		{
			NewInstance.Zone = ToZone;
		}
		if (HasIdChange && !IsMergeBreakup)
		{
			_cardView = _cardViewManager.UpdateIdForCardView(OldId, NewId);
			_cardView.ClearDeadVFX();
		}
		UpdateCardModel(NewInstance);
		ApplyRevealEvents(_cardView);
		MoveCard();
	}

	private void ApplyRevealEvents(DuelScene_CDC source)
	{
		if (RevealEvents.Count == 0)
		{
			return;
		}
		foreach (CardRevealedEvent revealEvent in RevealEvents)
		{
			switch (revealEvent.EventType)
			{
			case RevealEventType.Create:
				_revealedCardsController.CreateRevealedCard(revealEvent.OwnerId, revealEvent.CreateInstance, source);
				break;
			case RevealEventType.Delete:
				_revealedCardsController.DeleteRevealedCard(revealEvent.DeleteId);
				break;
			}
		}
	}

	private void MoveCard()
	{
		switch (Reason)
		{
		case ZoneTransferReason.PendingToStack:
		{
			Transform root2 = _cardView.Root;
			if (NewInstance.ObjectType == GameObjectType.Ability && NewInstance.Parent != null && _cardViewManager.TryGetCardView(NewInstance.Parent.InstanceId, out var cardView))
			{
				_splineMovementSystem.MoveInstant(root2, new IdealPoint
				{
					Position = cardView.Root.position,
					Rotation = cardView.Root.rotation,
					Scale = cardView.Root.localScale * 0.333f
				});
			}
			break;
		}
		case ZoneTransferReason.Draw:
		{
			Transform root = _cardView.Root;
			_splineMovementSystem.MoveInstant(root, new IdealPoint
			{
				Position = root.position,
				Rotation = root.rotation * Quaternion.Euler(180f, 180f, 0f),
				Scale = root.localScale
			});
			break;
		}
		}
		_splineMovementSystem.RemoveTemporaryGoal(_cardView.Root);
		if (_cardView.Model.IsTapped)
		{
			_cardView.SetDimmedState(isDimmed: true);
		}
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		ICardHolder cardHolder = _cardHolderProvider.GetCardHolderByZoneId(ToZone.Id);
		if (mtgGameState.LocalPlayer.ControllerType == ControllerType.Player && mtgGameState.LocalPlayerPendingMessageType == ClientMessageType.MulliganResp && cardHolder.PlayerNum == GREPlayerNum.LocalPlayer && cardHolder.CardHolderType == CardHolderType.Hand)
		{
			cardHolder = _cardHolderProvider.GetCardHolder(GREPlayerNum.Invalid, CardHolderType.CardBrowserDefault);
		}
		_splineMovementSystem.MovementStarted += AddZteVfxToMoveSpline;
		_splineMovementSystem.MovementCompleted += OnMovementCompleted;
		_cardMovementController.MoveCard(_cardView, cardHolder, FromZoneType != ToZoneType);
		if (_isCreateEvent && ToZoneType != ZoneType.Stack)
		{
			Complete();
		}
	}

	private void AddZteVfxToMoveSpline(Transform cardTransform, IdealPoint idealWorldPosition, SplineEventData splineEvents)
	{
		AddZteVfxToMoveSpline(cardTransform, splineEvents, idealWorldPosition.Position, _zonePair);
	}

	private void AddZteVfxToMoveSpline(Transform cardTransform, SplineEventData splineEvents, Vector3 idealWorldPosition, ZonePair zonePair)
	{
		if (!(cardTransform == _cardView.Root))
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(_cardView.Model);
		_assetLookupSystem.Blackboard.CardHolderType = _cardView.CurrentCardHolder.CardHolderType;
		_assetLookupSystem.Blackboard.ZoneTransferReason = Reason;
		_assetLookupSystem.Blackboard.ZonePair = zonePair;
		_assetLookupSystem.Blackboard.IdealWorldPosition = idealWorldPosition;
		if (Instigator != null && _cardViewManager.TryGetCardView(Instigator.InstanceId, out var cardView))
		{
			_assetLookupSystem.Blackboard.SupplementalCardData[SupplementalKey.Instigator] = (CardData)cardView.Model;
		}
		if (OldInstance != null)
		{
			_assetLookupSystem.Blackboard.SupplementalCardData[SupplementalKey.OldInstance] = _cachedOldIsntanceData;
		}
		if (!_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<CardMovementVFX> loadedTree))
		{
			return;
		}
		CardMovementVFX payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null)
		{
			return;
		}
		foreach (VfxData vfxData in payload.VfxDatas)
		{
			if (vfxData.PrefabData == null)
			{
				continue;
			}
			float time = Mathf.Clamp01(vfxData.PrefabData.StartTime);
			splineEvents.Events.Add(new SplineEventCallbackWithParams<(IVfxProvider, DuelScene_CDC, VfxData)>(time, (_vfxProvider, _cardView, vfxData), (float _, (IVfxProvider vfxProvider, DuelScene_CDC card, VfxData vfxData) paramBlob) =>
			{
				if (!(paramBlob.card == null) && !(paramBlob.card.EffectsRoot == null))
				{
					paramBlob.vfxProvider.PlayVFX(paramBlob.vfxData, paramBlob.card.Model);
				}
			}));
		}
	}

	private void OnMovementCompleted(Transform t)
	{
		if (!(_cardView.Root != t))
		{
			_movementComplete = true;
			_splineMovementSystem.MovementStarted -= AddZteVfxToMoveSpline;
			_splineMovementSystem.MovementCompleted -= OnMovementCompleted;
		}
	}

	private void UpdateCardModel(MtgCardInstance newInstance)
	{
		ICardDataAdapter data = CardDataExtensions.CreateWithDatabase(newInstance, _cardDatabase);
		_cardView.SetModel(data);
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_pendingZoneTransferTimer > 0f)
		{
			_pendingZoneTransferTimer -= dt;
			if (_pendingZoneTransferTimer <= 0f)
			{
				PlayZoneTransferEffect();
			}
		}
		if (_movementComplete)
		{
			_timeSpentAtDestination += dt;
			if (_timeSpentAtDestination >= RequiredTimeSpentAtDestination)
			{
				Complete();
			}
		}
	}

	protected override void Cleanup()
	{
		if (_splineMovementSystem != null)
		{
			_splineMovementSystem.MovementStarted -= AddZteVfxToMoveSpline;
			_splineMovementSystem.MovementCompleted -= OnMovementCompleted;
		}
		ZoneTransferExecuted?.Invoke(Instigator, Reason);
		_stack.ClearCache();
		_battlefield.ClearCache();
		base.Cleanup();
	}

	public override string ToString()
	{
		return $"{OldId} -> {NewId} | {FromZoneType} -> {ToZoneType} | Reason: {Reason}";
	}

	private DuelScene_CDC CreateCard(MtgCardInstance card)
	{
		ICardDataAdapter cardDataAdapter = ((card == NewInstance) ? _cachedNewInstanceData : CardDataExtensions.CreateWithDatabase(card, _cardDatabase));
		if (_cardViewManager.TryGetCardView(cardDataAdapter.InstanceId, out var cardView))
		{
			return cardView;
		}
		cardView = _cardViewManager.CreateCardView(cardDataAdapter);
		Transform root = cardView.Root;
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardDataAdapter);
		_assetLookupSystem.Blackboard.ZoneTransferReason = Reason;
		_assetLookupSystem.Blackboard.ZonePair = new ZonePair(FromZone, ToZone);
		_assetLookupSystem.Blackboard.FromZone = FromZone;
		_assetLookupSystem.Blackboard.ToZone = ToZone;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<CreateCard_OriginOffsets> loadedTree))
		{
			CreateCard_OriginOffsets payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				Transform cardRoot = _battlefield.Get().CardRoot;
				if (_cardHolderProvider.TryGetCardHolder(cardDataAdapter.OwnerNum, payload.HolderType, out var cardHolder) || _cardHolderProvider.TryGetCardHolder(GREPlayerNum.Invalid, payload.HolderType, out cardHolder))
				{
					cardRoot = cardHolder.CardRoot;
				}
				root.position = cardRoot.position + payload.PositionOffset;
				root.rotation = cardRoot.rotation * Quaternion.Euler(payload.RotationOffset);
				root.localScale = Vector3.Scale(cardRoot.localScale, payload.ScaleMultiplier);
				goto IL_02ec;
			}
		}
		DuelScene_CDC cardView2;
		if (card.ObjectType == GameObjectType.Token)
		{
			root.position = new Vector3(0f, -10f, 0f);
			root.localScale = Vector3.one * 0.1f;
		}
		else if (card.ParentId != 0 && _cardViewManager.TryGetCardView(_cardViewManager.GetCardUpdatedId(card.ParentId), out cardView2))
		{
			root.position = cardView2.Root.position;
			root.rotation = cardView2.Root.rotation;
			root.localScale = cardView2.Root.localScale * 0.333f;
		}
		else
		{
			GREPlayerNum playerNum = card.Owner?.ClientPlayerEnum ?? GREPlayerNum.Invalid;
			ZoneType zoneType = card.Zone?.Type ?? ZoneType.Library;
			switch (zoneType)
			{
			case ZoneType.None:
			case ZoneType.Limbo:
			case ZoneType.Sideboard:
			case ZoneType.PhasedOut:
				zoneType = ZoneType.Library;
				break;
			case ZoneType.Pending:
				zoneType = ZoneType.Stack;
				break;
			}
			ICardHolder cardHolder2 = _cardHolderProvider.GetCardHolder(playerNum, zoneType.ToCardHolderType());
			root.position = cardHolder2.CardRoot.position;
			root.rotation = cardHolder2.CardRoot.rotation;
			root.localScale = Vector3.one * cardHolder2.CardScale;
		}
		goto IL_02ec;
		IL_02ec:
		_assetLookupSystem.Blackboard.Clear();
		return cardView;
	}

	public override IEnumerable<uint> GetInvolvedIds()
	{
		yield return OldId;
		if (OldId != NewId)
		{
			yield return NewId;
		}
	}

	private void DoPreDeleteMoveCard()
	{
		if ((bool)_cardView)
		{
			_cardView.PreviousCardHolder = _cardView.CurrentCardHolder;
			if (_cardView.PreviousCardHolder != null)
			{
				_cardView.PreviousCardHolder.RemoveCard(_cardView);
			}
			_cardView.CurrentCardHolder = new NoCardHolder();
			SplineEventData splineEvents = new SplineEventData();
			ZonePair fromToZoneForCard = CardViewUtilities.GetFromToZoneForCard(_cardView);
			if (fromToZoneForCard.ToZone == ZoneType.Stack || fromToZoneForCard.FromZone == ZoneType.Stack)
			{
				CardHolderBase.SetupSplineEventsALT<MovementPayload_Stack_VFX, MovementPayload_Stack_SFX>(new CardLayoutData
				{
					Card = _cardView,
					CardGameObject = _cardView.Root.gameObject
				}, added: true, ref splineEvents, CardHolderBase.CardPosition.None, fromToZoneForCard, _gameManager);
			}
			else
			{
				CardHolderBase.SetupSplineEventsALT<MovementPayload_NonStack_VFX, MovementPayload_NonStack_SFX>(new CardLayoutData
				{
					Card = _cardView,
					CardGameObject = _cardView.Root.gameObject
				}, added: true, ref splineEvents, CardHolderBase.CardPosition.None, fromToZoneForCard, _gameManager);
			}
			AddZteVfxToMoveSpline(_cardView.Root, splineEvents, _cardView.Root.transform.position, fromToZoneForCard);
			splineEvents.UpdateEvents(0f, 1f, new IdealPoint(_cardView.Root));
		}
	}

	public void AttachmentAddedViaZoneTransfer(uint attachedToId)
	{
		NewInstance = NewInstance.GetCopy();
		NewInstance.AttachedToId = attachedToId;
	}
}
