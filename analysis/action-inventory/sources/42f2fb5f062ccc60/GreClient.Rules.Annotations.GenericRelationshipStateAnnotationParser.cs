using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class GenericRelationshipStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_CATEGORY = "category";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.GenericRelationship))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count != 1)
		{
			throw new ArgumentException("Annotation does not contain the correct number of affected ids. This value should aways be equal to one.", "ai");
		}
		GenericRelationship item = new GenericRelationship
		{
			Category = ai.GetStringDetail("category"),
			AffectorId = ai.AffectorId,
			AffectedId = ai.AffectedIds[0]
		};
		newState.GenericRelationships.Add(item);
	}
}
