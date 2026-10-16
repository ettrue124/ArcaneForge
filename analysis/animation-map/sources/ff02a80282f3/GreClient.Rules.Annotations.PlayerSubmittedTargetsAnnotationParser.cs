using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PlayerSubmittedTargetsAnnotationParser : PlayerSelectingOrSubmittedTargetsAnnotationParser
{
	protected override bool AnnotationTypeIsValid(AnnotationInfo ai)
	{
		return ai.Type.Contains(AnnotationType.PlayerSubmittedTargets);
	}

	protected override GameRulesEvent GetEvent(MtgCardInstance affected, MtgPlayer affector)
	{
		return new PlayerSubmittedTargetsEvent(affected, affector);
	}
}
