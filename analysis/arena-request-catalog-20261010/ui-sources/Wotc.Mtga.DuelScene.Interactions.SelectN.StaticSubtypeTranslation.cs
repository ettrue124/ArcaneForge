using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class StaticSubtypeTranslation : IChoiceOptionsTranslation
{
	private readonly IGreLocProvider _greLocProvider;

	private readonly IStaticSubtypeHintProvider _subtypeHintProvider;

	public StaticSubtypeTranslation(IGreLocProvider greLocProvider, IStaticSubtypeHintProvider subtypeHintProvider)
	{
		_greLocProvider = greLocProvider;
		_subtypeHintProvider = subtypeHintProvider;
	}

	public ChoiceOptions Translate(SelectNRequest request)
	{
		List<uint> ids = request.Ids;
		Dictionary<string, uint> dictionary = new Dictionary<string, uint>();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string enumName = "SubType";
		uint num = 306u;
		foreach (uint item in (IEnumerable<uint>)ids)
		{
			if (item != num)
			{
				string localizedTextForEnumValue = _greLocProvider.GetLocalizedTextForEnumValue(enumName, (int)item);
				dictionary[localizedTextForEnumValue] = item;
			}
		}
		foreach (SubType subTypeHint in _subtypeHintProvider.GetSubTypeHints(request))
		{
			string localizedTextForEnumValue2 = _greLocProvider.GetLocalizedTextForEnumValue("SubType", (int)subTypeHint);
			if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
			{
				list2.Add(localizedTextForEnumValue2);
			}
		}
		list2.Sort();
		list.AddRange(dictionary.Keys);
		list.Sort();
		return new ChoiceOptions(dictionary, list, list2);
	}
}
