using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class SourceZoneStateParser : IAnnotationStateParser
{
	private const string DETAIL_SOURCEZONE = "source_zone";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint int32Detail = (uint)ai.GetInt32Detail("source_zone");
		if (int32Detail != 0)
		{
			newState.ReferenceMap.Add(ai.AffectorId, ReferenceMap.ReferenceType.AbilityInstanceParentSourceZone, int32Detail);
		}
	}
}
