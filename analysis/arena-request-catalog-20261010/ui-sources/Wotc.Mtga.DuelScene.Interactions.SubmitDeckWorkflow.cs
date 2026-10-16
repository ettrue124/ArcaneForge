using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Prefab;
using Core.Code.Input;
using GreClient.CardData;
using GreClient.Rules;
using MTGA.KeyboardManager;
using Pooling;
using SharedClientCore.SharedClientCore.Code.Providers;
using UnityEngine;
using Wizards.MDN;
using Wizards.Mtga;
using Wizards.Mtga.Decks;
using Wizards.Mtga.FrontDoorModels;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Loc;
using Wotc.Mtga.Providers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SubmitDeckWorkflow : WorkflowBase<SubmitDeckRequest>, IAutoRespondWorkflow
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IDuelSceneStateController _duelSceneStateController;

	private readonly IAccountClient _accountClient;

	private readonly MatchManager _matchManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly MtgTimer _sideboardTimer;

	private readonly Camera _camera;

	private SideboardInterface _sideboardInterface;

	private SystemMessageManager.SystemMessageHandle _sideboardMessage;

	public SubmitDeckWorkflow(SubmitDeckRequest request, Camera camera, ICardDatabaseAdapter cardDatabase, IAccountClient accountClient, IGameStateProvider gameStateProvider, IDuelSceneStateController duelSceneStateController, MatchManager matchManager, AssetLookupSystem assetLookupSystem, MtgTimer sideboardTimer)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_accountClient = accountClient;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_duelSceneStateController = duelSceneStateController;
		_matchManager = matchManager;
		_assetLookupSystem = assetLookupSystem;
		_sideboardTimer = sideboardTimer;
		_camera = camera;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		SideboardInterface[] array = Object.FindObjectsByType<SideboardInterface>(FindObjectsSortMode.None);
		foreach (SideboardInterface sideboardInterface in array)
		{
			if ((bool)sideboardInterface)
			{
				Object.Destroy(sideboardInterface.gameObject);
			}
		}
		FormatManager formatManager = Pantry.Get<FormatManager>();
		_assetLookupSystem.Blackboard.Clear();
		SideboardInterfacePrefab payload = _assetLookupSystem.TreeLoader.LoadTree<SideboardInterfacePrefab>(returnNewTree: false).GetPayload(_assetLookupSystem.Blackboard);
		if (payload != null)
		{
			_sideboardInterface = AssetLoader.Instantiate<SideboardInterface>(payload.PrefabPath);
		}
		_sideboardInterface.InitializeDeckBuilder(_camera, _matchManager?.Event?.PlayerEvent?.Format, GenerateDeckBuilderContext(_matchManager.Event, _matchManager, formatManager, _request.Deck), Pantry.Get<InventoryManager>(), Pantry.Get<CardDatabase>(), Pantry.Get<CardViewBuilder>(), Pantry.Get<IBILogger>(), Pantry.Get<CosmeticsProvider>(), Pantry.Get<KeyboardManager>(), Pantry.Get<IActionSystem>(), Pantry.Get<EventManager>(), formatManager, Pantry.Get<IClientLocProvider>(), Pantry.Get<IUnityObjectPool>(), Pantry.Get<IObjectPool>(), _assetLookupSystem, Pantry.Get<IEmoteDataProvider>(), Pantry.Get<ISetMetadataProvider>());
		string screenName = _matchManager.LocalPlayerInfo.ScreenName;
		string screenName2 = _matchManager.OpponentInfo.ScreenName;
		_sideboardInterface.SetPlayerName(GREPlayerNum.LocalPlayer, screenName);
		_sideboardInterface.SetPlayerName(GREPlayerNum.Opponent, screenName2);
		GameInfo gameInfo = mtgGameState.GameInfo;
		int num = 0;
		int num2 = 0;
		MtgPlayer localPlayer = mtgGameState.LocalPlayer;
		foreach (ResultSpec result in gameInfo.Results)
		{
			if (result.Result == ResultType.WinLoss)
			{
				if (result.WinningTeamId == localPlayer.InstanceId)
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		_sideboardInterface.SetPlayerWins(GREPlayerNum.LocalPlayer, num);
		_sideboardInterface.SetPlayerWins(GREPlayerNum.Opponent, num2);
		string localizedText = Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardIntro", ("gameNumber", (gameInfo.GameNumber + 1).ToString()));
		_sideboardInterface.SetIntroText(localizedText);
		_sideboardInterface.SetTimer(_sideboardTimer);
		_sideboardInterface.DoneClicked += OnDoneClicked;
	}

	private static DeckBuilderContext GenerateDeckBuilderContext(EventContext evt, MatchManager matchManager, FormatManager formatManager, DeckMessage deck)
	{
		string text = "Standard";
		DeckInfo deckInfo = new DeckInfo();
		if (evt != null)
		{
			text = evt.PlayerEvent.EventUXInfo.DeckSelectFormat;
			if (evt.PlayerEvent.CourseData.CourseDeck != null)
			{
				deckInfo = DeckServiceWrapperHelpers.ToAzureModel(evt.PlayerEvent.CourseData.CourseDeck);
			}
		}
		else if (matchManager.HasReconnected && matchManager.LocalPlayerInfo.DeckCards.Count < 60)
		{
			text = "DirectGameLimited";
		}
		deckInfo.format = (string.IsNullOrEmpty(deckInfo.format) ? text : deckInfo.format);
		deckInfo.name = (string.IsNullOrEmpty(deckInfo.name) ? "Deck" : deckInfo.name);
		deckInfo.mainDeck = generateCardsInDeckFromGrpIds(deck.DeckCards);
		deckInfo.sideboard = generateCardsInDeckFromGrpIds(deck.SideboardCards);
		uint deckMessageFieldFour = deck.DeckMessageFieldFour;
		if (deckMessageFieldFour != 0)
		{
			deckInfo.companion = new CardInDeck(deckMessageFieldFour, 1u);
		}
		DeckBuilderContext deckBuilderContext = new DeckBuilderContext(deckInfo, evt, sideboarding: true);
		string formatName = ((!string.IsNullOrEmpty(evt?.PlayerEvent.EventUXInfo.DeckSelectFormat)) ? evt.PlayerEvent.EventUXInfo.DeckSelectFormat : deckInfo.format);
		deckBuilderContext.Format = formatManager.GetSafeFormat(formatName);
		return deckBuilderContext;
		static List<CardInDeck> generateCardsInDeckFromGrpIds(ICollection<uint> grpIds)
		{
			Dictionary<uint, CardInDeck> dictionary = new Dictionary<uint, CardInDeck>();
			foreach (uint grpId in grpIds)
			{
				if (dictionary.TryGetValue(grpId, out var value))
				{
					value.Quantity++;
				}
				else
				{
					dictionary.Add(grpId, new CardInDeck(grpId, 1u));
				}
			}
			return new List<CardInDeck>(dictionary.Values);
		}
	}

	private void OnDoneClicked(DeckInfo deckInfo)
	{
		string userAccountID = _accountClient?.AccountInformation?.PersonaID;
		uint num = 0u;
		uint num2 = 0u;
		foreach (CardInDeck item in deckInfo.mainDeck)
		{
			num += item.Quantity;
		}
		foreach (CardInDeck item2 in deckInfo.sideboard)
		{
			num2 += item2.Quantity;
		}
		DeckConstraintInfo deckConstraintInfo = ((MtgGameState)_gameStateProvider.LatestGameState).GameInfo.DeckConstraintInfo;
		(string, string)[] locParams = new (string, string)[3]
		{
			("MinMainDeckRequired", deckConstraintInfo.MinDeckSize.ToString()),
			("MaxMainDeckRequired", deckConstraintInfo.MaxDeckSize.ToString()),
			("MaxSideboardCards", "15")
		};
		if (num < deckConstraintInfo.MinDeckSize)
		{
			_sideboardMessage = SystemMessageManager.Instance.ShowMessage(Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/DeckBuilder_MessageTitle_InvalidDeck"), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/DeckBuilder_InvalidDeckReasons_BelowX", locParams), Languages.ActiveLocProvider.GetLocalizedText("MainNav/Store/OK"), null);
			return;
		}
		if (num > deckConstraintInfo.MaxDeckSize)
		{
			_sideboardMessage = SystemMessageManager.Instance.ShowMessage(Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/DeckBuilder_MessageTitle_InvalidDeck"), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/DeckBuilder_InvalidDeckReasons_MoreThanXInMain", locParams), Languages.ActiveLocProvider.GetLocalizedText("MainNav/Store/OK"), null);
			return;
		}
		if (num2 > deckConstraintInfo.MaxSideboardSize)
		{
			_sideboardMessage = SystemMessageManager.Instance.ShowMessage(Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/DeckBuilder_MessageTitle_InvalidDeck"), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/DeckBuilder_InvalidDeckReasons_MoreThanXInSideboard", locParams), Languages.ActiveLocProvider.GetLocalizedText("MainNav/Store/OK"), null);
			return;
		}
		if (!MDNPlayerPrefs.GetHasSeenSideboardSubmitTip(userAccountID))
		{
			MtgTimer sideboardTimer = _sideboardTimer;
			if (sideboardTimer != null && sideboardTimer.RemainingTime > 10f)
			{
				MDNPlayerPrefs.SetHasSeenSideboardSubmitTip(userAccountID, value: true);
				_sideboardMessage = SystemMessageManager.Instance.ShowMessage(Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardTip_Title"), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardTip_Message"), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardTip_CancelButton"), null, Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardTip_OKButton"), submitDeck);
				return;
			}
		}
		CardInDeck companion = deckInfo.companion;
		if (companion != null && companion.Quantity != 0 && !deckInfo.isCompanionValid)
		{
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(deckInfo.companion.Id);
			_sideboardMessage = SystemMessageManager.Instance.ShowMessage(Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/CompanionInvalid_Title"), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/CompanionInvalid_Body", ("cardName", _cardDatabase.GreLocProvider.GetLocalizedText(cardPrintingById.TitleId))), Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardTip_CancelButton"), null, Languages.ActiveLocProvider.GetLocalizedText("MainNav/DeckBuilder/SideboardTip_OKButton"), SubmitWithoutCompanion);
		}
		else
		{
			submitDeck();
		}
		void SubmitWithoutCompanion()
		{
			deckInfo.companion = null;
			deckInfo.isCompanionValid = false;
			submitDeck();
		}
		void submitDeck()
		{
			DeckMessage deckMessage = new DeckMessage();
			deckMessage.DeckCards.Clear();
			foreach (CardInDeck item3 in deckInfo.mainDeck)
			{
				for (int i = 0; i < item3.Quantity; i++)
				{
					deckMessage.DeckCards.Add(item3.Id);
				}
			}
			deckMessage.SideboardCards.Clear();
			foreach (CardInDeck item4 in deckInfo.sideboard)
			{
				for (int j = 0; j < item4.Quantity; j++)
				{
					deckMessage.SideboardCards.Add(item4.Id);
				}
			}
			CardInDeck companion2 = deckInfo.companion;
			if (companion2 != null)
			{
				deckMessage.DeckMessageFieldFour = companion2.Id;
			}
			_request.SubmitDeck(deckMessage);
			_duelSceneStateController.SetState(false);
			MatchManager.PlayerInfo localPlayerInfo = _matchManager.LocalPlayerInfo;
			localPlayerInfo.SetDeckInfo(deckMessage.DeckCards, deckMessage.SideboardCards, localPlayerInfo.CardStyles);
		}
	}

	public override void CleanUp()
	{
		if (SystemMessageManager.Instance.ShowingMessage && _sideboardMessage != null)
		{
			SystemMessageManager.Instance.Close(_sideboardMessage);
			_sideboardMessage = null;
		}
		if (_sideboardInterface != null)
		{
			_sideboardInterface.DoneClicked -= OnDoneClicked;
		}
	}

	public bool TryAutoRespond()
	{
		DeckMessage deck = _request.Deck;
		if (deck.SideboardCards.Count > 0)
		{
			return false;
		}
		if (deckIsOversized(_gameStateProvider.LatestGameState, deck))
		{
			return false;
		}
		_request.SubmitDeck(deck);
		return true;
		static bool deckIsOversized(MtgGameState gameState, DeckMessage deckMessage)
		{
			if (gameState == null)
			{
				return false;
			}
			return gameState.GameInfo.SuperFormat switch
			{
				SuperFormat.Constructed => deckMessage.DeckCards.Count > 60, 
				SuperFormat.Limited => deckMessage.DeckCards.Count > 40, 
				_ => false, 
			};
		}
	}
}
