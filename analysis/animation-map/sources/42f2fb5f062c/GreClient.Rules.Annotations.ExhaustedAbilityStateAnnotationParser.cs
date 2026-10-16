using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ExhaustedAbilityStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "AbilityGrpId";

	public const string DETAIL_USES = "UsesRemaining";

	private const string DETAIL_UNIQUE_ABILITY_ID = "UniqueAbilityId";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		int usesRemaining = (ai.TryGetInt32Detail("UsesRemaining", out var value) ? value : 0);
		List<uint> list = new List<uint>();
		foreach (int @int in ai.GetIntList("AbilityGrpId"))
		{
			list.Add((uint)@int);
		}
		if (ai.TryGetInt32Detail("UniqueAbilityId", out var value2) && newState.AbilityInstances.TryGetValue((uint)value2, out var value3))
		{
			value3.AbilityExhaustedDetails = new AbilityExhaustedDetails(ai.AffectorId, usesRemaining, list);
		}
	}
}
