using System.Collections.Generic;
using Core.Code.Input;
using GreClient.CardData;
using GreClient.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.UXEvents;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Wish_DEBUG : WorkflowBase<SelectNRequest>
{
	private static uint _previouslyWishedForGrpId;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private bool _submitted;

	private WishGUI _wishInterface;

	public SelectNWorkflow_Wish_DEBUG(SelectNRequest request, ICardDatabaseAdapter cardDatabase)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
	}

	public override bool CanApply(List<UXEvent> events)
	{
		if (_submitted)
		{
			return false;
		}
		if (_previouslyWishedForGrpId != 0 && InputHelper.KeyIsPressed(Key.LeftShift))
		{
			_request.SubmitSelection(_previouslyWishedForGrpId);
			_submitted = true;
			return false;
		}
		return base.CanApply(events);
	}

	protected override void ApplyInteractionInternal()
	{
		GameObject gameObject = new GameObject("DEBUG_WISH");
		_wishInterface = gameObject.AddComponent<WishGUI>();
		_wishInterface.Init(_request.CanCancel, _cardDatabase, (CardPrintingData cardPrinting) => cardPrinting.ExpansionCode == "ArenaSUP" || !CardUtilities.IsMultifacetParent(cardPrinting.LinkedFaceType));
		_wishInterface.WishSelectedHandlers += OnSelect;
	}

	public override void CleanUp()
	{
		if ((bool)_wishInterface)
		{
			_wishInterface.WishSelectedHandlers -= OnSelect;
			GUIUtility.keyboardControl = 0;
			Object.Destroy(_wishInterface.gameObject);
			_wishInterface = null;
		}
		base.CleanUp();
	}

	private void OnSelect(uint grpId)
	{
		if (grpId == 0)
		{
			_request.Cancel();
			return;
		}
		_previouslyWishedForGrpId = grpId;
		_request.SubmitSelection(grpId);
	}
}
