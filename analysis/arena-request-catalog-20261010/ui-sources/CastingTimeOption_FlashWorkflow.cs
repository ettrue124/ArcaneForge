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
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class CastingTimeOption_FlashWorkflow : SelectCardsWorkflow<CastingTimeOptionRequest>, IClickableWorkflow
{
	private class CastingTimeOptionFlashHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> _doneRequestMap;

		private readonly IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_TimingPermissionRequest> _flashRequestMap;

		public CastingTimeOptionFlashHighlightsGenerator(IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> doneRequestMap, IReadOnlyDictionary<DuelScene_CDC, CastingTimeOption_TimingPermissionRequest> flashRequestMap)
		{
			_doneRequestMap = doneRequestMap;
			_flashRequestMap = flashRequestMap;
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
			foreach (DuelScene_CDC key2 in _flashRequestMap.Keys)
			{
				_highlights.EntityHighlights[key2] = highlightTypeForSolutionManaCost(_flashRequestMap[key2].Solution, _flashRequestMap[key2].ManaCost);
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
				if ((bool)card)
				{
					if (_doneRequestMap.TryGetValue(card, out var value))
					{
						return tryGetSolutionActions(value.Solution, out reference);
					}
					if (_flashRequestMap.TryGetValue(card, out var value2))
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

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly Dictionary<DuelScene_CDC, CastingTimeOption_DoneRequest> _doneOptionMappings = new Dictionary<DuelScene_CDC, CastingTimeOption_DoneRequest>();

	private readonly Dictionary<DuelScene_CDC, CastingTimeOption_TimingPermissionRequest> _flashOptionMappings = new Dictionary<DuelScene_CDC, CastingTimeOption_TimingPermissionRequest>();

	private AbilityPrintingData _flashAbility;

	private DuelScene_CDC _flashCardView;

	private readonly Dictionary<DuelScene_CDC, HighlightType> _emptyBrowserHighlights = new Dictionary<DuelScene_CDC, HighlightType>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public CastingTimeOption_FlashWorkflow(CastingTimeOptionRequest request, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider browserHeaderTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_cardBuilder = cardBuilder;
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
		_headerTextProvider = browserHeaderTextProvider;
		_highlightsGenerator = new CastingTimeOptionFlashHighlightsGenerator(_doneOptionMappings, _flashOptionMappings);
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
		CastingTimeOption_TimingPermissionRequest flashOption = childRequests.Find((BaseUserRequest x) => x is CastingTimeOption_TimingPermissionRequest) as CastingTimeOption_TimingPermissionRequest;
		_flashAbility = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(flashOption.GrpId);
		_flashCardView = _cardViewProvider.GetCardView(flashOption.SourceId);
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (BaseUserRequest item in childRequests)
		{
			DuelScene_CDC duelScene_CDC = null;
			if (item is CastingTimeOption_DoneRequest castingTimeOption_DoneRequest)
			{
				MtgCardInstance copy = _flashCardView.Model.Instance.GetCopy();
				copy.InstanceId = 0u;
				copy.ManaCostOverride = castingTimeOption_DoneRequest.ManaCost.ConvertAll((ManaRequirement x) => ManaQuantity.MakeMana((uint)x.Count, x.Color[0]));
				copy.Abilities.RemoveAll((AbilityPrintingData x) => x.Id == flashOption.GrpId);
				CardPrintingData printing = _flashCardView.Model.Printing;
				CardPrintingRecord record = _flashCardView.Model.Printing.Record;
				IReadOnlyList<(uint, uint)> abilityIds = _flashCardView.Model.Printing.AbilityIds.Where(((uint Id, uint TextId) x) => x.Id != flashOption.GrpId).ToArray();
				CardPrintingData printing2 = new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds));
				duelScene_CDC = _cardBuilder.CreateCDC(new CardData(copy, printing2));
				_doneOptionMappings[duelScene_CDC] = castingTimeOption_DoneRequest;
			}
			else if (item is CastingTimeOption_TimingPermissionRequest)
			{
				MtgCardInstance copy2 = _flashCardView.Model.Instance.GetCopy();
				CardPrintingData printing3 = _flashCardView.Model.Printing;
				copy2.InstanceId = 0u;
				copy2.ManaCostOverride = flashOption.ManaCost.ConvertAll((ManaRequirement x) => ManaQuantity.MakeMana((uint)x.Count, x.Color[0]));
				duelScene_CDC = _cardBuilder.CreateCDC(new CardData(copy2, printing3));
				_flashOptionMappings[duelScene_CDC] = flashOption;
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

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "CancelButton" && _request.CanCancel)
		{
			_request.Cancel();
		}
	}

	public override Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		return _emptyBrowserHighlights;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (CanClick(cardView, SimpleInteractionType.Primary))
		{
			OnClick(cardView, SimpleInteractionType.Primary);
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
		if (_flashOptionMappings.TryGetValue(cardView, out var value))
		{
			AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(value.GrpId);
			if (abilityPrintingById != null && abilityPrintingById.ReferencedAbilityIds.Count > 0)
			{
				string abilityTextForCard = _cardDatabase.AbilityTextProvider.GetAbilityTextForCard(cardView.Model, abilityPrintingById.ReferencedAbilityIds[0]);
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
		bb.SetCardDataExtensive(_flashCardView.Model);
		bb.Ability = _flashAbility;
	}

	public override void SetFxBlackboardDataForCard(DuelScene_CDC cardView, IBlackboard bb)
	{
		base.SetFxBlackboardDataForCard(cardView, bb);
		bb.Ability = cardView.Model.Abilities.FirstOrDefault((AbilityPrintingData x) => x.ReferencedAbilityTypes.Contains(AbilityType.Flash));
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (entity is DuelScene_CDC && clickType == SimpleInteractionType.Primary)
		{
			return true;
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (entity is DuelScene_CDC key)
		{
			CastingTimeOption_TimingPermissionRequest value2;
			if (_doneOptionMappings.TryGetValue(key, out var value))
			{
				value.SubmitDone();
			}
			else if (_flashOptionMappings.TryGetValue(key, out value2))
			{
				value2.SubmitFlash();
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
}
