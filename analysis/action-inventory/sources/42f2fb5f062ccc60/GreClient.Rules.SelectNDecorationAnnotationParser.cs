using System.Collections.Generic;
using GreClient.Rules.Annotations;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectNDecorationAnnotationParser : IRequestAnnotationParser<SelectNRequest>
{
	private const string AFFECTED_OBJECTS_DETAIL = "affected_objects";

	public void Parse(AnnotationInfo annotation, SelectNRequest request)
	{
		foreach (uint affectedId in annotation.AffectedIds)
		{
			request.AffectedObjects[affectedId] = new HashSet<uint>();
			foreach (int @int in annotation.GetIntList("affected_objects"))
			{
				request.AffectedObjects[affectedId].Add((uint)@int);
			}
		}
	}
}
