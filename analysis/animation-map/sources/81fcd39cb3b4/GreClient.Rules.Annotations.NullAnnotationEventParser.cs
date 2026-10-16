using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class NullAnnotationEventParser : IAnnotationEventParser
{
	public static readonly IAnnotationEventParser Default = new NullAnnotationEventParser();

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		return Array.Empty<GameRulesEvent>();
	}
}
