using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class StaticSuperTypeTranslation : IChoiceOptionsTranslation
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGreLocProvider _greLocProvider;

	public StaticSuperTypeTranslation(IContext context)
		: this(context.Get<IGameStateProvider>(), context.Get<IGreLocProvider>())
	{
	}

	public StaticSuperTypeTranslation(IGameStateProvider gameStateProvider, IGreLocProvider greLocProvider)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_greLocProvider = greLocProvider ?? NullGreLocManager.Default;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		Dictionary<string, uint> dictionary = new Dictionary<string, uint>();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (uint id in request.Ids)
		{
			string localizedTextForEnumValue = _greLocProvider.GetLocalizedTextForEnumValue("SuperType", (int)id);
			dictionary[localizedTextForEnumValue] = id;
		}
		List<MtgCardInstance> list3 = new List<MtgCardInstance>(mtgGameState.VisibleCards.Values);
		list3.AddRange(mtgGameState.RevealedCards.Values);
		foreach (MtgCardInstance item in list3)
		{
			foreach (SuperType supertype in item.Supertypes)
			{
				string localizedTextForEnumValue2 = _greLocProvider.GetLocalizedTextForEnumValue("SuperType", (int)supertype);
				if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
				{
					list2.Add(localizedTextForEnumValue2);
				}
			}
		}
		list2.Sort();
		list.AddRange(dictionary.Keys);
		list.Sort();
		return new ChoiceOptions(dictionary, list, list2);
	}
}
