using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ParentOfReferenceMapStateParser : IAnnotationStateParser
{
	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint affectorId = ai.AffectorId;
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.ReferenceMap.Add(affectorId, ReferenceMap.ReferenceType.ParentOf, affectedId);
		}
	}
}
