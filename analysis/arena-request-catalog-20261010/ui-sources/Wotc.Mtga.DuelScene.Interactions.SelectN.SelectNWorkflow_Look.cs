using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Look : WorkflowBase<SelectNRequest>
{
	private readonly IObjectPool _objectPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardViewManager _cardViewManager;

	private readonly IBrowserController _browserController;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly List<string> _fakeCardIds;

	private string FAKE_CARD_KEY_FORMAT = "FAKE_LOOK_BROWSER_CARD_{0}";

	public SelectNWorkflow_Look(SelectNRequest selectNRequest, IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, ICardViewManager cardViewManager, IBrowserController browserController, IFakeCardViewController fakeCardViewController)
		: base(selectNRequest)
	{
		_objectPool = objectPool;
		_cardDatabase = cardDatabase;
		_cardViewManager = cardViewManager;
		_browserController = browserController;
		_fakeCardViewController = fakeCardViewController;
		_fakeCardIds = _objectPool.PopObject<List<string>>();
	}

	protected override void ApplyInteractionInternal()
	{
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (uint id in _request.Ids)
		{
			uint cardUpdatedId = _cardViewManager.GetCardUpdatedId(id);
			if (_cardViewManager.TryGetCardView(cardUpdatedId, out var cardView))
			{
				list.Add(ShowCardFaceUp(cardView));
			}
		}
		ViewDismissBrowserProvider viewDismissBrowserProvider = new ViewDismissBrowserProvider(list, OnViewDismissed, Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Seen_Cards_Title"));
		viewDismissBrowserProvider.SetOpenedBrowser(_browserController.OpenBrowser(viewDismissBrowserProvider));
	}

	private void OnViewDismissed()
	{
		_request.SubmitSelection(Array.Empty<uint>());
	}

	private DuelScene_CDC ShowCardFaceUp(DuelScene_CDC card)
	{
		if (card.VisualModel.Instance.FaceDownState.IsFaceDown && _cardDatabase.CardDataProvider.TryGetCardPrintingById(card.Model.GrpId, out var card2))
		{
			string text = string.Format(FAKE_CARD_KEY_FORMAT, card.InstanceId.ToString());
			_fakeCardIds.Add(text);
			DuelScene_CDC duelScene_CDC = _fakeCardViewController.CreateFakeCard(text, new CardData(card.Model.Instance.GetCopy(), card2));
			duelScene_CDC.Model.Instance.IgnoreFaceDownAnnotation();
			duelScene_CDC.Model.Instance.IgnoreCopiedFaceDownAnnotation();
			return duelScene_CDC;
		}
		return card;
	}

	public override void CleanUp()
	{
		base.CleanUp();
		while (_fakeCardIds.Count > 0)
		{
			_fakeCardViewController.DeleteFakeCard(_fakeCardIds[0]);
			_fakeCardIds.RemoveAt(0);
		}
		_objectPool.PushObject(_fakeCardIds, tryClear: false);
	}
}
