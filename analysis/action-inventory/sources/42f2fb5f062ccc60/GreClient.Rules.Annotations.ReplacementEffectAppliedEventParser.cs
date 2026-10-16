using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ReplacementEffectAppliedEventParser : IAnnotationEventParser
{
	private const string DETAIL_GRPID = "grpid";

	private const string DETAIL_IS_DMG_REPLACEMENT = "IsDamageReplacement";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		uint affectorId = ai.AffectorId;
		uint abilityGrpId = (uint)ai.GetInt32Detail("grpid");
		bool isDamageReplacement = ai.GetInt32Detail("IsDamageReplacement") == 1;
		foreach (uint affectedId in ai.AffectedIds)
		{
			yield return new ReplacementEffectAppliedEvent
			{
				AffectorId = affectorId,
				AffectedId = affectedId,
				AbilityGrpId = abilityGrpId,
				IsDamageReplacement = isDamageReplacement
			};
		}
	}
}
