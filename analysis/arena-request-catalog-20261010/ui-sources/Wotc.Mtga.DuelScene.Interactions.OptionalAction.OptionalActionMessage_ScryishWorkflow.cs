using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Browser;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class OptionalActionMessage_ScryishWorkflow : ScryishWorkflow<OptionalActionMessageRequest>
{
	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IBrowserProvider _browserProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public override int NthFromTop
	{
		get
		{
			List<CardMechanicType> mechanics = _request.Mechanics;
			if (mechanics == null || !mechanics.Contains(CardMechanicType.PutTopOrBottom) || _request.Prompt.Parameters.Count <= 1)
			{
				return 1;
			}
			return _request.Prompt.Parameters[1].NumberValue;
		}
	}

	public override int NthFromBot
	{
		get
		{
			List<CardMechanicType> mechanics = _request.Mechanics;
			if (mechanics == null || !mechanics.Contains(CardMechanicType.PutTopOrBottom) || _request.Prompt.Parameters.Count <= 2)
			{
				return 1;
			}
			return _request.Prompt.Parameters[2].NumberValue;
		}
	}

	public static bool UseScryStyleBrowser(OptionalActionMessageRequest request, AssetLookupSystem assetLookupSystem, MtgGameState gameState)
	{
		UseScryStyleBrowserPayload payload;
		return TryGetPayload(request, assetLookupSystem, gameState, out payload);
	}

	private static bool TryGetPayload(OptionalActionMessageRequest request, AssetLookupSystem assetLookupSystem, MtgGameState gameState, out UseScryStyleBrowserPayload payload)
	{
		payload = null;
		ScryishWorkflow<OptionalActionMessageRequest>.SetBlackboard(request, gameState, assetLookupSystem.Blackboard);
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<UseScryStyleBrowserPayload> loadedTree))
		{
			UseScryStyleBrowserPayload payload2 = loadedTree.GetPayload(assetLookupSystem.Blackboard);
			if (payload2 != null)
			{
				payload = payload2;
			}
		}
		return payload != null;
	}

	public OptionalActionMessage_ScryishWorkflow(OptionalActionMessageRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IResolutionEffectProvider resolutionEffectProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager, AssetLookupSystem assetLookupSystem)
		: base(request, cardDatabase, gameStateProvider, cardViewProvider, (IBrowserController)browserManager)
	{
		_clientLocProvider = cardDatabase.ClientLocProvider;
		_gameStateProvider = gameStateProvider;
		_resolutionEffectProvider = resolutionEffectProvider;
		_browserProvider = browserManager;
		_assetLookupSystem = assetLookupSystem;
	}

	protected override void SetSubHeader()
	{
		_assetLookupSystem.Blackboard.Request = _request;
		_assetLookupSystem.Blackboard.Prompt = _request.Prompt;
		_assetLookupSystem.Blackboard.CardBrowserType = DuelSceneBrowserType.Scryish;
		ResolutionEffectModel resolutionEffectModel = _resolutionEffectProvider.ResolutionEffect;
		if (resolutionEffectModel != null && resolutionEffectModel.Model != null)
		{
			_assetLookupSystem.Blackboard.SetCardDataExtensive(resolutionEffectModel.Model);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SubHeader> loadedTree))
		{
			SubHeader payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				_subHeader = _clientLocProvider.GetLocalizedText(payload.LocKey);
			}
		}
	}

	protected override IEnumerable<uint> GetCardIds()
	{
		return _request.RecipientIds;
	}

	protected override void OnBrowserOpened()
	{
		base.OnBrowserOpened();
		if (_openedBrowser is ScryishBrowser scryishBrowser)
		{
			scryishBrowser.DragReleased = (Action<DuelScene_CDC>)Delegate.Combine(scryishBrowser.DragReleased, new Action<DuelScene_CDC>(OnDragRelease));
		}
		OnDragRelease(null);
	}

	protected override void Browser_OnBrowserShown()
	{
		base.Browser_OnBrowserShown();
		if (_openedBrowser is ScryishBrowser scryishBrowser)
		{
			scryishBrowser.DragReleased = (Action<DuelScene_CDC>)Delegate.Combine(scryishBrowser.DragReleased, new Action<DuelScene_CDC>(OnDragRelease));
		}
	}

	protected override void Browser_OnBrowserHidden()
	{
		base.Browser_OnBrowserHidden();
		if (_openedBrowser is ScryishBrowser scryishBrowser)
		{
			scryishBrowser.DragReleased = (Action<DuelScene_CDC>)Delegate.Remove(scryishBrowser.DragReleased, new Action<DuelScene_CDC>(OnDragRelease));
		}
	}

	protected override void Browser_OnBrowserClosed()
	{
		base.Browser_OnBrowserClosed();
		if (_openedBrowser is ScryishBrowser scryishBrowser)
		{
			scryishBrowser.DragReleased = (Action<DuelScene_CDC>)Delegate.Remove(scryishBrowser.DragReleased, new Action<DuelScene_CDC>(OnDragRelease));
		}
	}

	private void OnDragRelease(DuelScene_CDC cardView)
	{
		if (!(_browserProvider.CurrentBrowser is ScryishBrowser scryishBrowser))
		{
			return;
		}
		List<DuelScene_CDC> cardViews = scryishBrowser.GetCardViews();
		if (cardViews != null && cardViews.Count > 0)
		{
			int num = cardViews.FindIndex(0, (DuelScene_CDC x) => x.InstanceId != 0);
			if (NthFromTop == 2 && cardViews.Count > 1 && num == 0)
			{
				cardViews[0].CurrentCardHolder.ShiftCards(num, 1);
				num = 1;
			}
			int num2 = cardViews.FindIndex(0, (DuelScene_CDC x) => x.Model.Printing.AdditionalFrameDetails.Contains("median"));
			int num3 = num2 - 1;
			int num4 = num2 + 1;
			if (num == num3 || num == num4)
			{
				_buttonStateData["DoneButton"].Enabled = true;
				_buttonStateData["DoneButton"].StyleType = ButtonStyle.StyleType.Main;
			}
			else
			{
				_buttonStateData["DoneButton"].Enabled = false;
				_buttonStateData["DoneButton"].StyleType = ButtonStyle.StyleType.Secondary;
			}
			scryishBrowser.UpdateButtons();
		}
	}

	protected override void OnDoneButtonPressed(ScryBrowser browser)
	{
		List<DuelScene_CDC> cardViews = browser.GetCardViews();
		if (cardViews != null && cardViews.Count > 0)
		{
			bool flag = false;
			if (TryGetPayload(_request, _assetLookupSystem, _gameStateProvider.LatestGameState, out var payload))
			{
				flag = payload.InvertResponse;
			}
			int num = cardViews.FindIndex(0, cardViews.Count, (DuelScene_CDC x) => x.InstanceId != 0);
			OptionResponse optionResponse = ((cardViews.FindIndex(0, cardViews.Count, (DuelScene_CDC x) => x.Model.Printing.AdditionalFrameDetails.Contains("median")) >= num) ? OptionResponse.AllowYes : OptionResponse.CancelNo);
			if (flag)
			{
				optionResponse = ((optionResponse != OptionResponse.AllowYes) ? OptionResponse.AllowYes : OptionResponse.CancelNo);
			}
			_request.SubmitResponse(optionResponse);
		}
		else
		{
			_request.SubmitResponse(OptionResponse.CancelNo);
		}
	}
}
