using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class SuspendLikeStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_ABILITY_GRPID = "abilityGrpid";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint id = ai.Id;
		uint affectorId = ai.AffectorId;
		uint int32Detail = (uint)ai.GetInt32Detail("abilityGrpid");
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var _))
			{
				newState.SuspendLikeInfo.Add(new SuspendLikeData(id, affectorId, affectedId, int32Detail));
			}
		}
	}
}
