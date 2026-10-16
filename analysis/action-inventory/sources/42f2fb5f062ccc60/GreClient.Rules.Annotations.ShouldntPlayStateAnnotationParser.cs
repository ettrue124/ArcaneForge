using System;
using System.Collections.Generic;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ShouldntPlayStateAnnotationParser : IAnnotationStateParser
{
	private class ReasonTypeComparer : IComparer<ShouldntPlayData.ReasonType>
	{
		public static readonly ReasonTypeComparer DEFAULT = new ReasonTypeComparer();

		public int Compare(ShouldntPlayData.ReasonType x, ShouldntPlayData.ReasonType y)
		{
			if (x != ShouldntPlayData.ReasonType.StartingPlayer)
			{
				return 1;
			}
			return -1;
		}
	}

	public const string DETAIL_REASON = "Reason";

	public const string DETAIL_ABILITY_GRPID = "ability_grpid";

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
		if (!ai.Type.Contains(AnnotationType.ShouldntPlay))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint abilityGrpId = GetAbilityGrpId(ai);
		IReadOnlyList<ShouldntPlayData.ReasonType> reasonList = GetReasonList(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				card.PlayWarnings.Add(new ShouldntPlayData
				{
					AffectorId = ai.AffectorId,
					AbilityId = abilityGrpId,
					Reasons = reasonList
				});
			}
		}
	}

	private static uint GetAbilityGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("ability_grpid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static IReadOnlyList<ShouldntPlayData.ReasonType> GetReasonList(AnnotationInfo ai)
	{
		List<ShouldntPlayData.ReasonType> list = new List<ShouldntPlayData.ReasonType>(2);
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (!detail.Key.Equals("Reason") || detail.ValueString.Count <= 0)
			{
				continue;
			}
			foreach (string item in detail.ValueString)
			{
				if (EnumExtensions.EnumTryParse<ShouldntPlayData.ReasonType>(item, out var result))
				{
					list.Add(result);
				}
				else
				{
					list.Add(ShouldntPlayData.ReasonType.Undefined);
				}
			}
		}
		if (list.Count == 0)
		{
			list.Add(ShouldntPlayData.ReasonType.Undefined);
		}
		list.Sort(ReasonTypeComparer.DEFAULT);
		return list;
	}
}
