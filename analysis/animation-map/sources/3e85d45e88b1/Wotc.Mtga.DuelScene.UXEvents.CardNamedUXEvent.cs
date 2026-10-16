using GreClient.CardData;
using UnityEngine;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardNamedUXEvent : UXEvent
{
	private readonly uint _playerId;

	private readonly uint _locId;

	private readonly IEntityDialogControllerProvider _dialogueProvider;

	private readonly IGreLocProvider _locManager;

	public CardNamedUXEvent(uint playerId, uint locId, IEntityDialogControllerProvider dialogueProvider, IGreLocProvider locManager)
	{
		_playerId = playerId;
		_locId = locId;
		_dialogueProvider = dialogueProvider ?? NullEntityDialogControllerProvider.Default;
		_locManager = locManager ?? NullGreLocManager.Default;
	}

	public override void Execute()
	{
		if (_dialogueProvider.TryGetDialogControllerById(_playerId, out var dialogController))
		{
			string localizedText = _locManager.GetLocalizedText(_locId);
			if (!string.IsNullOrEmpty(localizedText))
			{
				dialogController.ShowPlayerChoice(CardUtilities.FormatComplexTitle(localizedText));
			}
		}
		else
		{
			Debug.LogErrorFormat("CardNamedUXEvent could not find Avatar object for player id: {0}", _playerId);
		}
		Complete();
	}
}
