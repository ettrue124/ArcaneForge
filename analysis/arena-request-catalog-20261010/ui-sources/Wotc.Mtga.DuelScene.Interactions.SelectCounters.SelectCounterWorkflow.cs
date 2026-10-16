using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Browser;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using Pooling;
using UnityEngine;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectCounters;

public class SelectCounterWorkflow : WorkflowBase<SelectCountersRequest>, IClickableWorkflow, IDuelSceneBrowserProvider, IBrowserHeaderProvider, IButtonScrollListBrowserProvider, IAutoRespondWorkflow
{
	private class SelectCountersHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _instanceIds;

		public SelectCountersHighlightsGenerator(IReadOnlyCollection<uint> instanceIds)
		{
			_instanceIds = instanceIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint instanceId in _instanceIds)
			{
				highlights.IdToHighlightType_Workflow[instanceId] = HighlightType.Hot;
			}
			return highlights;
		}
	}

	private readonly uint _minSelect;

	private readonly uint _maxSelect;

	private readonly List<CounterPair> _counterPairs;

	private readonly IUnityObjectPool _unityPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private StackCardHolder _stackCache;

	private IBattlefieldCardHolder _battlefieldCache;

	private readonly Dictionary<uint, List<CounterType>> _instanceToCounterTypes = new Dictionary<uint, List<CounterType>>();

	private readonly Dictionary<uint, List<CounterType>> _instanceToSelectedCounterTypes = new Dictionary<uint, List<CounterType>>();

	private ButtonScrollListBrowser _scrollListBrowser;

	private readonly Dictionary<string, ButtonStateData> _browserButtonData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _browserScrollListButtonData = new Dictionary<string, ButtonStateData>();

	private string _header;

	private string _subHeader;

	private readonly AssetLookupTree<ViewCounterUIPrefab> _counterPrefabLookupTree;

	private StackCardHolder Stack => _stackCache ?? (_stackCache = _cardHolderProvider.GetCardHolder<StackCardHolder>(GREPlayerNum.Invalid, CardHolderType.Stack));

	private IBattlefieldCardHolder Battlefield => _battlefieldCache ?? (_battlefieldCache = _cardHolderProvider.GetCardHolder<IBattlefieldCardHolder>(GREPlayerNum.Invalid, CardHolderType.Battlefield));

	public SelectCounterWorkflow(SelectCountersRequest request, IUnityObjectPool unityObjectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_minSelect = request.MinSelect;
		_maxSelect = request.MaxSelect;
		_counterPairs = new List<CounterPair>(request.CounterPairs);
		_unityPool = unityObjectPool ?? NullUnityObjectPool.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_counterPrefabLookupTree = _assetLookupSystem.TreeLoader.LoadTree<ViewCounterUIPrefab>();
		_highlightsGenerator = new SelectCountersHighlightsGenerator(_instanceToCounterTypes.Keys);
	}

	protected override void ApplyInteractionInternal()
	{
		SetHeaderAndSubheader();
		SetInstanceToCounterTypes();
		SetButtons();
		Stack.TryAutoDock(new List<uint>(_instanceToCounterTypes.Keys));
		if (_instanceToCounterTypes.Keys.Count == 1)
		{
			uint instanceId = _counterPairs[0].InstanceId;
			if (_instanceToCounterTypes[instanceId].Count > 1)
			{
				OpenCounterSelectionBrowser(instanceId);
			}
		}
	}

	private void SetInstanceToCounterTypes()
	{
		_instanceToCounterTypes.Clear();
		foreach (CounterPair counterPair in _counterPairs)
		{
			if (!_instanceToCounterTypes.ContainsKey(counterPair.InstanceId))
			{
				_instanceToCounterTypes.Add(counterPair.InstanceId, new List<CounterType>());
			}
			_instanceToCounterTypes[counterPair.InstanceId].Add(counterPair.CounterType);
		}
		foreach (List<CounterType> value in _instanceToCounterTypes.Values)
		{
			value.Sort();
		}
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax((int)_minSelect, _maxSelect);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.ButtonScrollList);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	protected override void SetPrompt()
	{
		_workflowPrompt.Reset();
		if (TryGetPromptOverride(_request.SourceId, _gameStateProvider.LatestGameState, out var promptOverride))
		{
			_workflowPrompt.LocKey = promptOverride.Key;
		}
		else
		{
			_workflowPrompt.GrePrompt = Prompt;
		}
		OnUpdatePrompt(_workflowPrompt);
	}

	private bool TryGetPromptOverride(uint sourceID, MtgGameState gameState, out PromptPayload promptOverride)
	{
		promptOverride = null;
		if (gameState.TryGetCard(sourceID, out var card))
		{
			IBlackboard blackboard = _assetLookupSystem.Blackboard;
			blackboard.Clear();
			blackboard.SetCardDataExtensive(card);
			AssetLookupTree<PromptPayload> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<PromptPayload>();
			if (assetLookupTree != null)
			{
				PromptPayload payload = assetLookupTree.GetPayload(blackboard);
				if (payload != null)
				{
					promptOverride = payload;
				}
			}
			blackboard.Clear();
		}
		return promptOverride != null;
	}

	public override void CleanUp()
	{
		if (_stackCache != null)
		{
			_stackCache.ResetAutoDock();
			_stackCache = null;
		}
		_battlefieldCache = null;
		if (_scrollListBrowser != null)
		{
			_scrollListBrowser.Close();
		}
		base.CleanUp();
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	private void MakeSelection(uint instanceId, CounterType counterType)
	{
		if (!_instanceToSelectedCounterTypes.ContainsKey(instanceId))
		{
			_instanceToSelectedCounterTypes.Add(instanceId, new List<CounterType>());
		}
		_instanceToSelectedCounterTypes[instanceId].Add(counterType);
		int num = 0;
		foreach (List<CounterType> value in _instanceToSelectedCounterTypes.Values)
		{
			num += value.Count;
		}
		if (num == _maxSelect)
		{
			SubmitSelections();
		}
	}

	private void SubmitSelections()
	{
		List<CounterPair> list = new List<CounterPair>();
		foreach (KeyValuePair<uint, List<CounterType>> instanceToSelectedCounterType in _instanceToSelectedCounterTypes)
		{
			foreach (CounterType item in instanceToSelectedCounterType.Value)
			{
				list.Add(new CounterPair
				{
					InstanceId = instanceToSelectedCounterType.Key,
					CounterType = item
				});
			}
		}
		_request.SubmitCountersResponse(list);
	}

	private void OpenCounterSelectionBrowser(uint instanceId)
	{
		UpdateNonScrollListBrowserButtons();
		UpdateScrollListBrowserButtons(instanceId, _gameStateProvider.LatestGameState);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary || _scrollListBrowser != null)
		{
			return false;
		}
		return _instanceToCounterTypes.ContainsKey(entity.InstanceId);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (WorkflowBase.TryRerouteClick(entity.InstanceId, Battlefield, isSelecting: true, out var reroutedEntityView))
		{
			entity = reroutedEntityView;
		}
		if (_instanceToCounterTypes.ContainsKey(entity.InstanceId))
		{
			if (_instanceToCounterTypes[entity.InstanceId].Count == 1)
			{
				MakeSelection(entity.InstanceId, _instanceToCounterTypes[entity.InstanceId][0]);
			}
			else
			{
				OpenCounterSelectionBrowser(entity.InstanceId);
			}
		}
	}

	public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
	{
		return false;
	}

	public void OnClickStack(CdcStackCounterView entity)
	{
	}

	public void OnBattlefieldClick()
	{
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed = new Dictionary<uint, bool>(_instanceToCounterTypes.Count);
		foreach (uint key in _instanceToCounterTypes.Keys)
		{
			Dimming.IdToIsDimmed[key] = false;
		}
		OnUpdateDimming(Dimming);
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonScrollList;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _browserButtonData;
	}

	private void Browser_OnButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitSelections();
		}
		else if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
		else if (buttonKey.StartsWith("Counter_"))
		{
			string[] array = buttonKey.Split('_');
			uint instanceId = uint.Parse(array[1]);
			uint counterType = uint.Parse(array[2]);
			MakeSelection(instanceId, (CounterType)counterType);
		}
	}

	private void Browser_OnClosed()
	{
		_scrollListBrowser.ButtonPressedHandlers -= Browser_OnButtonPressed;
		_scrollListBrowser.ClosedHandlers -= Browser_OnClosed;
		_scrollListBrowser = null;
	}

	private void SetOpenedBrowser(IBrowser browser)
	{
		_scrollListBrowser = (ButtonScrollListBrowser)browser;
		_scrollListBrowser.ButtonPressedHandlers += Browser_OnButtonPressed;
		_scrollListBrowser.ClosedHandlers += Browser_OnClosed;
	}

	public string GetHeaderText()
	{
		return _header;
	}

	public string GetSubHeaderText()
	{
		return _subHeader;
	}

	public Dictionary<string, ButtonStateData> GetScrollListButtonDataByKey()
	{
		return _browserScrollListButtonData;
	}

	protected override void SetButtons()
	{
		if (_request.CanCancel)
		{
			ButtonStyle.StyleType style = ((Buttons.WorkflowButtons.Count == 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = ((_request.CancellationType == AllowCancel.Continue) ? "DuelScene/ClientPrompt/Decline_Action" : "DuelScene/Browsers/Browser_CancelText"),
				Style = style,
				ButtonCallback = () =>
				{
					_request.Cancel();
				},
				ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
			};
		}
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = () =>
				{
					_request.Undo();
				}
			};
		}
		OnUpdateButtons(Buttons);
	}

	private void UpdateNonScrollListBrowserButtons()
	{
		_browserButtonData.Clear();
		bool canCancel = _request.CanCancel;
		int num = 0;
		foreach (List<CounterType> value in _instanceToSelectedCounterTypes.Values)
		{
			num += value.Count;
		}
		if (_minSelect != _maxSelect)
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			MTGALocalizedString localizedString = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					num.ToString()
				} }
			};
			buttonStateData.LocalizedString = localizedString;
			buttonStateData.BrowserElementKey = (canCancel ? "2Button_Left" : "SingleButton");
			buttonStateData.Enabled = num >= _minSelect && num <= _maxSelect;
			buttonStateData.StyleType = ((num > 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			_browserButtonData.Add("DoneButton", buttonStateData);
		}
		if (canCancel)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = ((_request.CancellationType == AllowCancel.Continue) ? "DuelScene/ClientPrompt/Decline_Action" : "DuelScene/Browsers/Browser_CancelText");
			buttonStateData2.BrowserElementKey = ((_browserButtonData.Count > 0) ? "2Button_Right" : "SingleButton");
			buttonStateData2.Enabled = true;
			buttonStateData2.StyleType = ((_browserButtonData.Count <= 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			_browserButtonData.Add("CancelButton", buttonStateData2);
		}
	}

	private void UpdateScrollListBrowserButtons(uint instanceId, MtgGameState gameState)
	{
		foreach (KeyValuePair<string, ButtonStateData> browserScrollListButtonDatum in _browserScrollListButtonData)
		{
			ButtonStateData value = browserScrollListButtonDatum.Value;
			GameObject gameObject = value.ChildView.gameObject;
			if (!(gameObject == null))
			{
				value.ChildView = null;
				if (!(gameObject.GetComponent<ViewCounter_UI>() == null))
				{
					_unityPool.PushObject(gameObject);
				}
			}
		}
		_browserScrollListButtonData.Clear();
		MtgEntity entityById = gameState.GetEntityById(instanceId);
		foreach (CounterType item in _instanceToCounterTypes[instanceId])
		{
			string text = $"Counter_{instanceId}_{(uint)item}";
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.BrowserElementKey = "ButtonDefault";
			string localizedTextForEnumValue = _cardDatabase.GreLocProvider.GetLocalizedTextForEnumValue("CounterType", (int)item);
			buttonStateData.LocalizedString = new UnlocalizedMTGAString(localizedTextForEnumValue);
			buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
			CounterAssetData counterAsset = CounterAssetUtil.GetCounterAsset(_assetLookupSystem, item, null, CardHolderType.Battlefield, GetBrowserType());
			if (counterAsset != null)
			{
				_assetLookupSystem.Blackboard.Clear();
				_assetLookupSystem.Blackboard.CounterType = item;
				ViewCounterUIPrefab payload = _counterPrefabLookupTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					ViewCounter_UI component = _unityPool.PopObject(payload.ViewCounterRef.RelativePath).GetComponent<ViewCounter_UI>();
					component.gameObject.name = $"{text}_CounterView";
					component.SetBackground(counterAsset.UiSpritePath);
					component.SetCount((uint)entityById.Counters[item]);
					buttonStateData.ChildView = component.transform as RectTransform;
				}
				_browserScrollListButtonData.Add(text, buttonStateData);
			}
		}
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
	}

	public bool TryAutoRespond()
	{
		SetInstanceToCounterTypes();
		if (_instanceToCounterTypes.Keys.Count == 1)
		{
			uint instanceId = _counterPairs[0].InstanceId;
			if (_instanceToCounterTypes[instanceId].Count == 1 && _request.MinSelect == _request.MaxSelect && _request.CancellationType != AllowCancel.Continue)
			{
				MakeSelection(instanceId, _instanceToCounterTypes[instanceId][0]);
				return true;
			}
		}
		return false;
	}
}
