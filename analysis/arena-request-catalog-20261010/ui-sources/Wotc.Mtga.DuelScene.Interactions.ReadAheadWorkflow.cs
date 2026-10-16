using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class ReadAheadWorkflow : BrowserWorkflowBase<NumericInputRequest>
{
	private const string FAKE_CARD_FORMAT = "READ_AHEAD_FAKE_CARD_{0}";

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IEntityViewManager _viewManager;

	private readonly IPromptEngine _promptEngine;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGreLocProvider _greLocProvier;

	private readonly IBrowserController _browserController;

	private DuelScene_CDC _sourceCDC;

	private ReadAheadBrowser _browser;

	private uint _current;

	public readonly uint Min;

	public readonly uint Max;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ReadAhead;
	}

	public ReadAheadWorkflow(NumericInputRequest request, IGameStateProvider gameStateProviderProvider, ICardDatabaseAdapter cardDatabaseAdapter, IEntityViewManager viewManager, IPromptEngine promptEngine, IClientLocProvider clientLocProvider, IGreLocProvider greLocProvider, IBrowserController browserController)
		: base(request)
	{
		_gameStateProvider = gameStateProviderProvider;
		_cardDatabase = cardDatabaseAdapter;
		_viewManager = viewManager;
		_promptEngine = promptEngine;
		_clientLocProvider = clientLocProvider;
		_greLocProvier = greLocProvider;
		_browserController = browserController;
		Min = request.Min;
		Max = request.Max;
		_current = Min;
	}

	public override bool CanApply(List<UXEvent> events)
	{
		return events.Count == 0;
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay.Clear();
		_sourceCDC = GetSourceCard(GetSourceId(_request.SourceId, _gameStateProvider.LatestGameState));
		_cardsToDisplay.Add(_sourceCDC);
		_buttonStateData = new Dictionary<string, ButtonStateData>(1);
		_buttonStateData["SubmitButton"] = new ButtonStateData
		{
			BrowserElementKey = "SubmitButton",
			Enabled = true,
			LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit",
			StyleType = ButtonStyle.StyleType.Main
		};
		SetButtons();
		_header = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/ReadAhead_Header");
		_subHeader = _promptEngine.GetPromptText((int)_request.Prompt.PromptId);
		IBrowser browser = _browserController.OpenBrowser(this);
		if (browser is ReadAheadBrowser browser2)
		{
			_browser = browser2;
			_browser.SetMaxVal((int)_request.Max);
			_browser.SetMinVal((int)_request.Min);
			_browser.SetValue((int)_request.Min);
			ReadAheadBrowser browser3 = _browser;
			browser3.SpinnerValueChanged = (Action<int>)Delegate.Combine(browser3.SpinnerValueChanged, new Action<int>(OnSpinnerValueChanged));
		}
		SetOpenedBrowser(browser);
	}

	private DuelScene_CDC GetSourceCard(uint sourceId)
	{
		if (_viewManager.TryGetCardView(sourceId, out var cardView))
		{
			return cardView;
		}
		if (_viewManager.TryGetFakeCard($"READ_AHEAD_FAKE_CARD_{sourceId}", out var fakeCdc))
		{
			return fakeCdc;
		}
		return CreateFakeSourceCard();
	}

	private uint GetSourceId(uint sourceId, MtgGameState gameState)
	{
		if (gameState.TryGetCard(sourceId, out var card))
		{
			return card.InstanceId;
		}
		return sourceId;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "SubmitButton" && NumericInputValidation.CanSubmit(_current, _request))
		{
			_request.SubmitValue(_current);
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_submit, AudioManager.Default);
		}
	}

	public override void CleanUp()
	{
		_viewManager.DeleteFakeCard($"READ_AHEAD_FAKE_CARD_{_request.SourceId}");
		_sourceCDC = null;
		if (_browser != null)
		{
			ReadAheadBrowser browser = _browser;
			browser.SpinnerValueChanged = (Action<int>)Delegate.Remove(browser.SpinnerValueChanged, new Action<int>(OnSpinnerValueChanged));
			_browser.Close();
			_browser = null;
		}
		base.CleanUp();
	}

	private ICardDataAdapter GetFakeCardModel(MtgGameState gameState, uint sourceId, int selectedChapter = 0)
	{
		if (gameState.TryGetCard(sourceId, out var card))
		{
			MtgCardInstance copy = card.GetCopy();
			for (int i = 0; i < copy.Abilities.Count; i++)
			{
				AbilityPrintingData abilityPrintingData = copy.Abilities[i];
				if (abilityPrintingData.BaseId == 166 && relativeBaseIdNumeral(i, copy.Abilities, _greLocProvier) < selectedChapter)
				{
					copy.Abilities.RemoveAt(i);
					copy.AbilityModifications.Add(AbilityModification.Removed(abilityPrintingData.Id));
					i--;
				}
			}
			return CardDataExtensions.CreateWithDatabase(copy, _cardDatabase);
		}
		return CardDataExtensions.CreateBlank();
		static uint? relativeBaseIdNumeral(int idx, IReadOnlyList<AbilityPrintingData> abilities, IGreLocProvider locProvider)
		{
			AbilityPrintingData abilityPrintingData2 = abilities[idx];
			uint? baseIdNumeral = abilityPrintingData2.BaseIdNumeral;
			string localizedText = locProvider.GetLocalizedText(abilityPrintingData2.TextId, "en-US");
			for (int j = idx + 1; j < abilities.Count; j++)
			{
				AbilityPrintingData abilityPrintingData3 = abilities[j];
				if (!localizedText.Equals(locProvider.GetLocalizedText(abilityPrintingData3.TextId, "en-US")))
				{
					break;
				}
				baseIdNumeral = abilityPrintingData3.BaseIdNumeral;
			}
			return baseIdNumeral;
		}
	}

	private DuelScene_CDC CreateFakeSourceCard()
	{
		uint sourceId = _request.SourceId;
		ICardDataAdapter fakeCardModel = GetFakeCardModel(_gameStateProvider.LatestGameState, sourceId);
		return _viewManager.CreateFakeCard($"READ_AHEAD_FAKE_CARD_{sourceId}", fakeCardModel);
	}

	public void OnSpinnerValueChanged(int current)
	{
		_current = (uint)current;
		if (_sourceCDC != null)
		{
			_sourceCDC.SetModel(GetFakeCardModel(_gameStateProvider.LatestGameState, _request.SourceId, current));
		}
	}
}
