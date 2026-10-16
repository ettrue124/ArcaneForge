using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LinkInfoEventAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New
{
	public const string LINK_TYPE_DETAIL = "LinkType";

	public const string CARD_TITLEID_DETAIL = "Choice_Value";

	public const string STATIC_LIST_TYPE_DETAIL = "Choice_Domain";

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.LinkInfo))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain chosen card details.", "ai");
		}
		LinkType linkType = GetLinkType(ai);
		StaticList staticListType = GetStaticListType(ai);
		if (linkType != LinkType.Choose || staticListType != StaticList.CardNames)
		{
			yield break;
		}
		foreach (GameRulesEvent item in HandleCardNamedEvent(ai, oldState, newState))
		{
			yield return item;
		}
	}

	private static IEnumerable<GameRulesEvent> HandleCardNamedEvent(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint playerId = GetPlayerId(ai);
		uint? num = GetCardTitleId(ai);
		if (num.HasValue)
		{
			yield return new CardNamedEvent
			{
				PlayerId = playerId,
				LocId = num.Value
			};
		}
		static uint? GetCardTitleId(AnnotationInfo innerAi)
		{
			foreach (KeyValuePairInfo detail in innerAi.Details)
			{
				if (detail.Key.Equals("Choice_Value") && detail.ValueInt32.Count == 1)
				{
					return (uint)detail.ValueInt32[0];
				}
			}
			return null;
		}
		static uint GetPlayerId(AnnotationInfo innerAi)
		{
			if (innerAi.AffectedIds.Count == 1)
			{
				return innerAi.AffectedIds[0];
			}
			throw new ArgumentException("Annotation does not contain player affected id.", "innerAi");
		}
	}

	private static StaticList GetStaticListType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("Choice_Domain") && detail.ValueInt32.Count == 1)
			{
				return (StaticList)detail.ValueInt32[0];
			}
		}
		return StaticList.None;
	}

	private static LinkType GetLinkType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("LinkType") && detail.ValueInt32.Count == 1)
			{
				return (LinkType)detail.ValueInt32[0];
			}
		}
		return LinkType.None;
	}
}
