using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class AdditionalCostWorkflow : SelectCardsWorkflow<CastingTimeOptionRequest>, IClickableWorkflow
{
	private class CastingTimeOptionAdditionalCostHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> _doneRequestMap;

		private readonly IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_AdditionalCostRequest> _additionalCostRequestMap;

		public CastingTimeOptionAdditionalCostHighlightsGenerator(IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> doneRequestMap, IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_AdditionalCostRequest> additionalCostRequestMap)
		{
			_doneRequestMap = doneRequestMap;
			_additionalCostRequestMap = additionalCostRequestMap;
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			foreach (KeyValuePair<uint, HighlightType> relatedUserHighlight in CardHoverController.GetRelatedUserHighlights())
			{
				_highlights.IdToHighlightType_User.Add(relatedUserHighlight.Key, relatedUserHighlight.Value);
			}
			foreach (DuelScene_CDC key in _doneRequestMap.Keys)
			{
				_highlights.EntityHighlights[key] = highlightTypeForSolutionManaCost(_doneRequestMap[key].Solution, _doneRequestMap[key].ManaCost);
			}
			foreach (DuelScene_CDC key2 in _additionalCostRequestMap.Keys)
			{
				_highlights.EntityHighlights[key2] = highlightTypeForSolutionManaCost(_additionalCostRequestMap[key2].Solution, _additionalCostRequestMap[key2].ManaCost);
			}
			if (tryGetAutoTapActions(CardHoverController.HoveredCard, out var outAutoTapActions))
			{
				foreach (AutoTapAction item in outAutoTapActions)
				{
					if (item.ManaId != 0)
					{
						_highlights.ManaHighlights[item.ManaId] = new ManaHighlight(HighlightType.AutoPay);
					}
					if (item.InstanceId != 0)
					{
						_highlights.IdToHighlightType_User[item.InstanceId] = HighlightType.AutoPay;
					}
				}
			}
			return _highlights;
			static HighlightType highlightTypeForSolutionManaCost(AutoTapSolution solution, List<ManaRequirement> manaCost)
			{
				if (solution == null && manaCost.Count != 0)
				{
					return HighlightType.None;
				}
				return HighlightType.Hot;
			}
			bool tryGetAutoTapActions(DuelScene_CDC card, out ICollection<AutoTapAction> reference)
			{
				if ((bool)card)
				{
					if (_doneRequestMap.TryGetValue(card, out var value))
					{
						return tryGetSolutionActions(value.Solution, out reference);
					}
					if (_additionalCostRequestMap.TryGetValue(card, out var value2))
					{
						return tryGetSolutionActions(value2.Solution, out reference);
					}
				}
				reference = new AutoTapAction[0];
				return false;
			}
			static bool tryGetSolutionActions(AutoTapSolution solution, out ICollection<AutoTapAction> solutionActions)
			{
				if (solution?.AutoTapActions == null)
				{
					solutionActions = new AutoTapAction[0];
					return false;
				}
				solutionActions = solution.AutoTapActions;
				return true;
			}
		}
	}

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly ICastTimeOptionHeaderProvider _cardHeaderProvider;

	private readonly Dictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> _doneOptionMappings = new Dictionary<DuelScene_CDC, CastingTimeOption_DoneRequest>();

	private readonly Dictionary<DuelScene_CDC, CastingTimeOption_AdditionalCostRequest> _additionalCostOptionMappings = new Dictionary<DuelScene_CDC, CastingTimeOption_AdditionalCostRequest>();

	private readonly Dictionary<DuelScene_CDC, MtgCastTimeOption> _cardToCastTimeOption = new Dictionary<DuelScene_CDC, MtgCastTimeOption>();

	private readonly HashSet<string> _fakeCardKeys = new HashSet<string>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public AdditionalCostWorkflow(CastingTimeOptionRequest request, ICardDatabaseAdapter cardDatabase, IFakeCardViewController fakeCardViewController, IGameStateProvider gameStateProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, ICastTimeOptionHeaderProvider cardHeaderProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_fakeCardViewController = fakeCardViewController ?? NullFakeCardViewController.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_cardHeaderProvider = cardHeaderProvider ?? NullCastTimeOptionHeaderProvider.Default;
		_highlightsGenerator = new CastingTimeOptionAdditionalCostHighlightsGenerator(_doneOptionMappings, _additionalCostOptionMappings);
		_buttonStateData = new Dictionary<string, ButtonStateData>();
	}

	protected override void ApplyInteractionInternal()
	{
		CardHoverController.OnHoveredCardUpdated += OnHoveredCardUpdated;
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 1, 1, _request.CancellationType);
		SetHeaderAndSubheader();
		List<BaseUserRequest> childRequests = _request.ChildRequests;
		childRequests.Sort((BaseUserRequest lhs, BaseUserRequest rhs) =>
		{
			bool value = lhs is CastingTimeOption_DoneRequest;
			return (rhs is CastingTimeOption_DoneRequest).CompareTo(value);
		});
		CastingTimeOption_AdditionalCostRequest castingTimeOption_AdditionalCostRequest = childRequests.Find((BaseUserRequest x) => x is CastingTimeOption_AdditionalCostRequest) as CastingTimeOption_AdditionalCostRequest;
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		for (int num = 0; num < childRequests.Count; num++)
		{
			BaseUserRequest baseUserRequest = childRequests[num];
			if (baseUserRequest is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest && mtgGameState.TryGetCard(castingTimeOption_AdditionalCostRequest.AffectedId, out var card))
			{
				ICardDataAdapter cardDataForDoneRequest = GetCardDataForDoneRequest(castingTimeOption_DoneRequest, childRequests, card);
				string text = KeyForFakeCard(num);
				DuelScene_CDC duelScene_CDC = _fakeCardViewController.CreateFakeCard(text, cardDataForDoneRequest, isVisible: true);
				_fakeCardKeys.Add(text);
				_doneOptionMappings[duelScene_CDC] = castingTimeOption_DoneRequest;
				_cardToCastTimeOption[duelScene_CDC] = MtgCastTimeOption.Done;
				_cardsToDisplay.Add(duelScene_CDC);
			}
			else if (baseUserRequest is CastingTimeOption_AdditionalCostRequest castingTimeOption_AdditionalCostRequest2)
			{
				ICardDataAdapter cardDataForAdditionalCost = GetCardDataForAdditionalCost(castingTimeOption_AdditionalCostRequest2, childRequests, mtgGameState.GetCardById(castingTimeOption_AdditionalCostRequest2.AffectorId), mtgGameState.GetCardById(castingTimeOption_AdditionalCostRequest2.AffectedId));
				string item = KeyForFakeCard(num);
				DuelScene_CDC duelScene_CDC2 = _fakeCardViewController.CreateFakeCard(KeyForFakeCard(num), cardDataForAdditionalCost, isVisible: true);
				_fakeCardKeys.Add(item);
				_additionalCostOptionMappings[duelScene_CDC2] = castingTimeOption_AdditionalCostRequest2;
				_cardToCastTimeOption[duelScene_CDC2] = new MtgCastTimeOption(CastingTimeOptionType.AdditionalCost, castingTimeOption_AdditionalCostRequest2.GrpId, castingTimeOption_AdditionalCostRequest2.AffectorId, castingTimeOption_AdditionalCostRequest2.AffectedId);
				_cardsToDisplay.Add(duelScene_CDC2);
			}
		}
		selectable.Clear();
		selectable.AddRange(_cardsToDisplay);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private static string KeyForFakeCard(int index)
	{
		return $"CTO_{index}";
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(1, 1u);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	private ICardDataAdapter GetCardDataForDoneRequest(CastingTimeOption_DoneRequest doneRequest, List<BaseUserRequest> childRequests, MtgCardInstance sourceInstance)
	{
		MtgCardInstance copy = sourceInstance.GetCopy();
		copy.InstanceId = 0u;
		copy.Abilities.RemoveAll((AbilityPrintingData x) => IsAnAdditionalCostOption(childRequests, x.Id));
		copy.ManaCostOverride = doneRequest.ManaCost.ConvertAll((ManaRequirement x) =>
		{
			if (x.Color.Count == 3)
			{
				return ManaQuantity.MakeTribrid((uint)x.Count, x.Color[0], x.Color[1], x.Color[2]);
			}
			return (x.Color.Count == 2) ? ManaQuantity.MakeHybrid((uint)x.Count, x.Color[0], x.Color[1]) : ManaQuantity.MakeMana((uint)x.Count, x.Color[0]);
		});
		CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(copy.GrpId, copy.SkinCode);
		CardPrintingRecord record = cardPrintingById.Record;
		IReadOnlyList<(uint, uint)> abilityIds = cardPrintingById.AbilityIds.Where(((uint Id, uint TextId) x) => !IsAnAdditionalCostOption(childRequests, x.Id)).ToArray();
		CardPrintingData printing = new CardPrintingData(cardPrintingById, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds));
		return new CardData(copy, printing);
	}

	private ICardDataAdapter GetCardDataForAdditionalCost(CastingTimeOption_AdditionalCostRequest additionalCost, List<BaseUserRequest> childRequests, MtgCardInstance affector, MtgCardInstance affected)
	{
		if (affector != null && affector == affected)
		{
			MtgCardInstance copy = affector.GetCopy();
			copy.InstanceId = 0u;
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(copy.GrpId, copy.SkinCode);
			copy.ManaCostOverride = additionalCost.ManaCost.ConvertAll((ManaRequirement x) => ManaQuantity.MakeMana((uint)x.Count, x.Color[0]));
			copy.Abilities.RemoveAll((AbilityPrintingData x) => IsAnAdditionalCostOption(childRequests, x.Id) && x.Id != additionalCost.GrpId);
			copy.CastingTimeOptions.Add(new GreClient.Rules.CastingTimeOption(CastingTimeOptionType.AdditionalCost, additionalCost.GrpId));
			return new CardData(copy, cardPrintingById);
		}
		AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(additionalCost.GrpId);
		MtgCardInstance mtgCardInstance = affector?.GetCopy() ?? new MtgCardInstance();
		mtgCardInstance.InstanceId = 0u;
		mtgCardInstance.ObjectType = GameObjectType.Ability;
		mtgCardInstance.Abilities.Clear();
		mtgCardInstance.Abilities.Add(abilityPrintingById);
		mtgCardInstance.Zone = null;
		mtgCardInstance.EnteredZoneThisTurn = ZoneType.None;
		CardPrintingData cardPrintingById2 = _cardDatabase.CardDataProvider.GetCardPrintingById(mtgCardInstance.GrpId, mtgCardInstance.SkinCode);
		return new CardData(mtgCardInstance, cardPrintingById2);
	}

	private void OnHoveredCardUpdated(DuelScene_CDC card)
	{
		SetHighlights();
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		_buttonStateData.Clear();
		if (cancelType == AllowCancel.No || cancelType == AllowCancel.None)
		{
			return _buttonStateData;
		}
		_buttonStateData["CancelButton"] = new ButtonStateData
		{
			LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel",
			BrowserElementKey = "SingleButton",
			Enabled = true,
			StyleType = ButtonStyle.StyleType.Main
		};
		return _buttonStateData;
	}

	public override BrowserCardHeader.BrowserCardHeaderData GetCardHeaderData(DuelScene_CDC cardView)
	{
		if (!_cardToCastTimeOption.TryGetValue(cardView, out var value))
		{
			return null;
		}
		return _cardHeaderProvider.GetCastTimeOptionHeader(value);
	}

	private static bool IsAnAdditionalCostOption(List<BaseUserRequest> requests, uint grpId)
	{
		foreach (BaseUserRequest request in requests)
		{
			if (request is CastingTimeOption_AdditionalCostRequest castingTimeOption_AdditionalCostRequest && castingTimeOption_AdditionalCostRequest.GrpId == grpId)
			{
				return true;
			}
		}
		return false;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "CancelButton" && _request.CanCancel)
		{
			_request.Cancel();
		}
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		OnClick(cardView, SimpleInteractionType.Primary);
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (entity is DuelScene_CDC key && (_doneOptionMappings.ContainsKey(key) || _additionalCostOptionMappings.ContainsKey(key)))
		{
			return true;
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (CanClick(entity, clickType) && entity is DuelScene_CDC key)
		{
			CastingTimeOption_AdditionalCostRequest value2;
			if (_doneOptionMappings.TryGetValue(key, out var value))
			{
				value.SubmitDone();
			}
			else if (_additionalCostOptionMappings.TryGetValue(key, out value2))
			{
				value2.SubmitAdditionalCost();
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

	public override void CleanUp()
	{
		_cardsToDisplay.Clear();
		_doneOptionMappings.Clear();
		_additionalCostOptionMappings.Clear();
		foreach (string fakeCardKey in _fakeCardKeys)
		{
			_fakeCardViewController.DeleteFakeCard(fakeCardKey);
		}
		_fakeCardKeys.Clear();
		base.CleanUp();
	}
}
