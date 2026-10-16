using System;
using GreClient.Rules;
using GreClient.Rules.Annotations;
using Wotc.Mtgo.Gre.External.Messaging;

public class GroupedIdsStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GROUPIDS = "group_id";

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
		if (!ai.Type.Contains(AnnotationType.GroupedIds))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		int int32Detail = ai.GetInt32Detail("group_id");
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				card.GroupedIds.Add(int32Detail);
			}
		}
	}
}
