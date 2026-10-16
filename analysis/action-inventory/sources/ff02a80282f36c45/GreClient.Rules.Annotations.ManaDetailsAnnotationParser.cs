using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ManaDetailsAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_ENUM_TYPE = "Enum_Type";

	public const string DETAIL_ENUM_VALUE_PREFIX = "Enum_Value_";

	public static readonly HashSet<string> ManaRiderPromptKeys = new HashSet<string> { "ManaSpecType_Restricted", "ManaSpecType_Trigger", "ManaSpecType_AdditionalEffect", "ManaSpecType_DoesNotEmpty" };

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.ManaDetails))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgMana value = null;
			if (!newState.LocalPlayer.IdToManaMap.TryGetValue(affectedId, out value) && !newState.Opponent.IdToManaMap.TryGetValue(affectedId, out value) && !oldState.LocalPlayer.IdToManaMap.TryGetValue(affectedId, out value) && !oldState.Opponent.IdToManaMap.TryGetValue(affectedId, out value))
			{
				continue;
			}
			RiderPromptData riderPromptData = new RiderPromptData();
			foreach (KeyValuePairInfo detail in ai.Details)
			{
				string key = detail.Key;
				if (ManaRiderPromptKeys.Contains(key))
				{
					if (riderPromptData.PromptId != 0)
					{
						riderPromptData = new RiderPromptData
						{
							PromptId = detail.ValueInt32[0]
						};
					}
					else
					{
						riderPromptData.PromptId = detail.ValueInt32[0];
					}
					value.Riders.Add(riderPromptData);
				}
				else if (key == "Enum_Type")
				{
					riderPromptData.SetEnumType(detail.ValueString[0]);
				}
				else if (key.StartsWith("Enum_Value_"))
				{
					string s = key.Replace("Enum_Value_", string.Empty);
					int result = -1;
					if (int.TryParse(s, out result))
					{
						riderPromptData.SetValue(result, detail.ValueInt32[0]);
					}
				}
			}
		}
	}
}
