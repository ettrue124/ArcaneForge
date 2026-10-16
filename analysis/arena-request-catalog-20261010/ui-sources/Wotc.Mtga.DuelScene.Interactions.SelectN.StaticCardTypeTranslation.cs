using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class StaticCardTypeTranslation : IChoiceOptionsTranslation
{
	private static readonly List<CardType> AllCastableTypes = new List<CardType>
	{
		CardType.Artifact,
		CardType.Battle,
		CardType.Creature,
		CardType.Enchantment,
		CardType.Instant,
		CardType.Kindred,
		CardType.Planeswalker,
		CardType.Sorcery
	};

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardsInDeckProvider _cardsInDeckProvider;

	public StaticCardTypeTranslation(IContext context, ICardsInDeckProvider cardsInDeckProvider)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), cardsInDeckProvider)
	{
	}

	public StaticCardTypeTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardsInDeckProvider cardsInDeckProvider)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardsInDeckProvider = cardsInDeckProvider;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		IGreLocProvider greLocProvider = _cardDatabase.GreLocProvider;
		Dictionary<string, uint> dictionary = new Dictionary<string, uint>();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (int item in CardTypeTranslator.ConvertIdsToCardTypes(request.Ids))
		{
			string localizedTextForEnumValue = greLocProvider.GetLocalizedTextForEnumValue("CardType", item);
			dictionary[localizedTextForEnumValue] = (uint)item;
		}
		PromptParameter promptParameter = request.Prompt.Parameters.FirstOrDefault((PromptParameter x) => x.ParameterName == "CardId");
		if (ShouldUseDeckBasedHinting(((promptParameter != null) ? mtgGameState.GetCardById((uint)promptParameter.NumberValue) : null)?.TitleId ?? 0))
		{
			foreach (uint item2 in _cardsInDeckProvider.GetCardsInDeck())
			{
				CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(item2);
				if (cardPrintingById == null || CardUtilities.IsOmenOrAdventureParentFace(cardPrintingById.LinkedFaceType) || CardUtilities.IsFrontFaceOfDoubleSided(cardPrintingById.LinkedFaceType) || cardPrintingById.LinkedFaceType == LinkedFace.SplitHalf)
				{
					continue;
				}
				foreach (CardType type in cardPrintingById.Types)
				{
					string localizedTextForEnumValue2 = greLocProvider.GetLocalizedTextForEnumValue("CardType", (int)type);
					if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
					{
						list2.Add(localizedTextForEnumValue2);
					}
				}
			}
		}
		else if (HintAllCastableTypes(_cardDatabase, mtgGameState))
		{
			foreach (CardType allCastableType in AllCastableTypes)
			{
				string localizedTextForEnumValue3 = greLocProvider.GetLocalizedTextForEnumValue("CardType", (int)allCastableType);
				if (dictionary.ContainsKey(localizedTextForEnumValue3))
				{
					list2.Add(localizedTextForEnumValue3);
				}
			}
		}
		else
		{
			List<MtgCardInstance> list3 = new List<MtgCardInstance>(mtgGameState.VisibleCards.Values);
			list3.AddRange(mtgGameState.RevealedCards.Values);
			foreach (MtgCardInstance item3 in list3)
			{
				foreach (CardType cardType in item3.CardTypes)
				{
					string localizedTextForEnumValue4 = greLocProvider.GetLocalizedTextForEnumValue("CardType", (int)cardType);
					if (dictionary.ContainsKey(localizedTextForEnumValue4) && !list2.Contains(localizedTextForEnumValue4))
					{
						list2.Add(localizedTextForEnumValue4);
					}
				}
			}
		}
		list.AddRange(dictionary.Keys);
		list.Sort();
		return new ChoiceOptions(dictionary, list, list2);
	}

	private static bool ShouldUseDeckBasedHinting(uint cardTitleId)
	{
		if (cardTitleId == 428606 || cardTitleId == 477101)
		{
			return true;
		}
		return false;
	}

	public static bool HintAllCastableTypes(ICardDatabaseAdapter cardDatabase, MtgGameState gameState)
	{
		MtgCardInstance topCardOnStack = gameState.GetTopCardOnStack();
		if (topCardOnStack == null)
		{
			return false;
		}
		return cardDatabase.GetPrintingFromInstance(topCardOnStack)?.Tags.Contains(MetaDataTag.HintAllCastableTypes) ?? false;
	}
}
