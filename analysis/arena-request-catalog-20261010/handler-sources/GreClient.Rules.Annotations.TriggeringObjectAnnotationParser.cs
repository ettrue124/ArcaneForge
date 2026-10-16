using System;
using System.Collections.Generic;
using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class TriggeringObjectAnnotationParser : IAnnotationStateParser
{
	private const string DETAIL_NEW_OBJECT_ID = "NEW_OBJECT_ID";

	private const string DETAIL_SOURCE_ZONE = "source_zone";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		uint affectorId = ai.AffectorId;
		foreach (uint item in (IEnumerable<uint>)ai.AffectedIds)
		{
			newState.ReferenceMap.Add(item, ReferenceMap.ReferenceType.Triggered, affectorId);
			uint int32Detail = (uint)ai.GetInt32Detail("NEW_OBJECT_ID");
			if (int32Detail != 0)
			{
				newState.ReferenceMap.Add(int32Detail, ReferenceMap.ReferenceType.Triggered, affectorId);
			}
			uint int32Detail2 = (uint)ai.GetInt32Detail("source_zone");
			if (int32Detail2 != 0)
			{
				newState.ReferenceMap.Add(affectorId, ReferenceMap.ReferenceType.TriggeringObjectSourceZone, int32Detail2);
			}
		}
	}
}
