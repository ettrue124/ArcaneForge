using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Browser;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class StaticCardNameTranslation : IChoiceOptionsTranslation
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardsInDeckProvider _cardsInDeckProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public StaticCardNameTranslation(IContext context, ICardsInDeckProvider cardsInDeckProvider, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), cardsInDeckProvider, assetLookupSystem)
	{
	}

	public StaticCardNameTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardsInDeckProvider cardsInDeckProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardsInDeckProvider = cardsInDeckProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		IReadOnlyList<uint> ids = request.Ids;
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		IEnumerable<PromptParameter> parameters = request.Prompt.Parameters;
		Dictionary<string, uint> dictionary = new Dictionary<string, uint>();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (CardPrintingData value in _cardDatabase.DatabaseUtilities.GetAllPrintings().Values)
		{
			if ((ids.Count != 0 && !ids.Contains(value.TitleId)) || value.IsToken || value.IsRebalanced || value.LinkedFaceType == LinkedFace.SplitHalf || value.LinkedFaceType == LinkedFace.RoomHalf || value.ExpansionCode.Equals("ArenaSUP"))
			{
				continue;
			}
			string cardTitle = _cardDatabase.CardTitleProvider.GetCardTitle(value.GrpId, null);
			if (cardTitle != null && !dictionary.ContainsKey(cardTitle))
			{
				dictionary[cardTitle] = value.TitleId;
				string localizedTitle = _cardDatabase.CardTitleProvider.GetLocalizedTitle(value.InterchangeableTitleId);
				if (localizedTitle != null && !dictionary.ContainsKey(localizedTitle))
				{
					dictionary[localizedTitle] = value.TitleId;
				}
			}
		}
		bool includeBackOfDoubleFaceCards = true;
		bool includeSplitChildren = true;
		bool includeAdventureChildren = true;
		bool includeLands = true;
		bool includeControlledCreatures = false;
		bool includeNonVisibleSpecializeChildren = true;
		bool includeNonOpponentCards = true;
		bool flag = false;
		PromptParameter promptParameter = parameters.FirstOrDefault((PromptParameter x) => x.ParameterName == "CardId");
		if (promptParameter != null)
		{
			MtgCardInstance cardById = gameState.GetCardById((uint)promptParameter.NumberValue);
			if (cardById != null)
			{
				_assetLookupSystem.Blackboard.Clear();
				_assetLookupSystem.Blackboard.SetCardDataExtensive(new CardData(cardById, cardById.ObjectSourcePrinting));
				if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NameACardPrepopulationLogic> loadedTree))
				{
					NameACardPrepopulationLogic payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
					if (payload != null)
					{
						includeBackOfDoubleFaceCards = payload.IncludeBackOfDoubleFaceCards;
						includeSplitChildren = payload.IncludeSplitChildren;
						includeAdventureChildren = payload.IncludeAdventureChildren;
						includeLands = payload.IncludeLands;
						includeNonVisibleSpecializeChildren = payload.IncludeNonVisibleSpecializeChildren;
						includeNonOpponentCards = payload.IncludeNonOpponentCards;
						includeControlledCreatures = payload.IncludeControlledCreatures;
						flag = payload.FallbackOnDeckGrpIds;
					}
				}
			}
		}
		IEnumerable<uint> enumerable = gameState.SeenCardGrpIds;
		if (gameState.ResolvingCardInstance != null)
		{
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(new CardData(gameState.ResolvingCardInstance, gameState.ResolvingCardInstance.ObjectSourcePrinting));
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NameACardPrepopulationLogic> loadedTree2))
			{
				NameACardPrepopulationLogic payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
				if (payload2 != null)
				{
					if (payload2.SetSourceGrpIdsToDeckGrpIds)
					{
						enumerable = _cardsInDeckProvider.GetCardsInDeck();
					}
					includeBackOfDoubleFaceCards = payload2.IncludeBackOfDoubleFaceCards;
					includeSplitChildren = payload2.IncludeSplitChildren;
					includeAdventureChildren = payload2.IncludeAdventureChildren;
					includeLands = payload2.IncludeLands;
					includeControlledCreatures = payload2.IncludeControlledCreatures;
					includeNonVisibleSpecializeChildren = payload2.IncludeNonVisibleSpecializeChildren;
					includeNonOpponentCards = payload2.IncludeNonOpponentCards;
					flag = payload2.FallbackOnDeckGrpIds;
				}
			}
		}
		foreach (uint item in enumerable)
		{
			CardPrintingData cardPrinting = _cardDatabase.CardDataProvider.GetCardPrintingById(item);
			if (!ExcludeCard(cardPrinting))
			{
				if (includeControlledCreatures && (gameState.Battlefield.VisibleCards.Exists((MtgCardInstance ci) => ci.GrpId == cardPrinting.GrpId && ci.Zone.Type == ZoneType.Battlefield && ci.Owner.IsLocalPlayer) || (IsDualFaceType(cardPrinting.LinkedFaceType) && gameState.Battlefield.VisibleCards.Exists((MtgCardInstance ci) => cardPrinting.LinkedFaceGrpIds.Contains(ci.GrpId) && ci.Zone.Type == ZoneType.Battlefield && ci.Owner.IsLocalPlayer))))
				{
					AddTitleToHintingOptions(_cardDatabase, cardPrinting, list2, dictionary);
				}
				if ((!ids.Any() && !flag) || ids.Contains(cardPrinting.TitleId))
				{
					AddTitleToHintingOptions(_cardDatabase, cardPrinting, list2, dictionary);
				}
			}
		}
		if ((list2.Count == 0) & flag)
		{
			foreach (uint item2 in _cardsInDeckProvider.GetCardsInDeck())
			{
				CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(item2);
				if (!ExcludeCard(cardPrintingById))
				{
					AddTitleToHintingOptions(_cardDatabase, cardPrintingById, list2, dictionary);
				}
			}
		}
		list2.Sort();
		list.AddRange(dictionary.Keys);
		list.Sort();
		return new ChoiceOptions(dictionary, list, list2);
		bool ExcludeCard(CardPrintingData cardPrintingData)
		{
			if (cardPrintingData == null)
			{
				return true;
			}
			if (cardPrintingData.LinkedFaceType == LinkedFace.SplitHalf)
			{
				return true;
			}
			if (!includeBackOfDoubleFaceCards && cardPrintingData.LinkedFaceType == LinkedFace.DfcFront)
			{
				return true;
			}
			if (!includeSplitChildren && cardPrintingData.LinkedFaceType == LinkedFace.SplitCard)
			{
				return true;
			}
			if (!includeAdventureChildren && cardPrintingData.LinkedFaceType == LinkedFace.AdventureParent)
			{
				return true;
			}
			if (!includeLands && cardPrintingData.Types.Contains(CardType.Land))
			{
				return true;
			}
			if (includeControlledCreatures && !cardPrintingData.Types.Contains(CardType.Creature))
			{
				return true;
			}
			IEnumerable<uint> enumerable2 = from x in gameState.VisibleCards
				where gameState.TryGetCard(x.Key, out var card) && (!card.Owner.IsLocalPlayer || !card.Controller.IsLocalPlayer)
				select x.Value.GrpId;
			IEnumerable<uint> enumerable3 = from x in gameState.RevealedCards
				where gameState.TryGetCard(x.Key, out var card) && (!card.Owner.IsLocalPlayer || !card.Controller.IsLocalPlayer)
				select x.Value.GrpId;
			if (!includeNonOpponentCards && !enumerable2.Contains(cardPrintingData.GrpId) && !enumerable3.Contains(cardPrintingData.GrpId) && enumerable2.Intersect(cardPrintingData.LinkedFaceGrpIds).Count() == 0 && enumerable3.Intersect(cardPrintingData.LinkedFaceGrpIds).Count() == 0)
			{
				return true;
			}
			if (!includeNonVisibleSpecializeChildren && (cardPrintingData.LinkedFaceType == LinkedFace.SpecializeChild || cardPrintingData.LinkedFaceType == LinkedFace.SpecializeParent) && !gameState.VisibleCards.Select((KeyValuePair<uint, MtgCardInstance> c) => c.Value.GrpId).Contains(cardPrintingData.GrpId) && !gameState.RevealedCards.Select((KeyValuePair<uint, MtgCardInstance> c) => c.Value.GrpId).Contains(cardPrintingData.GrpId))
			{
				return true;
			}
			return false;
		}
		static bool IsDualFaceType(LinkedFace linkedFace)
		{
			if (linkedFace != LinkedFace.DfcFront && linkedFace != LinkedFace.DfcBack && linkedFace != LinkedFace.MdfcBack)
			{
				return linkedFace == LinkedFace.MdfcFront;
			}
			return true;
		}
	}

	private static void AddTitleToHintingOptions(ICardDatabaseAdapter cdb, CardPrintingData cardPrinting, List<string> hintingOptions, Dictionary<string, uint> choiceToIdMap)
	{
		string originalCardTitle = cdb.CardTitleProvider.GetOriginalCardTitle(cardPrinting.GrpId, null);
		if ((choiceToIdMap == null || choiceToIdMap.ContainsKey(originalCardTitle)) && !hintingOptions.Contains(originalCardTitle))
		{
			hintingOptions.Add(originalCardTitle);
			string localizedTitle = cdb.CardTitleProvider.GetLocalizedTitle(cardPrinting.InterchangeableTitleId);
			if (localizedTitle != null && !hintingOptions.Contains(localizedTitle))
			{
				hintingOptions.Add(localizedTitle);
			}
		}
	}
}
