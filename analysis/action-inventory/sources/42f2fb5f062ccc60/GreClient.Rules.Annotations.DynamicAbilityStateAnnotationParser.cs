using System;
using System.Linq;
using Google.Protobuf.Collections;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DynamicAbilityStateAnnotationParser : IAnnotationStateParser
{
	public const string NO_COST_STRING = "{}";

	public const string ZERO_COST = "o0";

	public const string COST = "cost";

	public const string GRPID = "grpid";

	public const string BASE_GRPID = "base_grpid";

	public const string ACTION_COST_STRING = "action_cost_string";

	public const string PARAMETER = "parameter";

	private readonly IDynamicAbilityDataProvider _provider;

	public DynamicAbilityStateAnnotationParser(IDynamicAbilityDataProvider provider)
	{
		_provider = provider;
	}

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
		if (!ai.Type.Contains(AnnotationType.DynamicAbility))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		RepeatedField<uint> affectedIds = ai.AffectedIds;
		KeyValuePairInfo keyValuePairInfo = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key.Equals("cost"));
		KeyValuePairInfo keyValuePairInfo2 = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key.Equals("grpid"));
		KeyValuePairInfo keyValuePairInfo3 = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key.Equals("base_grpid"));
		KeyValuePairInfo keyValuePairInfo4 = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key.Equals("action_cost_string"));
		KeyValuePairInfo keyValuePairInfo5 = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key.Equals("parameter"));
		if (keyValuePairInfo2 != null && keyValuePairInfo3 != null)
		{
			string cost = "{}";
			if (keyValuePairInfo != null)
			{
				cost = (keyValuePairInfo.ValueString[0].Equals("{}") ? "o0" : keyValuePairInfo.ValueString[0]);
			}
			uint grpId = (uint)keyValuePairInfo2.ValueInt32[0];
			uint baseGrpId = (uint)keyValuePairInfo3.ValueInt32[0];
			int actionCostPromptId = 0;
			if (keyValuePairInfo4 != null)
			{
				actionCostPromptId = keyValuePairInfo4.ValueInt32[0];
			}
			uint? parameter = null;
			if (keyValuePairInfo5 != null)
			{
				parameter = (uint)keyValuePairInfo5.ValueInt32[0];
			}
			_provider.AddDynamicAbility(grpId, baseGrpId, affectedIds, cost, actionCostPromptId, parameter);
		}
	}
}
