using GreClient.Rules;
using UnityEngine;
using UnityEngine.UI;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Browsers;

public class ScryBrowser : CardBrowserBase
{
	protected readonly IBasicBrowserProvider _browserProvider;

	protected readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	protected readonly float dragScrollDelay = 0.05f;

	private Scrollbar scrollbar;

	private float lastDragScrollTime;

	public ScryBrowser(BrowserManager browserManager, IDuelSceneBrowserProvider provider, ICardBuilder<DuelScene_CDC> cardBuilder, GameManager gameManager)
		: base(browserManager, provider, gameManager)
	{
		_browserProvider = _duelSceneBrowserProvider as IBasicBrowserProvider;
		_cardBuilder = cardBuilder ?? NullCardBuilder<DuelScene_CDC>.Default;
		AllowsHoverInteractions = true;
	}

	protected override ICardLayout GetCardHolderLayout()
	{
		return scrollableLayout;
	}

	protected override void SetupCards()
	{
		cardViews = _browserProvider.GetCardsToDisplay();
		MtgPlayer owner = null;
		if (cardViews.Count > 0)
		{
			owner = cardViews[0].Model.Owner;
		}
		DuelScene_CDC item = _cardBuilder.CreateCDC(CardBrowserBase.CreateLibraryPlaceHolderCardData(owner));
		cardViews.Add(item);
		MoveCardViewsToBrowser(cardViews);
	}

	protected override void InitializeUIElements()
	{
		BrowserHeader component = GetBrowserElement("Header").GetComponent<BrowserHeader>();
		component.SetHeaderText(_browserProvider.GetHeaderText());
		component.SetSubheaderText(_browserProvider.GetSubHeaderText());
		OrderIndicator component2 = GetBrowserElement("OrderIndicator").GetComponent<OrderIndicator>();
		component2.SetText(Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/OrderRequest_Generic_LeftText"), Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/OrderRequest_Generic_RightText"));
		component2.SetArrowDirection(OrderIndicator.ArrowDirection.Right);
		scrollbar = GetBrowserElement("Scrollbar").GetComponent<Scrollbar>();
		scrollbar.onValueChanged.RemoveAllListeners();
		scrollbar.onValueChanged.AddListener(OnScroll);
		scrollbar.value = scrollableLayout.ScrollPosition;
		scrollbar.gameObject.SetActive(cardViews.Count > scrollableLayout.FrontCount);
		ScrollWheelInputForScrollbar component3 = GetBrowserElement("Scrollbar").GetComponent<ScrollWheelInputForScrollbar>();
		component3.ElementCount = cardViews.Count;
		component3.ElementsFeatured = scrollableLayout.FrontCount;
		base.InitializeUIElements();
	}

	protected override void ReleaseUIElements()
	{
		scrollbar.onValueChanged.RemoveAllListeners();
		base.ReleaseUIElements();
	}

	public override bool AllowsDragInteractions(DuelScene_CDC cardView)
	{
		return cardViews.Contains(cardView);
	}

	public override void HandleDrag(DuelScene_CDC draggedCard)
	{
		float time = Time.time;
		if (time - lastDragScrollTime < dragScrollDelay)
		{
			return;
		}
		DuelScene_CDC duelScene_CDC = null;
		DuelScene_CDC duelScene_CDC2 = null;
		foreach (DuelScene_CDC cardView in cardViews)
		{
			if (cardView.IsVisible && !(cardView == draggedCard))
			{
				if (duelScene_CDC == null || cardView.Root.position.x > duelScene_CDC.Root.position.x)
				{
					duelScene_CDC = cardView;
				}
				if (duelScene_CDC2 == null || cardView.Root.position.x < duelScene_CDC2.Root.position.x)
				{
					duelScene_CDC2 = cardView;
				}
			}
		}
		if (draggedCard.Root.position.x > duelScene_CDC.Root.position.x)
		{
			scrollbar.value = Mathf.Max(scrollbar.value - 1f / (float)cardViews.Count, 0f);
			lastDragScrollTime = time;
		}
		else if (draggedCard.Root.position.x < duelScene_CDC2.Root.position.x)
		{
			scrollbar.value = Mathf.Min(scrollbar.value + 1f / (float)cardViews.Count, 1f);
			lastDragScrollTime = time;
		}
	}
}
