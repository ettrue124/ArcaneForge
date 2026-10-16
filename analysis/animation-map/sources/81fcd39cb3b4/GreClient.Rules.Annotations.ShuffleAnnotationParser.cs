using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ShuffleAnnotationParser : IAnnotationEventParser
{
	public const string DETAIL_OLD_IDS = "OldIds";

	public const string DETAIL_NEW_IDS = "NewIds";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.Shuffle))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain shuffled card details.", "ai");
		}
		yield return new ShuffleEvent
		{
			PlayerId = ai.AffectedIds[0],
			OldIds = GetDetailList("OldIds", ai),
			NewIds = GetDetailList("NewIds", ai)
		};
	}

	private List<uint> GetDetailList(string detailKey, AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals(detailKey) && detail.ValueInt32.Count >= 0)
			{
				return new List<uint>(detail.ValueInt32.Select((int x) => (uint)x));
			}
		}
		throw new ArgumentException("Annotation does not contain " + detailKey + " detail.", "ai");
	}
}
