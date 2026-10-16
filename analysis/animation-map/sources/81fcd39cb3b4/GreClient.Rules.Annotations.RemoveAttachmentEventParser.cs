using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class RemoveAttachmentEventParser : IAnnotationEventParser
{
	private const string DETAIL_INVALID_GRPID = "invalidating_grpid";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		uint affectorId = ai.AffectorId;
		uint invalidatingGrpid = (uint)ai.GetInt32Detail("invalidating_grpid");
		foreach (uint affectedId in ai.AffectedIds)
		{
			yield return new IllegalAttachmentEvent
			{
				AffectorId = affectorId,
				AffectedId = affectedId,
				InvalidatingGrpid = invalidatingGrpid
			};
		}
	}
}
