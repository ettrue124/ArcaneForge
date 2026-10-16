using System;
using GreClient.Rules;
using UnityEngine.InputSystem;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ActionsAvailableWorkflow_NPE : ActionsAvailableWorkflow
{
	private readonly NPEDirector _director;

	private readonly ICardHolderProvider _cardHolderProvider;

	public ActionsAvailableWorkflow_NPE(ActionsAvailableRequest actionsAvailableRequest, GameManager gameManager, ClientSideInteraction clientSideInteraction, IContext context, NPEDirector director)
		: base(actionsAvailableRequest, gameManager, clientSideInteraction, context)
	{
		_director = director;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
	}

	protected override void SubmitAction(GreInteraction interaction)
	{
		Wotc.Mtgo.Gre.External.Messaging.Action greAction = interaction.GreAction;
		Interception interception = _director.GetInterception();
		System.Action action = _director.PromptButtonFunctionality();
		if (greAction.ActionType == ActionType.Pass && action != null)
		{
			action();
		}
		else if (greAction.ActionType == ActionType.Cast && !_director.CanAffordAction(greAction) && interception != null)
		{
			_director.IssueWarning(interception);
			if (_cardHolderProvider.TryGetCardHolder(GREPlayerNum.LocalPlayer, CardHolderType.Hand, out var cardHolder))
			{
				cardHolder.LayoutNow();
			}
		}
		else
		{
			base.SubmitAction(interaction);
		}
	}

	protected override System.Action GetPromptButtonAction()
	{
		System.Action action = _director.PromptButtonFunctionality();
		if (action == null)
		{
			return base.GetPromptButtonAction();
		}
		return action;
	}

	public override bool CanKeyUp(Key key)
	{
		if (key == Key.Q)
		{
			return false;
		}
		return base.CanKeyUp(key);
	}
}
