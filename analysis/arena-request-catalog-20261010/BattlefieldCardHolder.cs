using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Ability;
using AssetLookupTree.Payloads.Card;
using AssetLookupTree.Payloads.RegionTransfer;
using Core.Code.Input;
using GreClient.CardData;
using GreClient.Rules;
using InteractionSystem;
using MovementSystem;
using Pooling;
using UnityEngine;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Duel;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Battlefield;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.DuelScene.Universal;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class BattlefieldCardHolder : ZoneCardHolderBase, IBattlefieldCardHolder, ICardHolder
{
	public struct StackCounterData
	{
		public Vector3 positon;

		public int count;

		public uint parentInstanceId;
	}

	public class BattlefieldLayout : ICardLayout
	{
		public const float OFFSCREEN_Y = -5f;

		private readonly BattlefieldCardHolder _battlefield;

		private readonly List<PlayerBattlefieldRegions> _allPlayerBattlefieldRegions = new List<PlayerBattlefieldRegions>();

		public readonly List<CdcStackCounterView> ActiveStackCounters = new List<CdcStackCounterView>();

		private readonly List<BattlefieldRegion> _sortedRegions = new List<BattlefieldRegion>();

		private readonly Dictionary<CardType, Dictionary<uint, BattlefieldRegion>> _regionsByCardTypeAndOwnerId = new Dictionary<CardType, Dictionary<uint, BattlefieldRegion>>
		{
			[CardType.Creature] = new Dictionary<uint, BattlefieldRegion>(),
			[CardType.Artifact] = new Dictionary<uint, BattlefieldRegion>(),
			[CardType.Enchantment] = new Dictionary<uint, BattlefieldRegion>(),
			[CardType.Land] = new Dictionary<uint, BattlefieldRegion>(),
			[CardType.Planeswalker] = new Dictionary<uint, BattlefieldRegion>()
		};

		private readonly Dictionary<CardType, Dictionary<GREPlayerNum, BattlefieldRegion>> _regionsByCardTypeAndOwner = new Dictionary<CardType, Dictionary<GREPlayerNum, BattlefieldRegion>>
		{
			[CardType.Creature] = new Dictionary<GREPlayerNum, BattlefieldRegion>(),
			[CardType.Artifact] = new Dictionary<GREPlayerNum, BattlefieldRegion>(),
			[CardType.Enchantment] = new Dictionary<GREPlayerNum, BattlefieldRegion>(),
			[CardType.Land] = new Dictionary<GREPlayerNum, BattlefieldRegion>(),
			[CardType.Planeswalker] = new Dictionary<GREPlayerNum, BattlefieldRegion>()
		};

		private readonly List<Rect> _takenRects = new List<Rect>();

		private readonly List<StackCounterData> _pendingStackCounters = new List<StackCounterData>();

		public readonly Dictionary<DuelScene_CDC, KeyValuePair<BattlefieldRegion, BattlefieldRegion>> CardRegionChanges = new Dictionary<DuelScene_CDC, KeyValuePair<BattlefieldRegion, BattlefieldRegion>>();

		private readonly IBattlefieldStackFactory _stackFactory;

		private readonly IUnityObjectPool _unityPool;

		private readonly UXEventQueue _uxEventQueue;

		private readonly IGameStateProvider _gameStateProvider;

		private readonly IBrowserProvider _browserProvider;

		private readonly ICardDatabaseAdapter _cardDatabase;

		private readonly GameInteractionSystem _interactionSystem;

		private readonly NPEDirector _npeDirector;

		private readonly IPlayerOffsetCalculator _playerOffsetCalculator;

		private List<DuelScene_CDC> _unattachedCardsCache = new List<DuelScene_CDC>(4);

		private Dictionary<uint, List<DuelScene_CDC>> _attachedCardsByParentCache = new Dictionary<uint, List<DuelScene_CDC>>(4);

		private List<BattlefieldStack> _stacksCache = new List<BattlefieldStack>(4);

		private static HashSet<MtgCardInstance> _attachmentChain = new HashSet<MtgCardInstance>();

		private readonly Dictionary<uint, float> _attackerPositions = new Dictionary<uint, float>();

		public List<BattlefieldRegion> Regions { get; private set; } = new List<BattlefieldRegion>();

		public Dictionary<CardType, Dictionary<GREPlayerNum, BattlefieldRegion>> RegionsByCardTypeAndOwner => _regionsByCardTypeAndOwner;

		public Dictionary<CardType, Dictionary<uint, BattlefieldRegion>> RegionsByCardTypeAndOwnerId => _regionsByCardTypeAndOwnerId;

		public BattlefieldLayout(BattlefieldCardHolder holder, IBattlefieldStackFactory stackFactory, IContext context, GameInteractionSystem interactionSystem, NPEDirector npeDirector)
		{
			_battlefield = holder;
			_stackFactory = stackFactory;
			_unityPool = context.Get<IUnityObjectPool>() ?? NullUnityObjectPool.Default;
			_uxEventQueue = context.Get<UXEventQueue>();
			_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
			_browserProvider = context.Get<IBrowserProvider>() ?? NullBrowserProvider.Default;
			_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
			_playerOffsetCalculator = context.Get<IPlayerOffsetCalculator>();
			_interactionSystem = interactionSystem;
			_npeDirector = npeDirector;
			_uxEventQueue.EventExecutionCommenced += OnUxEventCommenced;
		}

		public void RegisterPlayer(PlayerBattlefieldRegions playerBattlefieldRegions)
		{
			uint playerId = playerBattlefieldRegions.PlayerId;
			bool isLocalPlayer = playerBattlefieldRegions.IsLocalPlayer;
			_allPlayerBattlefieldRegions.Add(playerBattlefieldRegions);
			if (_allPlayerBattlefieldRegions.Count > 2)
			{
				foreach (PlayerBattlefieldRegions allPlayerBattlefieldRegion in _allPlayerBattlefieldRegions)
				{
					Vector3 offset = _playerOffsetCalculator.GetOffset(allPlayerBattlefieldRegion.PlayerId);
					allPlayerBattlefieldRegion.SetLayoutOffset(offset);
				}
			}
			else
			{
				_allPlayerBattlefieldRegions.ForEach((PlayerBattlefieldRegions x) =>
				{
					x.SetLayoutOffset(Vector3.zero);
				});
			}
			GREPlayerNum key = (isLocalPlayer ? GREPlayerNum.LocalPlayer : GREPlayerNum.Opponent);
			_regionsByCardTypeAndOwner[CardType.Creature][key] = playerBattlefieldRegions.CreatureRegion;
			_regionsByCardTypeAndOwner[CardType.Artifact][key] = playerBattlefieldRegions.ArtifactRegion;
			_regionsByCardTypeAndOwner[CardType.Enchantment][key] = playerBattlefieldRegions.ArtifactRegion;
			_regionsByCardTypeAndOwner[CardType.Land][key] = playerBattlefieldRegions.LandRegion;
			_regionsByCardTypeAndOwner[CardType.Planeswalker][key] = playerBattlefieldRegions.PlaneswalkerRegion;
			_regionsByCardTypeAndOwnerId[CardType.Creature][playerId] = playerBattlefieldRegions.CreatureRegion;
			_regionsByCardTypeAndOwnerId[CardType.Artifact][playerId] = playerBattlefieldRegions.ArtifactRegion;
			_regionsByCardTypeAndOwnerId[CardType.Enchantment][playerId] = playerBattlefieldRegions.ArtifactRegion;
			_regionsByCardTypeAndOwnerId[CardType.Land][playerId] = playerBattlefieldRegions.LandRegion;
			_regionsByCardTypeAndOwnerId[CardType.Planeswalker][playerId] = playerBattlefieldRegions.PlaneswalkerRegion;
			Regions.Add(playerBattlefieldRegions.CreatureRegion);
			Regions.Add(playerBattlefieldRegions.LandRegion);
			Regions.Add(playerBattlefieldRegions.ArtifactRegion);
			Regions.Add(playerBattlefieldRegions.PlaneswalkerRegion);
			_sortedRegions.Clear();
			_sortedRegions.AddRange(_allPlayerBattlefieldRegions.Select((PlayerBattlefieldRegions regions) => regions.CreatureRegion));
			_sortedRegions.AddRange(_allPlayerBattlefieldRegions.Select((PlayerBattlefieldRegions regions) => regions.LandRegion));
			_sortedRegions.AddRange(_allPlayerBattlefieldRegions.Select((PlayerBattlefieldRegions regions) => regions.PlaneswalkerRegion));
			_sortedRegions.AddRange(_allPlayerBattlefieldRegions.Select((PlayerBattlefieldRegions regions) => regions.ArtifactRegion));
		}

		public void Cleanup()
		{
			Regions.Clear();
			_allPlayerBattlefieldRegions.Clear();
			foreach (KeyValuePair<CardType, Dictionary<uint, BattlefieldRegion>> item in _regionsByCardTypeAndOwnerId)
			{
				item.Value.Clear();
			}
			_regionsByCardTypeAndOwnerId.Clear();
			_sortedRegions.Clear();
			foreach (CdcStackCounterView activeStackCounter in ActiveStackCounters)
			{
				activeStackCounter.Cleanup();
				_unityPool.PushObject(activeStackCounter.gameObject);
			}
			ActiveStackCounters.Clear();
			_uxEventQueue.EventExecutionCommenced -= OnUxEventCommenced;
		}

		public virtual void GenerateData(List<DuelScene_CDC> allCardViews, ref List<CardLayoutData> allData, Vector3 center, Quaternion rotation)
		{
			Dictionary<BattlefieldRegion, HashSet<DuelScene_CDC>> dictionary = new Dictionary<BattlefieldRegion, HashSet<DuelScene_CDC>>();
			foreach (BattlefieldRegion region in Regions)
			{
				dictionary[region] = new HashSet<DuelScene_CDC>(region.AllCards);
				region.Stacks.Clear();
			}
			_takenRects.Clear();
			_pendingStackCounters.Clear();
			CardRegionChanges.Clear();
			if (allCardViews.Count == 0)
			{
				CleanupArrowsAndCounters();
				return;
			}
			_unattachedCardsCache.Clear();
			_unattachedCardsCache.AddRange(allCardViews);
			_attachedCardsByParentCache.Clear();
			foreach (DuelScene_CDC allCardView in allCardViews)
			{
				IEnumerable<MtgCardInstance> otherInstances = CardViewAndLimboInstances(allCardViews, _gameStateProvider.CurrentGameState, _browserProvider.CurrentBrowser);
				MtgCardInstance mtgCardInstance = FindStackParentCardInstance(allCardView.Model.Instance, otherInstances);
				if (mtgCardInstance != null)
				{
					_unattachedCardsCache.Remove(allCardView);
					if (_attachedCardsByParentCache.TryGetValue(mtgCardInstance.InstanceId, out var value))
					{
						value.Add(allCardView);
						continue;
					}
					_attachedCardsByParentCache[mtgCardInstance.InstanceId] = new List<DuelScene_CDC> { allCardView };
				}
			}
			_stacksCache.Clear();
			foreach (DuelScene_CDC item in _unattachedCardsCache)
			{
				uint instanceId = item.Model.InstanceId;
				if (_attachedCardsByParentCache.TryGetValue(instanceId, out var value2))
				{
					value2.Sort((DuelScene_CDC lhs, DuelScene_CDC rhs) => rhs.Model.Instance.AttachedToId.CompareTo(lhs.Model.Instance.AttachedToId));
					_stacksCache.Add(_stackFactory.Create(item, value2));
					continue;
				}
				if (item.Model.Instance.AttachedWithIds.Count != 0)
				{
					_stacksCache.Add(_stackFactory.Create(item, null));
					continue;
				}
				BattlefieldStack battlefieldStack = null;
				foreach (BattlefieldStack item2 in _stacksCache)
				{
					if (item2.CanStack(item))
					{
						battlefieldStack = item2;
						break;
					}
				}
				if (battlefieldStack == null || (_npeDirector != null && _npeDirector.AllowsStackingOnBattlefield && item.Model.CardTypes.Contains(CardType.Creature)))
				{
					_stacksCache.Add(_stackFactory.Create(item, null));
				}
				else
				{
					battlefieldStack.PushCardOnStack(item);
				}
			}
			foreach (KeyValuePair<uint, List<DuelScene_CDC>> item3 in _attachedCardsByParentCache)
			{
				BattlefieldStack battlefieldStack2 = null;
				foreach (BattlefieldStack item4 in _stacksCache)
				{
					if (item4.StackParentModel.InstanceId == item3.Key)
					{
						battlefieldStack2 = item4;
						break;
					}
				}
				if (battlefieldStack2 == null)
				{
					IEnumerable<MtgCardInstance> otherInstances2 = CardViewAndLimboInstances(allCardViews, _gameStateProvider.CurrentGameState, _browserProvider.CurrentBrowser);
					MtgCardInstance mtgCardInstance2 = FindStackParentCardInstance(item3.Value[0].Model.Instance, otherInstances2);
					if (mtgCardInstance2 != null)
					{
						_stacksCache.Add(_stackFactory.Create(CardDataExtensions.CreateWithDatabase(mtgCardInstance2, _cardDatabase), item3.Value));
					}
				}
			}
			long num = 0L;
			foreach (BattlefieldStack item5 in _stacksCache)
			{
				if (item5.StackParent != null)
				{
					num++;
				}
				if (item5.StackedCards.Count > 0)
				{
					num += item5.StackedCards.Count;
				}
			}
			if (num < allCardViews.Count)
			{
				throw new UnityException($"[BattlefieldLayout] The total number of stacked cards ({num}) does not match the number of cardViews provided ({allCardViews.Count}).");
			}
			MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
			IReadOnlyList<MtgPlayer> players = mtgGameState.Players;
			foreach (BattlefieldStack item6 in _stacksCache)
			{
				BattlefieldRegion regionForStack = GetRegionForStack(item6, players);
				if (regionForStack == null)
				{
					continue;
				}
				regionForStack.Stacks.Add(item6);
				HashSet<DuelScene_CDC> hashSet = dictionary[regionForStack];
				foreach (DuelScene_CDC allCard in item6.AllCards)
				{
					if (hashSet.Contains(allCard))
					{
						continue;
					}
					BattlefieldRegion battlefieldRegion = null;
					foreach (KeyValuePair<BattlefieldRegion, HashSet<DuelScene_CDC>> item7 in dictionary)
					{
						if (item7.Value.Contains(allCard))
						{
							battlefieldRegion = item7.Key;
							break;
						}
					}
					if (battlefieldRegion != null)
					{
						CardRegionChanges[allCard] = new KeyValuePair<BattlefieldRegion, BattlefieldRegion>(battlefieldRegion, regionForStack);
					}
				}
			}
			BattlefieldRegion currentlyHoveredRegion = _battlefield.GetCurrentlyHoveredRegion();
			foreach (PlayerBattlefieldRegions allPlayerBattlefieldRegion in _allPlayerBattlefieldRegions)
			{
				BattlefieldRegion planeswalkerRegion = allPlayerBattlefieldRegion.PlaneswalkerRegion;
				BattlefieldRegion artifactRegion = allPlayerBattlefieldRegion.ArtifactRegion;
				BattlefieldRegion landRegion = allPlayerBattlefieldRegion.LandRegion;
				planeswalkerRegion.SortStacksByAge();
				artifactRegion.SortStacksByAge();
				landRegion.SortStacksByAge();
				GenerateRegionData(planeswalkerRegion, allData, planeswalkerRegion != currentlyHoveredRegion);
				GenerateRegionData(artifactRegion, allData, artifactRegion != currentlyHoveredRegion);
				GenerateRegionData(landRegion, allData, landRegion != currentlyHoveredRegion);
			}
			if (mtgGameState.CurrentPhase == Phase.Combat && _allPlayerBattlefieldRegions.Count == 2)
			{
				bool isLocalPlayer = mtgGameState.ActivePlayer.IsLocalPlayer;
				BattlefieldRegion creatureRegion = _allPlayerBattlefieldRegions.Find((PlayerBattlefieldRegions x) => x.IsLocalPlayer).CreatureRegion;
				BattlefieldRegion creatureRegion2 = _allPlayerBattlefieldRegions.Find((PlayerBattlefieldRegions x) => !x.IsLocalPlayer).CreatureRegion;
				BattlefieldRegion battlefieldRegion2 = (isLocalPlayer ? creatureRegion : creatureRegion2);
				BattlefieldRegion battlefieldRegion3 = (isLocalPlayer ? creatureRegion2 : creatureRegion);
				battlefieldRegion2.SortStacksByAge();
				GenerateRegionData(battlefieldRegion2, allData, battlefieldRegion2 != currentlyHoveredRegion);
				battlefieldRegion3.SortStacksByBlocking(CollectAttackerPositions(battlefieldRegion2, allData));
				GenerateRegionData(battlefieldRegion3, allData, battlefieldRegion3 != currentlyHoveredRegion);
			}
			else
			{
				foreach (PlayerBattlefieldRegions allPlayerBattlefieldRegion2 in _allPlayerBattlefieldRegions)
				{
					BattlefieldRegion creatureRegion3 = allPlayerBattlefieldRegion2.CreatureRegion;
					creatureRegion3.SortStacksByAge();
					GenerateRegionData(creatureRegion3, allData, creatureRegion3 != currentlyHoveredRegion);
				}
			}
			while (_pendingStackCounters.Count < ActiveStackCounters.Count)
			{
				CdcStackCounterView cdcStackCounterView = ActiveStackCounters[0];
				ActiveStackCounters.RemoveAt(0);
				cdcStackCounterView.Cleanup();
				_unityPool.PushObject(cdcStackCounterView.gameObject);
			}
			while (ActiveStackCounters.Count < _pendingStackCounters.Count)
			{
				CdcStackCounterView component = _unityPool.PopObject(_battlefield.CounterPrefab.gameObject).GetComponent<CdcStackCounterView>();
				Transform transform = component.transform;
				transform.parent = _battlefield.transform;
				transform.localRotation = Quaternion.identity;
				ActiveStackCounters.Add(component);
			}
			for (int num2 = 0; num2 < _pendingStackCounters.Count; num2++)
			{
				StackCounterData stackCounterData = _pendingStackCounters[num2];
				CdcStackCounterView cdcStackCounterView2 = ActiveStackCounters[num2];
				cdcStackCounterView2.transform.position = stackCounterData.positon;
				cdcStackCounterView2.SetCount(stackCounterData.count);
				cdcStackCounterView2.Init(_interactionSystem, stackCounterData.parentInstanceId);
			}
			for (int num3 = 0; num3 < _takenRects.Count; num3++)
			{
				DebugDraw.Square(_takenRects[num3], UnityEngine.Color.Lerp(UnityEngine.Color.red, UnityEngine.Color.blue, (float)num3 / (float)_takenRects.Count), 2.5f);
			}
		}

		private BattlefieldRegion GetRegionForStack(BattlefieldStack stack, IReadOnlyList<MtgPlayer> players)
		{
			ICardDataAdapter stackParentModel = stack.StackParentModel;
			ValidatorBlackboard blackboard = new ValidatorBlackboard
			{
				CardData = stackParentModel,
				Players = players
			};
			foreach (BattlefieldRegion sortedRegion in _sortedRegions)
			{
				if (sortedRegion.CardIsValid(blackboard))
				{
					return sortedRegion;
				}
			}
			return null;
		}

		private static IEnumerable<MtgCardInstance> CardViewAndLimboInstances(IEnumerable<DuelScene_CDC> cardViews, MtgGameState gameState, BrowserBase currentBrowser)
		{
			if (cardViews != null)
			{
				foreach (DuelScene_CDC cardView in cardViews)
				{
					if (cardView == null)
					{
						continue;
					}
					ICardDataAdapter model = cardView.Model;
					if (model != null)
					{
						MtgCardInstance instance = model.Instance;
						if (instance != null)
						{
							yield return instance;
						}
					}
				}
			}
			if (gameState != null)
			{
				MtgZone limbo = gameState.Limbo;
				if (limbo != null)
				{
					foreach (MtgCardInstance visibleCard in limbo.VisibleCards)
					{
						yield return visibleCard;
					}
				}
			}
			if (!(currentBrowser is CardBrowserBase cardBrowserBase))
			{
				yield break;
			}
			foreach (DuelScene_CDC cardView2 in cardBrowserBase.GetCardViews())
			{
				if ((bool)cardView2)
				{
					ICardDataAdapter model2 = cardView2.Model;
					if (model2 != null && model2.ZoneType == ZoneType.Battlefield)
					{
						yield return cardView2.Model.Instance;
					}
				}
			}
		}

		public static MtgCardInstance FindStackParentCardInstance(MtgCardInstance cardInstance, IEnumerable<MtgCardInstance> otherInstances)
		{
			if (cardInstance == null || cardInstance.AttachedToId == 0)
			{
				return null;
			}
			MtgCardInstance mtgCardInstance = cardInstance;
			_attachmentChain.Clear();
			_attachmentChain.Add(cardInstance);
			MtgCardInstance attachedInstance;
			while (TryGetAttachedInstance(mtgCardInstance, otherInstances, out attachedInstance) && _attachmentChain.Add(attachedInstance))
			{
				mtgCardInstance = attachedInstance;
			}
			_attachmentChain.Clear();
			if (mtgCardInstance == cardInstance)
			{
				return null;
			}
			return mtgCardInstance;
		}

		private static bool TryGetAttachedInstance(MtgCardInstance cardInstance, IEnumerable<MtgCardInstance> allInstances, out MtgCardInstance attachedInstance)
		{
			foreach (MtgCardInstance item in allInstances ?? Array.Empty<MtgCardInstance>())
			{
				if (cardInstance.AttachedToId == item.InstanceId)
				{
					attachedInstance = item;
					return true;
				}
			}
			attachedInstance = null;
			return false;
		}

		private void OnUxEventCommenced(UXEvent uxEvent)
		{
			if (!(uxEvent is ZoneTransferGroup zoneTransferGroup))
			{
				if (!(uxEvent is ZoneTransferUXEvent zoneTransferUxEvent))
				{
					if (uxEvent is UpdateCardModelUXEvent { Property: PropertyType.TitleId } updateCardModelUXEvent && updateCardModelUXEvent.NewInstance.Zone.Type == ZoneType.Battlefield && TryGetRelevantRegion(updateCardModelUXEvent.NewInstance, out var result))
					{
						result.RemapStackTitleId(updateCardModelUXEvent.OldInstance.TitleId, updateCardModelUXEvent.NewInstance.TitleId);
					}
				}
				else
				{
					TryUpdateStackAgeIfZoneTransferToBattlefield(zoneTransferUxEvent);
				}
				return;
			}
			foreach (ZoneTransferUXEvent zoneTransfer in zoneTransferGroup._zoneTransfers)
			{
				TryUpdateStackAgeIfZoneTransferToBattlefield(zoneTransfer);
			}
		}

		private void TryUpdateStackAgeIfZoneTransferToBattlefield(ZoneTransferUXEvent zoneTransferUxEvent)
		{
			if (zoneTransferUxEvent.ToZoneType == ZoneType.Battlefield)
			{
				uint oldId = zoneTransferUxEvent.OldId;
				uint newId = zoneTransferUxEvent.NewId;
				if (TryGetRelevantRegion(zoneTransferUxEvent.NewInstance, out var result))
				{
					result.RemapStackAge(oldId, newId);
				}
			}
		}

		private bool TryGetRelevantRegion(MtgCardInstance card, out BattlefieldRegion result)
		{
			if (_regionsByCardTypeAndOwnerId.TryGetValue(GetRelevantRegionType(card), out var value) && value.TryGetValue(card.Controller.InstanceId, out var value2))
			{
				result = value2;
				return true;
			}
			result = null;
			return false;
		}

		private CardType GetRelevantRegionType(MtgCardInstance card)
		{
			if (card.CardTypes.Contains(CardType.Creature))
			{
				return CardType.Creature;
			}
			if (card.CardTypes.Contains(CardType.Planeswalker) || card.Subtypes.Contains(SubType.Saga) || card.Subtypes.Contains(SubType.Class) || card.Subtypes.Contains(SubType.Case) || card.Subtypes.Contains(SubType.Room))
			{
				return CardType.Planeswalker;
			}
			if (card.CardTypes.Contains(CardType.Land))
			{
				return CardType.Land;
			}
			return CardType.Artifact;
		}

		private IReadOnlyDictionary<uint, float> CollectAttackerPositions(BattlefieldRegion attackerRegion, List<CardLayoutData> layoutDatas)
		{
			_attackerPositions.Clear();
			foreach (BattlefieldStack stack in attackerRegion.Stacks)
			{
				foreach (DuelScene_CDC allCard in stack.AllCards)
				{
					if (!allCard)
					{
						continue;
					}
					foreach (CardLayoutData layoutData in layoutDatas)
					{
						if (!(layoutData.Card != allCard))
						{
							_attackerPositions[layoutData.Card.InstanceId] = layoutData.Position.x;
							break;
						}
					}
				}
			}
			return _attackerPositions;
		}

		private void GenerateRegionData(BattlefieldRegion region, List<CardLayoutData> layoutData, bool calcRegionVariant)
		{
			float y = _battlefield.transform.position.y;
			region.GenerateData(layoutData, (region.Opponent ? 1f : (-1f)) * _battlefield.DeclaredAttackOffset, _battlefield.TapRotation, _pendingStackCounters, y, _takenRects, calcRegionVariant);
			if (region.LayoutVariant != null && region.LayoutVariant.UsesPaging && region.Stacks.Count > 0 && (region.PagedOutLeftCount > 0 || region.PagedOutRightCount > 0))
			{
				if (region.LeftPagingButton == null)
				{
					region.LeftPagingButton = _unityPool.PopObject(_battlefield.LeftPagingButton.gameObject).GetComponent<RegionPagingButton>();
					region.LeftPagingButton.transform.SetParent(_battlefield.transform);
				}
				region.LeftPagingButton.SetCount(region.PagedOutLeftCount);
				region.LeftPagingButton.SetPosition(region.LeftArrowButtonRect, y);
				region.LeftPagingButton.SetCallback(() =>
				{
					region.PageLeft();
					_battlefield.LayoutNow();
				});
				if (region.RightPagingButton == null)
				{
					region.RightPagingButton = _unityPool.PopObject(_battlefield.RightPagingButton.gameObject).GetComponent<RegionPagingButton>();
					region.RightPagingButton.transform.SetParent(_battlefield.transform);
				}
				region.RightPagingButton.SetCount(region.PagedOutRightCount);
				region.RightPagingButton.SetPosition(region.RightArrowButtonRect, y);
				region.RightPagingButton.SetCallback(() =>
				{
					region.PageRight();
					_battlefield.LayoutNow();
				});
			}
			else
			{
				if (region.LeftPagingButton != null)
				{
					_unityPool.PushObject(region.LeftPagingButton.gameObject);
					region.LeftPagingButton = null;
				}
				if (region.RightPagingButton != null)
				{
					_unityPool.PushObject(region.RightPagingButton.gameObject);
					region.RightPagingButton = null;
				}
			}
		}

		private void CleanupArrowsAndCounters()
		{
			foreach (BattlefieldRegion region in Regions)
			{
				if (region.LeftPagingButton != null)
				{
					_unityPool.PushObject(region.LeftPagingButton.gameObject);
					region.LeftPagingButton = null;
				}
				if (region.RightPagingButton != null)
				{
					_unityPool.PushObject(region.RightPagingButton.gameObject);
					region.RightPagingButton = null;
				}
			}
			while (ActiveStackCounters.Count > 0)
			{
				CdcStackCounterView cdcStackCounterView = ActiveStackCounters[0];
				ActiveStackCounters.RemoveAt(0);
				cdcStackCounterView.Cleanup();
				_unityPool.PushObject(cdcStackCounterView.gameObject);
			}
		}
	}

	public class BattlefieldStack : IBattlefieldStack
	{
		private Vector3 _cardSize = new Vector3(4.17f, 3.5f, 0.26f);

		private readonly ICardDatabaseAdapter _cardDatabase;

		private readonly IGameStateProvider _gameStateProvider;

		private readonly IWorkflowProvider _workflowProvider;

		private readonly ICardViewProvider _cardViewProvider;

		private readonly IEqualityComparer<DuelScene_CDC> _canStackComparer;

		private readonly List<DuelScene_CDC> _tmpCardList = new List<DuelScene_CDC>(10);

		private ActionsAvailableWorkflow _tmpActionsAvail;

		public List<DuelScene_CDC> AllCards { get; private set; } = new List<DuelScene_CDC>();

		public DuelScene_CDC StackParent { get; private set; }

		public List<DuelScene_CDC> StackedCards { get; private set; } = new List<DuelScene_CDC>();

		public ICardDataAdapter StackParentModel { get; private set; }

		public int CardCount => AllCards.Count;

		public int AttachmentCount { get; private set; }

		public int ExileCount { get; private set; }

		public bool HasAttachmentOrExile
		{
			get
			{
				if (AttachmentCount <= 0)
				{
					return ExileCount > 0;
				}
				return true;
			}
		}

		public bool IsAttackStack
		{
			get
			{
				if (StackParentModel.Instance.AttackState != AttackState.Declared)
				{
					return StackParentModel.Instance.AttackState == AttackState.Attacking;
				}
				return true;
			}
		}

		public bool IsBlockStack
		{
			get
			{
				if (StackParentModel.Instance.BlockState != BlockState.Declared)
				{
					return StackParentModel.Instance.BlockState == BlockState.Blocking;
				}
				return true;
			}
		}

		public uint Age
		{
			get
			{
				uint result = uint.MaxValue;
				if (0 < AttachmentCount || 0 < ExileCount)
				{
					result = StackParentModel.InstanceId;
				}
				else
				{
					DuelScene_CDC oldestCard = OldestCard;
					if ((object)oldestCard != null)
					{
						result = oldestCard.InstanceId;
					}
				}
				return result;
			}
		}

		public DuelScene_CDC OldestCard
		{
			get
			{
				DuelScene_CDC duelScene_CDC = null;
				foreach (DuelScene_CDC allCard in AllCards)
				{
					if (allCard.Model.ZoneType == ZoneType.Battlefield && (duelScene_CDC == null || duelScene_CDC.InstanceId > allCard.InstanceId))
					{
						duelScene_CDC = allCard;
					}
				}
				return duelScene_CDC;
			}
		}

		public DuelScene_CDC YoungestCard
		{
			get
			{
				DuelScene_CDC duelScene_CDC = null;
				foreach (DuelScene_CDC allCard in AllCards)
				{
					if (allCard.Model.ZoneType == ZoneType.Battlefield && (duelScene_CDC == null || duelScene_CDC.InstanceId < allCard.InstanceId))
					{
						duelScene_CDC = allCard;
					}
				}
				return duelScene_CDC;
			}
		}

		public BattlefieldStack(DuelScene_CDC parent, List<DuelScene_CDC> attachmentsAndExiles, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IWorkflowProvider workflowProvider, ICardViewProvider cardViewProvider, IEqualityComparer<DuelScene_CDC> canStackComparer)
		{
			_cardDatabase = cardDatabase;
			_gameStateProvider = gameStateProvider;
			_workflowProvider = workflowProvider;
			_cardViewProvider = cardViewProvider;
			_canStackComparer = canStackComparer;
			StackParent = parent;
			StackParentModel = parent.Model;
			if (attachmentsAndExiles != null)
			{
				StackedCards.AddRange(attachmentsAndExiles);
			}
			if (StackParent != null)
			{
				AllCards.Add(StackParent);
			}
			AllCards.AddRange(StackedCards);
			if (StackParent.IsDirty)
			{
				StackParent.ImmediateUpdate();
			}
			_cardSize = StackParent.Collider.size;
			InitializeAttachmentAndExileCounts();
		}

		public BattlefieldStack(ICardDataAdapter parentModel, List<DuelScene_CDC> attachmentsAndExiles, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IWorkflowProvider workflowProvider, ICardViewProvider cardViewProvider, IEqualityComparer<DuelScene_CDC> canStackComparer)
		{
			_cardDatabase = cardDatabase;
			_gameStateProvider = gameStateProvider;
			_workflowProvider = workflowProvider;
			_cardViewProvider = cardViewProvider;
			_canStackComparer = canStackComparer;
			StackParent = null;
			StackParentModel = parentModel;
			if (attachmentsAndExiles != null)
			{
				StackedCards.AddRange(attachmentsAndExiles);
				_cardSize = attachmentsAndExiles[0].Collider.size;
			}
			if (StackParent != null)
			{
				AllCards.Add(StackParent);
			}
			AllCards.AddRange(StackedCards);
			InitializeAttachmentAndExileCounts();
		}

		public void PushCardOnStack(DuelScene_CDC card)
		{
			if (!(card == null))
			{
				if (StackParent != null)
				{
					StackedCards.Insert(0, StackParent);
				}
				StackParent = card;
				StackParentModel = card.Model;
				AllCards.Insert(0, card);
			}
		}

		public bool CanStack(DuelScene_CDC card)
		{
			if (AttachmentCount > 0 || ExileCount > 0)
			{
				return false;
			}
			return _canStackComparer.Equals(StackParent, card);
		}

		private void Sort()
		{
			ActionsAvailableWorkflow result;
			if (AttachmentCount > 0 || ExileCount > 0)
			{
				_tmpCardList.Clear();
				DuelScene_CDC stackParent = StackParent;
				ICardDataAdapter stackParentModel = StackParentModel;
				MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
				addCards(AttachmentAndExileStackGroupData.GenerateGroups(mtgGameState.GetCardById(stackParentModel.InstanceId), mtgGameState, _cardViewProvider, setParent: false), _tmpCardList);
				if ((bool)stackParent && !_tmpCardList.Contains(stackParent))
				{
					_tmpCardList.Insert(0, stackParent);
				}
				if (_tmpCardList.Count > 0)
				{
					AllCards.Sort((DuelScene_CDC x, DuelScene_CDC y) => _tmpCardList.IndexOf(x).CompareTo(_tmpCardList.IndexOf(y)));
					_tmpCardList.Clear();
				}
			}
			else if (IsBlockStack)
			{
				AllCards.Sort(CompareDistinguishedObjects);
			}
			else if (TryGetActionsAvailableWorkflow(_workflowProvider.GetCurrentWorkflow(), out result))
			{
				_tmpActionsAvail = result;
				AllCards.Sort(CompareAvailableActions);
			}
			else
			{
				AllCards.Sort(CompareDistinguishedObjects);
			}
			if (AttachmentCount <= 0 && ExileCount <= 0)
			{
				StackParent = AllCards[0];
				StackParentModel = StackParent.Model;
			}
			StackedCards.Clear();
			StackedCards.AddRange(AllCards);
			StackedCards.Remove(StackParent);
			static void addCards(List<AttachmentAndExileStackGroupData> groupList, List<DuelScene_CDC> cards)
			{
				foreach (AttachmentAndExileStackGroupData group in groupList)
				{
					cards.AddRange(group.Cards);
					if (group.Children != null)
					{
						addCards(group.Children, cards);
					}
				}
			}
		}

		public float CalcWidth(float cardScale, float maxWidth, float minWidth)
		{
			float num = _cardSize.x * cardScale;
			if (CardCount != 1)
			{
				return num * maxWidth;
			}
			return num * minWidth;
		}

		public float CalcHeight(float cardScale)
		{
			return _cardSize.y * cardScale;
		}

		public Vector3 GetCopyCounterPosition(float cardScale, float maxWidth, float minWidth, int maxStacked)
		{
			return GetCounterPosition(cardScale, maxWidth, minWidth, maxStacked);
		}

		public Vector3 GetAttachmentCounterPosition(float cardScale, float maxWidth, float minWidth, int maxStacked)
		{
			return GetCounterPosition(cardScale, maxWidth, minWidth, maxStacked);
		}

		public Vector3 GetExileCounterPosition(float cardScale, float maxWidth, float minWidth, int maxStacked)
		{
			return GetCounterPosition(cardScale, maxWidth, minWidth, maxStacked, isExileCounter: true);
		}

		private Vector3 GetCounterPosition(float cardScale, float maxWidth, float minWidth, int maxStacked, bool isExileCounter = false)
		{
			float num = CalcWidth(cardScale, maxWidth, minWidth);
			float num2 = CalcHeight(cardScale);
			float num3 = _cardSize.z * cardScale;
			Vector3 result = new Vector3(num * 0.5f - 0.5f, ((float)Mathf.Min(CardCount, maxStacked) + 4f) * num3, (0f - num2) * 0.5f);
			switch (StackParentModel.OwnerNum)
			{
			case GREPlayerNum.LocalPlayer:
				result.z += 0.5f;
				break;
			case GREPlayerNum.Opponent:
				result.z++;
				break;
			}
			if (isExileCounter)
			{
				result.z++;
			}
			return result;
		}

		public List<CardLayoutData> GenerateData(float cardScale, float maxWidth, float minWidth, int maxStacked)
		{
			List<CardLayoutData> list = new List<CardLayoutData>();
			Sort();
			float num = CalcWidth(cardScale, maxWidth, minWidth);
			Vector3 vector = Vector3.one * cardScale;
			Vector3 vector2 = _cardSize * cardScale;
			int num2 = Mathf.Min(CardCount, maxStacked);
			float num3 = ((num2 > 1) ? ((num - vector2.x) / (float)(num2 - 1)) : 0f);
			Vector3 vector3 = new Vector3((IsBlockStack && AttachmentCount == 0 && ExileCount == 0) ? (num * 0.5f - vector2.x * 0.5f) : (0f - num * 0.5f + vector2.x * 0.5f), vector2.z * (float)(num2 - 1), 0f);
			Vector3 vector4 = new Vector3((IsBlockStack && AttachmentCount == 0 && ExileCount == 0) ? (0f - num3) : num3, 0f - vector2.z, 0f);
			if (StackParent != null)
			{
				list.Add(new CardLayoutData(StackParent, vector3, Quaternion.identity, vector));
				if (num2 > 1)
				{
					float num4 = (StackParent.Collider.size.x - StackedCards[0].Collider.size.x) * cardScale / 2f;
					vector3.x += ((IsBlockStack && AttachmentCount == 0 && ExileCount == 0) ? (0f - num4) : num4);
				}
			}
			else
			{
				num2++;
			}
			int i;
			for (i = 0; i < num2 - 1; i++)
			{
				vector3 += vector4;
				Vector3 pos = vector3;
				Vector3 vector5 = vector;
				if (StackedCards[i].Model.Zone.Type == ZoneType.Exile)
				{
					Vector3 a = new Vector3(0f, 0f, 1.25f);
					float num5 = 0.6f;
					vector5 *= num5;
					float num6 = (vector2.x - vector2.x * num5) * 0.5f;
					pos.x += num6;
					pos += Vector3.Scale(a, vector5);
				}
				list.Add(new CardLayoutData(StackedCards[i], pos, Quaternion.identity, vector5));
			}
			vector3.y = -5f;
			for (; i < StackedCards.Count; i++)
			{
				list.Add(new CardLayoutData(StackedCards[i], vector3, Quaternion.identity, vector, isVisibleInLayout: false));
			}
			return list;
		}

		public void RefreshAbilitiesBasedOnStackPosition()
		{
			float value = 0f;
			if ((bool)StackParent && StackParent.Model != null)
			{
				value = StackParent.UpdateAbilityVisuals<PersistVFX, PersistSfx>(display: true, animate: true, null);
				StackParent.UpdateTopCardRelevantVisuals(display: true);
				StackParent.UpdateCounterVisibility(display: true);
			}
			foreach (DuelScene_CDC stackedCard in StackedCards)
			{
				if ((bool)stackedCard && stackedCard.Model != null)
				{
					stackedCard.UpdateAbilityVisuals<PersistVFX, PersistSfx>(display: false, !HasAttachmentOrExile, value);
					stackedCard.UpdateTopCardRelevantVisuals(display: false);
					stackedCard.UpdateCounterVisibility(AttachmentCount > 0 || ExileCount > 0);
				}
			}
		}

		public override string ToString()
		{
			return string.Format("Stack: {0} {1}\n   Parent: {2}\n      Card: {3}", _cardDatabase.GreLocProvider.GetLocalizedText(StackParentModel.TitleId), (CardCount > 1) ? $"({CardCount} cards)" : string.Empty, (StackParent != null) ? StackParent.name : "NULL", string.Join("\n      Card: ", StackedCards.ConvertAll((DuelScene_CDC x) => x.name).ToArray()));
		}

		private void InitializeAttachmentAndExileCounts()
		{
			int num = 0;
			int num2 = 0;
			foreach (DuelScene_CDC stackedCard in StackedCards)
			{
				if ((object)stackedCard == null || stackedCard.Model?.Instance?.AttachedToId != 0)
				{
					if (stackedCard.Model.Instance.Zone.Type == ZoneType.Exile)
					{
						num2++;
					}
					else
					{
						num++;
					}
				}
			}
			AttachmentCount = num;
			ExileCount = num2;
		}

		private bool TryGetActionsAvailableWorkflow(WorkflowBase workflow, out ActionsAvailableWorkflow result)
		{
			if (workflow is ActionsAvailableWorkflow actionsAvailableWorkflow)
			{
				result = actionsAvailableWorkflow;
				return true;
			}
			if (workflow is IParentWorkflow parentWorkflow)
			{
				foreach (WorkflowBase childWorkflow in parentWorkflow.ChildWorkflows)
				{
					if (TryGetActionsAvailableWorkflow(childWorkflow, out result))
					{
						return true;
					}
				}
			}
			result = null;
			return false;
		}

		private int CompareAge(DuelScene_CDC cardViewA, DuelScene_CDC cardViewB)
		{
			return cardViewA.InstanceId.CompareTo(cardViewB.InstanceId);
		}

		private int CompareAvailableActions(DuelScene_CDC cardViewA, DuelScene_CDC cardViewB)
		{
			int num = 0;
			if (_tmpActionsAvail != null)
			{
				bool flag = _tmpActionsAvail.GetActionsForId(cardViewA.InstanceId).Exists((GreInteraction x) => x.IsActive);
				bool value = _tmpActionsAvail.GetActionsForId(cardViewB.InstanceId).Exists((GreInteraction x) => x.IsActive);
				num = -flag.CompareTo(value);
				_tmpActionsAvail = null;
			}
			if (num == 0)
			{
				return CompareDistinguishedObjects(cardViewA, cardViewB);
			}
			return num;
		}

		private int CompareDistinguishedObjects(DuelScene_CDC cardViewA, DuelScene_CDC cardViewB)
		{
			int num = 0;
			bool value = (cardViewA?.Model?.Instance?.DistinguishedByIds.Count).GetValueOrDefault() > 0;
			num = ((cardViewB?.Model?.Instance?.DistinguishedByIds.Count).GetValueOrDefault() > 0).CompareTo(value);
			if (num == 0)
			{
				num = CompareAge(cardViewA, cardViewB);
			}
			return num;
		}
	}

	public static class DebugDraw
	{
		public static void Square(Rect rect, UnityEngine.Color color, float duration = 0.1f)
		{
			Debug.DrawLine(new Vector3(rect.xMin, 0.1f, rect.yMin), new Vector3(rect.xMin, 0.1f, rect.yMax), color, duration);
			Debug.DrawLine(new Vector3(rect.xMin, 0.1f, rect.yMax), new Vector3(rect.xMax, 0.1f, rect.yMax), color, duration);
			Debug.DrawLine(new Vector3(rect.xMax, 0.1f, rect.yMax), new Vector3(rect.xMax, 0.1f, rect.yMin), color, duration);
			Debug.DrawLine(new Vector3(rect.xMax, 0.1f, rect.yMin), new Vector3(rect.xMin, 0.1f, rect.yMin), color, duration);
		}
	}

	public static class GizmosDraw
	{
		public static void Square(Rect rect, UnityEngine.Color color)
		{
			Gizmos.color = color;
			Gizmos.DrawLine(new Vector3(rect.xMin, 0.1f, rect.yMin), new Vector3(rect.xMin, 0.1f, rect.yMax));
			Gizmos.DrawLine(new Vector3(rect.xMin, 0.1f, rect.yMax), new Vector3(rect.xMax, 0.1f, rect.yMax));
			Gizmos.DrawLine(new Vector3(rect.xMax, 0.1f, rect.yMax), new Vector3(rect.xMax, 0.1f, rect.yMin));
			Gizmos.DrawLine(new Vector3(rect.xMax, 0.1f, rect.yMin), new Vector3(rect.xMin, 0.1f, rect.yMin));
		}
	}

	[Space(10f)]
	[SerializeField]
	private CdcStackCounterView StackCounterPrefab;

	[SerializeField]
	private RegionPagingButton _leftPagingButtonPrefab;

	[SerializeField]
	private RegionPagingButton _rightPagingButtonPrefab;

	public BattlefieldRegionDefinition LocalCreatureRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition LocalLandRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition LocalArtifactRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition LocalPlaneswalkerRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition OpponentCreatureRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition OpponentLandRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition OpponentArtifactRegion = new BattlefieldRegionDefinition();

	public BattlefieldRegionDefinition OpponentPlaneswalkerRegion = new BattlefieldRegionDefinition();

	private IObjectPool _objectPool = NullObjectPool.Default;

	private readonly List<PlayerBattlefieldRegions> _allRegions = new List<PlayerBattlefieldRegions>();

	public Vector3 DeclaredAttackOffset = new Vector3(0f, 0.1f, 0.1f);

	public Vector3 TapRotation = new Vector3(0f, 0f, -8f);

	private Phase _lastActivePhase;

	protected BattlefieldLayout _battlefieldLayout;

	private readonly HashSet<IBattlefieldStack> _handledEtbStacks = new HashSet<IBattlefieldStack>();

	private readonly HashSet<IBattlefieldStack> _stacksToRefreshInLateUpdate = new HashSet<IBattlefieldStack>();

	public CdcStackCounterView CounterPrefab => StackCounterPrefab;

	public RegionPagingButton LeftPagingButton => _leftPagingButtonPrefab;

	public RegionPagingButton RightPagingButton => _rightPagingButtonPrefab;

	public bool LayoutLocked { get; set; }

	public Transform Transform => transform;

	public List<CardLayoutData> PreviousLayoutData => _previousLayoutData;

	protected override void LateUpdate()
	{
		if (_stacksToRefreshInLateUpdate.Count > 0)
		{
			foreach (IBattlefieldStack item in _stacksToRefreshInLateUpdate)
			{
				item?.RefreshAbilitiesBasedOnStackPosition();
			}
			_stacksToRefreshInLateUpdate.Clear();
		}
		if (LayoutLocked)
		{
			return;
		}
		base.LateUpdate();
		if (Time.frameCount % 60 != 0)
		{
			return;
		}
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			if (region.HasPendingVariantCalculation)
			{
				LayoutNow();
				break;
			}
		}
	}

	public override void Init(GameManager gameManager, ICardViewManager cardViewManager, ISplineMovementSystem splineMovementSystem, CardViewBuilder cardViewBuilder, IClientLocProvider locMan, MatchManager matchManager)
	{
		base.Init(gameManager, cardViewManager, splineMovementSystem, cardViewBuilder, locMan, matchManager);
		IContext context = gameManager.Context;
		_objectPool = context.Get<IObjectPool>();
		IBattlefieldStackFactory stackFactory = new BattlefieldStackFactory(context, new CanStackComparer(context, gameManager.ReferenceMapAggregate, gameManager.UIManager));
		Layout = (_battlefieldLayout = new BattlefieldLayout(this, stackFactory, context, gameManager.InteractionSystem, gameManager.NpeDirector));
		if (_cardViewBuilder != null)
		{
			_cardViewBuilder.onCardUpdated += OnCardUpdated;
		}
	}

	public void RegisterPlayer(MtgPlayer player)
	{
		uint instanceId = player.InstanceId;
		bool isLocalPlayer = player.IsLocalPlayer;
		BattlefieldRegionDefinition battlefieldRegionDefinition = (isLocalPlayer ? LocalCreatureRegion : OpponentCreatureRegion);
		BattlefieldRegionDefinition battlefieldRegionDefinition2 = (isLocalPlayer ? LocalArtifactRegion : OpponentArtifactRegion);
		BattlefieldRegionDefinition battlefieldRegionDefinition3 = (isLocalPlayer ? LocalLandRegion : OpponentLandRegion);
		BattlefieldRegionDefinition battlefieldRegionDefinition4 = (isLocalPlayer ? LocalPlaneswalkerRegion : OpponentPlaneswalkerRegion);
		PlayerBattlefieldRegions playerBattlefieldRegions = new PlayerBattlefieldRegions(instanceId, isLocalPlayer, new BattlefieldRegion(battlefieldRegionDefinition, battlefieldRegionDefinition.CreateInstancedValidator(instanceId), $"Player {instanceId} Creatures:", !isLocalPlayer, this, _objectPool), new BattlefieldRegion(battlefieldRegionDefinition2, battlefieldRegionDefinition2.CreateInstancedValidator(instanceId), $"Player {instanceId} Artifacts:", !isLocalPlayer, this, _objectPool), new BattlefieldRegion(battlefieldRegionDefinition3, battlefieldRegionDefinition3.CreateInstancedValidator(instanceId), $"Player {instanceId} Lands:", !isLocalPlayer, this, _objectPool), new BattlefieldRegion(battlefieldRegionDefinition4, battlefieldRegionDefinition4.CreateInstancedValidator(instanceId), $"Player {instanceId} Planeswalkers:", !isLocalPlayer, this, _objectPool));
		_battlefieldLayout.RegisterPlayer(playerBattlefieldRegions);
		_allRegions.Add(playerBattlefieldRegions);
		LayoutNow();
	}

	protected override void OnDestroy()
	{
		_allRegions.Clear();
		_battlefieldLayout.Cleanup();
		if (_cardViewBuilder != null)
		{
			_cardViewBuilder.onCardUpdated -= OnCardUpdated;
		}
		base.OnDestroy();
	}

	protected override void HandleAddedCard(DuelScene_CDC cardView)
	{
		foreach (CDCPart value in cardView.ActiveParts.Values)
		{
			value.OnPhaseUpdate(_lastActivePhase);
		}
		base.HandleAddedCard(cardView);
	}

	public override void RemoveCard(DuelScene_CDC cardView)
	{
		if (!_cardViews.Contains(cardView))
		{
			return;
		}
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			region.RemoveCard(cardView);
		}
		base.RemoveCard(cardView);
		cardView.UpdateAbilityVisuals<PersistVFX, PersistSfx>(display: false, animate: false, null);
		cardView.UpdateTopCardRelevantVisuals(display: true);
		cardView.UpdateCounterVisibility(display: true);
	}

	public void UpdateForPhase(Phase phase, Step step)
	{
		if (_lastActivePhase == phase)
		{
			return;
		}
		_lastActivePhase = phase;
		if (_battlefieldLayout == null || _battlefieldLayout.Regions == null)
		{
			return;
		}
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			if (region.Stacks == null)
			{
				continue;
			}
			foreach (BattlefieldStack stack in region.Stacks)
			{
				stack.RefreshAbilitiesBasedOnStackPosition();
				if (stack.AllCards == null)
				{
					continue;
				}
				foreach (DuelScene_CDC allCard in stack.AllCards)
				{
					if (allCard == null || allCard.ActiveParts == null)
					{
						continue;
					}
					foreach (CDCPart value in allCard.ActiveParts.Values)
					{
						value.OnPhaseUpdate(phase);
					}
				}
			}
		}
	}

	public IBattlefieldStack GetStackForInstanceId(uint id)
	{
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			foreach (BattlefieldStack stack in region.Stacks)
			{
				foreach (DuelScene_CDC allCard in stack.AllCards)
				{
					if (allCard.InstanceId == id)
					{
						return stack;
					}
				}
			}
		}
		return null;
	}

	public IBattlefieldStack GetStackForCard(DuelScene_CDC card)
	{
		if (card == null)
		{
			return null;
		}
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			foreach (BattlefieldStack stack in region.Stacks)
			{
				if (stack.AllCards.Contains(card))
				{
					return stack;
				}
			}
		}
		return null;
	}

	public BattlefieldRegion GetRegionForCard(DuelScene_CDC card)
	{
		if (card == null)
		{
			return null;
		}
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			foreach (BattlefieldStack stack in region.Stacks)
			{
				if (stack.AllCards.Contains(card))
				{
					return region;
				}
			}
		}
		return null;
	}

	public Transform GetRegionTransformForCardType(CardType cardType, GREPlayerNum grePlayerNum)
	{
		return GetRegionForCardType(cardType, grePlayerNum)?.RegionLocator;
	}

	private BattlefieldRegion GetRegionForCardType(CardType cardType, GREPlayerNum grePlayerNum)
	{
		if (!_battlefieldLayout.RegionsByCardTypeAndOwner.TryGetValue(cardType, out var value))
		{
			return null;
		}
		if (!value.TryGetValue(grePlayerNum, out var value2))
		{
			return null;
		}
		return value2;
	}

	public Transform GetRegionTransformForCardType(CardType cardType, uint ownerId)
	{
		return GetRegionForCardType(cardType, ownerId)?.RegionLocator;
	}

	private BattlefieldRegion GetRegionForCardType(CardType cardType, uint ownerId)
	{
		if (!_battlefieldLayout.RegionsByCardTypeAndOwnerId.TryGetValue(cardType, out var value))
		{
			return null;
		}
		if (!value.TryGetValue(ownerId, out var value2))
		{
			return null;
		}
		return value2;
	}

	public BattlefieldRegion GetCurrentlyHoveredRegion()
	{
		Vector2 pointerPosition = InputHelper.GetPointerPosition();
		if (Physics.Raycast(CurrentCamera.Value.ScreenPointToRay(pointerPosition), out var hitInfo))
		{
			Vector3 point = hitInfo.point;
			if (point.y < 2f)
			{
				Vector2 point2 = new Vector2(point.x, point.z);
				foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
				{
					if (region.LayoutVariant == null || !region.LayoutVariant.Bounds.Contains(point2))
					{
						continue;
					}
					Collider collider = hitInfo.collider;
					foreach (BattlefieldStack stack in region.Stacks)
					{
						_ = stack;
						foreach (DuelScene_CDC allCard in region.AllCards)
						{
							if (collider == allCard.Collider)
							{
								return region;
							}
						}
					}
				}
			}
		}
		return null;
	}

	public bool CardIsStackParent(DuelScene_CDC card)
	{
		if (card == null)
		{
			return false;
		}
		IBattlefieldStack stackForCard = GetStackForCard(card);
		if (stackForCard == null)
		{
			return false;
		}
		return stackForCard.StackParent == card;
	}

	public void RefreshAbilitiesForCardsStack(DuelScene_CDC card)
	{
		GetStackForCard(card)?.RefreshAbilitiesBasedOnStackPosition();
	}

	protected override void LayoutNowInternal(List<DuelScene_CDC> cardsToLayout, bool layoutInstantly = false)
	{
		_handledEtbStacks.Clear();
		DuelScene_CDC duelScene_CDC = _newlyAddedCards.FirstOrDefault();
		base.LayoutNowInternal(cardsToLayout, layoutInstantly);
		if (duelScene_CDC != null)
		{
			BattlefieldRegion battlefieldRegion = _battlefieldLayout.Regions.Find(duelScene_CDC, (BattlefieldRegion x, DuelScene_CDC card) => x.ContainsCardView(card));
			if (battlefieldRegion != null)
			{
				while (!battlefieldRegion.IsInVisibleStack(duelScene_CDC))
				{
					if (battlefieldRegion.IsPagedLeft(duelScene_CDC))
					{
						battlefieldRegion.PageLeft();
					}
					else if (battlefieldRegion.IsPagedRight(duelScene_CDC))
					{
						battlefieldRegion.PageRight();
					}
					base.LayoutNowInternal(cardsToLayout, layoutInstantly);
				}
			}
		}
		foreach (BattlefieldRegion region in _battlefieldLayout.Regions)
		{
			foreach (BattlefieldStack stack in region.Stacks)
			{
				stack.RefreshAbilitiesBasedOnStackPosition();
			}
		}
	}

	public override IdealPoint GetLayoutEndpoint(CardLayoutData data)
	{
		return new IdealPoint(data.Position, transform.rotation * data.Rotation, data.Scale);
	}

	protected override string GetInternalLayoutSplinePath(CardLayoutData data)
	{
		string text = base.GetInternalLayoutSplinePath(data);
		if (string.IsNullOrEmpty(text) && _battlefieldLayout.CardRegionChanges.ContainsKey(data.Card))
		{
			KeyValuePair<BattlefieldRegion, BattlefieldRegion> keyValuePair = _battlefieldLayout.CardRegionChanges[data.Card];
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(data.Card.Model);
			_assetLookupSystem.Blackboard.CardHolderType = CardHolderType.Battlefield;
			_assetLookupSystem.Blackboard.RegionPair = new RegionPair(keyValuePair.Key.RegionDef.Type, keyValuePair.Key.RegionDef.Owner, keyValuePair.Value.RegionDef.Type, keyValuePair.Value.RegionDef.Owner);
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<InternalMovementPayload_Spline> loadedTree))
			{
				InternalMovementPayload_Spline payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					text = payload.SplineDataRef.RelativePath;
				}
			}
			_assetLookupSystem.Blackboard.Clear();
		}
		return text;
	}

	protected override SplineEventData GetInternalLayoutSplineEvents(CardLayoutData data)
	{
		SplineEventData splineEvents = base.GetInternalLayoutSplineEvents(data);
		CardHolderBase.SetupSplineEventsALT<InternalMovementPayload_VFX, InternalMovementPayload_SFX>(data, added: false, ref splineEvents, GetCardPosition(data.Card), CardViewUtilities.GetFromToZoneForCard(data.Card, added: false), _gameManager);
		if (_battlefieldLayout.CardRegionChanges.ContainsKey(data.Card))
		{
			KeyValuePair<BattlefieldRegion, BattlefieldRegion> keyValuePair = _battlefieldLayout.CardRegionChanges[data.Card];
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(data.Card.Model);
			_assetLookupSystem.Blackboard.CardHolderType = CardHolderType.Battlefield;
			_assetLookupSystem.Blackboard.RegionPair = new RegionPair(keyValuePair.Key.RegionDef.Type, keyValuePair.Key.RegionDef.Owner, keyValuePair.Value.RegionDef.Type, keyValuePair.Value.RegionDef.Owner);
			AssetLookupTree<RegionSFX> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<RegionSFX>();
			AssetLookupTree<RegionVFX> assetLookupTree2 = _assetLookupSystem.TreeLoader.LoadTree<RegionVFX>();
			RegionSFX payload = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
			RegionVFX payload2 = assetLookupTree2.GetPayload(_assetLookupSystem.Blackboard);
			if (payload2 != null)
			{
				foreach (VfxData vfxData in payload2.VfxDatas)
				{
					float time = Mathf.Clamp01(vfxData.PrefabData.StartTime);
					if (payload != null)
					{
						splineEvents.Events.Add(new SplineEventAudio(time, payload.SfxData.AudioEvents, data.CardGameObject));
					}
					if (vfxData.PrefabData == null)
					{
						continue;
					}
					DuelScene_CDC cacheCard = data.Card;
					VfxData cachedVfxData = vfxData;
					splineEvents.Events.Add(new SplineEventCallback(time, (float _) =>
					{
						if (!(cacheCard == null) && !(cacheCard.EffectsRoot == null))
						{
							_vfxProvider.PlayVFX(cachedVfxData, cacheCard.Model);
						}
					}));
				}
				if (CardViews != null && data.Card.Model.Instance != null && data.Card.Model.Instance.AttachedToId != 0)
				{
					splineEvents.Events.Add(new SplineEventCallback(1f, (float _) =>
					{
						CardViews.FirstOrDefault((DuelScene_CDC x) => x.InstanceId == data.Card.Model.Instance.AttachedToId)?.PlayReactionAnimation(CardReactionEnum.Attachment);
					}));
				}
			}
		}
		return splineEvents;
	}

	private void PlayEtbTriggerVFX(SplineEventData events, IBattlefieldStack battlefieldStack)
	{
		foreach (DuelScene_CDC cardView in CardViews)
		{
			_vfxProvider.GenerateEtbTriggerEvents(cardView, events.Events, battlefieldStack, cardView.EffectsRoot);
		}
	}

	protected override SplineEventData GetLayoutSplineEvents(CardLayoutData data)
	{
		SplineEventData layoutSplineEvents = base.GetLayoutSplineEvents(data);
		if (data == null || data.Card == null || data.CardGameObject == null)
		{
			Debug.LogErrorFormat("Null card passed into BattlefieldCardHolder.GetLayoutSplineEvents()!");
			return layoutSplineEvents;
		}
		IBattlefieldStack stackForCard = GetStackForCard(data.Card);
		bool num = data.Card.Model.Zone.Type == ZoneType.Battlefield && data.Card.PreviousCardHolder.CardHolderType != CardHolderType.Hand;
		bool flag = _gameManager.ViewManager.GetCardPreviousId(data.Card.InstanceId) != data.Card.InstanceId && data.Card.CurrentCardHolder.CardHolderType == CardHolderType.Battlefield && data.Card.PreviousCardHolder.CardHolderType == CardHolderType.Hand;
		bool flag2 = data.Card.Model.Zone.Type == ZoneType.Exile && data.Card.CurrentCardHolder.CardHolderType == CardHolderType.Battlefield;
		if (num | flag)
		{
			PlayEtbTriggerVFX(layoutSplineEvents, stackForCard);
			layoutSplineEvents.Events.AddRange(_vfxProvider.GenerateEtbSplineEvents(data.Card, stackForCard, !_handledEtbStacks.Contains(stackForCard), data.Card.EffectsRoot));
			layoutSplineEvents.Events.Add(new SplineEventCallbackWithParams<IReadOnlyCollection<DuelScene_CDC>>(1f, _cardViews, (float _, IReadOnlyCollection<DuelScene_CDC> cdcList) =>
			{
				foreach (DuelScene_CDC cdc in cdcList)
				{
					cdc.PlayReactionAnimation(CardReactionEnum.ETB);
				}
			}));
		}
		else if (flag2)
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_basicloc_exile, data.Card.gameObject);
		}
		_handledEtbStacks.Add(stackForCard);
		return layoutSplineEvents;
	}

	private void OnCardUpdated(BASE_CDC cardView)
	{
		if (cardView.Model != null && cardView.Model.Zone != null && cardView.Model.Zone.Type == ZoneType.Battlefield && cardView is DuelScene_CDC card && GetStackForCard(card) is BattlefieldStack item)
		{
			_stacksToRefreshInLateUpdate.Add(item);
		}
	}

	protected override void OnDrawGizmos()
	{
		base.OnDrawGizmos();
		foreach (PlayerBattlefieldRegions allRegion in _allRegions)
		{
			allRegion.DrawGizmos();
		}
	}
}
