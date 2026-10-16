using System;
using System.Collections.Generic;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_Browser : SelectCardsWorkflow<SelectNRequest>, IAutoRespondWorkflow
{
	private readonly struct ConfirmationBrowserParams(string header, string subHeader, string yesLocKey, string noLocKey)
	{
		public readonly string Header = header;

		public readonly string SubHeader = subHeader;

		public readonly string YesLocKey = yesLocKey;

		public readonly string NoLocKey = noLocKey;
	}

	private const uint MH3_ELADAMRI_ABILITY = 172357u;

	private const uint FAMISHED_WORLDSIRE_ABILITY = 191043u;

	private const uint PUT_ANY_NUMBER_OF_LANDS_ON_BATTLEFIELD_PROMPT_ID = 8176u;

	private readonly IObjectPool _objPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly StartingZoneIdCalculator _startingZoneIdCalculator = new StartingZoneIdCalculator();

	private readonly Dictionary<uint, List<DuelScene_CDC>> _cardViewsByZoneId = new Dictionary<uint, List<DuelScene_CDC>>();

	private readonly HashSet<uint> _selectedIds = new HashSet<uint>();

	private readonly Dictionary<DuelScene_CDC, uint> _cardToIdMap = new Dictionary<DuelScene_CDC, uint>();

	private readonly Dictionary<uint, DuelScene_CDC> _idToCardMap = new Dictionary<uint, DuelScene_CDC>();

	private ICardDataAdapter _sourceModel;

	private uint _currentZoneId = uint.MaxValue;

	private IBrowser _selectionBrowser;

	private IBrowser _confirmationBrowser;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCardsMultiZone;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "MultiZone";
	}

	private bool IsFamishedWorldsire()
	{
		ResolutionEffectModel value = _resolutionEffectProvider.ResolutionEffect.Value;
		if (value != null && value.AbilityPrinting != null)
		{
			return value.AbilityPrinting.Id == 191043;
		}
		return false;
	}

	private bool IsPutAnyNumberOfLandCardsOnBattlefieldPromptId()
	{
		return _request.Prompt.PromptId == 8176;
	}

	public SelectNWorkflow_Selection_Browser(SelectNRequest request, IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IResolutionEffectProvider resolutionEffectProvider, IGameplaySettingsProvider gameplaySettings, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider, IPlayWarningManager playWarningManager)
		: base(request)
	{
		_objPool = objectPool ?? NullObjectPool.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_resolutionEffectProvider = resolutionEffectProvider ?? NullResolutionEffectProvider.Default;
		_gameplaySettings = gameplaySettings ?? NullGameplaySettingsProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		_cardViewsByZoneId.Clear();
		foreach (uint id in _request.Ids)
		{
			if (_cardViewProvider.TryGetCardView(id, out var cardView))
			{
				_cardToIdMap[cardView] = id;
				_idToCardMap[id] = cardView;
			}
		}
		HashSet<MtgZone> hashSet = new HashSet<MtgZone>();
		foreach (uint zoneId2 in _request.ZoneIds)
		{
			if (gameState.Zones.TryGetValue(zoneId2, out var value))
			{
				hashSet.Add(value);
			}
		}
		if (hashSet.Count == 0)
		{
			foreach (uint id2 in _request.Ids)
			{
				if (gameState.TryGetCard(id2, out var card))
				{
					hashSet.Add(card.Zone);
				}
			}
			foreach (uint unfilteredId in _request.UnfilteredIds)
			{
				if (gameState.TryGetCard(unfilteredId, out var card2))
				{
					hashSet.Add(card2.Zone);
				}
			}
		}
		foreach (MtgZone item in hashSet)
		{
			AddZoneCardsToZoneList(item);
		}
		foreach (uint zoneId in _cardViewsByZoneId.Keys)
		{
			_cardViewsByZoneId[zoneId].Sort((DuelScene_CDC x, DuelScene_CDC y) =>
			{
				bool value3 = selectable.Contains(x);
				int num = selectable.Contains(y).CompareTo(value3);
				if (num != 0)
				{
					return num;
				}
				bool value4 = IsHotSelectable(x);
				num = IsHotSelectable(y).CompareTo(value4);
				if (num != 0)
				{
					return num;
				}
				int value5 = _request.UnfilteredIds.IndexOf(x.InstanceId);
				num = _request.UnfilteredIds.IndexOf(y.InstanceId).CompareTo(value5);
				if (num != 0)
				{
					return num;
				}
				if (gameState.Zones.TryGetValue(zoneId, out var value6))
				{
					int num2 = value6.CardIds.IndexOf(x.InstanceId);
					int value7 = value6.CardIds.IndexOf(y.InstanceId);
					return num2.CompareTo(value7);
				}
				return num;
			});
		}
		if (_currentZoneId == uint.MaxValue)
		{
			_currentZoneId = _startingZoneIdCalculator.GetStartingZoneId(_request.SourceId, _gameStateProvider.LatestGameState, _cardDatabase, _cardViewsByZoneId.Keys);
		}
		bool applySourceOffset = (ApplyTargetOffset = gameState.Zones.TryGetValue(_currentZoneId, out var value2) && value2.Type == ZoneType.Graveyard);
		ApplySourceOffset = applySourceOffset;
		UpdateButtonStateData();
		_cardsToDisplay = GetCurrentZoneCardViews(_currentZoneId);
		DuelScene_CDC cardView2;
		if (gameState.TryGetCard(_request.SourceId, out var card3))
		{
			_sourceModel = CardDataExtensions.CreateWithDatabase(card3, _cardDatabase);
		}
		else if (_cardViewProvider.TryGetCardView(_request.SourceId, out cardView2))
		{
			_sourceModel = cardView2.Model;
		}
		SetHeaderAndSubheader(_sourceModel);
		OpenBrowser();
		PreSelectCards();
	}

	private void SetHeaderAndSubheader(ICardDataAdapter sourceModel)
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(_request.MinSel, _request.MaxSel);
		_headerTextProvider.SetSourceModel(sourceModel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCardsMultiZone);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.GetPrompt());
		_headerTextProvider.ClearParams();
	}

	private void OpenBrowser()
	{
		SetOpenedBrowser(_browserManager.OpenBrowser(this));
	}

	private void AddZoneCardsToZoneList(MtgZone zone)
	{
		foreach (uint cardId in zone.CardIds)
		{
			if (_cardViewProvider.TryGetCardView(cardId, out var cardView))
			{
				if (_request.Ids.Contains(cardId))
				{
					selectable.Add(cardView);
					AddCardViewToZoneList(zone.Id, cardView);
				}
				else if (_request.UnfilteredIds.Contains(cardId) || _request.ZoneIds.Contains(zone.Id))
				{
					nonSelectable.Add(cardView);
					AddCardViewToZoneList(zone.Id, cardView);
				}
				else if (zone.Type != ZoneType.Library && zone.Type != ZoneType.Exile)
				{
					nonSelectable.Add(cardView);
					AddCardViewToZoneList(zone.Id, cardView);
				}
			}
		}
	}

	private void AddCardViewToZoneList(uint zoneId, DuelScene_CDC cdc)
	{
		if (!_cardViewsByZoneId.ContainsKey(zoneId))
		{
			_cardViewsByZoneId.Add(zoneId, new List<DuelScene_CDC>());
		}
		_cardViewsByZoneId[zoneId].Add(cdc);
	}

	private List<DuelScene_CDC> GetCurrentZoneCardViews(uint currentZoneId)
	{
		return _cardViewsByZoneId[currentZoneId];
	}

	private void PreSelectCards()
	{
		if (!IsFamishedWorldsire())
		{
			return;
		}
		foreach (uint hotId in _request.HotIds)
		{
			if (_selectedIds.Count >= _request.MaxSel)
			{
				break;
			}
			_selectedIds.Add(hotId);
			if (_idToCardMap.TryGetValue(hotId, out var value))
			{
				currentSelections.Add(value);
			}
		}
		UpdateButtonStateData();
		UpdateHighlightsAndDimming();
		_browserManager.CurrentBrowser.UpdateButtons();
	}

	private void SelectAllCards()
	{
		foreach (uint id in _request.Ids)
		{
			if (_selectedIds.Count >= _request.MaxSel)
			{
				break;
			}
			_selectedIds.Add(id);
			if (_idToCardMap.TryGetValue(id, out var value) && !currentSelections.Contains(value))
			{
				currentSelections.Add(value);
			}
		}
		UpdateButtonStateData();
		UpdateHighlightsAndDimming();
		_browserManager.CurrentBrowser.UpdateButtons();
		if (_browserManager.CurrentBrowser is SelectCardsBrowser selectCardsBrowser)
		{
			selectCardsBrowser.UpdateHighlightsAndDimming();
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		if (buttonKey.StartsWith("ZoneButton"))
		{
			uint currentZoneId = (uint)Convert.ToInt32(buttonKey.Replace("ZoneButton", string.Empty));
			_currentZoneId = currentZoneId;
			bool applySourceOffset = (ApplyTargetOffset = mtgGameState.Zones.TryGetValue(_currentZoneId, out var value) && value.Type == ZoneType.Graveyard);
			ApplySourceOffset = applySourceOffset;
			_cardsToDisplay = GetCurrentZoneCardViews(_currentZoneId);
			UpdateButtonStateData();
			(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
		}
		switch (buttonKey)
		{
		case "DoneButton":
			SubmitResponse();
			break;
		case "CancelButton":
			CancelRequest();
			break;
		case "SelectAllButton":
			SelectAllCards();
			break;
		}
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	protected override bool IsHotSelectable(DuelScene_CDC cdc)
	{
		if (_request.HotIds.Count != 0)
		{
			return _request.HotIds.Contains(cdc.InstanceId);
		}
		return true;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_cardToIdMap.TryGetValue(cardView, out var id))
		{
			ConfirmationBrowserParams browserParams;
			if (_selectedIds.Contains(id))
			{
				_selectedIds.Remove(id);
				currentSelections.Remove(cardView);
				Arrows.ClearLines();
				UpdateButtonStateData();
				UpdateHighlightsAndDimming();
				_browserManager.CurrentBrowser.UpdateButtons();
			}
			else if (ShouldShowConfirmationBrowser(id, _gameStateProvider.LatestGameState, _resolutionEffectProvider.ResolutionEffect, out browserParams))
			{
				YesNoProvider browserTypeProvider = new YesNoProvider(browserParams.Header, browserParams.SubHeader, YesNoProvider.CreateButtonMap(browserParams.YesLocKey, browserParams.NoLocKey), YesNoProvider.CreateActionMap(() =>
				{
					OpenBrowser();
					SelectCard(id);
					_playWarningManager.IncrementPlayWarningCount();
				}, OpenBrowser));
				_browserManager.OpenBrowser(browserTypeProvider);
			}
			else
			{
				SelectCard(id);
			}
		}
		else
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid.EventName, cardView.gameObject);
		}
	}

	private void SelectCard(uint id)
	{
		_selectedIds.Add(id);
		if (_idToCardMap.TryGetValue(id, out var value))
		{
			currentSelections.Add(value);
		}
		if (CanAutoSubmit(id, _gameStateProvider.LatestGameState))
		{
			SubmitResponse();
			return;
		}
		UpdateButtonStateData();
		UpdateHighlightsAndDimming();
		_browserManager.CurrentBrowser.UpdateButtons();
	}

	private bool ShouldShowConfirmationBrowser(uint selectionId, MtgGameState gameState, ResolutionEffectModel resolutionEffect, out ConfirmationBrowserParams browserParams)
	{
		if (_playWarningManager.HasReachedWarningLimit())
		{
			browserParams = default;
			return false;
		}
		if (resolutionEffect != null && resolutionEffect.AbilityPrinting != null && resolutionEffect.AbilityPrinting.Id == 172357 && _request.HotIds.Count > 0 && !_request.HotIds.Contains(selectionId) && gameState.TryGetCard(selectionId, out var card))
		{
			string cardTitle = _cardDatabase.CardTitleProvider.GetCardTitle(card);
			browserParams = new ConfirmationBrowserParams(_cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/NotACreatureConfirmation_Subheader", ("cardTitle", cardTitle)), "DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No");
			return true;
		}
		browserParams = default;
		return false;
	}

	private bool CanAutoSubmit(uint id, MtgGameState gameState)
	{
		if (_gameplaySettings.FullControlEnabled)
		{
			return false;
		}
		if (IsPutAnyNumberOfLandCardsOnBattlefieldPromptId())
		{
			return false;
		}
		uint sourceId = _request.SourceId;
		uint sourceCardId = ((gameState.GetCardById(sourceId) == null) ? id : sourceId);
		return CurrentSelection(sourceCardId, (uint)_request.MinSel, _request.MaxSel, (uint)_selectedIds.Count, _gameStateProvider.LatestGameState).CanAutoSubmit();
	}

	public bool TryAutoRespond()
	{
		if (_gameplaySettings.FullControlEnabled)
		{
			return false;
		}
		if (_request.CancellationType == AllowCancel.Continue)
		{
			return false;
		}
		int count = _request.Ids.Count;
		if (count != _request.MinSel || count != _request.MaxSel)
		{
			return false;
		}
		if (!CanAutoSubmitFromZones(_objPool, _gameStateProvider.LatestGameState, _request.Ids, _request.UnfilteredIds))
		{
			return false;
		}
		_request.SubmitSelection(_request.Ids);
		return true;
	}

	public static bool CanAutoSubmitFromZones(IObjectPool objectPool, MtgGameState currentState, IEnumerable<uint> ids, IEnumerable<uint> unfilteredIds)
	{
		HashSet<uint> hashSet = objectPool.PopObject<HashSet<uint>>();
		foreach (uint id in ids)
		{
			hashSet.Add(id);
		}
		foreach (uint unfilteredId in unfilteredIds)
		{
			hashSet.Add(unfilteredId);
		}
		bool result = true;
		HashSet<uint> hashSet2 = objectPool.PopObject<HashSet<uint>>();
		foreach (uint item in hashSet)
		{
			if (!currentState.TryGetCard(item, out var card))
			{
				continue;
			}
			MtgZone zone = card.Zone;
			if (zone.Type != ZoneType.Limbo)
			{
				ZoneType type = zone.Type;
				if ((type != ZoneType.Exile && type != ZoneType.Graveyard) || (hashSet2.Add(zone.Id) && hashSet2.Count > 1))
				{
					result = false;
					break;
				}
			}
		}
		hashSet.Clear();
		objectPool.PushObject(hashSet, tryClear: false);
		hashSet2.Clear();
		objectPool.PushObject(hashSet2, tryClear: false);
		return result;
	}

	private void SubmitResponse()
	{
		_request.SubmitSelection(_selectedIds);
	}

	private void UpdateButtonStateData()
	{
		List<uint> list = new List<uint>(_cardViewsByZoneId.Keys);
		if (_currentZoneId == uint.MaxValue)
		{
			_currentZoneId = list[0];
		}
		_buttonStateData = SelectCardsWorkflow<SelectNRequest>.GenerateMultiZoneButtonStates(_selectedIds.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType, list, _currentZoneId, _gameStateProvider.LatestGameState, _cardDatabase.ClientLocProvider);
		if (_buttonStateData.TryGetValue("DoneButton", out var value))
		{
			int count = _selectedIds.Count;
			bool num = count == 0 && selectable.Count == 0;
			MTGALocalizedString mTGALocalizedString = null;
			ButtonStyle.StyleType styleType = ButtonStyle.StyleType.Main;
			if (num)
			{
				mTGALocalizedString = "DuelScene/Browsers/ViewDismiss_Done";
			}
			else
			{
				mTGALocalizedString = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Submit_N",
					Parameters = new Dictionary<string, string> { 
					{
						"submitCount",
						count.ToString()
					} }
				};
				if (count < _request.MaxSel)
				{
					styleType = ButtonStyle.StyleType.Secondary;
				}
			}
			value.StyleType = styleType;
			value.LocalizedString = mTGALocalizedString;
		}
		if (IsPutAnyNumberOfLandCardsOnBattlefieldPromptId())
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.StyleType = ((currentSelections.Count != _request.Ids.Count) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Outlined);
			buttonStateData.LocalizedString = "DuelScene/Browsers/BrowserButton_SelectAll";
			buttonStateData.BrowserElementKey = "SelectAllButton";
			buttonStateData.Enabled = true;
			_buttonStateData.Add("SelectAllButton", buttonStateData);
		}
	}

	public override void SetFxBlackboardData(IBlackboard bb)
	{
		base.SetFxBlackboardData(bb);
		bb.SetCardDataExtensive(_sourceModel);
		bb.Ability = ((_sourceModel != null && _sourceModel.ObjectType == GameObjectType.Ability) ? _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(_sourceModel.GrpId) : null);
	}
}
