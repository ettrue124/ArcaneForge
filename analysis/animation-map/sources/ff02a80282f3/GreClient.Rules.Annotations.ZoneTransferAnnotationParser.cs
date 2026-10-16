using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ZoneTransferAnnotationParser : IAnnotationEventParser, INeedMtgGameState_New
{
	public const string ZONE_SOURCE_DETAIL = "zone_src";

	public const string ZONE_DESTINATION_DETAIL = "zone_dest";

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.ZoneTransfer))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain zone details.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected card ids.", "ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		ZoneTransferReason reason = ZoneChangeEvent.GetChangeReason(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			uint zoneId = (uint)ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("zone_src")).ValueInt32[0];
			uint zoneId2 = (uint)ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("zone_dest")).ValueInt32[0];
			MtgZone zoneById = newState.GetZoneById(zoneId);
			MtgZone zoneById2 = newState.GetZoneById(zoneId2);
			if (zoneById != null && zoneById2 != null)
			{
				bool flag = zoneById.OwnerId == zoneById2.OwnerId;
				ZoneType type = zoneById.Type;
				ZoneType type2 = zoneById2.Type;
				if ((type2 == ZoneType.Hand || type2 == ZoneType.Library) && type == ZoneType.Battlefield)
				{
					reason = ZoneTransferReason.Bounce;
				}
				else if (flag && type == ZoneType.Library && type2 == ZoneType.Graveyard)
				{
					reason = ZoneTransferReason.Mill;
				}
				else if (type == ZoneType.Library && type2 == ZoneType.Exile)
				{
					reason = ZoneTransferReason.LibToExile;
				}
			}
			yield return new ZoneChangeEvent(affectedId, zoneById, zoneById2, ai.AffectorId, reason);
		}
	}
}
