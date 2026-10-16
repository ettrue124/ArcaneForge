using System;
using System.Collections.Generic;
using ReferenceMap;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class MtgGameState
{
	public enum StateType
	{
		None,
		InitialGameState
	}

	public readonly Map ReferenceMap = new Map();

	public uint Id;

	public uint PrevId;

	public GameInfo GameInfo;

	public MtgGameLossData GameLossData;

	public StateType Type;

	public Dictionary<uint, MtgCardInstance> AllCards = new Dictionary<uint, MtgCardInstance>();

	public Dictionary<uint, MtgCardInstance> VisibleCards = new Dictionary<uint, MtgCardInstance>();

	public Dictionary<uint, MtgCardInstance> AbsorbedFacetCards = new Dictionary<uint, MtgCardInstance>();

	public Dictionary<uint, MtgCardInstance> RevealedCards = new Dictionary<uint, MtgCardInstance>();

	public HashSet<uint> ObjectIds = new HashSet<uint>();

	public Dictionary<uint, MtgZone> Zones = new Dictionary<uint, MtgZone> { 
	{
		12u,
		PhasedOutZone()
	} };

	public Dictionary<uint, MtgTeam> Teams = new Dictionary<uint, MtgTeam>();

	public IReadOnlyList<MtgPlayer> Players = Array.Empty<MtgPlayer>();

	public Dictionary<uint, MtgTimer> Timers = new Dictionary<uint, MtgTimer>();

	public List<TargetSpec> TargetInfo = new List<TargetSpec>();

	public Dictionary<uint, BlockInfo> BlockInfo = new Dictionary<uint, BlockInfo>();

	public Dictionary<uint, AttackInfo> AttackInfo = new Dictionary<uint, AttackInfo>();

	public List<uint> RemainingSelections = new List<uint>();

	public List<ActionInfo> Actions = new List<ActionInfo>();

	public List<SuspendLikeData> SuspendLikeInfo = new List<SuspendLikeData>();

	public Dictionary<uint, List<ReplacementEffectData>> ReplacementEffects = new Dictionary<uint, List<ReplacementEffectData>>();

	public readonly Dictionary<uint, List<MtgCardInstance>> BattlefieldPermanentsByControllerId = new Dictionary<uint, List<MtgCardInstance>>();

	public Dictionary<uint, MtgAbilityInstance> AbilityInstances = new Dictionary<uint, MtgAbilityInstance>();

	public Dictionary<uint, List<MtgCardInstance>> ExiledCardsByControllerId = new Dictionary<uint, List<MtgCardInstance>>();

	public List<ChoosingAttachmentsInfo> ChoosingAttachmentsInfos = new List<ChoosingAttachmentsInfo>();

	public readonly List<QualificationData> Qualifications = new List<QualificationData>();

	public readonly List<MtgCardInstance> DelayedTriggers = new List<MtgCardInstance>();

	public readonly List<DelayedTriggerData> DelayedTriggerAffectees = new List<DelayedTriggerData>();

	public readonly List<DesignationData> Designations = new List<DesignationData>();

	public readonly List<LoopCountData> LoopCounts = new List<LoopCountData>();

	public readonly List<HighlightReason> HighlightReasons = new List<HighlightReason>();

	public HashSet<uint> SeenCardGrpIds = new HashSet<uint>();

	public Dictionary<uint, MtgCardInstance> TrackedHistoricCards = new Dictionary<uint, MtgCardInstance>();

	public Dictionary<uint, Dictionary<string, int>> PlayerToActiveAbilityWords = new Dictionary<uint, Dictionary<string, int>>();

	public GameStage Stage;

	public MtgCardInstance ResolvingCardInstance;

	public bool ResolutionStartedThisGameState;

	public uint GameWideTurn;

	public MtgPlayer ActivePlayer;

	public MtgPlayer DecidingPlayer;

	public Phase CurrentPhase;

	public Step CurrentStep;

	public Phase NextPhase;

	public Step NextStep;

	public List<ExtraTurn> ExtraTurns = new List<ExtraTurn>();

	public List<GenericRelationship> GenericRelationships = new List<GenericRelationship>();

	public IReadOnlyList<MtgCardInstance> LocalPlayerBattlefieldCards
	{
		get
		{
			if (!BattlefieldPermanentsByControllerId.TryGetValue(LocalPlayer.InstanceId, out var value))
			{
				return Array.Empty<MtgCardInstance>();
			}
			return value;
		}
	}

	public IReadOnlyList<MtgCardInstance> OpponentBattlefieldCards
	{
		get
		{
			if (!BattlefieldPermanentsByControllerId.TryGetValue(Opponent.InstanceId, out var value))
			{
				return Array.Empty<MtgCardInstance>();
			}
			return value;
		}
	}

	public IReadOnlyList<MtgCardInstance> LocalPlayerExileCards
	{
		get
		{
			if (!ExiledCardsByControllerId.TryGetValue(LocalPlayer.InstanceId, out var value))
			{
				return Array.Empty<MtgCardInstance>();
			}
			return value;
		}
	}

	public MtgPlayer LocalPlayer
	{
		get
		{
			foreach (MtgPlayer player in Players)
			{
				if (player.IsLocalPlayer)
				{
					return player;
				}
			}
			return new MtgPlayer();
		}
	}

	public MtgPlayer Opponent
	{
		get
		{
			foreach (MtgPlayer player in Players)
			{
				if (!player.IsLocalPlayer)
				{
					return player;
				}
			}
			return new MtgPlayer();
		}
	}

	public ClientMessageType LocalPlayerPendingMessageType
	{
		get
		{
			if (LocalPlayer == null)
			{
				return ClientMessageType.None;
			}
			return LocalPlayer.PendingMessageType;
		}
	}

	public ClientMessageType OpponentPendingMessageType
	{
		get
		{
			if (Opponent == null)
			{
				return ClientMessageType.None;
			}
			return Opponent.PendingMessageType;
		}
	}

	public MtgZone Stack => GetZone(ZoneType.Stack);

	public MtgZone Battlefield => GetZone(ZoneType.Battlefield);

	public MtgZone LocalHand => GetZone(ZoneType.Hand, LocalPlayer);

	public MtgZone OpponentHand => GetZone(ZoneType.Hand, Opponent);

	public MtgZone LocalGraveyard => GetZone(ZoneType.Graveyard, LocalPlayer);

	public MtgZone LocalLibrary => GetZone(ZoneType.Library, LocalPlayer);

	public MtgZone OpponentGraveyard => GetZone(ZoneType.Graveyard, Opponent);

	public MtgZone OpponentLibrary => GetZone(ZoneType.Library, Opponent);

	public MtgZone LocalSideboard => GetZone(ZoneType.Sideboard, LocalPlayer);

	public MtgZone Command => GetZone(ZoneType.Command);

	public MtgZone Limbo => GetZone(ZoneType.Limbo);

	public MtgZone Exile => GetZone(ZoneType.Exile);

	public MtgZone OpponentRevealed => GetZone(ZoneType.Revealed, GREPlayerNum.Opponent);

	public MtgZone PhasedOut => GetZone(ZoneType.PhasedOut, GREPlayerNum.Invalid);

	public bool IsMultiplayer
	{
		get
		{
			GameInfo gameInfo = GameInfo;
			if (gameInfo == null || gameInfo.Type != GameType.MultiPlayer)
			{
				return Players.Count > 2;
			}
			return true;
		}
	}

	public MtgZone GetZone(ZoneType type, GREPlayerNum ownerNum)
	{
		foreach (MtgZone value in Zones.Values)
		{
			if (value.OwnerNum == ownerNum && value.Type == type)
			{
				return value;
			}
		}
		return null;
	}

	public MtgZone GetZoneForPlayer(uint playerId, ZoneType zoneType)
	{
		foreach (KeyValuePair<uint, MtgZone> zone in Zones)
		{
			MtgZone value = zone.Value;
			if (value.Type == zoneType && value.Owner != null && value.Owner.InstanceId == playerId)
			{
				return value;
			}
		}
		return null;
	}

	public MtgZone GetZone(ZoneType type, MtgPlayer owner)
	{
		return GetZone(type, owner?.InstanceId ?? 0);
	}

	public MtgZone GetZone(ZoneType type, uint ownerId = 0u)
	{
		foreach (MtgZone value in Zones.Values)
		{
			if (((value.Owner != null) ? value.Owner.InstanceId : 0) == ownerId && value.Type == type)
			{
				return value;
			}
		}
		return null;
	}

	public MtgZone GetZoneById(uint zoneId)
	{
		if (Zones.TryGetValue(zoneId, out var value))
		{
			return value;
		}
		return null;
	}

	public ZoneType GetZoneTypeById(uint zoneId)
	{
		if (!Zones.TryGetValue(zoneId, out var value))
		{
			return ZoneType.None;
		}
		return value.Type;
	}

	public MtgCardInstance GetCardById(uint cardId)
	{
		TryGetCard(cardId, out var card);
		return card;
	}

	public bool TryGetCard(uint instanceId, out MtgCardInstance card)
	{
		if (ObjectIds.Contains(instanceId) && VisibleCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		if (RevealedCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		if (ReplacementEffects.TryGetValue(instanceId, out var value))
		{
			foreach (ReplacementEffectData item in value)
			{
				if (!item.SourceZcId.HasValue)
				{
					continue;
				}
				uint value2 = item.SourceZcId.Value;
				if (TryGetCard(value2, out card))
				{
					return true;
				}
				foreach (MtgCardInstance visibleCard in Limbo.VisibleCards)
				{
					if (visibleCard.InstanceId == value2)
					{
						card = visibleCard;
						return true;
					}
				}
			}
		}
		card = null;
		return false;
	}

	public bool TryGetCardInChildren(uint instanceId, MtgGameState gameState, out MtgCardInstance result)
	{
		result = null;
		foreach (MtgCardInstance value in gameState.VisibleCards.Values)
		{
			foreach (MtgCardInstance child in value.Children)
			{
				if (child.InstanceId == instanceId)
				{
					result = child;
					return true;
				}
			}
		}
		return false;
	}

	public MtgPlayer GetPlayerById(uint id)
	{
		TryGetPlayer(id, out var player);
		return player;
	}

	public bool TryGetPlayer(uint instanceId, out MtgPlayer player)
	{
		player = Players.Find(instanceId, (MtgPlayer mtgPlayer, uint num) => mtgPlayer.InstanceId == num);
		return player != null;
	}

	public MtgPlayer GetPlayerByEnum(GREPlayerNum player)
	{
		return player switch
		{
			GREPlayerNum.LocalPlayer => LocalPlayer, 
			GREPlayerNum.Opponent => Opponent, 
			_ => null, 
		};
	}

	public MtgEntity GetEntityById(uint id)
	{
		TryGetEntity(id, out var mtgEntity);
		return mtgEntity;
	}

	public bool TryGetEntity(uint instanceId, out MtgEntity mtgEntity)
	{
		mtgEntity = null;
		if (TryGetPlayer(instanceId, out var player))
		{
			mtgEntity = player;
			return true;
		}
		if (TryGetCard(instanceId, out var card))
		{
			mtgEntity = card;
			return true;
		}
		return false;
	}

	public void TrackHistoricEntity(uint instanceId, MtgGameState oldState)
	{
		if (GetEntityById(instanceId) == null && !TrackedHistoricCards.ContainsKey(instanceId))
		{
			if (oldState.TrackedHistoricCards.TryGetValue(instanceId, out var value))
			{
				TrackedHistoricCards[instanceId] = value.GetCopy();
			}
			else if (oldState.TryGetCard(instanceId, out value))
			{
				TrackedHistoricCards[instanceId] = value.GetCopy();
			}
		}
	}

	public List<ActionInfo> GetActionsForCardId(uint cardId)
	{
		if (cardId == 0)
		{
			return new List<ActionInfo>();
		}
		return Actions.FindAll((ActionInfo x) => x.Action.InstanceId == cardId || (x.Action.SourceId == cardId && (x.Action.IsActivateAction() || x.Action.IsCastMultiFacetAction())));
	}

	public MtgCardInstance GetTopCardOnStack()
	{
		if (Stack == null || Stack.CardIds.Count == 0)
		{
			return null;
		}
		return GetCardById(Stack.CardIds[0]);
	}

	private static MtgZone PhasedOutZone()
	{
		return new MtgZone
		{
			Type = ZoneType.PhasedOut,
			Id = 12u,
			Owner = null,
			Visibility = Visibility.Public
		};
	}
}
