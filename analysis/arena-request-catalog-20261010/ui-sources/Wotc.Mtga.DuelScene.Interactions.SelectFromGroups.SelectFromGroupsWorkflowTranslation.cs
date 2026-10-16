using System;
using System.Collections.Generic;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectFromGroups;

public class SelectFromGroupsWorkflowTranslation : IWorkflowTranslation<SelectFromGroupsRequest>
{
	private static HashSet<ZoneType> _defaultSingleBrowserZoneTypes = new HashSet<ZoneType>
	{
		ZoneType.Sideboard,
		ZoneType.Graveyard,
		ZoneType.Exile,
		ZoneType.Library
	};

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IObjectPool _genericPool;

	public SelectFromGroupsWorkflowTranslation(IContext context)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<IBrowserManager>(), context.Get<IBrowserHeaderTextProvider>(), context.Get<IObjectPool>())
	{
	}

	private SelectFromGroupsWorkflowTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider, IObjectPool genericPool)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_genericPool = genericPool ?? NullObjectPool.Default;
	}

	private bool InvolvesOpponentHand(List<MtgZone> zoneList, List<uint> zoneIds, MtgGameState gameState)
	{
		if (zoneList.Count == 1)
		{
			MtgZone mtgZone = zoneList[0];
			if (mtgZone.Type == ZoneType.Hand && mtgZone.OwnerNum == GREPlayerNum.Opponent)
			{
				return true;
			}
		}
		if (zoneIds.Count == 1)
		{
			MtgZone zoneById = gameState.GetZoneById(zoneIds[0]);
			if (zoneById.Type == ZoneType.Hand && zoneById.OwnerNum == GREPlayerNum.Opponent)
			{
				return true;
			}
		}
		return false;
	}

	private IEnumerable<MtgZone> GetZonesFromSelectFromGroups(MtgGameState currentState, SelectFromGroupsRequest request)
	{
		foreach (uint zoneId in request.ZoneIds)
		{
			if (currentState.Zones.TryGetValue(zoneId, out var value))
			{
				yield return value;
			}
		}
		foreach (Group group in request.Groups)
		{
			foreach (MtgZone item in _getZoneByIds(group.Ids, currentState))
			{
				yield return item;
			}
		}
		foreach (MtgZone item2 in _getZoneByIds(request.UnfilteredIds, currentState))
		{
			yield return item2;
		}
		static IEnumerable<MtgZone> _getZoneByIds(IEnumerable<uint> ids, MtgGameState gameState)
		{
			foreach (uint id in ids)
			{
				if (gameState.TryGetCard(id, out var card))
				{
					yield return card.Zone;
				}
			}
		}
	}

	public WorkflowBase Translate(SelectFromGroupsRequest req)
	{
		List<MtgZone> list = _genericPool.PopObject<List<MtgZone>>();
		foreach (MtgZone zonesFromSelectFromGroup in GetZonesFromSelectFromGroups(_gameStateProvider.LatestGameState, req))
		{
			if (!list.Contains(zonesFromSelectFromGroup))
			{
				list.Add(zonesFromSelectFromGroup);
			}
		}
		if (InvolvesOpponentHand(list, req.ZoneIds, _gameStateProvider.LatestGameState) || (list.Count == 1 && InvolvedZonesOverlap(list, _defaultSingleBrowserZoneTypes)))
		{
			return new SelectFromGroupsWorkflow_Browser(req, _cardViewProvider, _browserManager, _headerTextProvider);
		}
		if (req.ZoneIds.Count > 1 && !list.Exists((MtgZone x) => x.Type == ZoneType.Battlefield))
		{
			return new SelectFromGroupsWorkflow_MultiZone(req, _cardDatabase.ClientLocProvider, _gameStateProvider, _cardViewProvider, _browserManager, _headerTextProvider);
		}
		if (req.ZoneIds.Count == 0 && list.Count > 1)
		{
			throw new ArgumentException("Received a SelectFromGroupsReq with multiple zones but no ZoneIds");
		}
		list.Clear();
		_genericPool.PushObject(list);
		return new SelectFromGroupsWorkflow(req, _genericPool, _gameStateProvider, _cardHolderProvider, _browserManager);
	}

	private static bool InvolvedZonesOverlap(List<MtgZone> involvedZones, HashSet<ZoneType> zonesToConsider)
	{
		foreach (ZoneType zoneType in zonesToConsider)
		{
			if (involvedZones.Exists((MtgZone x) => x.Type == zoneType))
			{
				return true;
			}
		}
		return false;
	}
}
