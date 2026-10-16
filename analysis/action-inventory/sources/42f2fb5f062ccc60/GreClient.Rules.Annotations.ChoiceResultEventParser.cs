using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ChoiceResultEventParser : IAnnotationEventParser, INeedMtgGameState_New
{
	private const string DETAIL_CHOICE_VALUE = "Choice_Value";

	private const string DETAIL_CHOICE_OPTIONS = "Choice_Options";

	private const string DETAIL_CHOICE_DOMAIN = "Choice_Domain";

	private const string DETAIL_CHOICE_SENTIMENT = "Choice_Sentiment";

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (newState == null)
		{
			throw new ArgumentNullException("new gamestate state not set");
		}
		IReadOnlyList<uint> readOnlyList = CopyAllInt32Details(ai, "Choice_Value");
		(string, StaticList) domainValues = GetDomainValues(ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key == "Choice_Domain"));
		if (newState.TryGetCard(ai.AffectorId, out var card) && domainValues.Item2 == StaticList.CardNames)
		{
			card.LinkedInfoTitleLocIds.UnionWith(readOnlyList);
		}
		yield return new ChoiceResultEvent
		{
			AffectorId = ai.AffectorId,
			AffectedId = ai.AffectedIds[0],
			ChoiceDomainString = domainValues.Item1,
			ChoiceDomainStaticListType = domainValues.Item2,
			ChoiceOptions = CopyAllInt32Details(ai, "Choice_Options"),
			ChoiceValues = readOnlyList,
			ChoiceSentiment = (HighlightType)ai.GetInt32Detail("Choice_Sentiment")
		};
	}

	private static IReadOnlyList<uint> CopyAllInt32Details(AnnotationInfo ai, string key)
	{
		RepeatedField<int> repeatedField = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key == key)?.ValueInt32;
		if (repeatedField == null)
		{
			return Array.Empty<uint>();
		}
		List<uint> list = new List<uint>(repeatedField.Count);
		foreach (int item in repeatedField)
		{
			list.Add((uint)item);
		}
		return list;
	}

	private static (string domainString, StaticList listType) GetDomainValues(KeyValuePairInfo domainDetail)
	{
		if (domainDetail == null)
		{
			return (domainString: string.Empty, listType: StaticList.None);
		}
		if (domainDetail.ValueString.Count > 0)
		{
			return (domainString: domainDetail.ValueString[0], listType: StaticList.None);
		}
		if (domainDetail.ValueInt32.Count > 0)
		{
			return (domainString: string.Empty, listType: (StaticList)domainDetail.ValueInt32[0]);
		}
		return (domainString: string.Empty, listType: StaticList.None);
	}
}
