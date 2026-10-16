using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ScryEventAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New
{
	public const string TOP_IDS_DETAIL = "topIds";

	public const string BOTTOM_IDS_DETAIL = "bottomIds";

	public const string LIBRARY_IDS_DETAIL = "libraryIds";

	public const string GRAVEYARD_IDS_DETAIL = "graveyardIds";

	public const string EFFECT_GRPID_DETAIL = "effectGrpid";

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.Scry))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain chosen card details.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected card ids.", "ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		List<uint> list = GetDetailList("topIds", ai) ?? new List<uint>();
		List<uint> list2 = GetDetailList("bottomIds", ai) ?? new List<uint>();
		List<uint> affectedIds = new List<uint>(ai.AffectedIds);
		if (list != null || list2 != null)
		{
			GREPlayerNum playerEnum = FindScryingPlayer(affectedIds, ai.AffectorId, oldState, newState);
			uint effectGrpId = GetEffectGrpId(ai);
			yield return new ScryResultEvent
			{
				AffectorId = ai.AffectorId,
				AffectedIds = affectedIds,
				TopIds = list,
				BottomIds = list2,
				PlayerEnum = playerEnum,
				EffectGrpId = effectGrpId
			};
		}
	}

	private List<uint> GetDetailList(string detailKey, AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals(detailKey) && detail.ValueInt32.Count > 0)
			{
				return new List<uint>(detail.ValueInt32.Select((int x) => (uint)x));
			}
		}
		return null;
	}

	private GREPlayerNum FindScryingPlayer(IList<uint> affectedIds, uint affectorId, MtgGameState oldState, MtgGameState newState)
	{
		uint cardId = ((affectedIds.Count > 0) ? affectedIds[0] : affectorId);
		GREPlayerNum gREPlayerNum = GREPlayerNum.Invalid;
		if (gREPlayerNum == GREPlayerNum.Invalid)
		{
			MtgCardInstance cardById = newState.GetCardById(cardId);
			if (cardById != null)
			{
				gREPlayerNum = cardById.Controller.ClientPlayerEnum;
			}
		}
		if (gREPlayerNum == GREPlayerNum.Invalid)
		{
			MtgCardInstance cardById2 = oldState.GetCardById(cardId);
			if (cardById2 != null)
			{
				gREPlayerNum = cardById2.Controller.ClientPlayerEnum;
			}
		}
		if (gREPlayerNum == GREPlayerNum.Invalid)
		{
			MtgZone mtgZone = newState.Zones.Values.FirstOrDefault((MtgZone x) => x.CardIds.Intersect(affectedIds).Count() > 0);
			if (mtgZone != null)
			{
				gREPlayerNum = mtgZone.OwnerNum;
			}
		}
		if (gREPlayerNum == GREPlayerNum.Invalid)
		{
			MtgZone mtgZone2 = oldState.Zones.Values.FirstOrDefault((MtgZone x) => x.CardIds.Intersect(affectedIds).Count() > 0);
			if (mtgZone2 != null)
			{
				gREPlayerNum = mtgZone2.OwnerNum;
			}
		}
		return gREPlayerNum;
	}

	private uint GetEffectGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("effectGrpid") && detail.ValueInt32.Count == 1)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}
}
