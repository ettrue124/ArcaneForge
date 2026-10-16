using System.Collections.Generic;
using System.Linq;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class CostKeywordWorkflow : SelectCardsWorkflow<CastingTimeOptionRequest>
{
	private class CostKeywordHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly IReadOnlyDictionary<DuelScene_CDC, BaseUserRequest> _cardToRequestMap;

		public CostKeywordHighlightsGenerator(IReadOnlyDictionary<DuelScene_CDC, BaseUserRequest> cardToRequestMap)
		{
			_cardToRequestMap = cardToRequestMap;
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			foreach (KeyValuePair<uint, HighlightType> relatedUserHighlight in CardHoverController.GetRelatedUserHighlights())
			{
				_highlights.IdToHighlightType_User.Add(relatedUserHighlight.Key, relatedUserHighlight.Value);
			}
			foreach (DuelScene_CDC key in _cardToRequestMap.Keys)
			{
				BaseUserRequest baseUserRequest = _cardToRequestMap[key];
				if (!(baseUserRequest is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest))
				{
					if (baseUserRequest is CastingTimeOption_CostKeywordRequest castingTimeOption_CostKeywordRequest)
					{
						_highlights.EntityHighlights[key] = highlightTypeForSolutionManaCost(castingTimeOption_CostKeywordRequest.Solution);
					}
				}
				else
				{
					_highlights.EntityHighlights[key] = highlightTypeForSolutionManaCost(castingTimeOption_DoneRequest.Solution, castingTimeOption_DoneRequest.ManaCost);
				}
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
			static HighlightType highlightTypeForSolutionManaCost(AutoTapSolution solution, List<ManaRequirement> manaCost = null)
			{
				if (solution == null && (manaCost == null || manaCost.Count != 0))
				{
					return HighlightType.None;
				}
				return HighlightType.Hot;
			}
			bool tryGetAutoTapActions(DuelScene_CDC card, out ICollection<AutoTapAction> reference)
			{
				if ((bool)card && _cardToRequestMap.TryGetValue(card, out var value))
				{
					if (value is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest2)
					{
						return tryGetSolutionActions(castingTimeOption_DoneRequest2.Solution, out reference);
					}
					if (value is CastingTimeOption_CostKeywordRequest castingTimeOption_CostKeywordRequest2)
					{
						return tryGetSolutionActions(castingTimeOption_CostKeywordRequest2.Solution, out reference);
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

	private const string FAKE_CARD_FORMAT = "Cost Keyword CTO Option {0}";

	private readonly IComparer<BaseUserRequest> _requestComparer;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly uint _abilityId;

	private readonly bool _hasBaseId;

	private readonly IReadOnlyList<CastingTimeOptionType> _keywordTypes = new List<CastingTimeOptionType>
	{
		CastingTimeOptionType.Casualty,
		CastingTimeOptionType.Kicker,
		CastingTimeOptionType.Bargain,
		CastingTimeOptionType.Conspire
	};

	private readonly IReadOnlyList<uint> _ctoBaseIds = new List<uint> { 241u, 34u };

	private readonly IReadOnlyList<uint> _ctoAbilityIds = new List<uint> { 303u, 79u };

	private readonly Dictionary<DuelScene_CDC, BaseUserRequest> _fakeCardsToRequestMap = new Dictionary<DuelScene_CDC, BaseUserRequest>();

	private DuelScene_CDC _cardView;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public CostKeywordWorkflow(CastingTimeOptionRequest request, uint abilityId, IComparer<BaseUserRequest> requestComparer, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IFakeCardViewController fakeCardController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_requestComparer = requestComparer;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_fakeCardController = fakeCardController;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_highlightsGenerator = new CostKeywordHighlightsGenerator(_fakeCardsToRequestMap);
		_abilityId = abilityId;
		_hasBaseId = _ctoBaseIds.Contains(abilityId);
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay.Clear();
		selectable.Clear();
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 1, 1, _request.CancellationType);
		SetHeaderAndSubheader();
		List<BaseUserRequest> childRequests = _request.ChildRequests;
		childRequests.Sort(_requestComparer);
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_cardView = _cardViewProvider.GetCardView(mtgGameState.GetTopCardOnStack().InstanceId);
		for (int i = 0; i < childRequests.Count; i++)
		{
			BaseUserRequest baseUserRequest = childRequests[i];
			if (TryConvertRequestToCardData(baseUserRequest, out var cardData))
			{
				DuelScene_CDC duelScene_CDC = _fakeCardController.CreateFakeCard($"Cost Keyword CTO Option {i}", cardData);
				selectable.Add(duelScene_CDC);
				_cardsToDisplay.Add(duelScene_CDC);
				_fakeCardsToRequestMap.Add(duelScene_CDC, baseUserRequest);
			}
		}
		UpdateCostKeywordCardHighlights();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(1, 1u);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	private bool TryConvertRequestToCardData(BaseUserRequest request, out ICardDataAdapter cardData)
	{
		IReadOnlyList<(uint, uint)> abilityIds;
		if (!(request is CastingTimeOption_DoneRequest))
		{
			if (request is CastingTimeOption_CostKeywordRequest castingTimeOption_CostKeywordRequest)
			{
				MtgCardInstance copy = _cardView.Model.Instance.GetCopy();
				copy.InstanceId = 0u;
				copy.AbilityAdders.Clear();
				copy.AbilityModifications.Clear();
				CullCTOAbilities(copy.Abilities);
				List<AbilityPrintingData> list = new List<AbilityPrintingData>(_cardView.Model.Printing.Abilities);
				CullCTOAbilities(list);
				foreach (GreClient.Rules.CastingTimeOption castingTimeOption in copy.CastingTimeOptions)
				{
					if (_keywordTypes.Contains(castingTimeOption.Type) && castingTimeOption.AbilityId.HasValue)
					{
						AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(castingTimeOption.AbilityId.Value);
						if (abilityPrintingById != null)
						{
							copy.Abilities.Insert(0, abilityPrintingById);
							list.Insert(0, abilityPrintingById);
						}
					}
				}
				copy.CastingTimeOptions.Add(new GreClient.Rules.CastingTimeOption(castingTimeOption_CostKeywordRequest.OptionType, castingTimeOption_CostKeywordRequest.GrpId));
				AbilityPrintingData abilityPrintingById2 = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(castingTimeOption_CostKeywordRequest.GrpId);
				if (abilityPrintingById2 != null)
				{
					copy.Abilities.Insert(0, abilityPrintingById2);
					list.Insert(0, abilityPrintingById2);
				}
				CardPrintingData printing = _cardView.Model.Printing;
				CardPrintingRecord record = _cardView.Model.Printing.Record;
				abilityIds = list.Select((AbilityPrintingData x) => (Id: x.Id, TextId: x.TextId)).ToArray();
				CardPrintingData printing2 = new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds));
				cardData = new CardData(copy, printing2);
				return true;
			}
			cardData = null;
			return false;
		}
		MtgCardInstance copy2 = _cardView.Model.Instance.GetCopy();
		copy2.InstanceId = 0u;
		copy2.AbilityAdders.Clear();
		copy2.AbilityModifications.Clear();
		CullCTOAbilities(copy2.Abilities);
		List<AbilityPrintingData> list2 = new List<AbilityPrintingData>(_cardView.Model.Printing.Abilities);
		CullCTOAbilities(list2);
		foreach (GreClient.Rules.CastingTimeOption castingTimeOption2 in copy2.CastingTimeOptions)
		{
			if (_keywordTypes.Contains(castingTimeOption2.Type) && castingTimeOption2.AbilityId.HasValue)
			{
				AbilityPrintingData abilityPrintingById3 = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(castingTimeOption2.AbilityId.Value);
				if (abilityPrintingById3 != null)
				{
					copy2.Abilities.Insert(0, abilityPrintingById3);
					list2.Insert(0, abilityPrintingById3);
				}
			}
		}
		CardPrintingData printing3 = _cardView.Model.Printing;
		CardPrintingRecord record2 = _cardView.Model.Printing.Record;
		abilityIds = list2.Select((AbilityPrintingData x) => (Id: x.Id, TextId: x.TextId)).ToArray();
		CardPrintingData printing4 = new CardPrintingData(printing3, new CardPrintingRecord(record2, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds));
		cardData = new CardData(copy2, printing4);
		return true;
	}

	private void CullCTOAbilities(List<AbilityPrintingData> abilities)
	{
		for (int i = 0; i < abilities.Count; i++)
		{
			if (_ctoBaseIds.Contains(abilities[i].BaseId) || _ctoAbilityIds.Contains(abilities[i].Id))
			{
				abilities.RemoveAt(i);
				i--;
			}
		}
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_fakeCardsToRequestMap.TryGetValue(cardView, out var value))
		{
			SubmitRequest(value);
		}
	}

	private void SubmitRequest(BaseUserRequest request)
	{
		if (!(request is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest))
		{
			if (request is CastingTimeOption_CostKeywordRequest castingTimeOption_CostKeywordRequest)
			{
				castingTimeOption_CostKeywordRequest.SubmitKeywordAction();
			}
		}
		else
		{
			castingTimeOption_DoneRequest.SubmitDone();
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		if (cancelType != AllowCancel.No && cancelType != AllowCancel.None)
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData.BrowserElementKey = "SingleButton";
			buttonStateData.Enabled = true;
			buttonStateData.StyleType = ButtonStyle.StyleType.Main;
			_buttonStateData.Add("CancelButton", buttonStateData);
		}
		return _buttonStateData;
	}

	public override BrowserCardHeader.BrowserCardHeaderData GetCardHeaderData(DuelScene_CDC cardView)
	{
		if (_fakeCardsToRequestMap.TryGetValue(cardView, out var value) && value is CastingTimeOption_CostKeywordRequest castingTimeOption_CostKeywordRequest)
		{
			AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(castingTimeOption_CostKeywordRequest.GrpId);
			if (abilityPrintingById != null)
			{
				uint abilityGrpId = abilityPrintingById.Id;
				if (_hasBaseId)
				{
					if (abilityPrintingById.BaseId == 0)
					{
						return null;
					}
					if (abilityPrintingById.BaseIdNumeral == 0)
					{
						abilityGrpId = abilityPrintingById.BaseId;
					}
				}
				string abilityTextForCard = _cardDatabase.AbilityTextProvider.GetAbilityTextForCard(cardView.Model, abilityGrpId);
				if (!string.IsNullOrEmpty(abilityTextForCard))
				{
					return new BrowserCardHeader.BrowserCardHeaderData(Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/BrowserCardInfo_CastWith"), abilityTextForCard);
				}
			}
		}
		return null;
	}

	public override void CleanUp()
	{
		for (int i = 0; i < _request.ChildRequests.Count; i++)
		{
			_fakeCardController.DeleteFakeCard($"Cost Keyword CTO Option {i}");
		}
		base.CleanUp();
	}

	public override void SetFxBlackboardData(IBlackboard bb)
	{
		base.SetFxBlackboardData(bb);
		bb.SetCardDataExtensive(_cardView.Model);
		bb.Ability = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(_abilityId);
	}

	public override void SetFxBlackboardDataForCard(DuelScene_CDC cardView, IBlackboard bb)
	{
		base.SetFxBlackboardDataForCard(cardView, bb);
		bb.SetCardDataExtensive(cardView.Model);
		if (cardView.Model.Abilities.Count > 0)
		{
			bb.Ability = cardView.Model.Abilities[0];
		}
	}

	protected override bool IsHotSelectable(DuelScene_CDC cdc)
	{
		if (_fakeCardsToRequestMap.TryGetValue(cdc, out var value))
		{
			if (value is CastingTimeOption_CostKeywordRequest castingTimeOption_CostKeywordRequest)
			{
				return castingTimeOption_CostKeywordRequest.Solution != null;
			}
			if (value is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest)
			{
				return castingTimeOption_DoneRequest.Solution != null;
			}
		}
		return true;
	}

	private void UpdateCostKeywordCardHighlights()
	{
		foreach (KeyValuePair<DuelScene_CDC, BaseUserRequest> item in _fakeCardsToRequestMap)
		{
			item.Key.UpdateHighlight(IsHotSelectable(item.Key) ? HighlightType.Hot : HighlightType.None);
		}
	}
}
