using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Browser;
using AssetLookupTree.Payloads.Browser.OrderWorkflow;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class OrderWorkflow : OrderBrowserWorkflow<OrderRequest>, IAutoRespondWorkflow
{
	private readonly struct BrowserData
	{
		public readonly string LeftOrderIndicatorLocKey;

		public readonly string RightOrderIndicatorLocKey;

		public readonly bool ReverseIdsOnSubmit;

		public readonly bool ReverseDisplayOrder;

		internal BrowserData(string leftOrderKey, string rightOrderKey, bool reverseOnSubmit, bool reverseDisplay)
		{
			LeftOrderIndicatorLocKey = leftOrderKey;
			RightOrderIndicatorLocKey = rightOrderKey;
			ReverseIdsOnSubmit = reverseOnSubmit;
			ReverseDisplayOrder = reverseDisplay;
		}
	}

	private readonly IClientLocProvider _locProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameplaySettingsProvider _settingsProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IBrowserController _browserController;

	private readonly AssetLookupSystem _assetLookupSystem;

	private bool _reverseIdsOnSubmit;

	public override OrderingContext GetOrderingContext()
	{
		return _request.OrderingContext;
	}

	public OrderWorkflow(OrderRequest request, IClientLocProvider locProvider, IPromptTextProvider promptTextProvider, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IGameplaySettingsProvider settingsProvider, IResolutionEffectProvider resolutionEffectProvider, IBrowserController browserController, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_locProvider = locProvider ?? NullLocProvider.Default;
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_settingsProvider = settingsProvider ?? NullGameplaySettingsProvider.Default;
		_resolutionEffectProvider = resolutionEffectProvider ?? NullResolutionEffectProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public bool TryAutoRespond()
	{
		bool flag = false;
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		if (_request.Prompt != null && _request.Prompt.PromptId == 91)
		{
			flag = true;
		}
		else if (_request.OrderingContext == OrderingContext.OrderingForBottom && _settingsProvider.FullControlDisabled && mtgGameState.LocalLibrary.TotalCardCount > 10)
		{
			flag = true;
		}
		if (flag)
		{
			_request.SubmitOrder(_request.Ids);
		}
		return flag;
	}

	protected override void ApplyInteractionInternal()
	{
		IBlackboard blackboard = _assetLookupSystem.Blackboard;
		blackboard.Clear();
		blackboard.SetCardDataExtensive(((ResolutionEffectModel)_resolutionEffectProvider.ResolutionEffect)?.Model);
		_header = GetHeader(blackboard);
		_subHeader = GetSubheader(blackboard);
		BrowserData browserData = GetBrowserData(blackboard);
		leftOrderIndicatorText = _locProvider.GetLocalizedText(browserData.LeftOrderIndicatorLocKey);
		rightOrderIndicatorText = _locProvider.GetLocalizedText(browserData.RightOrderIndicatorLocKey);
		_cardsToDisplay = _cardViewProvider.GetCardViews(_request.Ids);
		_reverseIdsOnSubmit = browserData.ReverseIdsOnSubmit;
		if (browserData.ReverseDisplayOrder)
		{
			_cardsToDisplay.Reverse();
		}
		orderIndicatorArrowDirection = OrderIndicator.ArrowDirection.Right;
		SetupButton();
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private string GetHeader(IBlackboard bb)
	{
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<Header> loadedTree))
		{
			Header payload = loadedTree.GetPayload(bb);
			if (payload != null)
			{
				return _locProvider.GetLocalizedText(payload.LocKey, payload.GetLocParams(bb));
			}
		}
		return _locProvider.GetLocalizedText("DuelScene/Browsers/OrderRequest_Bottom_Header");
	}

	private string GetSubheader(IBlackboard bb)
	{
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SubHeader> loadedTree))
		{
			SubHeader payload = loadedTree.GetPayload(bb);
			if (payload != null)
			{
				return _locProvider.GetLocalizedText(payload.LocKey, payload.GetLocParams(bb));
			}
		}
		return _request.OrderingContext switch
		{
			OrderingContext.OrderingForBottom => _locProvider.GetLocalizedText("DuelScene/Browsers/OrderRequest_Bottom_SubHeader"), 
			OrderingContext.OrderingForTop => _locProvider.GetLocalizedText("DuelScene/Browsers/OrderRequest_Top_SubHeader"), 
			_ => _promptTextProvider.GetPromptText(Prompt), 
		};
	}

	private BrowserData GetBrowserData(IBlackboard bb)
	{
		string leftOrderKey = "DuelScene/Browsers/Order_Bottom";
		string rightOrderKey = "DuelScene/Browsers/Order_Top";
		bool reverseOnSubmit = false;
		bool reverseDisplay = false;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<BrowserOverrides> loadedTree))
		{
			BrowserOverrides payload = loadedTree.GetPayload(bb);
			if (payload != null)
			{
				if (!string.IsNullOrEmpty(payload.LeftOrderIndicatorLocKey))
				{
					leftOrderKey = payload.LeftOrderIndicatorLocKey;
				}
				if (!string.IsNullOrEmpty(payload.RightOrderIndicatorLocKey))
				{
					rightOrderKey = payload.RightOrderIndicatorLocKey;
				}
				reverseOnSubmit = payload.ReverseIdsOnSubmit;
				reverseDisplay = payload.ReverseDisplayOrder;
			}
		}
		return new BrowserData(leftOrderKey, rightOrderKey, reverseOnSubmit, reverseDisplay);
	}

	protected override void Submit()
	{
		List<uint> list = new List<uint>();
		foreach (DuelScene_CDC cardView in (_openedBrowser as CardBrowserBase).GetCardViews())
		{
			if (cardView.InstanceId != 0)
			{
				list.Add(cardView.InstanceId);
			}
		}
		if (_reverseIdsOnSubmit)
		{
			list.Reverse();
		}
		_request.SubmitOrder(list);
	}
}
