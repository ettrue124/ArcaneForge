using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class UserActionTakenAnnotationParser : IAnnotationEventParser
{
	public const string ACTION_TYPE_DETAIL = "actionType";

	public const string ABILITY_GRPID_DETAIL = "abilityGrpId";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.UserActionTaken))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected ID.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation contains no details");
		}
		ActionType int32Detail = (ActionType)ai.GetInt32Detail("actionType");
		uint int32Detail2 = (uint)ai.GetInt32Detail("abilityGrpId");
		if (int32Detail != ActionType.None)
		{
			yield return new UserActionTakenEvent
			{
				Affector = ai.AffectorId,
				Affected = ai.AffectedIds.ToList(),
				AbilityGrpId = int32Detail2,
				Type = int32Detail
			};
		}
	}
}
