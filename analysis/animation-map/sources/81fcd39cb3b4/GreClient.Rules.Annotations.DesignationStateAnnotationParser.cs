using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DesignationStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_PROMPT = "PromptMessage";

	public const string DETAIL_COST_INCREASE = "CostIncrease";

	public const string DETAIL_GRPID = "grpid";

	public const string DETAIL_SOURCE_GRPID = "source_grpid";

	public const string DETAIL_ACTIVE_PLAYER_SPELL_COUNT = "ActivePlayerSpellCount";

	public const string DETAIL_VALUE = "value";

	public const string DETAIL_COLOR_IDENTITY = "ColorIdentity";

	public const string DETAIL_PREPARED_COPY_ID = "PreparedCopyZcid";

	public const string DETAIL_CONTROLLER_ID = "ControllerId";

	public const string DETAIL_SKIN_CODE = "skin_code";

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
		if (!ai.Type.Contains(AnnotationType.Designation))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint id = ai.Id;
		uint affectorId = ai.AffectorId;
		Designation designationType = DesignationUtils.GetDesignationType(ai);
		PromptMessage promptMessage = (ai.TryGetInt32Detail("PromptMessage", out var value) ? ((PromptMessage)value) : PromptMessage.None);
		int int32Detail = ai.GetInt32Detail("CostIncrease");
		uint grpId = GetGrpId(ai);
		int? activePlayerSpellCount = (ai.TryGetInt32Detail("ActivePlayerSpellCount", out var value2) ? new int?(value2) : ((int?)null));
		uint? preparedCopyId = (ai.TryGetInt32Detail("PreparedCopyZcid", out var value3) ? new uint?((uint)value3) : ((uint?)null));
		uint? value4 = (ai.TryGetInt32Detail("value", out var value5) ? new uint?((uint)value5) : ((uint?)null));
		int[] colorIdentity = GetColorIdentity(ai);
		uint? controllerId = (ai.TryGetInt32Detail("ControllerId", out var value6) ? new uint?((uint)value6) : ((uint?)null));
		string stringDetail = ai.GetStringDetail("skin_code");
		foreach (uint affectedId in ai.AffectedIds)
		{
			DesignationData item = new DesignationData(id, affectorId, affectedId, designationType, promptMessage, int32Detail, grpId, activePlayerSpellCount, value4, colorIdentity, preparedCopyId, controllerId, stringDetail);
			newState.Designations.Add(item);
			if (designationType == Designation.Protector && newState.TryGetCard(affectorId, out var card))
			{
				card.ProtectorId = affectedId;
			}
			if (!newState.TryGetEntity(affectedId, out var mtgEntity))
			{
				continue;
			}
			mtgEntity.Designations.Add(item);
			if (!(mtgEntity is MtgCardInstance mtgCardInstance))
			{
				continue;
			}
			if (designationType == Designation.Commander || designationType == Designation.Companion)
			{
				foreach (MtgCardInstance linkedFaceInstance in mtgCardInstance.LinkedFaceInstances)
				{
					linkedFaceInstance.Designations.Add(item);
				}
			}
			if (mtgCardInstance.Owner != null)
			{
				switch (designationType)
				{
				case Designation.Commander:
					mtgCardInstance.Owner.CommanderIds.Add(affectedId);
					break;
				case Designation.Companion:
					mtgCardInstance.Owner.CompanionId = affectedId;
					break;
				}
			}
		}
	}

	private static uint GetGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("grpid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
			if (detail.Key.Equals("source_grpid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static int[] GetColorIdentity(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("ColorIdentity"))
			{
				int[] array = new int[detail.ValueInt32.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = detail.ValueInt32[i];
				}
				return array;
			}
		}
		return null;
	}
}
