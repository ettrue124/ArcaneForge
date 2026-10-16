using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ManaPaidAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old
{
	public const string DETAIL_MANA_ID = "id";

	public const string DETAIL_SUBSTITUTION_GRPID = "substitution_grpid";

	public const string DETAIL_COLOR = "color";

	public MtgGameState oldState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.ManaPaid))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		uint consumerId = ai.AffectedIds[0];
		uint manaId = GetManaId(ai);
		uint producerId;
		MtgMana mana;
		if (oldState.LocalPlayer.IdToManaMap.ContainsKey(manaId))
		{
			producerId = oldState.LocalPlayer.InstanceId;
			mana = oldState.LocalPlayer.IdToManaMap[manaId];
		}
		else if (oldState.Opponent.IdToManaMap.ContainsKey(manaId))
		{
			producerId = oldState.Opponent.InstanceId;
			mana = oldState.Opponent.IdToManaMap[manaId];
		}
		else
		{
			uint substitutionGrpId = GetSubstitutionGrpId(ai);
			ManaColor color = GetColor(ai);
			mana = new MtgMana(new ManaInfo
			{
				Color = color,
				ManaId = manaId,
				Count = 1u
			}, oldState.LocalPlayer.InstanceId, substitutionGrpId);
			producerId = ai.AffectorId;
		}
		yield return new ManaPaidEvent(producerId, consumerId, mana);
	}

	private static uint GetManaId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("id") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static uint GetSubstitutionGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("substitution_grpid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static ManaColor GetColor(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("color") && detail.ValueInt32.Count > 0)
			{
				return (ManaColor)detail.ValueInt32[0];
			}
		}
		return ManaColor.None;
	}
}
