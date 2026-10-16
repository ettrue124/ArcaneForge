using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CoinFlipEventParser : IAnnotationEventParser
{
	private const string DETAIL_FLIPRESULT = "CoinFlipResult";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		KeyValuePairInfo keyValuePairInfo = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key == "CoinFlipResult");
		if (keyValuePairInfo == null)
		{
			yield break;
		}
		List<bool> list = new List<bool>();
		foreach (int item in keyValuePairInfo.ValueInt32)
		{
			list.Add(item == 1);
		}
		yield return new CoinFlipEvent
		{
			Results = list,
			AffectorId = ai.AffectorId,
			AffectedId = ai.AffectedIds[0]
		};
	}
}
