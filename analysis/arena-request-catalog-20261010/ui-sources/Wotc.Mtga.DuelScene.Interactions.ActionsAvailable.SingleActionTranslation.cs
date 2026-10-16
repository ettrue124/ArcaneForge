using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public abstract class SingleActionTranslation : ICardViewVariantTranslation
{
	public WorkflowVariant Translate(DuelScene_CDC cardView, List<GreInteraction> interactions)
	{
		if (interactions.Count != 1)
		{
			return null;
		}
		GreInteraction greInteraction = interactions[0];
		if (greInteraction == null)
		{
			return null;
		}
		Action greAction = greInteraction.GreAction;
		if (greAction == null)
		{
			return null;
		}
		return TranslateAction(cardView, greAction);
	}

	protected abstract WorkflowVariant TranslateAction(DuelScene_CDC cardView, Action action);
}
