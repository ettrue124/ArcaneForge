using System;
using System.Collections.Generic;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class MiscContinuousEffectStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "grpid";

	public const string DETAIL_QUALIFICATION_TYPE = "QualificationType";

	public const string DETAIL_QUALIFICATION_SUBTYPE = "QualificationSubtype";

	public const string DETAIL_EXTRATURNS = "extra_turns";

	public const string DETAIL_CHOSEN_SUBTYPE = "Chosen_SubType";

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
		if (!ai.Type.Contains(AnnotationType.MiscContinuousEffect))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		AnnotationDataUtils.ExtractDetails(ai, dictionary);
		if (dictionary.ContainsKey("Chosen_SubType"))
		{
			ProcessLinkInfoModification(ai, newState, dictionary);
		}
		else
		{
			ProcessPendingEffect(ai, newState, dictionary);
		}
	}

	private void ProcessLinkInfoModification(AnnotationInfo ai, MtgGameState newState, Dictionary<string, object> detailsDict)
	{
		if (!newState.TryGetCard(ai.AffectorId, out var card) || !detailsDict.TryGetValue("Chosen_SubType", out var value))
		{
			return;
		}
		int num = (int)value;
		LinkedInfoText linkedInfoText = new LinkedInfoText(TypeCategory.SubType, num, new LinkInfoData(ai.Id, LinkType.Note, ai.AffectorId, (uint)num, (card.ObjectType == GameObjectType.Ability) ? card.GrpId : 0u));
		if (card.LinkedInfoText.Exists(linkedInfoText, (LinkedInfoText linkInfo, LinkedInfoText newLinkInfo) => linkInfo.Equals(newLinkInfo)))
		{
			return;
		}
		card.LinkedInfoText.Add(linkedInfoText);
		if (newState.TryGetCard(card.ParentId, out var card2))
		{
			int num2 = card2.LinkedInfoText.FindIndex(linkedInfoText, (LinkedInfoText parentInfo, LinkedInfoText childInfo) => parentInfo.Equals(childInfo));
			LinkedInfoText original = ((num2 == -1) ? new LinkedInfoText(TypeCategory.SubType, num, new LinkInfoData(ai.Id, LinkType.Note, card.ParentId, (uint)num)) : card2.LinkedInfoText[num2]);
			if (num2 != -1)
			{
				card2.LinkedInfoText.RemoveAt(num2);
			}
			card2.LinkedInfoText.Add(new LinkedInfoText(original, highlighted: true));
		}
	}

	private void ProcessPendingEffect(AnnotationInfo ai, MtgGameState newState, Dictionary<string, object> detailsDict)
	{
		uint grpId = GetGrpId(ai);
		QualificationType qualificationType = GetQualificationType(ai);
		QualificationSubtype qualificationSubType = GetQualificationSubType(ai);
		int extraTurnCount = GetExtraTurnCount(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetPlayer(affectedId, out var player))
			{
				for (int i = 0; i < extraTurnCount; i++)
				{
					newState.ExtraTurns.Insert(0, new ExtraTurn(player.ClientPlayerEnum, grpId));
				}
				player.PendingEffects.Add(new PendingEffectData(ai.Id, qualificationType, qualificationSubType, ai.AffectorId, affectedId, grpId, detailsDict));
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
		}
		return 0u;
	}

	private static QualificationType GetQualificationType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("QualificationType") && detail.ValueInt32.Count > 0)
			{
				return (QualificationType)detail.ValueInt32[0];
			}
		}
		return QualificationType.None;
	}

	private static QualificationSubtype GetQualificationSubType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("QualificationSubtype") && detail.ValueInt32.Count > 0)
			{
				return (QualificationSubtype)detail.ValueInt32[0];
			}
		}
		return QualificationSubtype.None;
	}

	private static int GetExtraTurnCount(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("extra_turns") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0];
			}
		}
		return 0;
	}
}
