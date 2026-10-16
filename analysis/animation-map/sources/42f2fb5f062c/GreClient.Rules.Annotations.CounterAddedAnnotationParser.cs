using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CounterAddedAnnotationParser : CounterAddedRemovedAnnotationParser
{
	protected override int GetCount(AnnotationInfo ai)
	{
		return ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("transaction_amount")).ValueInt32[0];
	}

	public override IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai != null && !ai.Type.Contains(AnnotationType.CounterAdded))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		return base.Process(ai);
	}
}
