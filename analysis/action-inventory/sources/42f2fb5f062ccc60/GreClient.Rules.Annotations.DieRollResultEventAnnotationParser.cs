using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DieRollResultEventAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New
{
	public const string DETAIL_RESULT = "Result";

	public const string DETAIL_NATURAL_RESULT = "NaturalResult";

	public const string DETAIL_FACES = "Faces";

	public const string DETAIL_IGNORED = "Ignored";

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.DieRoll))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		bool ignored = GetIgnored(ai);
		yield return new DieRollEvent
		{
			Results = new DieRollResultData(ai.AffectorId, ai.AffectedIds[0], GetDieFaces(ai), GetNaturalResult(ai), GetResult(ai), ignored)
		};
		if (!ignored)
		{
			yield return new CardChangedEvent(oldState.GetCardById(ai.AffectorId) ?? newState.GetCardById(ai.AffectorId), newState.GetCardById(ai.AffectorId) ?? oldState.GetCardById(ai.AffectorId), ai.AffectorId, PropertyType.DieRollResult);
		}
	}

	private static int GetResult(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("Result") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation contained no detail for Result", "ai");
	}

	private static uint GetNaturalResult(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("NaturalResult") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation contained no detail for NaturalResult", "ai");
	}

	private static uint GetDieFaces(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("Faces") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation contained no detail for Faces", "ai");
	}

	private static bool GetIgnored(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("Ignored") && detail.ValueString.Count > 0 && bool.TryParse(detail.ValueString[0], out var result))
			{
				return result;
			}
		}
		return false;
	}
}
