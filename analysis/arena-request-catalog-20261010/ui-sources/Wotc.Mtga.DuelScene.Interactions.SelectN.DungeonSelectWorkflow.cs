using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class DungeonSelectWorkflow : SelectCardsWorkflow<SelectNRequest>, IClickableWorkflow
{
	private Dictionary<DuelScene_CDC, uint> _dungeonGrpIdToFakeCard = new Dictionary<DuelScene_CDC, uint>();

	private HashSet<uint> _dungeonMapRevealed = new HashSet<uint>();

	private List<uint> _selectedIds = new List<uint>();

	private readonly IFakeCardViewController _fakeCardController;

	private readonly ICardDataProvider _cardDataProvider;

	private readonly IBrowserManager _browserManager;

	private readonly CardHolderReference<CardBrowserCardHolder> _defaultBrowser;

	private const string DUNGEON_SELECTION_CARD = "DUNGEON_SELECTION_CARD_{0}";

	private ISplineMovementSystem _splineMovementSystem;

	private Dictionary<DuelScene_CDC, IdealPoint> _dungeonMapIdealPoints = new Dictionary<DuelScene_CDC, IdealPoint>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public DungeonSelectWorkflow(SelectNRequest request, IFakeCardViewController fakeCardController, ICardDataProvider cardDataProvider, ICardHolderProvider cardHolderProvider, IBrowserManager browserManager, ISplineMovementSystem splineMovementSystem)
		: base(request)
	{
		_fakeCardController = fakeCardController;
		_cardDataProvider = cardDataProvider;
		_browserManager = browserManager;
		_splineMovementSystem = splineMovementSystem;
		_defaultBrowser = new CardHolderReference<CardBrowserCardHolder>(cardHolderProvider, GREPlayerNum.Invalid, CardHolderType.CardBrowserDefault);
	}

	protected override void ApplyInteractionInternal()
	{
		currentSelections.Clear();
		_header = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_A_Dungeon");
		_subHeader = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Options");
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
		selectable.Clear();
		nonSelectable.Clear();
		_dungeonGrpIdToFakeCard.Clear();
		_selectedIds.Clear();
		_dungeonMapRevealed.Clear();
		foreach (uint id in _request.Ids)
		{
			DuelScene_CDC duelScene_CDC = createFakeCard(id);
			_dungeonGrpIdToFakeCard.Add(duelScene_CDC, id);
			_dungeonMapRevealed.Add(id);
			selectable.Add(duelScene_CDC);
		}
		_cardsToDisplay = new List<DuelScene_CDC>();
		_cardsToDisplay.AddRange(selectable);
		SetOpenedBrowser(_browserManager.OpenBrowser(this));
		_defaultBrowser.Get().OnCardHolderUpdated += DefaultBrowser_OnCardHolderUpdated;
		CardHoverController.OnHoveredCardUpdated += RevealFaceDownDungeonCard;
		DuelScene_CDC createFakeCard(uint grpId)
		{
			CardPrintingData cardPrintingById = _cardDataProvider.GetCardPrintingById(grpId);
			CardPrintingRecord record = cardPrintingById.Record;
			IReadOnlyList<(uint, uint)> abilityIds = Array.Empty<(uint, uint)>();
			IReadOnlyDictionary<uint, IReadOnlyList<uint>> abilityIdToLinkedTokenGrpId = DictionaryExtensions.Empty<uint, IReadOnlyList<uint>>();
			CardPrintingData cardPrintingData = new CardPrintingData(cardPrintingById, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds, null, null, null, abilityIdToLinkedTokenGrpId));
			MtgCardInstance mtgCardInstance = cardPrintingData.CreateInstance();
			mtgCardInstance.Zone = null;
			CardData cardData = new CardData(mtgCardInstance, cardPrintingData);
			return _fakeCardController.CreateFakeCard($"DUNGEON_SELECTION_CARD_{grpId}", cardData);
		}
	}

	private void DefaultBrowser_OnCardHolderUpdated()
	{
		foreach (DuelScene_CDC item in _cardsToDisplay)
		{
			CardHolderBase cardHolderBase = (CardHolderBase)item.CurrentCardHolder;
			IdealPoint layoutEndpoint = cardHolderBase.GetLayoutEndpoint(item);
			_dungeonMapIdealPoints.Add(item, layoutEndpoint);
			IdealPoint endPoint = new IdealPoint(layoutEndpoint.Position - cardHolderBase.CardRoot.forward * 2f, layoutEndpoint.Rotation * Quaternion.Euler(0f, 180f, 0f), layoutEndpoint.Scale);
			_splineMovementSystem.MoveInstant(item.Root, endPoint);
		}
		_defaultBrowser.Get().OnCardHolderUpdated -= DefaultBrowser_OnCardHolderUpdated;
	}

	public override void CleanUp()
	{
		CardHoverController.OnHoveredCardUpdated -= RevealFaceDownDungeonCard;
		base.CleanUp();
		foreach (KeyValuePair<DuelScene_CDC, uint> item in _dungeonGrpIdToFakeCard)
		{
			_fakeCardController.DeleteFakeCard($"DUNGEON_SELECTION_CARD_{item.Value}");
		}
		_dungeonGrpIdToFakeCard.Clear();
		_dungeonMapRevealed.Clear();
		_selectedIds.Clear();
		_dungeonMapIdealPoints.Clear();
		_defaultBrowser.ClearCache();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitResponse();
		}
	}

	public void SubmitResponse()
	{
		if (_selectedIds.Count >= _request.MinSel && _selectedIds.Count <= _request.MaxSel)
		{
			_request.SubmitSelection(_selectedIds);
		}
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		RevealFaceDownDungeonCard(cardView);
		if (CanClick(cardView, SimpleInteractionType.Primary))
		{
			OnClick(cardView, SimpleInteractionType.Primary);
		}
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (_browserManager.IsAnyBrowserOpen && entity is DuelScene_CDC key)
		{
			return _dungeonGrpIdToFakeCard.ContainsKey(key);
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_browserManager.IsAnyBrowserOpen && entity is DuelScene_CDC duelScene_CDC && _dungeonGrpIdToFakeCard.TryGetValue(duelScene_CDC, out var value))
		{
			if (_selectedIds.Contains(value))
			{
				_selectedIds.Remove(value);
				currentSelections.Remove(duelScene_CDC);
			}
			else
			{
				_selectedIds.Clear();
				currentSelections.Clear();
				_selectedIds.Add(value);
				currentSelections.Add(duelScene_CDC);
			}
			_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
			_openedBrowser.UpdateButtons();
			SetHighlights();
		}
	}

	private void RevealFaceDownDungeonCard(DuelScene_CDC cardView)
	{
		if ((bool)cardView && _dungeonGrpIdToFakeCard.TryGetValue(cardView, out var value) && _dungeonMapRevealed.Contains(value))
		{
			CardHolderBase cardHolderBase = (CardHolderBase)cardView.CurrentCardHolder;
			_dungeonMapIdealPoints.TryGetValue(cardView, out var value2);
			IdealPoint layoutEndpoint = cardHolderBase.GetLayoutEndpoint(cardView);
			IdealPoint endPoint = new IdealPoint(layoutEndpoint.Position - cardHolderBase.CardRoot.forward * 2f, value2.Rotation, layoutEndpoint.Scale);
			_splineMovementSystem.AddPermanentGoal(cardView.Root, endPoint);
			_dungeonMapRevealed.Remove(value);
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
}
