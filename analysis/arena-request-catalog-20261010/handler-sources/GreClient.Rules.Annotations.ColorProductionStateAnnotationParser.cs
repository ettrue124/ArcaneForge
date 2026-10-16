using System;
using GreClient.CardData;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ColorProductionStateAnnotationParser : IAnnotationStateParser
{
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
		if (!ai.Type.Contains(AnnotationType.ColorProduction))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		for (int i = 0; i < ai.AffectedIds.Count; i++)
		{
			if (!newState.TryGetCard(ai.AffectedIds[i], out var card))
			{
				continue;
			}
			foreach (int @int in ai.GetIntList("colors"))
			{
				switch ((ManaColor)@int)
				{
				case ManaColor.White:
				case ManaColor.Blue:
				case ManaColor.Black:
				case ManaColor.Red:
				case ManaColor.Green:
				case ManaColor.Colorless:
					card.ColorProduction.Add((ManaColor)@int);
					break;
				case ManaColor.AnyColor:
					card.ColorProduction.AddRange(ManaUtilities.GetUncombinedColors(ManaColor.AnyColor));
					break;
				}
			}
		}
	}
}
