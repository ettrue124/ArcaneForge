using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class UseOrCostsManaCostAnnotationParser : IAnnotationStateParser
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
		if (!ai.Type.Contains(AnnotationType.UseOrCostsManaCost))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count != 1)
		{
			throw new ArgumentException("This annotation parser only supports one affected id", "ai");
		}
		if (ai.Details.Count != 1)
		{
			throw new ArgumentException("This annotation parser only supports one detail field", "ai");
		}
		MtgCardInstance cardById = newState.GetCardById(ai.AffectedIds.First());
		if (cardById == null)
		{
			return;
		}
		string text = ai.Details.First().ValueString[0];
		List<ManaQuantity> list = (List<ManaQuantity>)(cardById.ManaCostOverride = new List<ManaQuantity>(text.Length));
		for (int i = 0; i < text.Length; i++)
		{
			ManaColor manaColor = ManaUtilities.CharToColor(text[i]);
			if (manaColor != ManaColor.None)
			{
				text = text.Remove(i, 1);
				i--;
				list.Add(ManaQuantity.MakeMana(1u, manaColor));
			}
		}
		if (uint.TryParse(text, out var result) && result != 0)
		{
			list.Insert(0, ManaQuantity.MakeGeneric(result));
		}
	}
}
