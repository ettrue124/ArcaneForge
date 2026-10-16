using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LossOfGameStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_REASON = "reason";

	public const string DETAIL_CARD_GRPID = "gameEffectCardGrpId";

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
		if (!ai.Type.Contains(AnnotationType.LossOfGame))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		newState.GameLossData = new MtgGameLossData
		{
			AffectorId = ai.AffectorId,
			AffectedId = ai.AffectedIds[0],
			Reason = GetReason(ai),
			EffectCardGrpId = GetEffectCardGrpId(ai)
		};
	}

	private static LossOfGameReason GetReason(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("reason") && detail.ValueString.Count > 0)
			{
				return detail.ValueString[0] switch
				{
					"SBA_LifeTotal" => LossOfGameReason.LifeTotal, 
					"SBA_DrawFromEmptyLib" => LossOfGameReason.DrawFromEmptyLib, 
					"GameEffect" => LossOfGameReason.GameEffect, 
					"SBA_Concede" => LossOfGameReason.Concede, 
					"SBA_Poison" => LossOfGameReason.Poison, 
					_ => LossOfGameReason.Other, 
				};
			}
		}
		return LossOfGameReason.None;
	}

	private static uint GetEffectCardGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("gameEffectCardGrpId") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}
}
