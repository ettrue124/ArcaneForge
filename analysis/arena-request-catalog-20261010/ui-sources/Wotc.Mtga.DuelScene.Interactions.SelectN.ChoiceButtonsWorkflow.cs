using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Ability;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.Prompts;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceButtonsWorkflow : WorkflowBase<SelectNRequest>, IDuelSceneBrowserProvider, IBrowserHeaderProvider, IButtonScrollListBrowserProvider, IAutoRespondWorkflow
{
	private readonly ChoiceOptions _choiceOptions;

	private BrowserBase _openedBrowser;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _scrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly List<uint> SortOrderForGideonBlackblade = new List<uint> { 15u, 12u, 104u };

	private readonly AssetTracker _assetTracker = new AssetTracker();

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly ICardDatabaseAdapter _cardDatabaseAdapter;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly IBrowserController _browserController;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	public ChoiceButtonsWorkflow(SelectNRequest request, ChoiceOptions choiceOptions, AssetLookupSystem assetLookupSystem, ICardDatabaseAdapter cardDatabaseAdapter, ICardHolderProvider cardHolderProvider, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, IBrowserController browserController)
		: base(request)
	{
		_choiceOptions = choiceOptions;
		_assetLookupSystem = assetLookupSystem;
		_cardDatabaseAdapter = cardDatabaseAdapter;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_browserController = browserController;
	}

	protected override void ApplyInteractionInternal()
	{
		List<uint> sortedIds = ((_stack.Get().TryGetTopCardOnStack(out var topCard) && topCard.Model.GrpId == 133344) ? SortOrderForGideonBlackblade : new List<uint>());
		foreach (string sortedChoice in GetSortedChoices(sortedIds))
		{
			Sprite sprite = null;
			if (_request.StaticList == StaticList.Keywords)
			{
				AbilityPrintingData abilityPrintingById = _cardDatabaseAdapter.AbilityDataProvider.GetAbilityPrintingById(_choiceOptions.ChoiceToIdMap[sortedChoice]);
				_assetLookupSystem.Blackboard.Clear();
				_assetLookupSystem.Blackboard.Ability = abilityPrintingById;
				if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<BadgeEntry> loadedTree))
				{
					BadgeEntry payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
					if (payload != null)
					{
						sprite = _assetTracker.AcquireAndTrack<Sprite>(sortedChoice, payload.Data.SpriteRef.RelativePath);
					}
				}
				_assetLookupSystem.Blackboard.Clear();
			}
			_scrollListButtonStateData[sortedChoice] = new ButtonStateData
			{
				Sprite = sprite,
				LocalizedString = new UnlocalizedMTGAString(sortedChoice),
				StyleType = HighlightForButton(_request.HotIds, _choiceOptions.ChoiceToIdMap[sortedChoice]),
				Enabled = _request.Ids.Contains(_choiceOptions.ChoiceToIdMap[sortedChoice])
			};
		}
		if (_request.CancellationType == AllowCancel.Continue)
		{
			_assetLookupSystem.Blackboard.Cleanup();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(topCard.Model);
			string text = "DuelScene/ClientPrompt/ClientPrompt_Button_Decline";
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonTextPayload> loadedTree2))
			{
				SecondaryButtonTextPayload payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
				if (payload2 != null)
				{
					text = payload2.LocKey.Key;
				}
			}
			_scrollListButtonStateData["CancelButton"] = new ButtonStateData
			{
				LocalizedString = text,
				BrowserElementKey = "CancelButton",
				StyleType = ButtonStyle.StyleType.Secondary
			};
		}
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private ButtonStyle.StyleType HighlightForButton(IReadOnlyList<uint> hotIds, uint id)
	{
		if (hotIds.Count > 0)
		{
			if (!hotIds.Contains(id))
			{
				return ButtonStyle.StyleType.Tepid;
			}
			return ButtonStyle.StyleType.Secondary;
		}
		return ButtonStyle.StyleType.Secondary;
	}

	private List<string> GetSortedChoices(List<uint> sortedIds)
	{
		List<string> list = new List<string>(_choiceOptions.SortedChoices);
		list.Sort((string x, string y) =>
		{
			uint xId = _choiceOptions.ChoiceToIdMap[x];
			uint yId = _choiceOptions.ChoiceToIdMap[y];
			int num = sortedIds.FindIndex((uint i) => i == xId);
			if (num < 0)
			{
				num = int.MaxValue;
			}
			int num2 = sortedIds.FindIndex((uint i) => i == yId);
			if (num2 < 0)
			{
				num2 = int.MaxValue;
			}
			return (num != num2) ? num.CompareTo(num2) : xId.CompareTo(yId);
		});
		return list;
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonScrollList;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _browserButtonStateData;
	}

	public Dictionary<string, ButtonStateData> GetScrollListButtonDataByKey()
	{
		return _scrollListButtonStateData;
	}

	private void OnButtonPressed(string buttonKey)
	{
		uint value;
		if (buttonKey.Equals("CancelButton"))
		{
			_request.Cancel();
		}
		else if (_choiceOptions.ChoiceToIdMap.TryGetValue(buttonKey, out value))
		{
			_request.SubmitSelection(value);
		}
	}

	private void Browser_OnClosed()
	{
		_openedBrowser.ButtonPressedHandlers -= OnButtonPressed;
		_openedBrowser.ClosedHandlers -= Browser_OnClosed;
		_openedBrowser = null;
	}

	private void SetOpenedBrowser(IBrowser browser)
	{
		_openedBrowser = (BrowserBase)browser;
		_openedBrowser.ButtonPressedHandlers += OnButtonPressed;
		_openedBrowser.ClosedHandlers += Browser_OnClosed;
	}

	public override void CleanUp()
	{
		_openedBrowser?.Close();
		_assetTracker.Cleanup();
		_stack.ClearCache();
	}

	public virtual string GetHeaderText()
	{
		return Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_One");
	}

	public virtual string GetSubHeaderText()
	{
		string localizedText = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_Text");
		if (AppendLoopCountFormatter.TryFormat(localizedText, _gameStateProvider.CurrentGameState, out var promptText))
		{
			return promptText;
		}
		return localizedText;
	}

	protected override void SetPrompt()
	{
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
	}

	public bool TryAutoRespond()
	{
		if (_gameplaySettings.FullControlEnabled)
		{
			return false;
		}
		int count = _request.Ids.Count;
		if (count != _request.MinSel || count != _request.MaxSel)
		{
			return false;
		}
		if (_request.CancellationType == AllowCancel.Continue)
		{
			return false;
		}
		_request.SubmitSelection(_request.Ids);
		return true;
	}
}
