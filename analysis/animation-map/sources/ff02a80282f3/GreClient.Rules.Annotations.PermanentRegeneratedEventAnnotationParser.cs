using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PermanentRegeneratedEventAnnotationParser : IAnnotationEventParser
{
	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			yield return new RegeneratedCardEvent(affectedId);
		}
	}
}
