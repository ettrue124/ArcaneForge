using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PhaseModifiedEventParser : IAnnotationEventParser
{
	private const string DETAIL_PHASE = "phase";

	private const string DETAIL_STEP = "step";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		yield return new GamePhaseChangeEvent((Phase)ai.GetInt32Detail("phase"), (Step)ai.GetInt32Detail("step"));
	}
}
