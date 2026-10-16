using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class MultistepEffectCompleteAnnotationParser : MultistepEffectAnnotationParser
{
	protected override bool AnnotationTypeIsValid(AnnotationInfo ai)
	{
		return ai.Type.Contains(AnnotationType.MultistepEffectComplete);
	}

	protected override MultistepEffectEvent GetEffect(MtgPlayer affected, MtgCardInstance affector, AbilitySubCategory category)
	{
		return new MultistepEffectEvent(isStarting: false, affected, affector, category);
	}
}
