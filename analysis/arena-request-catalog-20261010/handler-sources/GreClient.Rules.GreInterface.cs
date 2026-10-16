using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using GreClient.CardData;
using GreClient.History;
using GreClient.Rules.Annotations;
using GreClient.Rules.CardReferenceHandlers;
using GreClient.Rules.EventGeneration;
using GreClient.Rules.Notifications;
using Newtonsoft.Json.Linq;
using ReferenceMap;
using Wizards.Arena.Client.Logging;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class GreInterface : IDisposable
{
	private readonly IReadOnlyList<ICardReferenceHandler> _cardReferenceHandlers;

	private readonly HashSet<uint> _attachedWithChangeList = new HashSet<uint>();

	private readonly Dictionary<uint, int> _attachedToChanges = new Dictionary<uint, int>();

	private HashSet<ReferenceMap.Reference> _oldReferences = new HashSet<ReferenceMap.Reference>();

	private HashSet<ReferenceMap.Reference> _newReferences = new HashSet<ReferenceMap.Reference>();

	private readonly List<ReplacementEffectData> _oldReplacementEffects = new List<ReplacementEffectData>();

	private readonly List<ReplacementEffectData> _newReplacementEffects = new List<ReplacementEffectData>();

	private readonly HashSet<uint> zoneCardIdsToDelete = new HashSet<uint>();

	private readonly HashSet<uint> zoneCardIdsToAdd = new HashSet<uint>();

	private readonly RelatedZoneUpdateGenerator _relatedZoneGenerator = new RelatedZoneUpdateGenerator();

	private readonly IEventGenerator<MtgGameState> _playerDiffGenerator = DiffEventGeneratorFactory.PlayerDiffGenerator();

	private readonly IEventGenerator<MtgGameState> _revealedCardGenerator = new RevealedCardUpdatedEventGenerator();

	private readonly IEventGenerator<MtgGameState> _undoPlayerDiffGenerator = DiffEventGeneratorFactory.UndoPlayerDiffGenerator();

	private readonly IEventGenerator<MtgGameState> _undoRevealedCardGenerator = new GameStateEventGeneratorAggregate(new RevealedCardDeletedEventGenerator(), new RevealedCardUpdatedEventGenerator(), new RevealedCardCreatedEventGenerator());

	private readonly IEventGenerator<MtgCardInstance> _linkInfoGenerator = new LinkInfoEventGenerator();

	private readonly ActionInfoComparer _actionInfoComparer = new ActionInfoComparer();

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IAnnotationEventProcessor _annotationEventProcessor;

	private readonly IAnnotationStateParser _annotationStateParsers;

	private readonly IRequestAnnotationProcessor<BaseUserRequest> _requestAnnotationProcessor;

	private readonly MessageHistory _messageHistory;

	private readonly DynamicAbilityStateAnnotationParser _dynamicAbilityStateAnnotationParser;

	private readonly ConcurrentQueue<GREToClientMessage> _incomingMessages = new ConcurrentQueue<GREToClientMessage>();

	private readonly ConcurrentQueue<ClientToGREMessage> _outgoingMessages = new ConcurrentQueue<ClientToGREMessage>();

	private readonly ConcurrentQueue<ClientUpdateBase> _clientUpdates = new ConcurrentQueue<ClientUpdateBase>();

	private MtgGameState _currentGameState = new MtgGameState();

	private uint _mySeatId;

	private readonly Dictionary<uint, Dictionary<uint, uint>> _idHistory = new Dictionary<uint, Dictionary<uint, uint>>();

	private uint _currentMessageCount;

	private uint _targetMessageCount;

	private uint _currentGameStateId;

	private GameStateMessage _lastGameStateMessage;

	private GameStateUpdate _pendingGameStateUpdate;

	private readonly CompoundLogger _loggerForNonEmergencies;

	private bool _disposed;

	private readonly Dictionary<GREMessageType, GreToClientMessageTranslatorBase> _messageTranslators;

	private readonly IReadOnlyDictionary<GREMessageType, INotificationParser> _notificationParsers;

	private readonly IGreInterfaceCallbacks _callbacks;

	private List<ClientUpdateBase> updateCache = new List<ClientUpdateBase>();

	public static MtgEntity GetPreviousIncrementalEntity(uint id, IReadOnlyList<GameRulesEvent> changes)
	{
		if (id == 0 || changes == null)
		{
			return null;
		}
		for (int num = changes.Count - 1; num >= 0; num--)
		{
			GameRulesEvent gameRulesEvent = changes[num];
			if (gameRulesEvent is IncrementalModelUpdateEvent { NewInstance: { } newInstance } incrementalModelUpdateEvent && newInstance.InstanceId == id)
			{
				return incrementalModelUpdateEvent.NewInstance;
			}
			if (gameRulesEvent is CardCreatedEvent { Card: { } card } cardCreatedEvent && card.InstanceId == id)
			{
				return cardCreatedEvent.Card;
			}
		}
		return null;
	}

	public static MtgEntity CreateNewIncrementalEntity(MtgEntity oldEntity, MtgEntity previousEntity, MtgEntity newEntity)
	{
		if (!(oldEntity is MtgCardInstance mtgCardInstance))
		{
			if (oldEntity is MtgPlayer mtgPlayer)
			{
				MtgPlayer mtgPlayer2 = previousEntity as MtgPlayer;
				_ = newEntity is MtgPlayer;
				return (mtgPlayer2 ?? mtgPlayer).GetCopy();
			}
			return (previousEntity ?? newEntity).GetCopy();
		}
		object obj = previousEntity as MtgCardInstance;
		MtgCardInstance mtgCardInstance2 = newEntity as MtgCardInstance;
		if (obj == null)
		{
			obj = mtgCardInstance;
		}
		MtgCardInstance copy = ((MtgCardInstance)obj).GetCopy();
		copy.PendingEffectOverrides = null;
		if (mtgCardInstance2 != null && mtgCardInstance2.Zone != null && mtgCardInstance2.Zone.Type != ZoneType.Limbo && mtgCardInstance2.Zone.Type != ZoneType.PhasedOut && (copy.Zone == null || copy.Zone.Type != ZoneType.PhasedOut))
		{
			copy.Zone = mtgCardInstance2.Zone;
		}
		if (mtgCardInstance2 != null)
		{
			copy.CardTypes.Clear();
			copy.CardTypes.AddRange(mtgCardInstance2.CardTypes);
			copy.Subtypes.Clear();
			copy.Subtypes.AddRange(mtgCardInstance2.Subtypes);
			copy.Supertypes.Clear();
			copy.Supertypes.AddRange(mtgCardInstance2.Supertypes);
			copy.OverlayGrpId = mtgCardInstance2.OverlayGrpId;
			copy.TitleId = mtgCardInstance2.TitleId;
			copy.Colors.Clear();
			copy.Colors.AddRange(mtgCardInstance2.Colors);
			copy.IsTapped = mtgCardInstance2.IsTapped;
			copy.LayeredEffects.Clear();
			copy.LayeredEffects.AddRange(mtgCardInstance2.LayeredEffects);
			copy.SkinCode = mtgCardInstance2.SkinCode;
			copy.HasSummoningSickness = mtgCardInstance2.HasSummoningSickness;
		}
		return copy;
	}

	private void MergeDiffMessageIntoFullMessage(GameStateMessage left, GameStateMessage right)
	{
		left.GameStateId = right.GameStateId;
		left.PrevGameStateId = right.PrevGameStateId;
		left.PendingMessageCount = right.PendingMessageCount;
		if (right.GameInfo != null)
		{
			left.GameInfo = right.GameInfo;
		}
		if (right.TurnInfo != null)
		{
			left.TurnInfo = right.TurnInfo;
		}
		if (right.Annotations != null)
		{
			left.Annotations.Clear();
			foreach (AnnotationInfo annotation in right.Annotations)
			{
				left.Annotations.Add(annotation);
			}
		}
		if (right.Actions != null)
		{
			left.Actions.Clear();
			foreach (ActionInfo action in right.Actions)
			{
				left.Actions.Add(action);
			}
		}
		MergeRepeatedFields(left.PersistentAnnotations, right.PersistentAnnotations, (AnnotationInfo x) => x.Id);
		MergeRepeatedFields(left.GameObjects, right.GameObjects, (GameObjectInfo x) => x.InstanceId);
		MergeRepeatedFields(left.Zones, right.Zones, (ZoneInfo x) => x.ZoneId);
		MergeRepeatedFields(left.Players, right.Players, (PlayerInfo x) => x.SystemSeatNumber);
		MergeRepeatedFields(left.Timers, right.Timers, (TimerInfo x) => x.TimerId);
		RemoveDeletedObjects(left.GameObjects, right.DiffDeletedInstanceIds, (GameObjectInfo x) => x.InstanceId);
		RemoveDeletedObjects(left.PersistentAnnotations, right.DiffDeletedPersistentAnnotationIds, (AnnotationInfo x) => x.Id);
	}

	private void RemoveDeletedObjects<T>(RepeatedField<T> objs, RepeatedField<uint> deletedIds, Func<T, uint> getId)
	{
		for (int num = objs.Count - 1; num >= 0; num--)
		{
			if (deletedIds.Contains(getId(objs[num])))
			{
				objs.RemoveAt(num);
			}
		}
	}

	private void MergeRepeatedFields<T>(RepeatedField<T> left, RepeatedField<T> right, Func<T, uint> getId) where T : class
	{
		for (int i = 0; i < left.Count; i++)
		{
			T arg = left[i];
			T val = null;
			for (int j = 0; j < right.Count; j++)
			{
				T val2 = right[j];
				if (getId(arg) == getId(val2))
				{
					val = val2;
					break;
				}
			}
			if (val != null)
			{
				left[i] = val;
			}
		}
		int count = right.Count;
		for (int k = 0; k < count; k++)
		{
			T val3 = right[k];
			bool flag = true;
			for (int l = 0; l < left.Count; l++)
			{
				T arg2 = left[l];
				if (getId(arg2) == getId(val3))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				left.Add(val3);
			}
		}
	}

	private MtgGameState ParseFullGameStateMessage(GameStateMessage msg)
	{
		MtgGameState mtgGameState = new MtgGameState();
		mtgGameState.Id = msg.GameStateId;
		mtgGameState.PrevId = msg.PrevGameStateId;
		mtgGameState.GameInfo = msg.GameInfo;
		foreach (TimerInfo timer in msg.Timers)
		{
			MtgTimer value = new MtgTimer(timer);
			mtgGameState.Timers.Add(timer.TimerId, value);
		}
		uint num = getMyTeamId(msg.Players, _mySeatId);
		List<MtgPlayer> list = new List<MtgPlayer>();
		foreach (PlayerInfo player in msg.Players)
		{
			uint systemSeatNumber = player.SystemSeatNumber;
			bool flag = systemSeatNumber == _mySeatId;
			bool flag2 = player.TeamId == num;
			MtgPlayer mtgPlayer = new MtgPlayer(flag ? GREPlayerNum.LocalPlayer : (flag2 ? GREPlayerNum.Teammate : GREPlayerNum.Opponent));
			foreach (ManaInfo item in player.ManaPool)
			{
				MtgMana mtgMana = new MtgMana(item, systemSeatNumber);
				mtgPlayer.ManaPool.Add(mtgMana);
				mtgPlayer.IdToManaMap.Add(item.ManaId, mtgMana);
			}
			foreach (uint timerId in player.TimerIds)
			{
				if (mtgGameState.Timers.TryGetValue(timerId, out var value2))
				{
					mtgPlayer.Timers.Add(value2.SetOwnerId(systemSeatNumber));
				}
			}
			mtgPlayer.StartingLifeTotal = player.StartingLifeTotal;
			mtgPlayer.LifeTotal = player.LifeTotal;
			mtgPlayer.InstanceId = systemSeatNumber;
			mtgPlayer.MulliganCount = player.MulliganCount;
			mtgPlayer.MaxHandSize = player.MaxHandSize;
			mtgPlayer.ControllerType = player.ControllerType;
			mtgPlayer.TimeoutCount = player.TimeoutCount;
			mtgPlayer.PendingMessageType = player.PendingMessageType;
			mtgPlayer.ControllerId = player.ControllerSeatId;
			mtgPlayer.ControlledByLocalPlayer = mtgPlayer.ControllerId == _mySeatId;
			mtgPlayer.Status = player.Status;
			list.Add(mtgPlayer);
			if (mtgGameState.Teams.TryGetValue(player.TeamId, out var value3))
			{
				value3.Players.Add(mtgPlayer);
				mtgPlayer.Team = value3;
				continue;
			}
			MtgTeam mtgTeam = new MtgTeam
			{
				TeamId = player.TeamId,
				IsMyTeam = (player.TeamId == num),
				Players = new List<MtgPlayer> { mtgPlayer }
			};
			mtgGameState.Teams[player.TeamId] = mtgTeam;
			mtgPlayer.Team = mtgTeam;
		}
		list.Sort((MtgPlayer a, MtgPlayer b) => a.InstanceId.CompareTo(b.InstanceId));
		mtgGameState.Players = list;
		foreach (ZoneInfo zone2 in msg.Zones)
		{
			MtgZone mtgZone = new MtgZone();
			mtgZone.Id = zone2.ZoneId;
			mtgZone.Owner = mtgGameState.GetPlayerById(zone2.OwnerSeatId);
			mtgZone.Type = zone2.Type;
			mtgZone.CardIds = new List<uint>(zone2.ObjectInstanceIds);
			mtgZone.Visibility = zone2.Visibility;
			mtgGameState.Zones.Add(mtgZone.Id, mtgZone);
			mtgGameState.ObjectIds.UnionWith(zone2.ObjectInstanceIds);
		}
		foreach (GameObjectInfo obj in msg.GameObjects)
		{
			MtgCardInstance mtgCardInstance = new MtgCardInstance();
			mtgCardInstance.InstanceId = obj.InstanceId;
			mtgCardInstance.GrpId = obj.GrpId;
			mtgCardInstance.ObjectType = obj.Type;
			mtgCardInstance.AttackState = obj.AttackState;
			mtgCardInstance.BlockState = obj.BlockState;
			mtgCardInstance.Controller = mtgGameState.GetPlayerById(obj.ControllerSeatId);
			mtgCardInstance.Owner = mtgGameState.GetPlayerById(obj.OwnerSeatId);
			mtgCardInstance.HasSummoningSickness = obj.HasSummoningSickness;
			mtgCardInstance.IsTapped = obj.IsTapped;
			mtgCardInstance.Power = ((obj.Power != null) ? new StringBackedInt(obj.Power.Value) : StringBackedInt.UNDEFINED);
			mtgCardInstance.Toughness = ((obj.Toughness != null) ? new StringBackedInt(obj.Toughness.Value) : StringBackedInt.UNDEFINED);
			mtgCardInstance.Visibility = obj.Visibility;
			if ((mtgCardInstance.ObjectSourceGrpId = obj.ObjectSourceGrpId) != 0)
			{
				mtgCardInstance.ObjectSourcePrinting = _cardDatabase.CardDataProvider.GetCardPrintingById(obj.ObjectSourceGrpId);
			}
			mtgCardInstance.OthersideGrpId = obj.OthersideGrpId;
			mtgCardInstance.Supertypes.AddRange(obj.SuperTypes);
			mtgCardInstance.CardTypes.AddRange(obj.CardTypes);
			mtgCardInstance.Subtypes.AddRange(obj.Subtypes);
			mtgCardInstance.RemovedSubtypes.AddRange(obj.RemovedSubtypes);
			mtgCardInstance.Colors.AddRange(obj.Color);
			mtgCardInstance.ParentId = obj.ParentId;
			mtgCardInstance.SkinCode = obj.SkinCode;
			mtgCardInstance.BaseSkinCode = obj.BaseSkinCode;
			mtgCardInstance.IsCopy = obj.IsCopy;
			if (obj.Loyalty != null)
			{
				mtgCardInstance.Loyalty = obj.Loyalty.Value;
			}
			if (obj.Defense != null)
			{
				mtgCardInstance.Defense = obj.Defense.Value;
			}
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(obj.GrpId);
			if (cardPrintingById != null)
			{
				if (mtgCardInstance.ObjectType == GameObjectType.Card)
				{
					mtgGameState.SeenCardGrpIds.Add(mtgCardInstance.GrpId);
					foreach (uint linkedFaceGrpId in cardPrintingById.LinkedFaceGrpIds)
					{
						mtgGameState.SeenCardGrpIds.Add(linkedFaceGrpId);
					}
				}
				if (mtgCardInstance.IsCopy && !mtgCardInstance.Colors.SequenceEqual(cardPrintingById.Colors))
				{
					mtgCardInstance.ColorModifications.Add(new ColorModification(0u, mtgCardInstance.InstanceId, ColorModification.ModType.Set, mtgCardInstance.Colors));
				}
			}
			if (obj.CardTypes.Contains(CardType.Creature) || !obj.CardTypes.Contains(CardType.Planeswalker))
			{
				mtgCardInstance.Damage = obj.Damage;
				mtgCardInstance.Damaged = obj.Damage != 0;
			}
			if (obj.BlockInfo != null)
			{
				mtgGameState.BlockInfo[obj.InstanceId] = obj.BlockInfo;
				List<uint> blockingIds = mtgCardInstance.BlockingIds;
				IEnumerable<uint> collection;
				if (obj.BlockInfo.OrderedAttackers.Count <= 0)
				{
					IEnumerable<uint> attackerIds = obj.BlockInfo.AttackerIds;
					collection = attackerIds;
				}
				else
				{
					collection = obj.BlockInfo.OrderedAttackers.Select((OrderedDamageAssignment x) => x.InstanceId);
				}
				blockingIds.AddRange(collection);
			}
			if (obj.AttackInfo != null)
			{
				mtgGameState.AttackInfo[obj.InstanceId] = obj.AttackInfo;
				mtgCardInstance.AttackTargetId = obj.AttackInfo.TargetId;
				if (obj.AttackInfo.OrderedBlockers.Count > 0)
				{
					mtgCardInstance.BlockedByIds.AddRange(obj.AttackInfo.OrderedBlockers.Select((OrderedDamageAssignment x) => x.InstanceId));
				}
			}
			if (mtgCardInstance.ParentId != 0)
			{
				mtgGameState.ReferenceMap.Add(mtgCardInstance.ParentId, ReferenceMap.ReferenceType.ParentOf, mtgCardInstance.InstanceId);
				if ((obj.Type == GameObjectType.SplitLeft || obj.Type == GameObjectType.SplitRight) && mtgGameState.TryGetCard(mtgCardInstance.ParentId, out var card))
				{
					CardPrintingData cardPrintingById2 = _cardDatabase.CardDataProvider.GetCardPrintingById(card.GrpId);
					foreach (AbilityPrintingData parentAbility in card.Abilities)
					{
						if (!cardPrintingById2.Abilities.Exists((AbilityPrintingData x) => x.Id == parentAbility.Id) && !obj.UniqueAbilities.Exists((UniqueAbilityInfo x) => x.GrpId == parentAbility.Id))
						{
							mtgCardInstance.AbilityModifications.Add(AbilityModification.Added(parentAbility.Id));
							mtgCardInstance.AbilityAdders.Add(new AddedAbilityData(parentAbility.Id, 0u, 0u, 0u, card.InstanceId));
							mtgCardInstance.Abilities.Add(_cardDatabase.AbilityDataProvider.GetAbilityPrintingById(parentAbility.Id));
						}
					}
				}
			}
			if (obj.Type == GameObjectType.TriggerHolder)
			{
				mtgCardInstance.ObjectType = GameObjectType.Ability;
				mtgCardInstance.GrpId = mtgCardInstance.ObjectSourceGrpId;
				mtgGameState.DelayedTriggers.Add(mtgCardInstance);
			}
			if (obj.OverlayGrpId < 11)
			{
				mtgCardInstance.CatalogId = (WellKnownCatalogId)obj.OverlayGrpId;
			}
			else if (obj.OverlayGrpId != obj.GrpId)
			{
				mtgCardInstance.OverlayGrpId = obj.OverlayGrpId;
			}
			mtgCardInstance.TitleId = obj.Name;
			if (obj.Type == GameObjectType.Ability)
			{
				GameObjectInfo gameObjectInfo = msg.GameObjects.FirstOrDefault((GameObjectInfo go) => go.InstanceId == obj.ParentId);
				if (gameObjectInfo != null)
				{
					mtgCardInstance.TitleId = gameObjectInfo.Name;
				}
			}
			foreach (UniqueAbilityInfo uniqueAbility in obj.UniqueAbilities)
			{
				uint grpId = uniqueAbility.GrpId;
				if (_cardDatabase.AbilityDataProvider.TryGetAbilityPrintingById(grpId, out var ability))
				{
					if (ability.Id != 114)
					{
						mtgCardInstance.Abilities.Add(ability);
						uint id = uniqueAbility.Id;
						MtgAbilityInstance mtgAbilityInstance = new MtgAbilityInstance(id, uniqueAbility.GrpId, ability);
						mtgCardInstance.AbilityInstances.Add(mtgAbilityInstance);
						if (id != 0)
						{
							mtgGameState.AbilityInstances[id] = mtgAbilityInstance;
						}
					}
				}
				else
				{
					_loggerForNonEmergencies.Warn("Ability found on card is missing from the _abilities dictionary.", new JObject
					{
						["abilityId"] = grpId,
						["cardId"] = mtgCardInstance.GrpId
					});
					mtgCardInstance.Abilities.Add(AbilityPrintingData.InvalidAbility(grpId));
				}
			}
			foreach (uint abilityOriginalCardGrpId in obj.AbilityOriginalCardGrpIds)
			{
				if (abilityOriginalCardGrpId != 0)
				{
					mtgCardInstance.AbilityOriginalCardGrpIds.Add(abilityOriginalCardGrpId);
				}
			}
			if (obj.Viewers != null)
			{
				foreach (uint viewer in obj.Viewers)
				{
					if (viewer == _mySeatId)
					{
						mtgCardInstance.Viewers.Add(GREPlayerNum.LocalPlayer);
					}
					else
					{
						mtgCardInstance.Viewers.Add(GREPlayerNum.Opponent);
					}
				}
			}
			mtgGameState.AllCards[obj.InstanceId] = mtgCardInstance;
			if (mtgCardInstance.ObjectType == GameObjectType.RevealedCard)
			{
				MtgZone zone = mtgGameState.GetZone(ZoneType.Revealed, mtgCardInstance.Owner);
				if (zone != null)
				{
					MtgZone zoneById = mtgGameState.GetZoneById(obj.ZoneId);
					if (zoneById != null)
					{
						mtgCardInstance.Zone = zoneById;
					}
					zone.VisibleCards.Add(mtgCardInstance);
					mtgGameState.RevealedCards.Add(mtgCardInstance.InstanceId, mtgCardInstance);
				}
				continue;
			}
			MtgZone zoneById2 = mtgGameState.GetZoneById(obj.ZoneId);
			if (zoneById2 != null)
			{
				mtgCardInstance.Zone = zoneById2;
				zoneById2.VisibleCards.Add(mtgCardInstance);
				if (mtgCardInstance.IsPhasedOut)
				{
					mtgGameState.Battlefield?.VisibleCards.Add(mtgCardInstance);
				}
			}
			else if (obj.ZoneId == 0)
			{
				mtgCardInstance.Zone = new MtgZone();
				mtgGameState.ObjectIds.Add(obj.InstanceId);
			}
			else
			{
				mtgCardInstance.Zone = new MtgZone
				{
					Id = obj.ZoneId
				};
				mtgGameState.ObjectIds.Add(obj.InstanceId);
				_loggerForNonEmergencies.Error("Invalid zoneId found", new JObject { ["zoneId"] = obj.ZoneId });
			}
			mtgGameState.VisibleCards.Add(mtgCardInstance.InstanceId, mtgCardInstance);
		}
		foreach (ActionInfo action2 in msg.Actions)
		{
			mtgGameState.Actions.Add(action2);
			Wotc.Mtgo.Gre.External.Messaging.Action action = action2.Action;
			if (action != null)
			{
				uint instanceId = action.InstanceId;
				if (instanceId != 0 && (mtgGameState.VisibleCards.TryGetValue(instanceId, out var value4) || mtgGameState.AbsorbedFacetCards.TryGetValue(instanceId, out value4)))
				{
					value4.Actions.Add(action2);
				}
				uint sourceId = action.SourceId;
				if (sourceId != 0 && (mtgGameState.VisibleCards.TryGetValue(sourceId, out var value5) || mtgGameState.AbsorbedFacetCards.TryGetValue(sourceId, out value5)))
				{
					value5.Actions.Add(action2);
				}
			}
		}
		mtgGameState.GameWideTurn = msg.TurnInfo.TurnNumber;
		mtgGameState.CurrentPhase = msg.TurnInfo.Phase;
		mtgGameState.CurrentStep = msg.TurnInfo.Step;
		mtgGameState.NextStep = msg.TurnInfo.NextStep;
		mtgGameState.NextPhase = msg.TurnInfo.NextPhase;
		mtgGameState.DecidingPlayer = mtgGameState.GetPlayerById(msg.TurnInfo.DecisionPlayer);
		mtgGameState.ActivePlayer = mtgGameState.GetPlayerById(msg.TurnInfo.ActivePlayer);
		mtgGameState.Stage = msg.GameInfo.Stage;
		foreach (MtgCardInstance visibleCard in mtgGameState.Battlefield.VisibleCards)
		{
			MtgPlayer controller = visibleCard.Controller;
			if (controller != null)
			{
				uint instanceId2 = controller.InstanceId;
				if (mtgGameState.BattlefieldPermanentsByControllerId.TryGetValue(instanceId2, out var value6))
				{
					value6.Add(visibleCard);
					continue;
				}
				mtgGameState.BattlefieldPermanentsByControllerId[instanceId2] = new List<MtgCardInstance> { visibleCard };
			}
		}
		foreach (MtgCardInstance visibleCard2 in mtgGameState.Exile.VisibleCards)
		{
			MtgPlayer controller2 = visibleCard2.Controller;
			if (controller2 != null)
			{
				uint instanceId3 = controller2.InstanceId;
				if (mtgGameState.ExiledCardsByControllerId.TryGetValue(instanceId3, out var value7))
				{
					value7.Add(visibleCard2);
					continue;
				}
				mtgGameState.ExiledCardsByControllerId[instanceId3] = new List<MtgCardInstance> { visibleCard2 };
			}
		}
		return mtgGameState;
		static uint getMyTeamId(IEnumerable<PlayerInfo> players, uint mySeatId)
		{
			foreach (PlayerInfo player2 in players)
			{
				if (player2.SystemSeatNumber == mySeatId)
				{
					return player2.TeamId;
				}
			}
			return 0u;
		}
	}

	private void SetupCrossCardReferences(MtgGameState state)
	{
		foreach (ICardReferenceHandler cardReferenceHandler in _cardReferenceHandlers)
		{
			cardReferenceHandler.SetupReferences(state);
		}
	}

	public void CarryOverStateDataUndo(MtgGameState newState, MtgGameState oldState)
	{
		if (oldState == null)
		{
			return;
		}
		if (!oldState.ResolutionStartedThisGameState)
		{
			newState.ResolvingCardInstance = oldState.ResolvingCardInstance;
		}
		foreach (uint seenCardGrpId in oldState.SeenCardGrpIds)
		{
			newState.SeenCardGrpIds.Add(seenCardGrpId);
		}
	}

	public void CarryOverStateData(MtgGameState newState, MtgGameState oldState)
	{
		if (oldState == null)
		{
			return;
		}
		newState.ResolvingCardInstance = oldState.ResolvingCardInstance;
		foreach (uint seenCardGrpId in oldState.SeenCardGrpIds)
		{
			newState.SeenCardGrpIds.Add(seenCardGrpId);
		}
	}

	private List<GameRulesEvent> GetZoneUpdates(GameStateMessage msg)
	{
		List<GameRulesEvent> list = new List<GameRulesEvent>();
		foreach (ZoneInfo zone in msg.Zones)
		{
			list.Add(new ZoneUpdatedEvent
			{
				Zone = _currentGameState.GetZoneById(zone.ZoneId)
			});
		}
		return list;
	}

	private void ParseAnnotationsPreGameState(IEnumerable<AnnotationInfo> annotationInfos)
	{
		foreach (AnnotationInfo annotationInfo in annotationInfos)
		{
			foreach (AnnotationType item in annotationInfo.Type)
			{
				if (item == AnnotationType.DynamicAbility)
				{
					_dynamicAbilityStateAnnotationParser.Process(annotationInfo, null, _currentGameState);
				}
			}
		}
	}

	private void ParseAnnotations(IEnumerable<AnnotationInfo> annotations, MtgGameState oldState, MtgGameState newState)
	{
		foreach (AnnotationInfo annotation in annotations)
		{
			_annotationStateParsers.Process(annotation, oldState, newState);
		}
	}

	private static bool AreSameEntity(MtgEntity a, MtgEntity b)
	{
		if (a == null)
		{
			return b == null;
		}
		if (b == null)
		{
			return false;
		}
		return a.InstanceId == b.InstanceId;
	}

	public void GetDiffChanges(MtgGameState oldState, MtgGameState newState, Dictionary<uint, uint> newIdToOldIdMap, AnnotationData annotationData, List<GameRulesEvent> changes)
	{
		if ((newState.GameInfo.MatchWinCondition == MatchWinCondition.Best2Of3 || newState.GameInfo.MatchWinCondition == MatchWinCondition.Best3Of5) && newState.GameInfo.Stage == GameStage.Start && newState.OpponentPendingMessageType == ClientMessageType.ChooseStartingPlayerResp)
		{
			changes.Add(new WaitingOnStartingPlayerEvent());
		}
		if (!AreSameEntity(oldState.DecidingPlayer, newState.DecidingPlayer))
		{
			changes.Add(new DecidingPlayerChangeEvent
			{
				DecidingPlayer = newState.DecidingPlayer
			});
		}
		changes.AddRange(ExtraTurnsEventGenerator.Generate(oldState, newState));
		foreach (KeyValuePair<uint, MtgZone> zone in oldState.Zones)
		{
			uint key = zone.Key;
			if (!newState.Zones.ContainsKey(zone.Key))
			{
				changes.Add(new DeleteZoneEvent(key));
			}
		}
		foreach (uint key3 in newState.Zones.Keys)
		{
			List<GameRulesEvent> zoneChanges = GetZoneChanges(key3, oldState, newState, newIdToOldIdMap, annotationData);
			if (zoneChanges.Count > 0)
			{
				changes.AddRange(zoneChanges);
			}
		}
		if (!oldState.Actions.ContainSame(newState.Actions))
		{
			changes.Add(new HypotheticalActionsChangedEvent
			{
				OldActions = oldState.Actions,
				NewActions = newState.Actions
			});
		}
		_oldReplacementEffects.Clear();
		_newReplacementEffects.Clear();
		foreach (List<ReplacementEffectData> value4 in oldState.ReplacementEffects.Values)
		{
			_oldReplacementEffects.AddRange(value4);
		}
		foreach (List<ReplacementEffectData> value5 in newState.ReplacementEffects.Values)
		{
			_newReplacementEffects.AddRange(value5);
		}
		if (oldState.ReplacementEffects.Count > 0 || newState.ReplacementEffects.Count > 0)
		{
			IEnumerable<ReplacementEffectData> enumerable = _oldReplacementEffects.Except(_newReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
			IEnumerable<ReplacementEffectData> enumerable2 = _newReplacementEffects.Except(_oldReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
			IEnumerable<ReplacementEffectData> enumerable3 = _newReplacementEffects.Intersect(_oldReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
			foreach (ReplacementEffectData item in enumerable)
			{
				changes.Add(new ReplacementEffectChangedEvent
				{
					EffectData = item,
					Entity = null,
					Added = false,
					Removed = true
				});
			}
			foreach (ReplacementEffectData item2 in enumerable2)
			{
				changes.Add(new ReplacementEffectChangedEvent
				{
					EffectData = item2,
					Entity = null,
					Added = true,
					Removed = false
				});
			}
			foreach (ReplacementEffectData item3 in enumerable3)
			{
				changes.Add(new ReplacementEffectChangedEvent
				{
					EffectData = item3,
					Entity = null,
					Added = false,
					Removed = false
				});
			}
		}
		changes.AddRange(SuspendLikeEventGenerator.Generate(oldState, newState));
		_attachedWithChangeList.Clear();
		_attachedToChanges.Clear();
		foreach (MtgCardInstance value6 in newState.VisibleCards.Values)
		{
			GetCardDiffChanges(oldState, newState, value6, newIdToOldIdMap, changes);
		}
		foreach (KeyValuePair<uint, int> attachedToChange in _attachedToChanges)
		{
			uint key2 = attachedToChange.Key;
			int value = attachedToChange.Value;
			if (newState.VisibleCards.TryGetValue(key2, out var value2))
			{
				changes.Insert(value, new CardChangedEvent(value2, value2, 0u, PropertyType.AttachedTo));
			}
		}
		changes.AddRange(_revealedCardGenerator.Generate(oldState, newState));
		changes.AddRange(_relatedZoneGenerator.GenerateEvents(changes));
		changes.AddRange(_playerDiffGenerator.Generate(oldState, newState));
		changes.AddRange(DelayedTriggerEventGenerator.GenerateEvents(oldState, newState));
		changes.AddRange(DungeonDeletionEventGenerator.GenerateEvents(oldState, newState));
		changes.AddRange(BoonDeletionEventGenerator.GenerateEvents(oldState, newState));
		changes.AddRange(DesignationEventGenerator.Generate(oldState, newState));
		foreach (ChoosingAttachmentsInfo choosingAttachmentsInfo in newState.ChoosingAttachmentsInfos)
		{
			if (!oldState.ChoosingAttachmentsInfos.Contains(choosingAttachmentsInfo) && choosingAttachmentsInfo.AffectorId != 0)
			{
				changes.Add(new ChoosingAttachmentsEvent(choosingAttachmentsInfo.AffectorId, choosingAttachmentsInfo.AffectedIds));
			}
		}
		foreach (GameRulesEvent change in changes)
		{
			if (change is CardChangedEvent { Property: PropertyType.AttachedWith } cardChangedEvent)
			{
				_attachedWithChangeList.Remove(cardChangedEvent.NewInstance.InstanceId);
			}
		}
		foreach (uint attachedWithChange in _attachedWithChangeList)
		{
			if (newState.VisibleCards.TryGetValue(attachedWithChange, out var value3))
			{
				MtgCardInstance oldCardInstance = GetOldCardInstance(oldState, newIdToOldIdMap, value3, changes);
				changes.Add(new CardChangedEvent(oldCardInstance, value3, 0u, PropertyType.AttachedWith));
			}
		}
		_attachedWithChangeList.Clear();
		_attachedToChanges.Clear();
		_oldReferences.Clear();
		_newReferences.Clear();
	}

	private MtgCardInstance GetOldCardInstance(MtgGameState oldState, Dictionary<uint, uint> newIdToOldIdMap, MtgCardInstance newInstance, List<GameRulesEvent> changes)
	{
		uint num = newInstance.InstanceId;
		while (newIdToOldIdMap.ContainsKey(num))
		{
			num = newIdToOldIdMap[num];
		}
		return (GetPreviousIncrementalEntity(num, changes) as MtgCardInstance) ?? oldState.GetCardById(num);
	}

	private void GetCardDiffChanges(MtgGameState oldState, MtgGameState newState, MtgCardInstance newInstance, Dictionary<uint, uint> newToOldIdMap, List<GameRulesEvent> changes)
	{
		MtgCardInstance oldCardInstance = GetOldCardInstance(oldState, newToOldIdMap, newInstance, changes);
		if (oldCardInstance == null)
		{
			return;
		}
		int count = changes.Count;
		if (oldCardInstance.EnteredZoneThisTurn != newInstance.EnteredZoneThisTurn)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AddedToZone));
		}
		if (oldCardInstance.SkinCode != newInstance.SkinCode)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Style));
		}
		if (newInstance.Zone.Type != ZoneType.Battlefield && oldCardInstance.Counters.Count != newInstance.Counters.Count)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Counters));
		}
		if (oldCardInstance.Decorators.Count != 0 || newInstance.Decorators.Count != 0)
		{
			IEnumerable<uint> enumerable = oldCardInstance.Decorators.Keys.Except(newInstance.Decorators.Keys);
			IEnumerable<uint> enumerable2 = newInstance.Decorators.Keys.Except(oldCardInstance.Decorators.Keys);
			IEnumerable<uint> enumerable3 = oldCardInstance.Decorators.Keys.Intersect(newInstance.Decorators.Keys);
			foreach (uint item5 in enumerable)
			{
				MtgCardInstance affectorCard = newState.GetCardById(item5) ?? oldState.GetCardById(item5);
				CardDecoratorUpdatedEvent cardDecoratorUpdatedEvent = new CardDecoratorUpdatedEvent
				{
					Added = false,
					AffectedCard = newInstance,
					AffectorCard = affectorCard,
					Decorators = oldCardInstance.Decorators[item5]
				};
				CardChangedEvent item = new CardChangedEvent(oldCardInstance, newInstance, 0u, cardDecoratorUpdatedEvent.AssociatedPropertyType);
				int num = IndexOfPreviousMatchingCardChange(changes, item5, newInstance.InstanceId, cardDecoratorUpdatedEvent.Decorators, out var associatedPropertyType);
				cardDecoratorUpdatedEvent.AssociatedPropertyType = associatedPropertyType;
				if (num >= 0)
				{
					changes.Insert(num + 1, item);
					changes.Insert(num + 1, cardDecoratorUpdatedEvent);
				}
				else
				{
					changes.Add(cardDecoratorUpdatedEvent);
					changes.Add(item);
				}
			}
			foreach (uint item6 in enumerable2)
			{
				MtgCardInstance affectorCard2 = newState.GetCardById(item6) ?? oldState.GetCardById(item6);
				CardDecoratorUpdatedEvent cardDecoratorUpdatedEvent2 = new CardDecoratorUpdatedEvent
				{
					Added = true,
					AffectedCard = newInstance,
					AffectorCard = affectorCard2,
					Decorators = newInstance.Decorators[item6]
				};
				CardChangedEvent item2 = new CardChangedEvent(oldCardInstance, newInstance, 0u, cardDecoratorUpdatedEvent2.AssociatedPropertyType);
				int num2 = IndexOfPreviousMatchingCardChange(changes, item6, newInstance.InstanceId, cardDecoratorUpdatedEvent2.Decorators, out var associatedPropertyType2);
				cardDecoratorUpdatedEvent2.AssociatedPropertyType = associatedPropertyType2;
				if (num2 >= 0)
				{
					changes.Insert(num2 + 1, item2);
					changes.Insert(num2 + 1, cardDecoratorUpdatedEvent2);
				}
				else
				{
					changes.Add(cardDecoratorUpdatedEvent2);
					changes.Add(item2);
				}
			}
			foreach (uint item7 in enumerable3)
			{
				HashSet<DecoratorType> hashSet = oldCardInstance.Decorators[item7];
				HashSet<DecoratorType> hashSet2 = newInstance.Decorators[item7];
				HashSet<DecoratorType> hashSet3 = hashSet.Except(hashSet2).ToHashSet();
				HashSet<DecoratorType> hashSet4 = hashSet2.Except(hashSet).ToHashSet();
				if (hashSet3.Count > 0)
				{
					MtgCardInstance affectorCard3 = newState.GetCardById(item7) ?? oldState.GetCardById(item7);
					CardDecoratorUpdatedEvent cardDecoratorUpdatedEvent3 = new CardDecoratorUpdatedEvent
					{
						Added = false,
						AffectedCard = newInstance,
						AffectorCard = affectorCard3,
						Decorators = hashSet3
					};
					CardChangedEvent item3 = new CardChangedEvent(oldCardInstance, newInstance, 0u, cardDecoratorUpdatedEvent3.AssociatedPropertyType);
					int num3 = IndexOfPreviousMatchingCardChange(changes, item7, newInstance.InstanceId, hashSet3, out var associatedPropertyType3);
					cardDecoratorUpdatedEvent3.AssociatedPropertyType = associatedPropertyType3;
					if (num3 >= 0)
					{
						changes.Insert(num3 + 1, item3);
						changes.Insert(num3 + 1, cardDecoratorUpdatedEvent3);
					}
					else
					{
						changes.Add(cardDecoratorUpdatedEvent3);
						changes.Add(item3);
					}
				}
				if (hashSet4.Count > 0)
				{
					MtgCardInstance affectorCard4 = newState.GetCardById(item7) ?? oldState.GetCardById(item7);
					CardDecoratorUpdatedEvent cardDecoratorUpdatedEvent4 = new CardDecoratorUpdatedEvent
					{
						Added = true,
						AffectedCard = newInstance,
						AffectorCard = affectorCard4,
						Decorators = hashSet4
					};
					CardChangedEvent item4 = new CardChangedEvent(oldCardInstance, newInstance, 0u, cardDecoratorUpdatedEvent4.AssociatedPropertyType);
					int num4 = IndexOfPreviousMatchingCardChange(changes, item7, newInstance.InstanceId, hashSet4, out var associatedPropertyType4);
					cardDecoratorUpdatedEvent4.AssociatedPropertyType = associatedPropertyType4;
					if (num4 >= 0)
					{
						changes.Insert(num4 + 1, item4);
						changes.Insert(num4 + 1, cardDecoratorUpdatedEvent4);
					}
					else
					{
						changes.Add(cardDecoratorUpdatedEvent4);
						changes.Add(item4);
					}
				}
			}
		}
		oldState.ReferenceMap.GetReferences(oldCardInstance.InstanceId, ReferenceMap.ReferenceType.Referencing, 0u, ref _oldReferences);
		newState.ReferenceMap.GetReferences(newInstance.InstanceId, ReferenceMap.ReferenceType.Referencing, 0u, ref _newReferences);
		if (_oldReferences.Count != _newReferences.Count || _oldReferences.Except(_newReferences).Any() || _newReferences.Except(_oldReferences).Any())
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.References));
		}
		foreach (uint item8 in oldCardInstance.DistinguishedByIds.Except(newInstance.DistinguishedByIds))
		{
			_ = item8;
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.DistinguishedBy));
		}
		foreach (uint item9 in newInstance.DistinguishedByIds.Except(oldCardInstance.DistinguishedByIds))
		{
			_ = item9;
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.DistinguishedBy));
		}
		if (newInstance.LayeredEffects.Count > 0)
		{
			MtgCardInstance mtgCardInstance = oldState.GetCardById(oldCardInstance.InstanceId) ?? oldCardInstance;
			LayeredEffectData[] array = mtgCardInstance.LayeredEffects.Except(newInstance.LayeredEffects, LayeredEffectData.PerpetualAwareComparer).ToArray();
			LayeredEffectData[] array2 = newInstance.LayeredEffects.Except(mtgCardInstance.LayeredEffects, LayeredEffectData.PerpetualAwareComparer).ToArray();
			if (array.Length != 0)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ActiveLayeredEffect));
			}
			if (array2.Length != 0)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ActiveLayeredEffect));
			}
			LayeredEffectData[] array3 = array2;
			foreach (LayeredEffectData layeredEffectData in array3)
			{
				if (layeredEffectData.IsPerpetualModification())
				{
					changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Perpetual));
				}
			}
		}
		if (oldCardInstance.ReplacementEffects.Count > 0 || newInstance.ReplacementEffects.Count > 0)
		{
			IEnumerable<ReplacementEffectData> enumerable4 = oldCardInstance.ReplacementEffects.Except(newInstance.ReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
			IEnumerable<ReplacementEffectData> enumerable5 = newInstance.ReplacementEffects.Except(oldCardInstance.ReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
			foreach (ReplacementEffectData item10 in enumerable4)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.CastingTimeOptions));
				changes.Add(new ReplacementEffectChangedEvent
				{
					Added = false,
					Removed = true,
					EffectData = item10,
					Entity = newInstance
				});
			}
			foreach (ReplacementEffectData item11 in enumerable5)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.CastingTimeOptions));
				changes.Add(new ReplacementEffectChangedEvent
				{
					Added = true,
					Removed = false,
					EffectData = item11,
					Entity = newInstance
				});
			}
		}
		if (oldCardInstance.Distributions.Count > 0 || newInstance.Distributions.Count > 0)
		{
			bool flag = false;
			if (oldCardInstance.Distributions.Count != newInstance.Distributions.Count)
			{
				flag = true;
			}
			else
			{
				for (int j = 0; j < oldCardInstance.Distributions.Count; j++)
				{
					KeyValuePair<MtgEntity, int> keyValuePair = oldCardInstance.Distributions[j];
					KeyValuePair<MtgEntity, int> keyValuePair2 = newInstance.Distributions[j];
					if (keyValuePair.Key.InstanceId != keyValuePair2.Key.InstanceId || keyValuePair.Value != keyValuePair2.Value)
					{
						flag = true;
						break;
					}
				}
			}
			if (flag)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Distributions));
			}
		}
		if (oldCardInstance.Children.Count > 0 || newInstance.Children.Count > 0)
		{
			bool flag2 = false;
			if (oldCardInstance.Children.Count != newInstance.Children.Count)
			{
				flag2 = true;
			}
			else
			{
				foreach (MtgCardInstance childInstance in newInstance.Children)
				{
					if (oldCardInstance.Children.Find((MtgCardInstance x) => x.InstanceId == childInstance.InstanceId) == null)
					{
						flag2 = true;
						break;
					}
				}
			}
			if (flag2)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Children));
			}
		}
		HashSet<uint> hashSet5 = oldState.GetCardById(oldCardInstance.InstanceId)?.SelectedBy;
		if (hashSet5 != null && (hashSet5.Count > 0 || newInstance.SelectedBy.Count > 0))
		{
			IEnumerable<uint> enumerable6 = hashSet5.Except(newInstance.SelectedBy);
			IEnumerable<uint> enumerable7 = newInstance.SelectedBy.Except(hashSet5);
			uint instanceId = oldCardInstance.InstanceId;
			uint instanceId2 = newInstance.InstanceId;
			foreach (uint item12 in enumerable6)
			{
				changes.Add(new ObjectDeselectedEvent
				{
					AffectorId = item12,
					AffectedId = instanceId
				});
			}
			foreach (uint item13 in enumerable7)
			{
				changes.Add(new ObjectSelectedEvent
				{
					AffectorId = item13,
					AffectedId = instanceId2
				});
			}
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Selected));
		}
		if (oldCardInstance.MutationChildrenIds.Count != newInstance.MutationChildrenIds.Count || !oldCardInstance.MutationChildrenIds.SequenceEqual(newInstance.MutationChildrenIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Mutation));
		}
		if (oldCardInstance.MutationParentId != newInstance.MutationParentId)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Mutation));
		}
		if (oldCardInstance.IsTemporary != newInstance.IsTemporary)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AffectedByQualifications));
		}
		if (!oldCardInstance.GroupedIds.SequenceEqual(newInstance.GroupedIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.GroupedIds));
		}
		if (oldCardInstance.GrpId != newInstance.GrpId)
		{
			if (oldCardInstance.GrpId == 3 || newInstance.GrpId == 3)
			{
				changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.FaceDown));
			}
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.GrpId));
		}
		if (oldCardInstance.HasSummoningSickness != newInstance.HasSummoningSickness)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.HasSummoningSickness));
		}
		if (!oldCardInstance.Power.Equals(newInstance.Power))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, (newInstance.Power.Value > oldCardInstance.Power.Value) ? PropertyType.PowerUp : PropertyType.PowerDown));
		}
		if (!oldCardInstance.Toughness.Equals(newInstance.Toughness))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, (newInstance.Toughness.Value > oldCardInstance.Toughness.Value) ? PropertyType.ToughnessUp : PropertyType.ToughnessDown));
		}
		if (!oldCardInstance.SuppressedPower.Equals(newInstance.SuppressedPower))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, (newInstance.SuppressedPower.Value > oldCardInstance.SuppressedPower.Value) ? PropertyType.PowerUp : PropertyType.PowerDown));
		}
		if (!oldCardInstance.SuppressedToughness.Equals(newInstance.SuppressedToughness))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, (newInstance.SuppressedToughness.Value > oldCardInstance.SuppressedToughness.Value) ? PropertyType.ToughnessUp : PropertyType.ToughnessDown));
		}
		if (oldCardInstance.Loyalty.HasValue && newInstance.Loyalty.HasValue && oldCardInstance.Loyalty.Value != newInstance.Loyalty.Value)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, (newInstance.Loyalty.Value > oldCardInstance.Loyalty.Value) ? PropertyType.LoyaltyUp : PropertyType.LoyaltyDown));
		}
		if (oldCardInstance.Defense.HasValue && newInstance.Defense.HasValue && oldCardInstance.Defense.Value != newInstance.Defense.Value)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, (newInstance.Defense.Value > oldCardInstance.Defense.Value) ? PropertyType.DefenseUp : PropertyType.DefenseDown));
		}
		if (oldCardInstance.RegenerateCount != newInstance.RegenerateCount)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.RegenerateCount));
		}
		if (oldCardInstance.AttackState != newInstance.AttackState)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AttackState));
		}
		if (oldCardInstance.BlockState != newInstance.BlockState)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.BlockState));
		}
		if (!oldCardInstance.BlockedByIds.SequenceEqual(newInstance.BlockedByIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.BlockedBy));
		}
		if (!oldCardInstance.BlockingIds.SequenceEqual(newInstance.BlockingIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Blocking));
		}
		if (!oldCardInstance.Abilities.ConvertAll((AbilityPrintingData x) => x.Id).SequenceEqual(newInstance.Abilities.ConvertAll((AbilityPrintingData x) => x.Id)))
		{
			List<uint> first = newInstance.Abilities.ConvertAll((AbilityPrintingData x) => x.Id);
			List<uint> second = oldCardInstance.Abilities.ConvertAll((AbilityPrintingData x) => x.Id);
			foreach (uint addedId in first.Except(second))
			{
				AddedAbilityData addedAbilityData = newInstance.AbilityAdders.Find((AddedAbilityData x) => x.AbilityId == addedId);
				if (addedAbilityData.AbilityId != 0)
				{
					changes.Add(new AbilityAddedEvent(oldCardInstance, newInstance, addedAbilityData));
				}
			}
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Abilities));
		}
		if (oldCardInstance.Damage != newInstance.Damage)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Damage));
		}
		if (oldCardInstance.Controller?.InstanceId != newInstance.Controller?.InstanceId)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Controller));
		}
		if (oldCardInstance.AttachedToId != newInstance.AttachedToId)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AttachedTo));
		}
		if (!oldCardInstance.AttachedWithIds.SetEquals(newInstance.AttachedWithIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AttachedWith));
			MtgCardInstance value = newInstance;
			while (value != null)
			{
				_attachedWithChangeList.Add(value.InstanceId);
				if (!newState.VisibleCards.TryGetValue(value.AttachedToId, out value))
				{
					value = null;
				}
			}
		}
		if (!oldCardInstance.AdditionalCostIds.SetEquals(newInstance.AdditionalCostIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AdditionalCost));
		}
		if (!oldCardInstance.Targets.Select((MtgEntity x) => x.InstanceId).SequenceEqual(newInstance.Targets.Select((MtgEntity x) => x.InstanceId)))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Target));
		}
		if (!oldCardInstance.TargetedBy.Select((MtgEntity x) => x.InstanceId).SequenceEqual(newInstance.TargetedBy.Select((MtgEntity x) => x.InstanceId)))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Target));
		}
		if (!oldCardInstance.ActiveAbilityWords.SequenceEqual(newInstance.ActiveAbilityWords))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ActiveAbilityWord));
		}
		if (!oldCardInstance.Supertypes.SequenceEqual(newInstance.Supertypes))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.SuperType));
		}
		if (!oldCardInstance.Subtypes.SequenceEqual(newInstance.Subtypes))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.SubType));
		}
		if (!oldCardInstance.CardTypes.SequenceEqual(newInstance.CardTypes))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.CardType));
		}
		if (!oldCardInstance.AffectorOfQualifications.SequenceEqual(newInstance.AffectorOfQualifications))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AffectorOfQualifications));
		}
		if (!oldCardInstance.AffectedByQualifications.SequenceEqual(newInstance.AffectedByQualifications))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AffectedByQualifications));
		}
		changes.AddRange(_linkInfoGenerator.Generate(oldCardInstance, newInstance));
		if (!oldCardInstance.AffectorOfLinkInfos.SequenceEqual(newInstance.AffectorOfLinkInfos))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AffectorOfLinkInfos));
		}
		if (!oldCardInstance.AffectedByLinkInfos.SequenceEqual(newInstance.AffectedByLinkInfos))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AffectedByLinkInfos));
		}
		if (!oldCardInstance.GamewideCounts.SequenceEqual(newInstance.GamewideCounts))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.GamewideCount));
		}
		if (!oldCardInstance.PlayWarnings.SequenceEqual(newInstance.PlayWarnings))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.AffectedByQualifications));
		}
		if (!oldCardInstance.ManaCostOverride.SequenceEqual(newInstance.ManaCostOverride))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ManaOverride));
		}
		if (!oldCardInstance.Actions.SequenceEqual(newInstance.Actions, _actionInfoComparer))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Actions));
		}
		if (!oldCardInstance.ColorProduction.SequenceEqual(newInstance.ColorProduction))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ColorProduction));
		}
		if (oldCardInstance.RevealedToOpponent != newInstance.RevealedToOpponent)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Revealed));
		}
		if (oldCardInstance.Visibility != newInstance.Visibility)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Visibility));
		}
		if (!oldCardInstance.Viewers.SequenceEqual(newInstance.Viewers))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Visibility));
		}
		if (oldCardInstance.ChooseXResult.HasValue != newInstance.ChooseXResult.HasValue || (oldCardInstance.ChooseXResult.HasValue && newInstance.ChooseXResult.HasValue && oldCardInstance.ChooseXResult.Value != newInstance.ChooseXResult.Value))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ChooseXResult));
		}
		if (oldCardInstance.LoyaltyActivationsRemaining != newInstance.LoyaltyActivationsRemaining)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.LoyaltyActivationsRemaining));
		}
		if (!oldCardInstance.CastingTimeOptions.SequenceEqual(newInstance.CastingTimeOptions))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.CastingTimeOptions));
		}
		if (oldCardInstance.TitleId != newInstance.TitleId)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.TitleId));
		}
		if (!oldCardInstance.AbilityInstances.ContainsSameExhaustedAbilities(newInstance.AbilityInstances))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ExhaustedAbilityGrpIds));
		}
		if (!oldCardInstance.ReferencedCardTitleIds.SequenceEqual(newInstance.ReferencedCardTitleIds))
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.ReferencedCardNames));
		}
		if (newInstance.PendingSpecializationColor != oldCardInstance.PendingSpecializationColor)
		{
			changes.Add(new CardChangedEvent(oldCardInstance, newInstance, 0u, PropertyType.Specialize));
		}
		if (count != changes.Count && newInstance.AttachedWithIds.Count != 0)
		{
			foreach (uint attachedWithId in newInstance.AttachedWithIds)
			{
				if (newState.VisibleCards.TryGetValue(attachedWithId, out var _))
				{
					_attachedToChanges.Add(attachedWithId, changes.Count);
				}
			}
		}
		else if (_attachedToChanges.ContainsKey(newInstance.InstanceId))
		{
			changes.Add(new CardChangedEvent(newInstance, newInstance, 0u, PropertyType.AttachedTo));
			_attachedToChanges.Remove(newInstance.InstanceId);
		}
		if (oldCardInstance.AttackTargetId != newInstance.AttackTargetId)
		{
			changes.Add(new AttackSelectionEvent
			{
				AttackerId = newInstance.InstanceId,
				QuarryId = newInstance.AttackTargetId,
				isSelected = (newInstance.AttackTargetId != 0)
			});
		}
		static bool DecoratorTypesMatchPropertyType(HashSet<DecoratorType> decorators, PropertyType propertyType)
		{
			switch (propertyType)
			{
			case PropertyType.PowerUp:
			case PropertyType.PowerDown:
				return decorators.Contains(DecoratorType.Power);
			case PropertyType.ToughnessUp:
			case PropertyType.ToughnessDown:
				return decorators.Contains(DecoratorType.Toughness);
			default:
				return false;
			}
		}
		static int IndexOfPreviousMatchingCardChange(IReadOnlyList<GameRulesEvent> gameRulesEvents, uint affectorId, uint affectedId, HashSet<DecoratorType> decoratorTypes, out PropertyType reference)
		{
			reference = PropertyType.None;
			if (affectedId == 0)
			{
				return -1;
			}
			if (decoratorTypes == null || decoratorTypes.Count == 0)
			{
				return -1;
			}
			for (int num5 = gameRulesEvents.Count - 1; num5 >= 0; num5--)
			{
				if (gameRulesEvents[num5] is CardChangedEvent cardChangedEvent && cardChangedEvent.AffectorId == affectorId)
				{
					MtgCardInstance newInstance2 = cardChangedEvent.NewInstance;
					if (newInstance2 != null && newInstance2.InstanceId == affectedId && DecoratorTypesMatchPropertyType(decoratorTypes, cardChangedEvent.Property))
					{
						reference = cardChangedEvent.Property;
						return num5;
					}
				}
			}
			return -1;
		}
	}

	public List<GameRulesEvent> GetUndoDiffChanges(MtgGameState oldState, MtgGameState newState, Dictionary<uint, uint> dstToSrcIdMap)
	{
		Dictionary<uint, uint> dictionary = new Dictionary<uint, uint>();
		foreach (uint key2 in dstToSrcIdMap.Keys)
		{
			uint num = key2;
			uint key = dstToSrcIdMap[key2];
			while (dstToSrcIdMap.ContainsKey(key))
			{
				key = dstToSrcIdMap[key];
			}
			if (dictionary.TryGetValue(key, out var value))
			{
				dictionary[key] = ((num > value) ? num : value);
			}
			else
			{
				dictionary.Add(key, num);
			}
		}
		List<GameRulesEvent> list = new List<GameRulesEvent>();
		if (!AreSameEntity(oldState.DecidingPlayer, newState.DecidingPlayer))
		{
			list.Add(new DecidingPlayerChangeEvent
			{
				DecidingPlayer = newState.DecidingPlayer
			});
		}
		list.AddRange(ExtraTurnsEventGenerator.Generate(oldState, newState));
		list.AddRange(DelayedTriggerEventGenerator.GenerateEvents(oldState, newState));
		list.AddRange(DungeonDeletionEventGenerator.GenerateEvents(oldState, newState));
		list.AddRange(BoonDeletionEventGenerator.GenerateEvents(oldState, newState));
		list.AddRange(DesignationEventGenerator.Generate(oldState, newState));
		if (!oldState.Actions.ContainSame(newState.Actions))
		{
			list.Add(new HypotheticalActionsChangedEvent
			{
				OldActions = oldState.Actions,
				NewActions = newState.Actions
			});
		}
		foreach (uint key3 in oldState.RevealedCards.Keys)
		{
			if (!newState.RevealedCards.ContainsKey(key3))
			{
				list.Add(new CardRevealedEvent
				{
					EventType = RevealEventType.Delete,
					DeleteId = key3
				});
			}
		}
		foreach (uint key4 in newState.Zones.Keys)
		{
			List<GameRulesEvent> zoneChanges = GetZoneChanges(key4, oldState, newState, dictionary, new AnnotationData());
			if (zoneChanges.Count > 0)
			{
				list.AddRange(zoneChanges);
			}
		}
		foreach (uint key5 in dictionary.Keys)
		{
			uint num2 = dictionary[key5];
			if (newState.GetCardById(key5) != null)
			{
				continue;
			}
			MtgZone mtgZone = null;
			foreach (uint key6 in newState.Zones.Keys)
			{
				MtgZone mtgZone2 = newState.Zones[key6];
				if (mtgZone2.CardIds.Contains(key5))
				{
					mtgZone = mtgZone2;
					break;
				}
			}
			if (mtgZone == null)
			{
				continue;
			}
			MtgZone mtgZone3 = null;
			foreach (uint key7 in oldState.Zones.Keys)
			{
				MtgZone mtgZone4 = oldState.Zones[key7];
				if (mtgZone4.CardIds.Contains(num2))
				{
					mtgZone3 = mtgZone4;
					break;
				}
			}
			if (mtgZone3 != null && mtgZone3.Id != mtgZone.Id)
			{
				list.Add(new InstanceIdChange(num2, key5));
				list.Add(new ZoneChangeEvent(key5, mtgZone3, mtgZone, 0u, ZoneTransferReason.Undo));
			}
		}
		foreach (MtgCardInstance value2 in newState.VisibleCards.Values)
		{
			if (value2.Zone.Type == ZoneType.Limbo && value2.CatalogId != WellKnownCatalogId.WellKnownCatalogId_TransientEffect)
			{
				continue;
			}
			uint num3 = 0u;
			if (dictionary.ContainsKey(value2.InstanceId))
			{
				num3 = dictionary[value2.InstanceId];
			}
			MtgCardInstance mtgCardInstance = oldState.GetCardById(value2.InstanceId);
			MtgCardInstance cardById = oldState.GetCardById(num3);
			if (cardById != null)
			{
				mtgCardInstance = cardById;
				list.Add(new InstanceIdChange(num3, value2.InstanceId));
			}
			if (mtgCardInstance == null)
			{
				continue;
			}
			if (value2.Zone.Id != mtgCardInstance.Zone.Id)
			{
				if (mtgCardInstance.Zone.Type == ZoneType.Limbo)
				{
					list.Add(new CardCreatedEvent(value2));
				}
				else
				{
					list.Add(new ZoneChangeEvent(value2.InstanceId, mtgCardInstance.Zone, value2.Zone, 0u, ZoneTransferReason.Undo));
				}
			}
			List<PropertyType> list2 = new List<PropertyType>();
			if (mtgCardInstance.IsPhasedOut != value2.IsPhasedOut)
			{
				list2.Add(PropertyType.PhaseState);
			}
			if (mtgCardInstance.HasSummoningSickness != value2.HasSummoningSickness)
			{
				list2.Add(PropertyType.HasSummoningSickness);
			}
			if (mtgCardInstance.AttackState != value2.AttackState)
			{
				list2.Add(PropertyType.AttackState);
			}
			if (mtgCardInstance.BlockState != value2.BlockState)
			{
				list2.Add(PropertyType.BlockState);
			}
			if (!mtgCardInstance.BlockedByIds.SequenceEqual(value2.BlockedByIds))
			{
				list2.Add(PropertyType.BlockedBy);
			}
			if (!mtgCardInstance.BlockingIds.SequenceEqual(value2.BlockingIds))
			{
				list2.Add(PropertyType.Blocking);
			}
			if (!mtgCardInstance.Abilities.ConvertAll((AbilityPrintingData x) => x.Id).SequenceEqual(value2.Abilities.ConvertAll((AbilityPrintingData x) => x.Id)))
			{
				list2.Add(PropertyType.Abilities);
			}
			if (mtgCardInstance.Damage != value2.Damage)
			{
				list2.Add(PropertyType.Damage);
			}
			if (mtgCardInstance.Controller.InstanceId != value2.Controller.InstanceId)
			{
				list2.Add(PropertyType.Controller);
			}
			if (mtgCardInstance.AttachedToId != value2.AttachedToId)
			{
				list2.Add(PropertyType.AttachedTo);
			}
			if (!mtgCardInstance.AttachedWithIds.SetEquals(value2.AttachedWithIds))
			{
				list2.Add(PropertyType.AttachedWith);
			}
			if (!mtgCardInstance.Targets.Select((MtgEntity x) => x.InstanceId).SequenceEqual(value2.Targets.Select((MtgEntity x) => x.InstanceId)))
			{
				list2.Add(PropertyType.Target);
			}
			if (!mtgCardInstance.TargetedBy.Select((MtgEntity x) => x.InstanceId).SequenceEqual(value2.TargetedBy.Select((MtgEntity x) => x.InstanceId)))
			{
				list2.Add(PropertyType.Target);
			}
			if (!mtgCardInstance.ActiveAbilityWords.SequenceEqual(value2.ActiveAbilityWords))
			{
				list2.Add(PropertyType.ActiveAbilityWord);
			}
			if (!mtgCardInstance.AffectorOfQualifications.SequenceEqual(value2.AffectorOfQualifications))
			{
				list2.Add(PropertyType.AffectorOfQualifications);
			}
			if (!mtgCardInstance.AffectedByQualifications.SequenceEqual(value2.AffectedByQualifications))
			{
				list2.Add(PropertyType.AffectedByQualifications);
			}
			if (!mtgCardInstance.AffectorOfLinkInfos.SequenceEqual(value2.AffectorOfLinkInfos))
			{
				list2.Add(PropertyType.AffectorOfLinkInfos);
			}
			if (!mtgCardInstance.AffectedByLinkInfos.SequenceEqual(value2.AffectedByLinkInfos))
			{
				list2.Add(PropertyType.AffectedByLinkInfos);
			}
			if (!mtgCardInstance.CastingTimeOptions.SequenceEqual(value2.CastingTimeOptions))
			{
				list2.Add(PropertyType.CastingTimeOptions);
			}
			if (!mtgCardInstance.Actions.SequenceEqual(value2.Actions, _actionInfoComparer))
			{
				list2.Add(PropertyType.Actions);
			}
			if (mtgCardInstance.RevealedToOpponent != value2.RevealedToOpponent)
			{
				list2.Add(PropertyType.Revealed);
			}
			if (mtgCardInstance.Visibility != value2.Visibility)
			{
				list2.Add(PropertyType.Visibility);
			}
			if (!mtgCardInstance.Viewers.SequenceEqual(value2.Viewers))
			{
				list2.Add(PropertyType.Visibility);
			}
			if (!mtgCardInstance.AbilityInstances.ContainsSameExhaustedAbilities(value2.AbilityInstances))
			{
				list2.Add(PropertyType.ExhaustedAbilityGrpIds);
			}
			if (!mtgCardInstance.GroupedIds.SequenceEqual(value2.GroupedIds))
			{
				list2.Add(PropertyType.GroupedIds);
			}
			if (mtgCardInstance.ChooseXResult.HasValue != value2.ChooseXResult.HasValue || (mtgCardInstance.ChooseXResult.HasValue && value2.ChooseXResult.HasValue && mtgCardInstance.ChooseXResult.Value != value2.ChooseXResult.Value))
			{
				list2.Add(PropertyType.ChooseXResult);
			}
			if (mtgCardInstance.ReplacementEffects.Count > 0 || value2.ReplacementEffects.Count > 0)
			{
				IEnumerable<ReplacementEffectData> enumerable = mtgCardInstance.ReplacementEffects.Except(value2.ReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
				IEnumerable<ReplacementEffectData> enumerable2 = value2.ReplacementEffects.Except(mtgCardInstance.ReplacementEffects, ReplacementEffectData.IgnoreSourcesAndRecipientsEqComparer);
				foreach (ReplacementEffectData item in enumerable)
				{
					list2.Add(PropertyType.CastingTimeOptions);
					list.Add(new ReplacementEffectChangedEvent
					{
						Added = false,
						Removed = true,
						EffectData = item,
						Entity = value2
					});
				}
				foreach (ReplacementEffectData item2 in enumerable2)
				{
					list2.Add(PropertyType.CastingTimeOptions);
					list.Add(new ReplacementEffectChangedEvent
					{
						Added = true,
						Removed = false,
						EffectData = item2,
						Entity = value2
					});
				}
			}
			if (!mtgCardInstance.SelectedBy.SequenceEqual(value2.SelectedBy))
			{
				IEnumerable<uint> enumerable3 = mtgCardInstance.SelectedBy.Except(value2.SelectedBy);
				IEnumerable<uint> enumerable4 = value2.SelectedBy.Except(mtgCardInstance.SelectedBy);
				uint instanceId = mtgCardInstance.InstanceId;
				uint instanceId2 = value2.InstanceId;
				foreach (uint item3 in enumerable3)
				{
					list.Add(new ObjectDeselectedEvent
					{
						AffectorId = item3,
						AffectedId = instanceId
					});
				}
				foreach (uint item4 in enumerable4)
				{
					list.Add(new ObjectSelectedEvent
					{
						AffectorId = item4,
						AffectedId = instanceId2
					});
				}
				list2.Add(PropertyType.Selected);
			}
			if (value2.PendingSpecializationColor != mtgCardInstance.PendingSpecializationColor)
			{
				list2.Add(PropertyType.Specialize);
			}
			oldState.ReferenceMap.GetReferences(mtgCardInstance.InstanceId, ReferenceMap.ReferenceType.Referencing, 0u, ref _oldReferences);
			newState.ReferenceMap.GetReferences(value2.InstanceId, ReferenceMap.ReferenceType.Referencing, 0u, ref _newReferences);
			if (_oldReferences.Count != _newReferences.Count || _oldReferences.Except(_newReferences).Any() || _newReferences.Except(_oldReferences).Any())
			{
				list2.Add(PropertyType.References);
			}
			foreach (PropertyType item5 in list2)
			{
				if (item5 == PropertyType.Abilities)
				{
					List<uint> list3 = value2.Abilities.ConvertAll((AbilityPrintingData x) => x.Id);
					List<uint> list4 = mtgCardInstance.Abilities.ConvertAll((AbilityPrintingData x) => x.Id);
					IEnumerable<uint> enumerable5 = list3.Except(list4);
					list4.Except(list3);
					foreach (uint addedId in enumerable5)
					{
						AddedAbilityData addedAbilityData = value2.AbilityAdders.Find((AddedAbilityData x) => x.AbilityId == addedId);
						list.Add(new AbilityAddedEvent(mtgCardInstance, value2, addedAbilityData));
					}
				}
				list.Add(new CardChangedEvent(mtgCardInstance, value2, 0u, item5));
			}
			list.Add(new CardChangedEvent(mtgCardInstance, value2, 0u, PropertyType.None));
		}
		list.AddRange(_undoRevealedCardGenerator.Generate(oldState, newState));
		list.AddRange(_relatedZoneGenerator.GenerateEvents(list));
		list.AddRange(_undoPlayerDiffGenerator.Generate(oldState, newState));
		_attachedWithChangeList.Clear();
		_oldReferences.Clear();
		_newReferences.Clear();
		return list;
	}

	public IEnumerable<GameRulesEvent> FirstGameStateEvents(MtgGameState newState)
	{
		if ((newState.GameInfo.MatchWinCondition == MatchWinCondition.Best2Of3 || newState.GameInfo.MatchWinCondition == MatchWinCondition.Best3Of5) && newState.GameInfo.Stage == GameStage.Start && newState.OpponentPendingMessageType == ClientMessageType.ChooseStartingPlayerResp)
		{
			yield return new WaitingOnStartingPlayerEvent();
		}
		foreach (MtgPlayer player in newState.Players)
		{
			yield return new CreatePlayerEvent
			{
				Player = player
			};
		}
		foreach (KeyValuePair<uint, MtgZone> zone3 in newState.Zones)
		{
			yield return new CreateZoneEvent(zone3.Value);
		}
		foreach (KeyValuePair<uint, MtgZone> zone4 in newState.Zones)
		{
			MtgZone zone = zone4.Value;
			if (zone.Type == ZoneType.Revealed || zone.Type == ZoneType.Limbo)
			{
				continue;
			}
			foreach (uint cardId in zone.CardIds)
			{
				MtgCardInstance mtgCardInstance = newState.GetCardById(cardId);
				if (mtgCardInstance == null)
				{
					mtgCardInstance = MtgCardInstance.UnknownCardData(cardId, zone);
				}
				yield return new CardCreatedEvent(mtgCardInstance);
			}
		}
		if (newState.ActivePlayer != null)
		{
			yield return new TurnChangeEvent(newState.ActivePlayer, null);
		}
		foreach (GameRulesEvent item in ExtraTurnsEventGenerator.Generate(null, newState))
		{
			yield return item;
		}
		if (newState.DecidingPlayer != null)
		{
			yield return new DecidingPlayerChangeEvent
			{
				DecidingPlayer = newState.DecidingPlayer
			};
		}
		if (newState.CurrentPhase != Phase.None)
		{
			yield return new GamePhaseChangeEvent(newState.CurrentPhase, newState.CurrentStep);
		}
		if (newState.Actions.Count > 0)
		{
			yield return new HypotheticalActionsChangedEvent
			{
				OldActions = new List<ActionInfo>(),
				NewActions = newState.Actions
			};
		}
		foreach (KeyValuePair<uint, List<ReplacementEffectData>> replacementEffect in newState.ReplacementEffects)
		{
			foreach (ReplacementEffectData item2 in replacementEffect.Value)
			{
				yield return new ReplacementEffectChangedEvent
				{
					EffectData = item2,
					Entity = null,
					Added = true,
					Removed = false
				};
			}
		}
		foreach (GameRulesEvent item3 in SuspendLikeEventGenerator.Generate(null, newState))
		{
			yield return item3;
		}
		foreach (GameRulesEvent item4 in DelayedTriggerEventGenerator.GenerateEvents(null, newState))
		{
			yield return item4;
		}
		foreach (GameRulesEvent item5 in DesignationEventGenerator.Generate(null, newState))
		{
			yield return item5;
		}
		foreach (KeyValuePair<uint, MtgZone> zone5 in newState.Zones)
		{
			MtgZone value = zone5.Value;
			if (value.Type != ZoneType.Revealed)
			{
				continue;
			}
			foreach (MtgCardInstance visibleCard in value.VisibleCards)
			{
				if (!visibleCard.Owner.IsLocalPlayer)
				{
					MtgZone zone2 = visibleCard.Zone;
					if (zone2 != null && zone2.Type == ZoneType.Hand)
					{
						yield return new CardRevealedEvent
						{
							CreateInstance = visibleCard,
							EventType = RevealEventType.Create,
							OwnerId = visibleCard.Owner.InstanceId
						};
					}
				}
			}
		}
	}

	private List<GameRulesEvent> GetZoneChanges(uint zoneId, MtgGameState oldState, MtgGameState newState, Dictionary<uint, uint> dstToSrcMap, AnnotationData annotationData)
	{
		List<GameRulesEvent> list = new List<GameRulesEvent>();
		MtgZone zoneById = newState.GetZoneById(zoneId);
		if (zoneById == null || zoneById.Type == ZoneType.None || zoneById.Type == ZoneType.Revealed || zoneById.Type == ZoneType.Limbo)
		{
			return list;
		}
		if (oldState == null || !oldState.Zones.ContainsKey(zoneId))
		{
			foreach (uint cardId2 in zoneById.CardIds)
			{
				MtgCardInstance mtgCardInstance = newState.GetCardById(cardId2);
				if (mtgCardInstance == null)
				{
					mtgCardInstance = MtgCardInstance.UnknownCardData(cardId2, zoneById);
				}
				list.Add(new CardCreatedEvent(mtgCardInstance));
			}
		}
		else
		{
			MtgZone zoneById2 = oldState.GetZoneById(zoneId);
			if (zoneById2.VisibleCards.Count != zoneById.VisibleCards.Count && zoneById2.CardIds.Count == zoneById.CardIds.Count)
			{
				list.Add(new ZoneUpdatedEvent
				{
					Zone = zoneById
				});
			}
			zoneCardIdsToDelete.Clear();
			zoneCardIdsToAdd.Clear();
			foreach (uint cardId3 in zoneById2.CardIds)
			{
				if (!zoneById.CardIds.Contains(cardId3) && !dstToSrcMap.Values.Contains(cardId3) && !annotationData.DeletedIds.Contains(cardId3) && !annotationData.ZoneTransferIds.Contains(cardId3) && !annotationData.ShuffleIds.Contains(cardId3))
				{
					zoneCardIdsToDelete.Add(cardId3);
				}
			}
			foreach (uint cardId4 in zoneById.CardIds)
			{
				if (!zoneById2.CardIds.Contains(cardId4) && !dstToSrcMap.Values.Contains(cardId4) && !annotationData.CreatedIds.Contains(cardId4) && !annotationData.ZoneTransferIds.Contains(cardId4) && !annotationData.ShuffleIds.Contains(cardId4))
				{
					zoneCardIdsToAdd.Add(cardId4);
				}
			}
			foreach (uint item in zoneCardIdsToDelete)
			{
				MtgCardInstance cardById = oldState.GetCardById(item);
				uint num = item;
				while (dstToSrcMap.ContainsValue(num))
				{
					num = dstToSrcMap.Find(num, (KeyValuePair<uint, uint> x, uint id) => x.Value == id).Key;
				}
				MtgCardInstance cardById2 = newState.GetCardById(num);
				bool flag = cardById != null && cardById2 == null && !newState.ObjectIds.Contains(item);
				bool flag2 = cardById == null && cardById2 == null && !newState.ObjectIds.Contains(item);
				if (flag | flag2)
				{
					list.Add(new CardDeletedEvent(item));
				}
			}
			foreach (uint cardId in zoneCardIdsToAdd)
			{
				uint key = cardId;
				bool flag3 = false;
				while (dstToSrcMap.ContainsKey(key))
				{
					flag3 = true;
					key = dstToSrcMap[key];
				}
				if (flag3)
				{
					continue;
				}
				if (oldState.ObjectIds.Contains(cardId))
				{
					MtgZone mtgZone = oldState.Zones.Values.FirstOrDefault((MtgZone x) => x.CardIds.Contains(cardId));
					if ((zoneById.Type != ZoneType.Limbo && zoneById.Type != ZoneType.None) || (mtgZone != null && mtgZone.Type != ZoneType.Limbo && mtgZone.Type != ZoneType.None))
					{
						ZoneTransferReason reason = ZoneTransferReason.ZoneTransferWithoutIdChange;
						if (mtgZone != null && mtgZone.Type == ZoneType.Pending && zoneById.Type == ZoneType.Stack)
						{
							reason = ZoneTransferReason.PendingToStack;
						}
						list.Add(new ZoneChangeEvent(cardId, mtgZone, zoneById, 0u, reason));
					}
				}
				else
				{
					MtgCardInstance mtgCardInstance2 = newState.GetCardById(cardId);
					if (mtgCardInstance2 == null)
					{
						mtgCardInstance2 = MtgCardInstance.UnknownCardData(cardId, zoneById);
					}
					list.Add(new CardCreatedEvent(mtgCardInstance2));
				}
			}
			foreach (uint cardId5 in zoneById.CardIds)
			{
				if (annotationData.CreatedIds.Contains(cardId5) || annotationData.ShuffleIds.Contains(cardId5) || zoneCardIdsToAdd.Contains(cardId5) || zoneCardIdsToDelete.Contains(cardId5) || !zoneById2.CardIds.Contains(cardId5))
				{
					continue;
				}
				MtgCardInstance cardById3 = oldState.GetCardById(cardId5);
				MtgCardInstance mtgCardInstance3 = newState.GetCardById(cardId5);
				bool num2 = cardById3 == null && mtgCardInstance3 != null;
				bool flag4 = cardById3 != null && mtgCardInstance3 == null;
				if (num2 | flag4)
				{
					if (mtgCardInstance3 == null)
					{
						mtgCardInstance3 = MtgCardInstance.UnknownCardData(cardId5, zoneById);
					}
					list.Add(new CardChangedEvent(cardById3, mtgCardInstance3, 0u, PropertyType.Anonymity));
				}
			}
		}
		return list;
	}

	private uint GetSeatId()
	{
		return _mySeatId;
	}

	private MtgGameState GetCurrentGamestate()
	{
		return _currentGameState;
	}

	private GameStateUpdate GetPendingGamestateUpdate()
	{
		return _pendingGameStateUpdate;
	}

	private void SetPendingGamestateUpdate(GameStateUpdate pendingGamestateUpdate)
	{
		_pendingGameStateUpdate = pendingGamestateUpdate;
	}

	private ConcurrentQueue<ClientUpdateBase> GetClientUpdates()
	{
		return _clientUpdates;
	}

	public GreInterface(ICardDatabaseAdapter cdb, MessageHistory messageHistory, ILogger logger, bool rethrowOnAnnotationParseFailure, IGreInterfaceCallbacks callbacks)
	{
		_cardDatabase = cdb ?? NullCardDatabaseAdapter.Default;
		_dynamicAbilityStateAnnotationParser = new DynamicAbilityStateAnnotationParser(_cardDatabase.DynamicAbilityDataProvider);
		_messageHistory = messageHistory ?? NullHistory.Default;
		_loggerForNonEmergencies = new CompoundLogger("GreInterface Notices", LoggerLevel.Notice, logger);
		_callbacks = callbacks;
		_annotationEventProcessor = new AnnotationEventProcessor(AnnotationParserFactory.CreateEventParsers(_cardDatabase.AbilityDataProvider), logger, rethrowOnAnnotationParseFailure);
		_requestAnnotationProcessor = new RequestAnnotationProcessor();
		ManaPaymentConditionAbilities manaConditionAbilities = new ManaPaymentConditionAbilities();
		_messageTranslators = new Dictionary<GREMessageType, GreToClientMessageTranslatorBase>
		{
			{
				GREMessageType.BinaryGameState,
				new BinaryGameStateTranslator(GetClientUpdates)
			},
			{
				GREMessageType.ChooseStartingPlayerReq,
				new ChooseStartingPlayerRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.MulliganReq,
				new MulliganRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.ActionsAvailableReq,
				new ActionsAvailableRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, manaConditionAbilities, _cardDatabase, _outgoingMessages)
			},
			{
				GREMessageType.PayCostsReq,
				new PayCostsRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, manaConditionAbilities, _cardDatabase, _outgoingMessages)
			},
			{
				GREMessageType.SelectCountersReq,
				new SelectCountersRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.DeclareAttackersReq,
				new DeclareAttackersRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.DeclareBlockersReq,
				new DeclareBlockersRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.AssignDamageReq,
				new AssignDamageRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.OptionalActionMessage,
				new OptionalActionRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SearchReq,
				new SearchRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SelectNreq,
				new SelectNRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.CastingTimeOptionsReq,
				new CastingTimeOptionsRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SelectFromGroupsReq,
				new SelectFromGroupsRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SelectTargetsReq,
				new SelectTargetsRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SelectReplacementReq,
				new SelectReplacementRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.GatherReq,
				new GatherRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.DistributionReq,
				new DistributionRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.GroupReq,
				new GroupRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SelectNgroupReq,
				new SelectNGroupRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.OrderReq,
				new OrderRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.StringInputReq,
				new StringInputRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.NumericInputReq,
				new NumericInputRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SearchFromGroupsReq,
				new SearchFromGroupsRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.IntermissionReq,
				new IntermissionRequestTranslator(GetSeatId, GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.SubmitDeckReq,
				new SubmitDeckRequestTranslator(GetCurrentGamestate, GetPendingGamestateUpdate, SetPendingGamestateUpdate, _outgoingMessages)
			},
			{
				GREMessageType.PromptReq,
				new PromptRequestTranslator(GetClientUpdates)
			},
			{
				GREMessageType.Uimessage,
				new UiMessageTranslator(GetClientUpdates)
			},
			{
				GREMessageType.GetSettingsResp,
				new GetSettingsResponseTranslator(GetClientUpdates)
			},
			{
				GREMessageType.SetSettingsResp,
				new SetSettingsResponseTranslator(GetClientUpdates)
			}
		};
		_notificationParsers = new Dictionary<GREMessageType, INotificationParser>
		{
			{
				GREMessageType.ConnectResp,
				new ConnectResponseNotificationParser()
			},
			{
				GREMessageType.IllegalRequest,
				new IllegalRequestNotificationParser()
			},
			{
				GREMessageType.EdictalMessage,
				new EdictalMessageNotificationParser()
			},
			{
				GREMessageType.TimeoutMessage,
				new TimeoutMessageNotificationParser()
			},
			{
				GREMessageType.TimerStateMessage,
				new TimerStateMessageNotificationParser(GetCurrentGamestate)
			}
		};
		_annotationStateParsers = new AnnotationStateParser(AnnotationParserFactory.CreateStateParsers(_cardDatabase), logger, rethrowOnAnnotationParseFailure);
		_cardReferenceHandlers = new ICardReferenceHandler[6]
		{
			new ParentChildReferenceHandler(cdb.AbilityDataProvider),
			new BlockerReferenceHandler(),
			new FacetReferenceHandler(),
			new TargetReferenceHandler(),
			new DelayedTriggerReferenceHandler(),
			new DesignationReferenceHandler()
		};
	}

	public void SubmitUIMessage(UIMessage outgoingMessage)
	{
		ClientToGREMessage clientToGREMessage = new ClientToGREMessage
		{
			SystemSeatId = _mySeatId
		};
		clientToGREMessage.Type = ClientMessageType.Uimessage;
		clientToGREMessage.UiMessage = outgoingMessage;
		clientToGREMessage.UiMessage.SeatIds.Add(3 - _mySeatId);
		EnqueueOutgoingMessage(clientToGREMessage);
	}

	public void SetAutoPass(AutoPassOption autoPassOption, AutoPassOption defaultAutoPassOption, uint turnNumber)
	{
		SettingsMessage settingsMessage = new SettingsMessage();
		settingsMessage.AutoPassOption = autoPassOption;
		settingsMessage.DefaultAutoPassOption = defaultAutoPassOption;
		bool flag = autoPassOption == AutoPassOption.FullControl || defaultAutoPassOption == AutoPassOption.FullControl;
		settingsMessage.ManaPaymentStrategyType = ((!flag) ? ManaPaymentStrategyType.Auto : ManaPaymentStrategyType.Manual);
		SetSettings(settingsMessage, turnNumber);
	}

	public void SetAutoPayMana(ManaSelectionType autoPayOptions)
	{
		SettingsMessage settingsMessage = new SettingsMessage
		{
			ManaSelectionType = autoPayOptions
		};
		SetSettings(settingsMessage);
	}

	public void SetSettings(SettingsMessage settingsMessage, uint turnNumber = 0u)
	{
		ClientToGREMessage msg = new ClientToGREMessage
		{
			Type = ClientMessageType.SetSettingsReq,
			SetSettingsReq = new SetSettingsReq
			{
				Settings = settingsMessage,
				TurnNumber = turnNumber
			}
		};
		EnqueueOutgoingMessage(msg);
	}

	public void ConcedeGame()
	{
		Concede(MatchScope.Game);
	}

	public void ConcedeMatch()
	{
		Concede(MatchScope.Match);
	}

	private void Concede(MatchScope matchScope)
	{
		ClientToGREMessage clientToGREMessage = new ClientToGREMessage();
		clientToGREMessage.Type = ClientMessageType.ConcedeReq;
		clientToGREMessage.ConcedeReq = new ConcedeReq
		{
			Scope = matchScope
		};
		EnqueueOutgoingMessage(clientToGREMessage);
	}

	public void OnMessageReceived(GREToClientMessage msg)
	{
		_incomingMessages.Enqueue(msg);
	}

	public void EnqueueOutgoingMessage(ClientToGREMessage msg)
	{
		_outgoingMessages.Enqueue(msg);
	}

	public List<ClientUpdateBase> RetrieveClientUpdates()
	{
		updateCache.Clear();
		ClientUpdateBase result;
		while (_clientUpdates.TryDequeue(out result))
		{
			updateCache.Add(result);
		}
		return updateCache;
	}

	public ClientToGREMessage DequeueOutgoingMessage()
	{
		_outgoingMessages.TryDequeue(out var result);
		return result;
	}

	public void ProcessQueues()
	{
		if (_callbacks != null)
		{
			_callbacks.ProcessQueues(_incomingMessages, ProcessMessage);
		}
		else
		{
			ProcessQueuesDefault();
		}
	}

	private void ProcessQueuesDefault()
	{
		if (_incomingMessages.TryDequeue(out var result))
		{
			ProcessMessage(result);
		}
	}

	private void ProcessMessage(GREToClientMessage msg)
	{
		if (msg.SystemSeatIds.Count == 1)
		{
			_mySeatId = msg.SystemSeatIds[0];
		}
		if (_pendingGameStateUpdate != null && msg.GameStateId == _currentGameStateId)
		{
			_currentMessageCount++;
		}
		INotificationParser value2;
		if (_messageTranslators.TryGetValue(msg.Type, out var value))
		{
			value.Translate(msg);
			BaseUserRequest baseUserRequest = _pendingGameStateUpdate?.Interaction;
			if (baseUserRequest != null && _lastGameStateMessage != null)
			{
				_requestAnnotationProcessor.Process(baseUserRequest, _lastGameStateMessage.Annotations);
				_requestAnnotationProcessor.Process(baseUserRequest, _lastGameStateMessage.PersistentAnnotations);
			}
		}
		else if (_notificationParsers.TryGetValue(msg.Type, out value2))
		{
			ClientUpdateBase clientUpdateBase = value2.Translate(msg);
			if (clientUpdateBase != null)
			{
				_clientUpdates.Enqueue(clientUpdateBase);
			}
		}
		else
		{
			GREMessageType type = msg.Type;
			if (type == GREMessageType.GameStateMessage || type == GREMessageType.QueuedGameStateMessage)
			{
				_currentMessageCount = 0u;
				_targetMessageCount = msg.GameStateMessage.PendingMessageCount;
				_currentGameStateId = msg.GameStateMessage.GameStateId;
				_pendingGameStateUpdate = new GameStateUpdate();
				_pendingGameStateUpdate.Prompt = msg.Prompt;
				_pendingGameStateUpdate.OldState = _currentGameState;
				bool flag = _lastGameStateMessage == null;
				if (msg.GameStateMessage.Type == GameStateType.Full)
				{
					_lastGameStateMessage = msg.GameStateMessage;
					_pendingGameStateUpdate.IsFullGameState = true;
				}
				else
				{
					MergeDiffMessageIntoFullMessage(_lastGameStateMessage, msg.GameStateMessage);
				}
				List<AnnotationInfo> list = new List<AnnotationInfo>();
				list.AddRange(_lastGameStateMessage.Annotations);
				list.AddRange(_lastGameStateMessage.PersistentAnnotations);
				ParseAnnotationsPreGameState(list);
				if (msg.GameStateMessage.Update == Wotc.Mtgo.Gre.External.Messaging.GameStateUpdate.Undo)
				{
					_currentGameState = ParseFullGameStateMessage(_lastGameStateMessage);
					CarryOverStateDataUndo(_currentGameState, _pendingGameStateUpdate.OldState);
					ParseAnnotations(list, _pendingGameStateUpdate.OldState, _currentGameState);
					SetupCrossCardReferences(_currentGameState);
					Dictionary<uint, uint> dictionary = new Dictionary<uint, uint>();
					uint gameStateId = msg.GameStateMessage.GameStateId;
					uint prevGameStateId = msg.GameStateMessage.PrevGameStateId;
					for (uint num = gameStateId; num > prevGameStateId; num--)
					{
						if (_idHistory.ContainsKey(num))
						{
							foreach (uint key3 in _idHistory[num].Keys)
							{
								uint value3 = _idHistory[num][key3];
								uint key = key3;
								dictionary.Add(key, value3);
							}
							_idHistory.Remove(num);
						}
					}
					List<GameRulesEvent> zoneUpdates = GetZoneUpdates(msg.GameStateMessage);
					List<GameRulesEvent> undoDiffChanges = GetUndoDiffChanges(_pendingGameStateUpdate.OldState, _currentGameState, dictionary);
					_pendingGameStateUpdate.NewState = _currentGameState;
					_pendingGameStateUpdate.Changes.AddRange(zoneUpdates);
					_pendingGameStateUpdate.Changes.AddRange(undoDiffChanges);
				}
				else
				{
					_currentGameState = ParseFullGameStateMessage(_lastGameStateMessage);
					CarryOverStateData(_currentGameState, _pendingGameStateUpdate.OldState);
					ParseAnnotations(list, _pendingGameStateUpdate.OldState, _currentGameState);
					SetupCrossCardReferences(_currentGameState);
					List<GameRulesEvent> list2 = new List<GameRulesEvent>(GetZoneUpdates(msg.GameStateMessage));
					if (flag)
					{
						list2.AddRange(FirstGameStateEvents(_currentGameState));
						_currentGameState.Type = MtgGameState.StateType.InitialGameState;
					}
					else
					{
						list2.AddRange(_annotationEventProcessor.Process(list, _pendingGameStateUpdate.OldState, _currentGameState));
						Dictionary<uint, uint> dictionary2 = new Dictionary<uint, uint>();
						List<uint> list3 = new List<uint>();
						List<uint> list4 = new List<uint>();
						List<uint> list5 = new List<uint>();
						List<uint> list6 = new List<uint>();
						foreach (AnnotationInfo item in list)
						{
							switch (item.Type.First())
							{
							case AnnotationType.ObjectIdChanged:
							{
								uint value4 = (uint)item.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("orig_id")).ValueInt32[0];
								uint key2 = (uint)item.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("new_id")).ValueInt32[0];
								dictionary2.Add(key2, value4);
								break;
							}
							case AnnotationType.TokenCreated:
							case AnnotationType.AbilityInstanceCreated:
							case AnnotationType.CardRevealed:
								foreach (uint affectedId in item.AffectedIds)
								{
									if (_currentGameState.GetCardById(affectedId) != null)
									{
										list3.Add(affectedId);
									}
								}
								break;
							case AnnotationType.AbilityInstanceDeleted:
							case AnnotationType.TokenDeleted:
								foreach (uint affectedId2 in item.AffectedIds)
								{
									list4.Add(affectedId2);
								}
								break;
							case AnnotationType.ZoneTransfer:
								foreach (uint affectedId3 in item.AffectedIds)
								{
									list5.Add(affectedId3);
								}
								break;
							case AnnotationType.Shuffle:
							{
								List<int> list7 = new List<int>(item.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("OldIds")).ValueInt32);
								List<int> list8 = new List<int>(item.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("NewIds")).ValueInt32);
								list6.AddRange(list7.ConvertAll((int x) => (uint)x));
								list6.AddRange(list8.ConvertAll((int x) => (uint)x));
								break;
							}
							}
						}
						AnnotationData annotationData = new AnnotationData
						{
							CreatedIds = list3,
							DeletedIds = list4,
							ZoneTransferIds = list5,
							ShuffleIds = list6
						};
						_idHistory[msg.GameStateMessage.GameStateId] = dictionary2;
						ModifyEventsList(list2, _currentGameState);
						GetDiffChanges(_pendingGameStateUpdate.OldState, _currentGameState, dictionary2, annotationData, list2);
					}
					_pendingGameStateUpdate.NewState = _currentGameState;
					_pendingGameStateUpdate.Changes.AddRange(list2);
				}
				_messageHistory.OnGameStateParsed(_lastGameStateMessage, _currentGameState);
			}
		}
		if (_pendingGameStateUpdate != null && _currentMessageCount == _targetMessageCount)
		{
			if (_pendingGameStateUpdate.NewState.LocalPlayer.ControllerType != ControllerType.Player)
			{
				_pendingGameStateUpdate.Interaction = null;
			}
			_clientUpdates.Enqueue(_pendingGameStateUpdate);
			_currentMessageCount = 0u;
			_targetMessageCount = 0u;
			_pendingGameStateUpdate = null;
		}
	}

	private static void ModifyEventsList(List<GameRulesEvent> changeEvents, MtgGameState currentGameState)
	{
		foreach (MtgPlayer player in currentGameState.Players)
		{
			List<DamageDealtEvent> combatDamageEventsAtPlayer = changeEvents.FindAll((GameRulesEvent x) => x is DamageDealtEvent { Type: DamageType.Combat } damageDealtEvent && damageDealtEvent.Victim.InstanceId == player.InstanceId).ConvertAll((GameRulesEvent x) => x as DamageDealtEvent);
			if (combatDamageEventsAtPlayer.Count == 0)
			{
				continue;
			}
			UpdatePlayerLifeTotal updatePlayerLifeTotal = changeEvents.Find((GameRulesEvent x) =>
			{
				UpdatePlayerLifeTotal uplt = x as UpdatePlayerLifeTotal;
				return uplt != null && uplt.Player.InstanceId == player.InstanceId && combatDamageEventsAtPlayer.Exists((DamageDealtEvent y) => y.Damager.InstanceId == uplt.Affector);
			}) as UpdatePlayerLifeTotal;
			uint num = ((updatePlayerLifeTotal != null && updatePlayerLifeTotal.LifeChange < 0) ? ((uint)(-updatePlayerLifeTotal.LifeChange)) : 0u);
			foreach (DamageDealtEvent item in combatDamageEventsAtPlayer)
			{
				item.Amount = Math.Min(item.Amount, num);
				num -= item.Amount;
			}
			foreach (DamageDealtEvent item2 in combatDamageEventsAtPlayer)
			{
				int num2 = changeEvents.IndexOf(item2);
				changeEvents.Insert(num2 + 1, new UpdatePlayerLifeTotal
				{
					Affector = item2.Damager.InstanceId,
					LifeChange = (int)(0L - (long)item2.Amount),
					Player = (item2.Victim as MtgPlayer),
					GeneratedByAnnotation = false
				});
			}
			if (updatePlayerLifeTotal != null)
			{
				if (num != 0)
				{
					updatePlayerLifeTotal.LifeChange = (int)(0L - (long)num);
				}
				else
				{
					changeEvents.Remove(updatePlayerLifeTotal);
				}
			}
		}
	}

	public void Dispose()
	{
		OnDisposed(isManualDisposal: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void OnDisposed(bool isManualDisposal)
	{
		if (_disposed)
		{
			return;
		}
		if (isManualDisposal)
		{
			_loggerForNonEmergencies.Dispose();
			foreach (KeyValuePair<GREMessageType, GreToClientMessageTranslatorBase> messageTranslator in _messageTranslators)
			{
				if (messageTranslator.Value is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}
			_messageTranslators.Clear();
			foreach (KeyValuePair<GREMessageType, INotificationParser> notificationParser in _notificationParsers)
			{
				notificationParser.Value?.Dispose();
			}
			_callbacks?.OnManualDispose();
		}
		_disposed = true;
	}
}
