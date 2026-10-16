using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ObjectIDChangedAnnotationParser : IAnnotationEventParser
{
	public const string DETAIL_OLD_ID = "orig_id";

	public const string DETAIL_NEW_ID = "new_id";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		yield return new InstanceIdChange((uint)ai.GetInt32Detail("orig_id"), (uint)ai.GetInt32Detail("new_id"));
	}
}
