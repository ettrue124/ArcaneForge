using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ModifiedColorAnnotationParser : IAnnotationStateParser
{
	private const string DETAIL_COLORS = "color";

	private const string DETAIL_MOD_TYPE = "modificationType";

	private const string DETAIL_SET = "Set";

	private const string DETAIL_ADD = "Add";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint affectorId = ai.AffectorId;
		ColorModification item = new ColorModification(ai.Id, affectorId, ConvertToModType(ai.GetStringDetail("modificationType")), GetColors(ai));
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				card.ColorModifications.Add(item);
			}
		}
	}

	private static ColorModification.ModType ConvertToModType(string detailKey)
	{
		if (!(detailKey == "Set"))
		{
			if (detailKey == "Add")
			{
				return ColorModification.ModType.Added;
			}
			return ColorModification.ModType.None;
		}
		return ColorModification.ModType.Set;
	}

	private IReadOnlyList<CardColor> GetColors(AnnotationInfo ai)
	{
		List<CardColor> list = new List<CardColor>();
		foreach (int @int in ai.GetIntList("color"))
		{
			list.Add((CardColor)@int);
		}
		return list;
	}
}
