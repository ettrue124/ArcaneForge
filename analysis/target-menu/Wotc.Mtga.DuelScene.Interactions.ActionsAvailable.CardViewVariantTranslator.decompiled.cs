using System;
using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class CardViewVariantTranslator : IVariantWorkflowTranslator
{
	private readonly ICardViewVariantTranslation[] _translations;

	public CardViewVariantTranslator(params ICardViewVariantTranslation[] translations)
	{
		_translations = translations ?? Array.Empty<ICardViewVariantTranslation>();
	}

	public WorkflowVariant Translate(IEntityView entityView, List<GreInteraction> interactions)
	{
		if (!(entityView is DuelScene_CDC cardView))
		{
			return null;
		}
		ICardViewVariantTranslation[] translations = _translations;
		for (int i = 0; i < translations.Length; i++)
		{
			WorkflowVariant workflowVariant = translations[i].Translate(cardView, interactions);
			if (workflowVariant != null)
			{
				return workflowVariant;
			}
		}
		return null;
	}
}
