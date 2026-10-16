using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class TargetSpecStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_INDEX = "index";

	public const string DETAIL_GRPID = "abilityGrpId";

	public const string DETAIL_DISTRIBUTIONS = "distributions";

	public const string PROMPT_ID = "promptId";

	public const string PROMPT_PARAMS = "promptParameters";

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
		if (!ai.Type.Contains(AnnotationType.TargetSpec))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint targetingIndex = GetTargetingIndex(ai);
		uint abilityGrpId = GetAbilityGrpId(ai);
		List<int> distributions = GetDistributions(ai);
		Prompt prompt = GetPrompt(ai);
		newState.TargetInfo.Add(new TargetSpec(ai.AffectorId, new List<uint>(ai.AffectedIds), abilityGrpId, targetingIndex, distributions, prompt));
	}

	private static uint GetTargetingIndex(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("index") && detail.ValueInt32.Count > 0)
			{
				return (uint)(detail.ValueInt32[0] - 1);
			}
		}
		throw new ArgumentException("Annotation does not contain targeting index.", "ai");
	}

	private static uint GetAbilityGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("abilityGrpId") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation does not contain ability grpid.", "ai");
	}

	private static List<int> GetDistributions(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("distributions") && detail.ValueInt32.Count > 0)
			{
				return new List<int>(detail.ValueInt32);
			}
		}
		return new List<int>(0);
	}

	private static Prompt GetPrompt(AnnotationInfo ai)
	{
		Prompt prompt = new Prompt();
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("promptId") && detail.ValueInt32.Count > 0)
			{
				prompt.PromptId = (uint)detail.ValueInt32[0];
			}
			else
			{
				if (!detail.Key.Equals("promptParameters") || detail.ValueInt32.Count <= 0)
				{
					continue;
				}
				foreach (int item in detail.ValueInt32)
				{
					prompt.Parameters.Add(new PromptParameter
					{
						NumberValue = item,
						Type = ParameterType.Number
					});
				}
			}
		}
		return prompt;
	}
}
