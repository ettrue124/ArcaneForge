using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ResolutionCompleteEventParser : IAnnotationEventParser
{
	private const string DETAIL_GRPID = "grpid";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		yield return new ResolutionEvent(ai.AffectorId, (uint)ai.GetInt32Detail("grpid"), isStart: false);
	}
}
