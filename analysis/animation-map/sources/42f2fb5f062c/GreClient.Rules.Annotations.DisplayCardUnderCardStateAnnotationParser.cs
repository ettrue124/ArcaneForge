using System;
using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DisplayCardUnderCardStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_DISABLE = "Disable";

	public const string DETAIL_DISPLAYUNDER = "DisplayUnderObjects";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (GetDisable(ai))
		{
			return;
		}
		uint num = (ai.TryGetInt32Detail("DisplayUnderObjects", out var value) ? ((uint)value) : ai.AffectorId);
		MtgEntity entityById = newState.GetEntityById(num);
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				if (CanAttach(card, entityById))
				{
					card.AttachedToId = num;
				}
				if (CanAttach(entityById, card))
				{
					entityById.AttachedWithIds.Add(card.InstanceId);
				}
			}
			newState.ReferenceMap.Add(affectedId, ReferenceMap.ReferenceType.DisplayedUnder, num);
		}
	}

	private bool CanAttach(MtgEntity baseEntity, MtgEntity attachingEntity)
	{
		if (!(baseEntity is MtgPlayer))
		{
			if (baseEntity is MtgCardInstance baseCard)
			{
				return attachingEntity is MtgPlayer || (attachingEntity is MtgCardInstance attachingCard && CanAttach(baseCard, attachingCard));
			}
			return false;
		}
		return true;
	}

	private bool CanAttach(MtgCardInstance baseCard, MtgCardInstance attachingCard)
	{
		MtgZone zone = baseCard.Zone;
		bool flag = zone != null && zone.Type == ZoneType.Limbo;
		MtgZone zone2 = attachingCard.Zone;
		bool flag2 = zone2 != null && zone2.Type == ZoneType.Limbo;
		return (flag == flag2) | flag;
	}

	private static bool GetDisable(AnnotationInfo ai)
	{
		if (ai.TryGetInt32Detail("Disable", out var value))
		{
			return value != 0;
		}
		throw new ArgumentException("Annotation contains no Disable detail.", "ai");
	}
}
