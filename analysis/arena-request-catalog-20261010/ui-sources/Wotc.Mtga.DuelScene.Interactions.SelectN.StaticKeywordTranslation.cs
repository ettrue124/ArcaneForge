using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class StaticKeywordTranslation : IChoiceOptionsTranslation
{
	private readonly IAbilityTextProvider _abilityTextProvider;

	public StaticKeywordTranslation(IAbilityTextProvider abilityTextProvider)
	{
		_abilityTextProvider = abilityTextProvider;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		Dictionary<string, uint> dictionary = new Dictionary<string, uint>();
		List<string> list = new List<string>();
		foreach (uint id in request.Ids)
		{
			string keywordText = _abilityTextProvider.GetKeywordText(id);
			dictionary[keywordText] = id;
		}
		list.AddRange(dictionary.Keys);
		list.Sort();
		return new ChoiceOptions(dictionary, list, Array.Empty<string>());
	}
}
