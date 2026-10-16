using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine.UI;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Browsers;

public class OrderCardsBrowser : CardBrowserBase, ISortedBrowser
{
	private class RelativeListComparer<T> : IComparer<T>
	{
		private List<T> _relativeCollection = new List<T>();

		public IComparer<T> SetRelativeCOllection(List<T> relativeCollection)
		{
			_relativeCollection = relativeCollection ?? new List<T>();
			return this;
		}

		public int Compare(T x, T y)
		{
			if (x == null || y == null)
			{
				return (x == null).CompareTo(y == null);
			}
			int num = _relativeCollection.IndexOf(x);
			int value = _relativeCollection.IndexOf(y);
			return num.CompareTo(value);
		}
	}

	private readonly IOrderBrowserProvider _browserProvider;

	private readonly RelativeListComparer<DuelScene_CDC> _relativeComparer = new RelativeListComparer<DuelScene_CDC>();

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	public OrderCardsBrowser(BrowserManager browserManager, IDuelSceneBrowserProvider provider, ICardBuilder<DuelScene_CDC> cardBuilder, GameManager gameManager)
		: base(browserManager, provider, gameManager)
	{
		_cardBuilder = cardBuilder;
		_browserProvider = (IOrderBrowserProvider)_duelSceneBrowserProvider;
		AllowsHoverInteractions = true;
	}

	protected override void SetupCards()
	{
		cardHolder.IgnoreDummyCards = true;
		cardViews = _browserProvider.GetCardsToDisplay();
		DuelScene_CDC duelScene_CDC = null;
		if (_browserProvider.GetOrderingContext() != OrderingContext.None)
		{
			MtgPlayer owner = null;
			if (cardViews.Count > 0)
			{
				owner = cardViews[0].Model.Owner;
			}
			duelScene_CDC = _cardBuilder.CreateCDC(CardBrowserBase.CreateLibraryPlaceHolderCardData(owner));
		}
		if (duelScene_CDC != null)
		{
			if (_browserProvider.GetOrderingContext() == OrderingContext.OrderingForBottom)
			{
				cardViews.Add(duelScene_CDC);
			}
			else
			{
				cardViews.Insert(0, duelScene_CDC);
			}
		}
		MoveCardViewsToBrowser(cardViews);
	}

	protected override void ReleaseCards()
	{
		base.ReleaseCards();
		cardHolder.IgnoreDummyCards = false;
	}

	protected override void InitializeUIElements()
	{
		BrowserHeader component = GetBrowserElement("Header").GetComponent<BrowserHeader>();
		if ((object)component != null)
		{
			component.SetHeaderText(_browserProvider.GetHeaderText());
			component.SetSubheaderText(_browserProvider.GetSubHeaderText());
		}
		else
		{
			GetBrowserElement("Header").GetComponent<BrowserSubtitle>()?.SetSubTitleText(_browserProvider.GetSubHeaderText());
		}
		OrderIndicator component2 = GetBrowserElement("OrderIndicator").GetComponent<OrderIndicator>();
		component2.SetText(_browserProvider.GetLeftOrderText(), _browserProvider.GetRightOrderText());
		component2.SetArrowDirection(_browserProvider.GetArrowDirection());
		Scrollbar component3 = GetBrowserElement("Scrollbar").GetComponent<Scrollbar>();
		component3.onValueChanged.RemoveAllListeners();
		component3.onValueChanged.AddListener(OnScroll);
		component3.value = scrollableLayout.ScrollPosition;
		component3.gameObject.SetActive(cardViews.Count > scrollableLayout.FrontCount);
		ScrollWheelInputForScrollbar component4 = GetBrowserElement("Scrollbar").GetComponent<ScrollWheelInputForScrollbar>();
		component4.ElementCount = cardViews.Count;
		component4.ElementsFeatured = scrollableLayout.FrontCount;
		base.InitializeUIElements();
	}

	public override bool AllowsDragInteractions(DuelScene_CDC cardView)
	{
		return cardViews.Contains(cardView);
	}

	public void Sort(List<DuelScene_CDC> toSort)
	{
		toSort.Sort(_relativeComparer.SetRelativeCOllection(cardViews));
	}
}
