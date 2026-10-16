using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class LibraryCardHolder : ZoneCardHolderBase, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler, IHoverableZone
{
	[SerializeField]
	private BoxCollider _inputCollider;

	[SerializeField]
	private float _faceUpThicknessMultiplier = 10f;

	[SerializeField]
	private Vector3 _faceUpOffsetMod = new Vector3(0f, 0f, 0f);

	[SerializeField]
	private float _cardThickness = 0.019f;

	private CardLayout_General _generalLayout;

	public DuelScene_CDC TopCard { get; private set; }

	public event Action<MtgZone> Hovered;

	protected override void OnDestroy()
	{
		Hovered = null;
		base.OnDestroy();
	}

	public override void Init(GameManager gameManager, ICardViewManager cardViewManager, ISplineMovementSystem splineMovementSystem, CardViewBuilder cardViewBuilder, IClientLocProvider locMan, MatchManager matchManager)
	{
		base.Init(gameManager, cardViewManager, splineMovementSystem, cardViewBuilder, locMan, matchManager);
		_orderType = IdOrderType.Normal;
		_generalLayout = new CardLayout_General();
		_generalLayout.StrictSplayOffset = 0.001f;
		_generalLayout.MinDegreesRotationOffset = -2f;
		_generalLayout.MaxDegreesRotationOffset = 2f;
		_generalLayout.CardThickness = _cardThickness;
		_generalLayout.IsReversedDisplay = true;
		Layout = _generalLayout;
	}

	protected override void HandleAddedCard(DuelScene_CDC cardView)
	{
		int count = _cardViews.Count;
		base.HandleAddedCard(cardView);
		if (count > 10)
		{
			ICardDataAdapter model = cardView.Model;
			if (model == null || model.Instance?.Visibility != Visibility.Public)
			{
				cardView.ModelOverride = new ModelOverride(null, null, null, true);
			}
		}
		if (count == 0 && cardView.ActiveScaffold == null)
		{
			cardView.CurrentCardHolder = this;
			cardView.UpdateVisibility(shouldBeVisible: true);
			cardView.UpdateVisuals();
			cardView.ImmediateUpdate();
		}
	}

	public override void RemoveCard(DuelScene_CDC cardView)
	{
		base.RemoveCard(cardView);
		cardView.ClearOverrides();
	}

	protected override void OnPreLayout()
	{
		base.OnPreLayout();
		TopCard = ((CardViews.Count > 0) ? CardViews[0] : null);
		for (int i = 0; i < CardViews.Count; i++)
		{
			if (i > 10)
			{
				ICardDataAdapter model = CardViews[i].Model;
				if (model == null || model.Instance?.Visibility != Visibility.Public)
				{
					CardViews[i].ModelOverride = new ModelOverride(null, null, null, true);
					continue;
				}
			}
			CardViews[i].ClearOverrides();
		}
	}

	public override IdealPoint GetLayoutEndpoint(CardLayoutData data)
	{
		int num = _previousLayoutData.IndexOf(data);
		int num2 = _previousLayoutData.Count - 1;
		if (num == num2)
		{
			data.Position += calcFaceUpTopCardOffset();
		}
		else if (num == num2 - 1)
		{
			data.Position += Vector3.back * (_generalLayout.CardThickness * _faceUpThicknessMultiplier);
		}
		return base.GetLayoutEndpoint(data);
		Vector3 calcFaceUpTopCardOffset()
		{
			Vector3 result = Vector3.back * (_generalLayout.CardThickness * _faceUpThicknessMultiplier * 2f);
			if (cardHasActions())
			{
				result += _faceUpOffsetMod;
			}
			return result;
		}
		bool cardHasActions()
		{
			DuelScene_CDC card = data.Card;
			if (card == null)
			{
				return false;
			}
			ICardDataAdapter model = card.Model;
			if (model == null)
			{
				return false;
			}
			return model.Actions.Count > 0;
		}
	}

	protected override void OnPostLayout()
	{
		base.OnPostLayout();
		AdjustCollider();
	}

	private void AdjustCollider()
	{
		if (_previousLayoutData.Find((CardLayoutData x) => x.Card == TopCard) != null)
		{
			_inputCollider.size = TopCard.Collider.size * CardScale;
		}
	}

	void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
	{
		Hovered?.Invoke(_zoneModel);
	}

	void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
	{
		Hovered?.Invoke(null);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		ViewLibrary(_gameManager.InteractionSystem.HandleViewDismissCardClick);
	}

	public void ViewLibrary(Action<DuelScene_CDC> onCardClicked)
	{
		if (playerNum == GREPlayerNum.LocalPlayer && _gameManager.GetPlayerInfoForNum(GREPlayerNum.LocalPlayer).SideboardCards.Count() > 0)
		{
			LibrarySideboardBrowserProvider librarySideboardBrowserProvider = new LibrarySideboardBrowserProvider(_gameManager, _locManager, onCardClicked);
			IBrowser openedBrowser = _gameManager.BrowserManager.OpenBrowser(librarySideboardBrowserProvider);
			librarySideboardBrowserProvider.SetOpenedBrowser(openedBrowser);
			return;
		}
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (uint cardId in _zoneModel.CardIds)
		{
			if (_cardViewProvider.TryGetCardView(cardId, out var cardView))
			{
				list.Add(cardView);
			}
		}
		ViewDismissBrowserProvider viewDismissBrowserProvider = new ViewDismissBrowserProvider(list, null, Languages.ActiveLocProvider.GetLocalizedText("Enum/ZoneType/ZoneType_Library"), onCardClicked);
		IBrowser openedBrowser2 = _gameManager.BrowserManager.OpenBrowser(viewDismissBrowserProvider);
		viewDismissBrowserProvider.SetOpenedBrowser(openedBrowser2);
	}
}
