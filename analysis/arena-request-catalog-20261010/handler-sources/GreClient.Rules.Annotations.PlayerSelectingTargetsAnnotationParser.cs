using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PlayerSelectingTargetsAnnotationParser : PlayerSelectingOrSubmittedTargetsAnnotationParser
{
	protected override bool AnnotationTypeIsValid(AnnotationInfo ai)
	{
		return ai.Type.Contains(AnnotationType.PlayerSelectingTargets);
	}

	protected override GameRulesEvent GetEvent(MtgCardInstance affected, MtgPlayer affector)
	{
		return new PlayerSelectingTargetsEvent(affected, affector);
	}
}
