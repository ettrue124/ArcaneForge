using System.Collections.Generic;
using System.Linq;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class CastingTimeOption_KickerWorkflow : SelectCardsWorkflow<CastingTimeOptionRequest>
{
	private class CastingTimeOptionKickerHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> _doneRequestMap;

		private readonly IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_KickerRequest> _kickerRequestMap;

		public CastingTimeOptionKickerHighlightsGenerator(IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> doneRequestMap, IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_KickerRequest> kickerRequestMap)
		{
			_doneRequestMap = doneRequestMap;
			_kickerRequestMap = kickerRequestMap;
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
			foreach (DuelScene_CDC key2 in _kickerRequestMap.Keys)
			{
				_highlights.EntityHighlights[key2] = highlightTypeForSolutionManaCost(_kickerRequestMap[key2].Solution, _kickerRequestMap[key2].ManaCost);
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
					if (_kickerRequestMap.TryGetValue(card, out var value2))
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

	private const int KICKER_ABILITY_ID = 34;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly Dictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> _doneOptionMappings = new Dictionary<DuelScene_CDC, CastingTimeOption_DoneRequest>();

	private readonly Dictionary<DuelScene_CDC, CastingTimeOption_KickerRequest> _kickerOptionMappings = new Dictionary<DuelScene_CDC, CastingTimeOption_KickerRequest>();

	private AbilityPrintingData _kickerAbility;

	private DuelScene_CDC _kickerCardView;

	private CastingTimeOption_DoneRequest _currentDoneRequest;

	private readonly Dictionary<DuelScene_CDC, HighlightType> _emptyBrowserHighlights = new Dictionary<DuelScene_CDC, HighlightType>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public CastingTimeOption_KickerWorkflow(CastingTimeOptionRequest request, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider browserHeaderTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_cardBuilder = cardBuilder;
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
		_headerTextProvider = browserHeaderTextProvider;
		_highlightsGenerator = new CastingTimeOptionKickerHighlightsGenerator(_doneOptionMappings, _kickerOptionMappings);
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
		CastingTimeOption_KickerRequest castingTimeOption_KickerRequest = childRequests.Find((BaseUserRequest x) => x is CastingTimeOption_KickerRequest) as CastingTimeOption_KickerRequest;
		_kickerAbility = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(castingTimeOption_KickerRequest.GrpId);
		_kickerCardView = _cardViewProvider.GetCardView(castingTimeOption_KickerRequest.AffectorId);
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (BaseUserRequest item in childRequests)
		{
			DuelScene_CDC duelScene_CDC = null;
			if (item is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest)
			{
				_currentDoneRequest = castingTimeOption_DoneRequest;
				MtgCardInstance copy = _kickerCardView.Model.Instance.GetCopy();
				copy.InstanceId = 0u;
				copy.ManaCostOverride = castingTimeOption_DoneRequest.ManaCost.ConvertAll((ManaRequirement x) => ManaQuantity.MakeMana((uint)x.Count, x.Color[0]));
				copy.Abilities.RemoveAll((AbilityPrintingData x) => IsAKickerOption(childRequests, x.Id));
				CardPrintingData printing = _kickerCardView.Model.Printing;
				CardPrintingRecord record = _kickerCardView.Model.Printing.Record;
				IReadOnlyList<(uint, uint)> abilityIds = _kickerCardView.Model.Printing.AbilityIds.Where(((uint Id, uint TextId) x) => !IsAKickerOption(childRequests, x.Id)).ToArray();
				CardPrintingData printing2 = new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds));
				duelScene_CDC = _cardBuilder.CreateCDC(new CardData(copy, printing2));
				_doneOptionMappings[duelScene_CDC] = castingTimeOption_DoneRequest;
			}
			else
			{
				CastingTimeOption_KickerRequest kickOption = item as CastingTimeOption_KickerRequest;
				if (kickOption != null)
				{
					MtgCardInstance copy2 = _kickerCardView.Model.Instance.GetCopy();
					CardPrintingData printing3 = _kickerCardView.Model.Printing;
					copy2.InstanceId = 0u;
					copy2.ManaCostOverride = kickOption.ManaCost.ConvertAll((ManaRequirement x) => ManaQuantity.MakeMana((uint)x.Count, x.Color[0]));
					copy2.Abilities.RemoveAll((AbilityPrintingData x) => IsAKickerOption(childRequests, x.Id) && x.Id != kickOption.GrpId);
					copy2.CastingTimeOptions.Add(new CastingTimeOption(CastingTimeOptionType.Kicker, kickOption.GrpId));
					duelScene_CDC = _cardBuilder.CreateCDC(new CardData(copy2, printing3));
					_kickerOptionMappings[duelScene_CDC] = kickOption;
				}
			}
			if (duelScene_CDC != null)
			{
				list.Add(duelScene_CDC);
			}
		}
		_cardsToDisplay = list;
		selectable.Clear();
		selectable.AddRange(list);
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

	private static bool IsAKickerOption(List<BaseUserRequest> requests, uint GrpId)
	{
		foreach (BaseUserRequest request in requests)
		{
			if (request is CastingTimeOption_KickerRequest castingTimeOption_KickerRequest && castingTimeOption_KickerRequest.GrpId == GrpId)
			{
				return true;
			}
		}
		return false;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
	}

	public override Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		return _emptyBrowserHighlights;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_doneOptionMappings.ContainsKey(cardView) || _kickerOptionMappings.ContainsKey(cardView))
		{
			CastingTimeOption_KickerRequest value2;
			if (_doneOptionMappings.TryGetValue(cardView, out var value))
			{
				value.SubmitDone();
			}
			else if (_kickerOptionMappings.TryGetValue(cardView, out value2))
			{
				value2.SubmitKicked();
			}
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
		if (_kickerOptionMappings.TryGetValue(cardView, out var value))
		{
			AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(value.GrpId);
			if (abilityPrintingById != null && abilityPrintingById.BaseId != 0)
			{
				string abilityTextForCard = _cardDatabase.AbilityTextProvider.GetAbilityTextForCard(cardView.Model, abilityPrintingById.BaseId);
				if (!string.IsNullOrEmpty(abilityTextForCard))
				{
					string text = ManaUtilities.ManaRequirementsToTextString(ManaUtilities.ManaDifferences(value.ManaCost, _currentDoneRequest.ManaCost));
					return new BrowserCardHeader.BrowserCardHeaderData(Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/BrowserCardInfo_CastWith"), abilityTextForCard + text);
				}
			}
		}
		return null;
	}

	public override void CleanUp()
	{
		CardHoverController.OnHoveredCardUpdated -= OnHoveredCardUpdated;
		base.CleanUp();
	}

	private void OnHoveredCardUpdated(DuelScene_CDC card)
	{
		SetHighlights();
	}

	public override void SetFxBlackboardData(IBlackboard bb)
	{
		base.SetFxBlackboardData(bb);
		bb.SetCardDataExtensive(_kickerCardView.Model);
		bb.Ability = _kickerAbility;
	}

	public override void SetFxBlackboardDataForCard(DuelScene_CDC cardView, IBlackboard bb)
	{
		base.SetFxBlackboardDataForCard(cardView, bb);
		bb.Ability = cardView.Model.Abilities.FirstOrDefault((AbilityPrintingData x) => x.BaseId == 34);
	}
}
