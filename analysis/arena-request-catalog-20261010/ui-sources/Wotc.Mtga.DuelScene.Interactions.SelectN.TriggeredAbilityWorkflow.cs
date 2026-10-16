using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Prefab;
using GreClient.Rules;
using Pooling;
using UnityEngine;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Hangers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class TriggeredAbilityWorkflow : OrderBrowserWorkflow<SelectNRequest>, IAutoRespondWorkflow
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IClientLocProvider _locProvider;

	private readonly IBrowserController _browserController;

	private readonly IUnityObjectPool _unityObjPool;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IFaceInfoGenerator _faceGenerator;

	private readonly CardViewBuilder _cardBuilder;

	private readonly Transform _faceHangerParent;

	private FaceHanger _faceHanger;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.TriggerOrder;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "TriggerOrder";
	}

	public override OrderingContext GetOrderingContext()
	{
		return OrderingContext.None;
	}

	public TriggeredAbilityWorkflow(SelectNRequest request, ICardViewProvider cardViewProvider, IClientLocProvider locProvider, IBrowserController browserController, IUnityObjectPool unityObjectPool, IFaceInfoGenerator faceInfoGenerator, AssetLookupSystem assetLookupSystem, CardViewBuilder cardViewBuilder, Transform faceHangerParent)
		: base(request)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_locProvider = locProvider ?? NullLocProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_unityObjPool = unityObjectPool ?? NullUnityObjectPool.Default;
		_faceGenerator = faceInfoGenerator ?? NullFaceInfoGenerator.Default;
		_assetLookupSystem = assetLookupSystem;
		_cardBuilder = cardViewBuilder;
		_faceHangerParent = faceHangerParent;
	}

	protected override void ApplyInteractionInternal()
	{
		leftOrderIndicatorText = _locProvider.GetLocalizedText("DuelScene/Browsers/OrderTriggeredAbilities_Last");
		rightOrderIndicatorText = _locProvider.GetLocalizedText("DuelScene/Browsers/OrderTriggeredAbilities_First");
		_header = _locProvider.GetLocalizedText("DuelScene/Browsers/OrderTriggeredAbilities_Header");
		_subHeader = _locProvider.GetLocalizedText("DuelScene/Browsers/OrderTriggeredAbilities_SubHeader");
		foreach (uint id in _request.Ids)
		{
			DuelScene_CDC cardView = _cardViewProvider.GetCardView(id);
			_cardsToDisplay.Add(cardView);
		}
		SetUpFaceHanger();
		SetupButton();
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
		Browser_OnBrowserShown();
	}

	private void SetUpFaceHanger()
	{
		IBlackboard blackboard = _assetLookupSystem.Blackboard;
		blackboard.Clear();
		blackboard.Interaction = this;
		FaceHangerPrefab payload = _assetLookupSystem.TreeLoader.LoadTree<FaceHangerPrefab>().GetPayload(blackboard);
		GameObject gameObject = _unityObjPool.PopObject(payload.PrefabPath, _faceHangerParent);
		if (!(gameObject == null))
		{
			_faceHanger = gameObject.GetComponent<FaceHanger>();
			_faceHanger?.Init(_faceGenerator, _cardBuilder);
		}
	}

	protected override void Submit()
	{
		List<uint> selections = (_openedBrowser as CardBrowserBase).GetCardViews().ConvertAll((DuelScene_CDC x) => x.InstanceId);
		_request.SubmitSelection(selections, OrderingType.OrderAsIndicated);
	}

	private void OnCardHovered(DuelScene_CDC card)
	{
		if (!(_faceHanger == null))
		{
			if (_cardsToDisplay.Contains(card))
			{
				_faceHanger.ActivateHanger(card, card.Model, new HangerSituation
				{
					DelayActivation = true
				});
			}
			else
			{
				_faceHanger.DeactivateHanger();
			}
		}
	}

	protected override void Browser_OnBrowserShown()
	{
		CardHoverController.OnHoveredCardUpdated += OnCardHovered;
		base.Browser_OnBrowserShown();
	}

	protected override void Browser_OnBrowserHidden()
	{
		CardHoverController.OnHoveredCardUpdated -= OnCardHovered;
		base.Browser_OnBrowserHidden();
	}

	public bool TryAutoRespond()
	{
		if (MDNPlayerPrefs.AutoOrderTriggers)
		{
			_request.SubmitArbitrary();
			return true;
		}
		return false;
	}

	public override void CleanUp()
	{
		if ((bool)_faceHanger)
		{
			_faceHanger.DeactivateHanger();
			_faceHanger.Cleanup();
			_unityObjPool.PushObject(_faceHanger.gameObject);
		}
		base.CleanUp();
	}
}
