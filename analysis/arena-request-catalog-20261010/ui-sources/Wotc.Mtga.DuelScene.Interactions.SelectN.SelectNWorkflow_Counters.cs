using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Browser;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using ReferenceMap;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Counters : BrowserWorkflowBase<SelectNRequest>, IButtonSelectionBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider
{
	private const string FAKE_CARD_KEY = "SelectCounterSourceCard";

	private readonly Dictionary<string, ButtonStateData> _scrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _selectedScrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly SortedDictionary<uint, uint> _counterTypeToCount = new SortedDictionary<uint, uint>();

	private readonly IObjectPool _objectPool;

	private readonly IUnityObjectPool _unityObjPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly SpinnerController _spinnerController;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly AssetLookupTree<ViewCounterUIPrefab> _counterPrefabLookupTree;

	private readonly List<uint> _selections = new List<uint>();

	private readonly List<(uint, uint)> _pendingCounters = new List<(uint, uint)>();

	private DuelScene_CDC _contextualCard;

	public SelectNWorkflow_Counters(SelectNRequest request, IObjectPool objectPool, IUnityObjectPool unityObjectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, IPromptTextProvider promptTextProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, IFakeCardViewController fakeCardController, AssetLookupSystem assetLookupSystem, SpinnerController spinnerController)
		: base(request)
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		_objectPool = objectPool;
		_unityObjPool = unityObjectPool;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_promptTextProvider = promptTextProvider;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_fakeCardController = fakeCardController;
		_assetLookupSystem = assetLookupSystem;
		_counterPrefabLookupTree = _assetLookupSystem.TreeLoader.LoadTree<ViewCounterUIPrefab>();
		_spinnerController = spinnerController;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_counterTypeToCount.Clear();
		foreach (uint id in _request.Ids)
		{
			if (!_counterTypeToCount.ContainsKey(id))
			{
				_counterTypeToCount.Add(id, 0u);
			}
			_counterTypeToCount[id]++;
		}
		_pendingCounters.Clear();
		HashSet<ReferenceMap.Reference> results = _objectPool.PopObject<HashSet<ReferenceMap.Reference>>();
		if (mtgGameState.ReferenceMap.GetReferences(_request.SourceId, ReferenceMap.ReferenceType.PendingAffector, 0u, ref results))
		{
			foreach (ReferenceMap.Reference item in results)
			{
				uint b = item.B;
				if (!mtgGameState.TryGetCard(b, out var card) || card.PendingEffectOverrides == null)
				{
					continue;
				}
				foreach (KeyValuePair<CounterType, int> counter in card.PendingEffectOverrides.Counters)
				{
					uint key = (uint)counter.Key;
					if (_counterTypeToCount.ContainsKey(key))
					{
						_pendingCounters.Add((key, (uint)counter.Value));
					}
				}
			}
		}
		results.Clear();
		_objectPool.PushObject(results, tryClear: false);
		ShowBrowserFlow();
	}

	private void ShowBrowserFlow()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		SetHeaderAndSubheader();
		UpdateBrowserButtons();
		if (mtgGameState.TryGetCard(_request.SourceId, out var card) && card.Zone != null && card.Zone.Type == ZoneType.None)
		{
			_contextualCard = _fakeCardController.CreateFakeCard("SelectCounterSourceCard", card.ToCardData(_cardDatabase), isVisible: true);
			_cardsToDisplay.Add(_contextualCard);
		}
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(_request.MinSel, _request.MaxSel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.ButtonSelection);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _promptTextProvider.GetPromptText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonSelection;
	}

	public override string GetHeaderText()
	{
		return _header;
	}

	public override string GetSubHeaderText()
	{
		return _subHeader;
	}

	public Dictionary<string, ButtonStateData> GetScrollListButtonStateData()
	{
		return _scrollListButtonStateData;
	}

	public Dictionary<string, ButtonStateData> GetSelectedScrollListButtonStateData()
	{
		return _selectedScrollListButtonStateData;
	}

	public bool SortButtonsByKey()
	{
		return false;
	}

	private void UpdateBrowserButtons()
	{
		UpdateDefaultBrowserButtons();
		UpdateScrollListButtons();
		UpdateSelectedScrollListButtons();
	}

	private void UpdateDefaultBrowserButtons()
	{
		_buttonStateData.Clear();
		bool flag = _request.CancellationType != AllowCancel.No && _request.CancellationType != AllowCancel.None;
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		int count = _selections.Count;
		ButtonStateData buttonStateData = new ButtonStateData();
		MTGALocalizedString mTGALocalizedString = new MTGALocalizedString();
		MtgCardInstance cardById = mtgGameState.GetCardById(_request.SourceId);
		if (cardById != null && cardById.GrpId == 69553)
		{
			mTGALocalizedString.Key = "DuelScene/ClientPrompt/Remove_N";
			mTGALocalizedString.Parameters = new Dictionary<string, string> { 
			{
				"count",
				count.ToString()
			} };
		}
		else
		{
			mTGALocalizedString.Key = "DuelScene/ClientPrompt/Submit_N";
			mTGALocalizedString.Parameters = new Dictionary<string, string> { 
			{
				"submitCount",
				count.ToString()
			} };
		}
		buttonStateData.IsActive = showDoneButton();
		buttonStateData.LocalizedString = mTGALocalizedString;
		buttonStateData.BrowserElementKey = (flag ? "2Button_Left" : "SingleButton");
		buttonStateData.Enabled = CanSubmit();
		buttonStateData.StyleType = ((count > 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
		_buttonStateData.Add("DoneButton", buttonStateData);
		if (flag)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData2.BrowserElementKey = ((_buttonStateData.Count > 0) ? "2Button_Right" : "SingleButton");
			buttonStateData2.Enabled = true;
			buttonStateData2.StyleType = ((_buttonStateData.Count <= 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			_buttonStateData.Add("CancelButton", buttonStateData2);
		}
		bool showDoneButton()
		{
			bool fullControlDisabled = _gameplaySettings.FullControlDisabled;
			if ((_request.MaxSel == 1 && _selections.Count == _request.MaxSel) & fullControlDisabled)
			{
				return false;
			}
			return true;
		}
	}

	private void UpdateScrollListButtons()
	{
		_scrollListButtonStateData.Clear();
		foreach (uint counterType in _counterTypeToCount.Keys)
		{
			string key = $"Counter_{counterType}";
			List<uint> list = _selections.FindAll((uint x) => x == counterType);
			uint num = _counterTypeToCount[counterType] - (uint)list.Count;
			ButtonStateData buttonStateData = CreateButtonStateData(counterType, num);
			buttonStateData.Enabled = num != 0 && _selections.Count < _request.MaxSel;
			_scrollListButtonStateData.Add(key, buttonStateData);
		}
	}

	private void UpdateSelectedScrollListButtons()
	{
		_selectedScrollListButtonStateData.Clear();
		for (int i = 0; i < _selections.Count; i++)
		{
			string key = $"Selection_{i}";
			ButtonStateData value = CreateButtonStateData(_selections[i], 1u);
			_selectedScrollListButtonStateData.Add(key, value);
		}
		for (int j = 0; j < _pendingCounters.Count; j++)
		{
			string key2 = $"Pending_{j}";
			ButtonStateData value2 = CreateButtonStateData(_pendingCounters[j].Item1, _pendingCounters[j].Item2, ButtonStyle.StyleType.Tepid_NoGlow, enabled: false);
			_selectedScrollListButtonStateData.Add(key2, value2);
		}
	}

	private ButtonStateData CreateButtonStateData(uint counterType, uint count)
	{
		ButtonStyle.StyleType style = (_request.HotIds.Contains(counterType) ? ButtonStyle.StyleType.Secondary : ButtonStyle.StyleType.Tepid);
		return CreateButtonStateData(counterType, count, style, enabled: true);
	}

	private ButtonStateData CreateButtonStateData(uint counterType, uint count, ButtonStyle.StyleType style, bool enabled)
	{
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.BrowserElementKey = "ButtonDefault";
		string localizedTextForEnumValue = _cardDatabase.GreLocProvider.GetLocalizedTextForEnumValue("CounterType", (int)counterType);
		buttonStateData.LocalizedString = new UnlocalizedMTGAString(localizedTextForEnumValue);
		buttonStateData.StyleType = style;
		buttonStateData.Enabled = enabled;
		CounterAssetData counterAsset = CounterAssetUtil.GetCounterAsset(_assetLookupSystem, (CounterType)counterType, null, CardHolderType.Battlefield, GetBrowserType());
		if (counterAsset == null)
		{
			Debug.LogError("No counter sprite payload found");
		}
		else
		{
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.CounterType = (CounterType)counterType;
			ViewCounterUIPrefab payload = _counterPrefabLookupTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				ViewCounter_UI component = _unityObjPool.PopObject(payload.ViewCounterRef.RelativePath).GetComponent<ViewCounter_UI>();
				component.gameObject.name = $"{counterType}_CounterView";
				component.SetBackground(counterAsset.UiSpritePath);
				component.SetCount(count);
				buttonStateData.ChildView = component.transform as RectTransform;
			}
		}
		return buttonStateData;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitSelections();
		}
		else if (buttonKey == "CancelButton")
		{
			Cancel();
		}
		else if (buttonKey.StartsWith("Counter_"))
		{
			uint counterType = uint.Parse(buttonKey.Split('_')[1]);
			MakeSelection(counterType);
		}
		else if (buttonKey.StartsWith("Selection_"))
		{
			uint index = uint.Parse(buttonKey.Split('_')[1]);
			RemoveSelection(index);
		}
	}

	private void RemoveSelection(uint index)
	{
		_selections.RemoveAt((int)index);
		UpdateBrowserButtons();
		if (_openedBrowser is IRefreshable refreshable)
		{
			refreshable.Refresh();
		}
	}

	private void MakeSelection(uint counterType)
	{
		_selections.Add(counterType);
		bool fullControlDisabled = _gameplaySettings.FullControlDisabled;
		if ((_request.MaxSel == 1 && _selections.Count == _request.MaxSel) & fullControlDisabled)
		{
			SubmitSelections();
			return;
		}
		UpdateBrowserButtons();
		if (_openedBrowser is IRefreshable refreshable)
		{
			refreshable.Refresh();
		}
	}

	private bool CanSubmit()
	{
		if (_selections == null || _request == null)
		{
			return false;
		}
		int count = _selections.Count;
		if (count >= _request.MinSel)
		{
			return count <= _request.MaxSel;
		}
		return false;
	}

	private void SubmitSelections()
	{
		_request.SubmitSelection(_selections);
	}

	private void Cancel()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	public override void CleanUp()
	{
		if (_openedBrowser != null)
		{
			_openedBrowser.Close();
			_openedBrowser = null;
		}
		_spinnerController?.Close();
		if ((bool)_contextualCard)
		{
			_fakeCardController.DeleteFakeCard("SelectCounterSourceCard");
			_contextualCard = null;
		}
		base.CleanUp();
	}
}
